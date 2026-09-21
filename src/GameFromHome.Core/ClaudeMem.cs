using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameFromHome.Core;

public static class ClaudeMem
{
    public static (int Pid, int Port)? ReadWorker()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude-mem", "worker.pid");
            if (!File.Exists(path) || new FileInfo(path).Length > 16384) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            int pid = doc.RootElement.GetProperty("pid").GetInt32(), port = doc.RootElement.GetProperty("port").GetInt32();
            return pid > 0 && port is > 0 and <= 65535 ? (pid, port) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    public static bool HealthMatches(string json, int pid)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return root.TryGetProperty("pid", out var process) && process.TryGetInt32(out int actual) && actual == pid
                && root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String
                && root.TryGetProperty("status", out var status) && status.GetString() is "ok" or "degraded";
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return false; }
    }

    internal static async Task<(int Code, string Detail)> RequestAsync(AppSnapshot app, Discovery discovery, CancellationToken token)
    {
        var worker = ReadWorker();
        var target = app.Processes.SingleOrDefault();
        if (target is null || worker is null || worker.Value.Pid != target.Pid || worker.Value.Port != app.ServicePort || !discovery.Matches(target))
            return (5, "claude-mem worker identity changed; no request sent.");
        var commands = ProcessContext.ReadRuntimeCommands();
        if (!ProcessContext.IsClaudeMemWorker(target.Name, commands.GetValueOrDefault(target.Pid)) || !OwnsListener(target.Pid, worker.Value.Port))
            return (5, "Could not verify claude-mem's worker and listening port; no request sent.");
        using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 65536 };
        string address = $"http://127.0.0.1:{worker.Value.Port}";
        try
        {
            using var health = await http.GetAsync(address + "/api/health", token);
            if (!HealthMatches(await health.Content.ReadAsStringAsync(token), target.Pid) || !discovery.Matches(target) || !OwnsListener(target.Pid, worker.Value.Port))
                return (5, "claude-mem health identity did not match; no request sent.");
            using var response = await http.PostAsync(address + "/api/admin/shutdown", new StringContent(""), token);
            return response.IsSuccessStatusCode ? (0, "Requested claude-mem's own shutdown; verifying exit.") : ((int)response.StatusCode, "claude-mem declined its shutdown request.");
        }
        catch (HttpRequestException) { return (1, "claude-mem did not complete the HTTP request; verifying process state."); }
        catch (TaskCanceledException) when (!token.IsCancellationRequested) { return (1, "claude-mem's endpoint timed out; no forced termination used."); }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)] private static extern uint GetExtendedTcpTable(nint table, ref int size, bool order, int family, int tableClass, uint reserved);
    internal static bool OwnsListener(int pid, int port)
    {
        int size = 0;
        if (GetExtendedTcpTable(0, ref size, false, 2, 3, 0) != 122 || size < 4) return false;
        nint data = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(data, ref size, false, 2, 3, 0) != 0) return false;
            int count = Marshal.ReadInt32(data);
            if (count < 0 || count > (size - 4) / 24) return false;
            for (int i = 0; i < count; i++)
            {
                nint row = data + 4 + i * 24;
                int localPort = (Marshal.ReadByte(row, 8) << 8) | Marshal.ReadByte(row, 9);
                uint address = unchecked((uint)Marshal.ReadInt32(row, 4));
                if (Marshal.ReadInt32(row) == 2 && localPort == port && address is 0 or 0x0100007f && Marshal.ReadInt32(row, 20) == pid) return true;
            }
            return false;
        }
        finally { Marshal.FreeHGlobal(data); }
    }
}
