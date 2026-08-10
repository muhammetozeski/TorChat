using System.Reflection;
using System.Text;
using Chat.Models;

namespace Chat.Stores;

/// <summary>
/// Registers, loads and saves all <see cref="Settings"/> fields.
/// Settings are saved next to the executable.
/// </summary>
internal static class SettingsManager
{
    private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TorChatSettings.ini");
    static readonly Dictionary<string, ISettingSetup> iSettingSetups = [];
    static readonly Dictionary<string, ISetting> iSettings = [];

    public static ISetting[] GetAllSettings() => [.. iSettings.Values];

    static SettingsManager()
    {
        foreach (var field in typeof(Settings).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            object? value = field.GetValue(null);
            if (value is ISettingSetup setupSetting)
            {
                setupSetting.InitializeKey(field.Name);
                iSettingSetups.Add(field.Name, setupSetting);
                if (value is ISetting setting)
                    iSettings[field.Name] = setting;
            }
        }
    }

    /// <summary>Loads settings from the config file, creating it with defaults if missing.</summary>
    public static void LoadSettings()
    {
        if (!File.Exists(ConfigPath))
        {
            SaveSettings();
            return;
        }

        foreach (var line in File.ReadAllLines(ConfigPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            // Split on the FIRST '=' only, so Base64 padding ('==') in values survives.
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;

            string key = parts[0].Trim();
            string value = parts[1].Trim();

            if (iSettingSetups.TryGetValue(key, out var setting))
                setting.LoadFromStr(value);
        }
    }

    /// <summary>Serializes all settings to the single config file.</summary>
    public static void SaveSettings()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# TorChat Configuration");
        sb.AppendLine($"# Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        foreach (var setting in iSettingSetups.Values)
            sb.AppendLine($"{setting.Key} = {setting.Serialize()}");

        try
        {
            File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Failed to save settings: " + ex);
        }
    }

    // The DPAPI-bound encrypted API-key blob: never exported/imported (it's machine-bound and sensitive).
    static readonly HashSet<string> PortableOmit = new(StringComparer.OrdinalIgnoreCase) { nameof(Settings.DefaultSecret) };

    /// <summary>Reset every registered setting to its shipped default and persist.</summary>
    public static void ResetAllToDefaults()
    {
        foreach (var s in iSettings.Values) s.ResetToDefault();
        SaveSettings();
    }

    /// <summary>Write the portable config (all key=value lines except the encrypted key vault) to a file.</summary>
    public static void ExportSettings(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# TorChat settings export — {DateTime.Now:yyyy-MM-dd}");
        foreach (var setting in iSettingSetups.Values)
            if (!PortableOmit.Contains(setting.Key))
                sb.AppendLine($"{setting.Key} = {setting.Serialize()}");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Capture the current serialized values of just the given setting keys (for scan profiles).</summary>
    public static Dictionary<string, string> CaptureSubset(IEnumerable<string> keys)
    {
        var map = new Dictionary<string, string>();
        foreach (var k in keys)
            if (iSettingSetups.TryGetValue(k, out var s)) map[k] = s.Serialize();
        return map;
    }

    /// <summary>Apply a captured key→value subset onto the live settings (its own keys only), then persist.
    /// Returns the number of keys applied.</summary>
    public static int ApplySubset(IReadOnlyDictionary<string, string> values)
    {
        int n = 0;
        foreach (var (k, v) in values)
            if (!PortableOmit.Contains(k) && iSettingSetups.TryGetValue(k, out var s)) { s.LoadFromStr(v); n++; }
        if (n > 0) SaveSettings();
        return n;
    }

    /// <summary>Apply a previously-exported config file (skipping the encrypted vault). Returns keys applied.</summary>
    public static int ImportSettings(string path)
    {
        int n = 0;
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('=', 2);
            if (parts.Length != 2) continue;
            string key = parts[0].Trim(), value = parts[1].Trim();
            if (PortableOmit.Contains(key)) continue;
            if (iSettingSetups.TryGetValue(key, out var s)) { s.LoadFromStr(value); n++; }
        }
        if (n > 0) SaveSettings();
        return n;
    }
}
