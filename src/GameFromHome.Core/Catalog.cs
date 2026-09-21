namespace GameFromHome.Core;

public static class Catalog
{
    public static readonly AppDefinition[] Apps = [
        new("edge", "Microsoft Edge", "E", ["msedge.exe"]),
        new("cursor", "Cursor", "↗", ["cursor.exe"]),
        new("teams", "Microsoft Teams", "T", ["ms-teams.exe", "teams.exe"]),
        new("zoom", "Zoom", "Z", ["zoom.exe"]),
        new("whatsapp", "WhatsApp", "W", ["whatsapp.exe"]),
        new("chatgpt", "ChatGPT", "G", ["chatgpt.exe"]),
        new("claude", "Claude", "C", ["claude.exe"]),
        new("discord", "Discord", "D", ["discord.exe", "discordptb.exe", "discordcanary.exe"], true),
        new("codex", "Codex", "◈", ["chatgpt.exe", "codex.exe"], DefaultSelected: false),
        new("claude-mem", "claude-mem worker (Bun)", "M", ["bun.exe", "node.exe"], ExitMethod: ExitMethod.ClaudeMem, DefaultSelected: false)
    ];
    public static bool ProtectedPath(string path) =>
        path.Contains("\\Discord\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\DiscordPTB\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\DiscordCanary\\", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(System.IO.Path.GetFileName(path), "GameFromHome.exe", StringComparison.OrdinalIgnoreCase);

    public static AppDefinition? Identify(string name, string path)
    {
        string p = path.Replace('/', '\\').ToLowerInvariant();
        string n = name.ToLowerInvariant();
        bool Has(string s) => p.Contains(s, StringComparison.Ordinal);
        string? id = n switch {
            "chatgpt.exe" or "codex.exe" when Has("\\openai.codex_") => "codex",
            "chatgpt.exe" when Has("\\openai.chatgpt-desktop_") || Has("\\openai.chatgpt_") || Has("\\programs\\chatgpt\\") => "chatgpt",
            "msedge.exe" when Has("\\microsoft\\edge\\application\\") => "edge",
            "cursor.exe" when Has("\\programs\\cursor\\") || Has("\\program files\\cursor\\") => "cursor",
            "ms-teams.exe" when Has("\\windowsapps\\msteams_") => "teams",
            "teams.exe" when Has("\\microsoft\\teams\\current\\") => "teams",
            "zoom.exe" when Has("\\zoom\\bin\\") => "zoom",
            "whatsapp.exe" when Has("\\windowsapps\\5319275a.whatsappdesktop_") || Has("\\whatsapp\\") => "whatsapp",
            "claude.exe" when Has("\\windowsapps\\claude_") || Has("\\windowsapps\\anthropic.") || Has("\\anthropicclaude\\") || Has("\\claude\\app-") => "claude",
            "discord.exe" when Has("\\discord\\app-") => "discord",
            "discordptb.exe" when Has("\\discordptb\\app-") => "discord",
            "discordcanary.exe" when Has("\\discordcanary\\app-") => "discord",
            _ => null
        };
        return Apps.FirstOrDefault(a => a.Id == id);
    }
}
