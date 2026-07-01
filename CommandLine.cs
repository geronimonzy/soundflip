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

// What a list/set command targets, and the input/output rings a cycle walks.
internal enum CycleScope { Outputs, Inputs }

internal sealed record ParsedCommand(
    Command Kind,
    string? DeviceQuery = null,
    string? TargetDirectory = null,
    string? BadCommand = null,
    AudioKind DeviceKind = AudioKind.Output,
    CycleScope Scope = CycleScope.Outputs);

internal static class CommandLine
{
    public static ParsedCommand Parse(IReadOnlyList<string> args, string currentDirectory)
    {
        if (args.Count == 0) return new ParsedCommand(Command.Tray);

        string verb = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
        return verb switch
        {
            "daemon" or "tray" => new ParsedCommand(Command.Tray),
            "list" => new ParsedCommand(Command.List, DeviceKind: ParseDeviceKind(Token(args, 1))),
            "set" => ParseSet(args),
            "cycle" => new ParsedCommand(Command.Cycle, Scope: ParseScope(Token(args, 1))),
            "export-assets" => new ParsedCommand(Command.ExportAssets, TargetDirectory: ResolveAssetDirectory(args, currentDirectory)),
            "help" or "/?" or "-?" or "--help" => new ParsedCommand(Command.Help),
            _ => new ParsedCommand(Command.Unknown, BadCommand: args[0]),
        };
    }

    // `set [output|input] <name>` -- an explicit kind word is optional and consumes
    // the first argument; otherwise the whole remainder is the output device name.
    static ParsedCommand ParseSet(IReadOnlyList<string> args)
    {
        string first = (Token(args, 1) ?? string.Empty).ToLowerInvariant();
        if (first is "output" or "input")
            return new ParsedCommand(Command.Set, DeviceKind: ParseDeviceKind(first), DeviceQuery: JoinArgs(args, 2));

        return new ParsedCommand(Command.Set, DeviceKind: AudioKind.Output, DeviceQuery: JoinArgs(args, 1));
    }

    static AudioKind ParseDeviceKind(string? token) =>
        (token ?? string.Empty).ToLowerInvariant().StartsWith("input") ? AudioKind.Input : AudioKind.Output;

    static CycleScope ParseScope(string? token) =>
        (token ?? string.Empty).ToLowerInvariant().StartsWith("input") ? CycleScope.Inputs : CycleScope.Outputs;

    public static string HelpText(string settingsPath) => $"""
        audsw -- minimal audio device switcher

          audsw                        launch the tray app
          audsw list [outputs|inputs]  list active devices (* = current default)
          audsw set [output|input] <name>
                                       set the default device (substring match)
          audsw cycle [outputs|inputs] advance the chosen ring to its next device
          audsw daemon                 alias for launching the tray app
          audsw export-assets <dir>    generate default Microsoft Store logo assets
          audsw help                   show this help text

        Settings file:
          {settingsPath}
        """;

    static string? Token(IReadOnlyList<string> args, int index) =>
        index < args.Count ? args[index]?.Trim() : null;

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
