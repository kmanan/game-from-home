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
    private Dictionary<int,string> runtimeCommands = [];
    private Dictionary<int,long> systemMemory = [];
    private DateTime commandsAt = DateTime.MinValue;
    private int? lastWorkerPid;

    public IReadOnlyList<AppSnapshot> Scan()
    {
        var raw = Entries();
        var parents = raw.ToDictionary(p => (int)p.Pid, p => (int)p.Parent);
        var names = raw.ToDictionary(p => (int)p.Pid, p => p.Exe);
        var inspected = new Dictionary<int, ProcessIdentity?>();
        ProcessIdentity? Own(Native.ProcessEntry entry)
        {
            int pid = (int)entry.Pid;
            if (!inspected.TryGetValue(pid, out var info)) inspected[pid] = info = Inspect(pid, (int)entry.Parent);
            return info;
        }
        var windows = new HashSet<int>();
        Native.EnumWindows((h, _) => { if(Native.IsWindowVisible(h) && Native.GetWindow(h,4)==0) {Native.GetWindowThreadProcessId(h,out int pid);windows.Add(pid);}return true;},0);
        var worker = ClaudeMem.ReadWorker();
        if ((DateTime.UtcNow-commandsAt).TotalSeconds >= 10 || worker?.Pid!=lastWorkerPid) { runtimeCommands=ProcessContext.ReadRuntimeCommands();systemMemory=ProcessContext.ReadPrivateWorkingSets();commandsAt=DateTime.UtcNow;lastWorkerPid=worker?.Pid; }
        var owned = new Dictionary<int, (AppDefinition Definition, ProcessIdentity Process)>();
        var missing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        InaccessibleCandidates = 0;
        foreach (var entry in raw)
        {
            var info = Own(entry);
            if (info is null)
            {
                if (Catalog.Apps.Any(a=>a.Executables.Contains(entry.Exe,StringComparer.OrdinalIgnoreCase)) && Native.ProcessIdToSessionId((int)entry.Pid,out var sid) && sid==session) { missing[entry.Exe]=missing.GetValueOrDefault(entry.Exe)+1;InaccessibleCandidates++; }
                continue;
            }
            var definition = Catalog.Identify(entry.Exe, info.Path);
            if (worker?.Pid == info.Pid && ProcessContext.IsClaudeMemWorker(info.Name,runtimeCommands.GetValueOrDefault(info.Pid))) definition=Catalog.Apps.Single(a=>a.Id=="claude-mem");
            if (definition is null && windows.Contains(info.Pid) && !ProcessContext.IsRuntime(info.Name) && !IsSystemPath(info.Path) && !Catalog.ProtectedPath(info.Path))
                definition=new(ProcessContext.AppId(info.Path),Path.GetFileNameWithoutExtension(info.Name),"A",[info.Name],DefaultSelected:false);
            if (definition is not null) owned[(int)entry.Pid] = (definition, info);
        }
        // Include same-executable GUI helpers, but never merge unrelated Bun/Node projects by runtime name.
        foreach(var entry in raw.Where(e=>!owned.ContainsKey((int)e.Pid)))
        {
            var info=Own(entry);if(info is null || ProcessContext.IsRuntime(info.Name))continue;
            var match=owned.Values.FirstOrDefault(v=>v.Definition.Id.StartsWith("app:",StringComparison.Ordinal) && string.Equals(v.Process.Path,info.Path,StringComparison.OrdinalIgnoreCase));
            if(match.Definition is not null)owned[info.Pid]=(match.Definition,info);
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
                    var info = Own(entry);
                    bool codexHelper=root.Definition.Id=="codex" && info is not null && info.Path.Contains("\\AppData\\Local\\OpenAI\\Codex\\",StringComparison.OrdinalIgnoreCase);
                    if(ProcessContext.IsRuntime(entry.Exe) && !codexHelper)break;
                    if(root.Definition.ExitMethod==ExitMethod.ClaudeMem)break;
                    var mainPaths=owned.Values.Where(v=>v.Definition.Id==root.Definition.Id&&root.Definition.Executables.Contains(v.Process.Name,StringComparer.OrdinalIgnoreCase)).Select(v=>Path.GetDirectoryName(v.Process.Path)!+"\\").Distinct().ToArray();
                    bool webView=entry.Exe.Equals("msedgewebview2.exe",StringComparison.OrdinalIgnoreCase);
                    if (info is not null && info.Created >= root.Process.Created && (codexHelper || mainPaths.Any(path=>info.Path.StartsWith(path,StringComparison.OrdinalIgnoreCase)) || webView&&info.Path.Contains("\\Microsoft\\EdgeWebView\\Application\\", StringComparison.OrdinalIgnoreCase))) owned[(int)entry.Pid] = (root.Definition, info);
                    else if(info is null && webView) missing[root.Definition.Executables[0]] = missing.GetValueOrDefault(root.Definition.Executables[0]) + 1;
                    break;
                }
                parent = parents.GetValueOrDefault(parent);
            }
        }
        InaccessibleNames=missing;
        var snapshots = owned.Values.GroupBy(p => p.Definition.Id).Select(g => {
            var app = g.First().Definition;
            int unreadable = app.Id=="claude-mem" ? 0 : app.Executables.Sum(n => missing.GetValueOrDefault(n));
            string context=app.Id=="codex"?"Closing Codex interrupts active tasks. Select only when ready.":app.Id=="claude-mem"?"Uses the worker's own shutdown API; hooks may start it again.":app.Protected?"Protected · stays open":app.Id.StartsWith("app:",StringComparison.Ordinal)?"Discovered desktop app · review before selecting":"";
            return new AppSnapshot(app, g.Select(v => v.Process).ToArray(), unreadable == 0, unreadable,context,app.Id=="claude-mem"?worker?.Port:null);
        }).ToList();
        foreach(var entry in raw.Where(e=>e.Pid>0&&!owned.ContainsKey((int)e.Pid)))
        {
            var own=Own(entry);
            var info=own??Inspect((int)entry.Pid,(int)entry.Parent,false)??new((int)entry.Pid,0,"",(int)entry.Parent,-1,entry.Exe,null);
            if(info.PrivateWorkingSet is null && systemMemory.TryGetValue(info.Pid,out var privateRam))info=info with {PrivateWorkingSet=privateRam};
            string parent=names.GetValueOrDefault((int)entry.Parent,"not running");
            string context=$"PID {info.Pid} · parent {parent} ({entry.Parent}) · ";
            bool system=own is null || IsSystemPath(info.Path);
            context+=system?"System / other owner / access restricted · view only":ProcessContext.IsRuntime(info.Name)?"Background runtime · no verified clean-exit method":"Background process · no verified clean-exit method";
            if(info.Path.Length==0 && info.PrivateWorkingSet is not null)context+=" · Windows performance counter";
            if(ProcessContext.IsClaudeMemWorker(info.Name,runtimeCommands.GetValueOrDefault(info.Pid)))context+=" · claude-mem worker (PID file not verified)";
            var definition=new AppDefinition($"process:{info.Pid}:{info.Created}",Path.GetFileNameWithoutExtension(info.Name),"P",[info.Name],Catalog.ProtectedPath(info.Path),ExitMethod.InspectOnly,false);
            snapshots.Add(new(definition,[info],own is not null,own is null?1:0,context));
        }
        return snapshots.OrderByDescending(a=>a.Ram).ToArray();
    }

    public static bool IsSystemPath(string path) => path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows)+"\\",StringComparison.OrdinalIgnoreCase);

    public ProcessIdentity? Inspect(int pid, int parentPid = 0, bool requireOwner = true)
    {
        if (!Native.ProcessIdToSessionId(pid, out var sid) || requireOwner && sid != session) return null;
        using var handle = Native.OpenProcess(0x1000 | 0x100000, false, pid);
        if (handle.IsInvalid) return null;
        if (!Native.GetProcessTimes(handle, out var created, out _, out _, out _)) return null;
        var text = new StringBuilder(32768); uint length = (uint)text.Capacity;
        if (!Native.QueryFullProcessImageName(handle, 0, text, ref length)) return null;
        if(requireOwner) {
            if (!Native.OpenProcessToken(handle, 8, out var token)) return null;
            using (token) { using var identity = new WindowsIdentity(token.DangerousGetHandle()); if (identity.User?.Value != user) return null; }
        }
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
