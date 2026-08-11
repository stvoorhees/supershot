using System.IO;
using System.Text.Json;

namespace Supershot;

public sealed class SettingsData
{
    public string Hotkey { get; set; } = "ctrl+shift+2";
    public bool AutoCopy { get; set; }
    public string SaveFolder { get; set; } = "";
}

/// <summary>Local settings, persisted to %AppData%\Supershot\settings.json. No network.</summary>
public static class AppSettings
{
    public static SettingsData Data { get; private set; } = new();

    /// <summary>Raised when the hotkey changes so the app can re-register it.</summary>
    public static event Action? HotkeyChanged;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Path
    {
        get
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Supershot");
            Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "settings.json");
        }
    }

    public static void Load()
    {
        try { if (File.Exists(Path)) Data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(Path)) ?? new(); }
        catch { Data = new(); }
    }

    public static void Save()
    {
        try { File.WriteAllText(Path, JsonSerializer.Serialize(Data, JsonOpts)); } catch { }
    }

    public static void SetHotkey(string hotkey)
    {
        if (string.Equals(Data.Hotkey, hotkey, StringComparison.OrdinalIgnoreCase)) return;
        Data.Hotkey = hotkey; Save(); HotkeyChanged?.Invoke();
    }

    /// <summary>Parse a string like "ctrl+shift+2" into Win32 (modifiers, virtual-key).</summary>
    public static (uint mods, uint vk) ParseHotkey(string s)
    {
        uint mods = 0, vk = 0x32; // default '2'
        foreach (var raw in s.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw)
            {
                case "ctrl" or "control": mods |= 0x0002; break;
                case "shift": mods |= 0x0004; break;
                case "alt": mods |= 0x0001; break;
                case "win": mods |= 0x0008; break;
                case "printscreen" or "prtsc": vk = 0x2C; break;
                default:
                    if (raw.Length == 1)
                    {
                        var ch = char.ToUpperInvariant(raw[0]);
                        if (ch is >= '0' and <= '9' || ch is >= 'A' and <= 'Z') vk = ch;
                    }
                    break;
            }
        }
        return (mods, vk);
    }
}
