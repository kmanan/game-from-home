using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace GameFromHome.Core;

public sealed class Discovery
{
    private readonly int session = Process.GetCurrentProcess().SessionId;
    private readonly string? user = WindowsIdentity.GetCurrent().User?.Value;
    public int InaccessibleCandidates { get; private set; }
    public IReadOnlyDictionary<string,int> InaccessibleNames { get; private set; } = new Dictionary<string,int>();

    public IReadOnlyList<AppSnapshot> Scan()
    {
        var raw = Entries();
        var parents = raw.ToDictionary(p => (int)p.Pid, p => (int)p.Parent);
        var owned = new Dictionary<int, (AppDefinition Definition, ProcessIdentity Process)>();
        var missing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        InaccessibleCandidates = 0;
        foreach (var entry in raw)
        {
            if (!Catalog.Apps.Any(a => a.Executables.Contains(entry.Exe, StringComparer.OrdinalIgnoreCase))) continue;
            if (!Native.ProcessIdToSessionId((int)entry.Pid, out var sid) || sid != session) continue;
            var info = Inspect((int)entry.Pid, (int)entry.Parent);
            if (info is null) { missing[entry.Exe] = missing.GetValueOrDefault(entry.Exe) + 1; InaccessibleCandidates++; continue; }
            var definition = Catalog.Identify(entry.Exe, info.Path);
            if (definition is not null) owned[(int)entry.Pid] = (definition, info);
        }
        // Shared runtimes are associated only through a verified ancestor, never by executable name alone.
        foreach (var entry in raw.Where(e => !owned.ContainsKey((int)e.Pid)))
        {
            var parent = (int)entry.Parent; var seen = new HashSet<int>();
            while (parent > 0 && seen.Add(parent))
            {
                if (owned.TryGetValue(parent, out var root))
                {
                    // User-launched developer runtimes are not app helpers even when descended from an editor.
                    if(new[]{"node.exe","bun.exe","python.exe","pythonw.exe","pwsh.exe","powershell.exe","cmd.exe","wsl.exe","conhost.exe","claude.exe"}.Contains(entry.Exe,StringComparer.OrdinalIgnoreCase))break;
                    var info = Inspect((int)entry.Pid, (int)entry.Parent);
                    var mainPaths=owned.Values.Where(v=>v.Definition.Id==root.Definition.Id&&root.Definition.Executables.Contains(v.Process.Name,StringComparer.OrdinalIgnoreCase)).Select(v=>Path.GetDirectoryName(v.Process.Path)!+"\\").Distinct().ToArray();
                    bool webView=entry.Exe.Equals("msedgewebview2.exe",StringComparison.OrdinalIgnoreCase);
                    if (info is not null && info.Created >= root.Process.Created && (mainPaths.Any(path=>info.Path.StartsWith(path,StringComparison.OrdinalIgnoreCase)) || webView&&info.Path.Contains("\\Microsoft\\EdgeWebView\\Application\\", StringComparison.OrdinalIgnoreCase))) owned[(int)entry.Pid] = (root.Definition, info);
                    else if(info is null && webView) missing[root.Definition.Executables[0]] = missing.GetValueOrDefault(root.Definition.Executables[0]) + 1;
                    break;
                }
                parent = parents.GetValueOrDefault(parent);
            }
        }
        InaccessibleNames=missing;
        return owned.Values.GroupBy(p => p.Definition.Id).Select(g => {
            var app = g.First().Definition;
            int unreadable = app.Executables.Sum(n => missing.GetValueOrDefault(n));
            return new AppSnapshot(app, g.Select(v => v.Process).ToArray(), unreadable == 0, unreadable);
        }).OrderByDescending(a => a.Ram).ToArray();
    }

    public ProcessIdentity? Inspect(int pid, int parentPid = 0)
    {
        if (!Native.ProcessIdToSessionId(pid, out var sid) || sid != session) return null;
        using var handle = Native.OpenProcess(0x1000 | 0x100000, false, pid);
        if (handle.IsInvalid) return null;
        if (!Native.GetProcessTimes(handle, out var created, out _, out _, out _)) return null;
        var text = new StringBuilder(32768); uint length = (uint)text.Capacity;
        if (!Native.QueryFullProcessImageName(handle, 0, text, ref length)) return null;
        if (!Native.OpenProcessToken(handle, 8, out var token)) return null;
        using (token) { using var identity = new WindowsIdentity(token.DangerousGetHandle()); if (identity.User?.Value != user) return null; }
        var counters = new Native.MemoryCounters { Size = (uint)Marshal.SizeOf<Native.MemoryCounters>() };
        long? memory = Native.GetProcessMemoryInfo(handle, ref counters, counters.Size) ? checked((long)counters.PrivateWorking) : null;
        return new(pid, created.Value, text.ToString(), parentPid, (int)sid, Path.GetFileName(text.ToString()), memory);
    }

    public bool Matches(ProcessIdentity identity)
    {
        var now = Inspect(identity.Pid, identity.ParentPid);
        return now is not null && SameInstance(identity, now);
    }
    public static bool SameInstance(ProcessIdentity before, ProcessIdentity now) => before.Pid == now.Pid && before.Created == now.Created && before.SessionId == now.SessionId && string.Equals(before.Path, now.Path, StringComparison.OrdinalIgnoreCase);
    public static bool? IsAlive(ProcessIdentity identity)
    {
        using var handle = Native.OpenProcess(0x1000 | 0x100000, false, identity.Pid);
        if (handle.IsInvalid) return Marshal.GetLastWin32Error() == 87 ? false : null;
        if (!Native.GetProcessTimes(handle, out var created, out _, out _, out _)) return null;
        if (created.Value != identity.Created) return false;
        return Native.WaitForSingleObject(handle, 0) switch { 0 => false, 258 => true, _ => null };
    }
    public static MemorySample ReadMemory()
    {
        var status = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        if (!Native.GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(DateTimeOffset.UtcNow, checked((long)status.Available), checked((long)status.Total));
    }
    public static async Task<MemorySample?> SampleMedianAsync()
    {
        try { var samples = new List<MemorySample>(); for (int i=0;i<3;i++) { samples.Add(ReadMemory()); if(i<2) await Task.Delay(500); } return samples.OrderBy(s=>s.Available).ElementAt(1); }
        catch (Win32Exception) { return null; }
    }
    public static bool ShowApp(AppSnapshot snapshot)
    {
        var live = snapshot.Processes.Where(p => IsAlive(p) == true).Select(p=>p.Pid).ToHashSet(); bool shown = false;
        Native.EnumWindows((h, _) => { Native.GetWindowThreadProcessId(h, out int pid); if (live.Contains(pid) && Native.IsWindowVisible(h)) { Native.ShowWindow(h, 9); Native.SetForegroundWindow(h); shown = true; return false; } return true; }, 0);
        return shown;
    }
    private static List<Native.ProcessEntry> Entries()
    {
        nint snapshot = Native.CreateToolhelp32Snapshot(2, 0);
        if (snapshot == -1) throw new Win32Exception(Marshal.GetLastWin32Error());
        try { var list = new List<Native.ProcessEntry>(); var entry = new Native.ProcessEntry { Size=(uint)Marshal.SizeOf<Native.ProcessEntry>(), Exe="" }; if(Native.Process32FirstW(snapshot, ref entry)) do { list.Add(entry); } while(Native.Process32NextW(snapshot, ref entry)); return list; }
        finally { Native.CloseHandle(snapshot); }
    }
}
