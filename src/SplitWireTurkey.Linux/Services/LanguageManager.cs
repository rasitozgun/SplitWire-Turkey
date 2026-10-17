using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using LogService = SplitWireTurkey.Linux.Services.LogService;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Linux port of SplitWireTurkey.LanguageManager. Reads the exact same nested
/// JSON language files (res/Languages/{code}.json) as the Windows edition, so
/// all four shipped languages (tr/en/ru/es) work without duplication.
/// </summary>
public static class LanguageManager
{
    private static Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private static string _currentLanguage = "en";

    public static string CurrentLanguage => _currentLanguage;

    public static readonly string[] SupportedLanguages = { "tr", "en", "ru", "es" };

    public static bool LoadLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return false;
        }

        // Requested language, then Turkish (the project's fallback), then English.
        if (TryLoad(languageCode)) return true;
        if (TryLoad("tr")) return true;
        return TryLoad("en");
    }

    private static bool TryLoad(string code)
    {
        try
        {
            var path = Path.Combine(Paths.LanguagesDir, $"{code.ToLowerInvariant()}.json");
            if (!File.Exists(path))
            {
                return false;
            }

            // The Windows edition language files may contain nested objects AND
            // arrays (e.g. about.error_reports_list), so parse permissively:
            // strings pass through, arrays are joined, other scalars use their raw text.
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in doc.RootElement.EnumerateObject())
            {
                if (section.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var inner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in section.Value.EnumerateObject())
                {
                    inner[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Array => string.Join(Environment.NewLine,
                            prop.Value.EnumerateArray().Select(i => i.ValueKind == JsonValueKind.String ? i.GetString() ?? string.Empty : i.GetRawText())),
                        _ => prop.Value.GetRawText(),
                    };
                }
                map[section.Name] = inner;
            }
            _translations = map;
            _currentLanguage = code.ToLowerInvariant();
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error("Language", $"Failed to load language '{code}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Gets "category.key", falling back to "category.key" text when missing.</summary>
    public static string T(string category, string key)
    {
        if (_translations.TryGetValue(category, out var section) && section.TryGetValue(key, out var value))
        {
            return value;
        }
        return $"{category}.{key}";
    }

    public static string T(string key) => T(key.Split('.')[0], string.Join('.', key.Split('.')[1..]));
}
