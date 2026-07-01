using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

internal static class Program
{
    [STAThread]
    static int Main(string[] args) => Run(args);

    static int Run(string[] args)
    {
        var command = CommandLine.Parse(args, Environment.CurrentDirectory);
        if (command.Kind != Command.Tray) AttachParentConsole();

        try
        {
            return command.Kind switch
            {
                Command.List => List(command.DeviceKind),
                Command.Set => Set(command.DeviceKind, command.DeviceQuery),
                Command.Cycle => Cycle(command.Scope),
                Command.Tray => RunTrayApp(),
                Command.ExportAssets => ExportAssets(command.TargetDirectory),
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

        var kind = scope == CycleScope.Inputs ? AudioKind.Input : AudioKind.Output;
        var ring = (kind == AudioKind.Output ? settings.Outputs : settings.Inputs)
            .Select(entry => entry.Match).ToList();

        if (ring.Count == 0)
        {
            Console.Error.WriteLine($"no {Word(kind)} ring configured. Tick devices in the tray menu.");
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

    static string Word(AudioKind kind) => kind == AudioKind.Output ? "output" : "input";

    static int ExportAssets(string? targetDirectory)
    {
        targetDirectory ??= Path.Combine(Environment.CurrentDirectory, "Store", "Assets");

        StoreAssetExporter.Export(targetDirectory);
        Console.WriteLine("-> " + targetDirectory);
        return 0;
    }

    static int RunTrayApp()
    {
        var settings = SettingsStore.Load();

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.Run(new TrayContext(settings));
            }
            catch (Exception ex)
            {
                failure = ex;
                MessageBox.Show(ex.Message, AppMetadata.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new InvalidOperationException("tray app failed", failure);

        return 0;
    }

    // As a WinExe we get no console of our own. For CLI verbs, borrow the parent
    // terminal's console so output is visible — but only when stdout isn't already
    // redirected to a pipe/file (which must keep working as-is). Must run before
    // the first Console use so .NET picks up the attached handles.
    static void AttachParentConsole()
    {
        IntPtr stdout = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
        if (stdout == IntPtr.Zero || stdout == NativeMethods.INVALID_HANDLE_VALUE)
            NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
    }
}
