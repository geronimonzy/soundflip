internal enum Command
{
    Tray,
    List,
    Set,
    Cycle,
    ExportAssets,
    Help,
    Unknown,
}

internal sealed record ParsedCommand(
    Command Kind,
    string? DeviceQuery = null,
    string? TargetDirectory = null,
    string? BadCommand = null);

internal static class CommandLine
{
    public static ParsedCommand Parse(IReadOnlyList<string> args, string currentDirectory)
    {
        if (args.Count == 0) return new ParsedCommand(Command.Tray);

        string verb = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
        return verb switch
        {
            "daemon" or "tray" => new ParsedCommand(Command.Tray),
            "list" => new ParsedCommand(Command.List),
            "set" => new ParsedCommand(Command.Set, DeviceQuery: JoinArgs(args, 1)),
            "cycle" => new ParsedCommand(Command.Cycle),
            "export-assets" => new ParsedCommand(Command.ExportAssets, TargetDirectory: ResolveAssetDirectory(args, currentDirectory)),
            "help" or "/?" or "-?" or "--help" => new ParsedCommand(Command.Help),
            _ => new ParsedCommand(Command.Unknown, BadCommand: args[0]),
        };
    }

    public static string HelpText(string settingsPath) => $"""
        audsw -- minimal audio device switcher

          audsw                     launch the tray app
          audsw list                list active playback devices
          audsw set <name>          set default playback device (substring match)
          audsw cycle               toggle between the two configured devices
          audsw daemon              alias for launching the tray app
          audsw export-assets <dir> generate default Microsoft Store logo assets
          audsw help                show this help text

        Settings file:
          {settingsPath}
        """;

    static string? JoinArgs(IReadOnlyList<string> args, int startIndex)
    {
        if (args.Count <= startIndex) return null;

        var parts = new string[args.Count - startIndex];
        for (int i = startIndex; i < args.Count; i++)
            parts[i - startIndex] = args[i];

        string joined = string.Join(' ', parts).Trim();
        return joined.Length == 0 ? null : joined;
    }

    static string ResolveAssetDirectory(IReadOnlyList<string> args, string currentDirectory) =>
        JoinArgs(args, 1) ?? Path.Combine(currentDirectory, "Store", "Assets");
}
