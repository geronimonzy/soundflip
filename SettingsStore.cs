sealed class AppSettings
{
    public string Device1 { get; set; } = "";
    public string Device2 { get; set; } = "";
    public string Hotkey { get; set; } = "ctrl+alt+o";
}

static class SettingsStore
{
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "audsw");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "audsw.cfg");

    public static AppSettings Load() => Load(SettingsPath);

    internal static AppSettings Load(string path)
    {
        if (!File.Exists(path)) return new AppSettings();
        return Parse(File.ReadAllText(path));
    }

    public static void Save(AppSettings settings) => Save(settings, SettingsPath);

    internal static void Save(AppSettings settings, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, Serialize(settings, Environment.NewLine));
    }

    internal static AppSettings Parse(string text)
    {
        var settings = new AppSettings();
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
                case "device1": settings.Device1 = value; break;
                case "device2": settings.Device2 = value; break;
                case "hotkey": settings.Hotkey = value; break;
            }
        }

        return settings;
    }

    internal static string Serialize(AppSettings settings, string newline = "\n") =>
        "# audsw config -- managed from the tray menu, but safe to edit by hand." + newline +
        "# Device names are matched case-insensitively as a substring." + newline + newline +
        $"device1 = {settings.Device1}" + newline +
        $"device2 = {settings.Device2}" + newline + newline +
        "# Hotkey for the tray app. Modifiers: ctrl, alt, shift, win." + newline +
        "# Key: a letter, a digit, or f1..f12." + newline +
        $"hotkey  = {settings.Hotkey}" + newline;
}
