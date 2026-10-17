using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SplitWireTurkey.Linux.Services;

public class AppSettings
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("auto_dns_change")]
    public bool AutoDnsChange { get; set; } = true;

    [JsonPropertyName("dns_via_local_crypt")]
    public bool UseLocalDnsCrypt { get; set; } = false;

    [JsonPropertyName("browser_tunneling")]
    public bool BrowserTunneling { get; set; } = false;

    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "dark";
}

/// <summary>
/// JSON replacement for the Windows registry store.
/// Settings persist under ~/.config/SplitWire-Turkey/settings.json.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Load()
    {
        try
        {
            var path = Paths.SettingsFile;
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Settings", $"Failed to read settings: {ex.Message}");
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Paths.ConfigDir);
            File.WriteAllText(Paths.SettingsFile, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex)
        {
            LogService.Error("Settings", $"Failed to save settings: {ex.Message}");
        }
    }
}
