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

        try
        {
            return command.Kind switch
            {
                Command.List => List(),
                Command.Set => Set(command.DeviceQuery),
                Command.Cycle => Cycle(),
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

    static int List()
    {
        using var controller = new CoreAudioController();
        foreach (var device in controller.GetPlaybackDevices(DeviceState.Active).OrderBy(d => d.FullName))
            Console.WriteLine($"{(device.IsDefaultDevice ? "* " : "  ")}{device.FullName}");
        return 0;
    }

    static int Set(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.Error.WriteLine("usage: audsw set <name>");
            return 2;
        }

        using var controller = new CoreAudioController();
        var device = Audio.Resolve(controller, query);
        if (device is null)
        {
            Console.Error.WriteLine($"no active playback device matching \"{query}\"");
            return 1;
        }

        Audio.MakeDefault(device);
        Console.WriteLine("-> " + device.FullName);
        return 0;
    }

    static int Cycle()
    {
        var settings = SettingsStore.Load();
        using var controller = new CoreAudioController();
        var target = Audio.DoCycle(controller, settings);
        if (target is null)
        {
            Console.Error.WriteLine(
                $"could not resolve both devices (device1=\"{settings.Device1}\", device2=\"{settings.Device2}\"). " +
                $"Run `audsw list` and update {SettingsStore.SettingsPath}.");
            return 1;
        }

        Console.WriteLine("-> " + target.FullName);
        return 0;
    }

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
        HideOwnConsoleWindow();

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

    static void HideOwnConsoleWindow()
    {
        // Hide the console only when we own it alone (double-click / VBS launch);
        // leave an interactive terminal we were started from untouched.
        uint[] buffer = new uint[2];
        uint count = NativeMethods.GetConsoleProcessList(buffer, (uint)buffer.Length);
        if (count <= 1)
        {
            IntPtr hwnd = NativeMethods.GetConsoleWindow();
            if (hwnd != IntPtr.Zero) NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        }
    }
}
