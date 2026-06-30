using AudioSwitcher.AudioApi.CoreAudio;

// Console-mode command handlers. The app is a GUI subsystem exe, so we attach to
// the parent console first to make Console output visible when run from a shell.
static class Cli
{
    public static int Run(ParsedCommand command)
    {
        Win32.AttachConsole(Win32.ATTACH_PARENT_PROCESS);

        try
        {
            return command.Kind switch
            {
                Command.List => List(command.DeviceKind),
                Command.Set => Set(command.DeviceKind, command.DeviceQuery),
                Command.Cycle => Cycle(command.Scope),
                Command.Help => Help(null),
                Command.Unknown => Help(command.BadCommand),
                _ => Help(null),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    static int Help(string? badCommand)
    {
        if (!string.IsNullOrWhiteSpace(badCommand))
            Console.Error.WriteLine($"unknown command: {badCommand}");

        Console.WriteLine(CommandLine.HelpText(SettingsStore.SettingsPath));
        return string.IsNullOrWhiteSpace(badCommand) ? 0 : 2;
    }

    static int List(AudioKind kind)
    {
        using var controller = new CoreAudioController();
        var current = Audio.CurrentDefault(controller, kind);
        foreach (var device in Audio.Devices(controller, kind).OrderBy(d => d.FullName))
            Console.WriteLine($"{(current != null && device.Id == current.Id ? "* " : "  ")}{device.FullName}");
        return 0;
    }

    static int Set(AudioKind kind, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.Error.WriteLine("usage: audsw set [output|input] <name>");
            return 2;
        }

        using var controller = new CoreAudioController();
        var device = Audio.SetTo(controller, kind, query);
        if (device is null)
        {
            Console.Error.WriteLine($"no active {Word(kind)} device matching \"{query}\"");
            return 1;
        }

        Console.WriteLine("-> " + device.FullName);
        return 0;
    }

    static int Cycle(CycleScope scope)
    {
        var settings = SettingsStore.Load();
        using var controller = new CoreAudioController();

        if (scope == CycleScope.Pairs)
            return CyclePairs(controller, settings);

        var kind = scope == CycleScope.Inputs ? AudioKind.Input : AudioKind.Output;
        var ring = (kind == AudioKind.Output ? settings.Outputs : settings.Inputs)
            .Select(entry => entry.Match).ToList();

        if (ring.Count == 0)
        {
            Console.Error.WriteLine($"no {Word(kind)} ring configured. Add devices in the tray Settings window.");
            return 1;
        }

        var target = Audio.CycleRing(controller, kind, ring);
        if (target is null)
        {
            Console.Error.WriteLine($"none of the configured {Word(kind)} devices are currently active.");
            return 1;
        }

        Console.WriteLine("-> " + target.FullName);
        return 0;
    }

    static int CyclePairs(CoreAudioController controller, AppSettings settings)
    {
        var pair = Audio.NextPair(controller, settings.Pairs);
        if (pair is null)
        {
            Console.Error.WriteLine("no pairs configured. Add output+input pairs in the tray Settings window.");
            return 1;
        }

        var result = Audio.SetPair(controller, pair.Output, pair.Input);
        if (!result.Any)
        {
            Console.Error.WriteLine("neither device in the next pair is currently active.");
            return 1;
        }

        Console.WriteLine("-> " + string.Join(" + ",
            new[] { result.Output?.FullName, result.Input?.FullName }.Where(name => name is not null)));
        return 0;
    }

    static string Word(AudioKind kind) => kind == AudioKind.Output ? "output" : "input";
}
