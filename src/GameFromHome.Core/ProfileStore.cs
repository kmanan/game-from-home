using System.Text.Json;
namespace GameFromHome.Core;

public sealed class ProfileStore(string directory)
{
    private static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public string DirectoryPath { get; }=Path.GetFullPath(directory);
    public string? LoadWarning { get; private set; }
    public Preferences Load()
    {
        var file=Path.Combine(DirectoryPath,"preferences.json"); if(!File.Exists(file))return new();
        try { var p=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file)) ?? throw new JsonException(); if(p.SchemaVersion!=1 || p.SelectedApps is null || p.ApprovedFingerprints is null)throw new JsonException(); p.SelectedApps=p.SelectedApps.Distinct().Where(id=>Catalog.Apps.Any(a=>a.Id==id&&!a.Protected) || (id.StartsWith("app:",StringComparison.Ordinal) && id.Length==68 && id[4..].All(Uri.IsHexDigit) && p.ApprovedFingerprints.ContainsKey(id))).ToList(); return p; }
        catch(Exception e) when(e is JsonException or IOException or UnauthorizedAccessException) { LoadWarning="Saved preferences could not be read. Review selections before closing apps."; return new(); }
    }
    public void Save(Preferences preferences)=>Write("preferences.json",preferences);
    public void SaveReport(CleanupReport report)=>Write("last-run.json",report);
    private void Write<T>(string name,T value)
    {
        Directory.CreateDirectory(DirectoryPath); string destination=Path.Combine(DirectoryPath,name); string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,JsonSerializer.Serialize(value,Options)); File.Move(temporary,destination,true); }
        finally { if(File.Exists(temporary))File.Delete(temporary); }
    }
}
