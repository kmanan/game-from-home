namespace GameFromHome.Core;

public enum ExitMethod { Cooperative, ClaudeMem, InspectOnly }
public sealed record AppDefinition(string Id, string Name, string Initials, string[] Executables, bool Protected = false, ExitMethod ExitMethod = ExitMethod.Cooperative, bool DefaultSelected = true);
public sealed record ProcessIdentity(int Pid, long Created, string Path, int ParentPid, int SessionId, string Name, long? PrivateWorkingSet);
public sealed record AppSnapshot(AppDefinition Definition, IReadOnlyList<ProcessIdentity> Processes, bool Complete, int UnreadableCount = 0, string Context = "", int? ServicePort = null)
{
    public string Id => Definition.Id;
    public long Ram => Processes.Sum(p => p.PrivateWorkingSet ?? 0);
    public bool MemoryComplete => (Complete || Definition.ExitMethod==ExitMethod.InspectOnly) && Processes.All(p => p.PrivateWorkingSet.HasValue);
    public bool CanClose => Complete && !Definition.Protected && Definition.ExitMethod != ExitMethod.InspectOnly;
    public string Fingerprint => Definition.ExitMethod+"|"+ServicePort+"|"+string.Join("|", Processes.Select(p => p.Path.ToLowerInvariant()).Distinct().Order());
}
public sealed record MemorySample(DateTimeOffset At, long Available, long Total);
public enum ExitStatus { Closed, AlreadyClosed, Protected, StillRunning, Relaunched, AccessUnavailable, Canceled, Failed }
public sealed record ExitResult(string AppId, string Name, ExitStatus Status, string Detail, int? WindowsCode = null, long RemainingRam = 0);
public sealed record CleanupReport(DateTimeOffset Started, DateTimeOffset Ended, MemorySample? Before, MemorySample? After, List<ExitResult> Apps)
{
    public long? Delta => Before is null || After is null ? null : After.Available - Before.Available;
    public bool AllClosed => Apps.Count > 0 && Apps.All(a => a.Status is ExitStatus.Closed or ExitStatus.AlreadyClosed);
}
public sealed class Preferences
{
    public int SchemaVersion { get; set; } = 1;
    public bool Configured { get; set; }
    public bool AutoExit { get; set; }
    public List<string> SelectedApps { get; set; } = [];
    public Dictionary<string, string> ApprovedFingerprints { get; set; } = [];
}
public static class Formatting
{
    public const double GiB = 1073741824d;
    public static string AppRam(long bytes) => bytes >= 1_000_000_000 ? $"{bytes / 1_000_000_000d:F1} GB" : $"{bytes / 1_000_000d:F0} MB";
    public static string Ram(long bytes) => bytes >= GiB ? $"{bytes / GiB:F1} GiB" : $"{bytes / 1048576d:F0} MiB";
    public static string Delta(long bytes) => Math.Abs(bytes) < GiB / 20 ? "No measurable change" : $"{(bytes > 0 ? "+" : "−")}{Math.Abs(bytes) / GiB:F1} GiB available RAM";
}
