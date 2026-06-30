using System.Text.Json;
using System.Text.Json.Serialization;

// One configured device in a cycle ring, with an optional direct-jump hotkey.
sealed class DeviceEntry
{
    public string Match { get; set; } = "";
    public string Hotkey { get; set; } = "";
}

// An output+input combination switched together, with an optional hotkey.
sealed class PairEntry
{
    public string Name { get; set; } = "";
    public string Output { get; set; } = "";
    public string Input { get; set; } = "";
    public string Hotkey { get; set; } = "";
}

sealed class AppSettings
{
    public List<DeviceEntry> Outputs { get; set; } = new();
    public List<DeviceEntry> Inputs { get; set; } = new();
    public List<PairEntry> Pairs { get; set; } = new();

    public string CycleOutputs { get; set; } = "ctrl+alt+o";
    public string CycleInputs { get; set; } = "";
    public string CyclePairs { get; set; } = "";
}

static class SettingsStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "audsw");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "audsw.json");

    public static string LegacyPath => Path.Combine(SettingsDirectory, "audsw.cfg");

    public static AppSettings Load() => Load(SettingsPath, LegacyPath);

    internal static AppSettings Load(string path, string? legacyPath = null)
    {
        if (File.Exists(path))
            return Deserialize(File.ReadAllText(path));

        // One-time migration: import the old flat audsw.cfg, then persist as JSON.
        if (legacyPath is not null && File.Exists(legacyPath))
        {
            var migrated = LegacyCfg.Migrate(File.ReadAllText(legacyPath));
            Save(migrated, path);
            return migrated;
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings) => Save(settings, SettingsPath);

    internal static void Save(AppSettings settings, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(settings));
    }

    internal static AppSettings Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    internal static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, JsonOptions);
}

// Parser for the legacy flat `key = value` config, kept only to migrate existing
// installs to the JSON format. New writes never use this format.
static class LegacyCfg
{
    public static AppSettings Migrate(string text)
    {
        Parse(text, out string device1, out string device2, out string hotkey);

        var settings = new AppSettings { CycleOutputs = hotkey.Length == 0 ? "ctrl+alt+o" : hotkey };
        if (device1.Length > 0) settings.Outputs.Add(new DeviceEntry { Match = device1 });
        if (device2.Length > 0) settings.Outputs.Add(new DeviceEntry { Match = device2 });
        return settings;
    }

    internal static void Parse(string text, out string device1, out string device2, out string hotkey)
    {
        device1 = device2 = hotkey = "";
        using var reader = new StringReader(text ?? string.Empty);

        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            int eq = line.IndexOf('=');
            if (eq < 0) continue;

            string key = line[..eq].Trim().ToLowerInvariant();
            string value = line[(eq + 1)..].Trim();
            switch (key)
            {
                case "device1": device1 = value; break;
                case "device2": device2 = value; break;
                case "hotkey": hotkey = value; break;
            }
        }
    }
}
