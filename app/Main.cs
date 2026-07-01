using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // CLI verbs run as a console tool and exit; no args (or `daemon`/`tray`)
        // launches the tray app.
        var command = CommandLine.Parse(args);
        if (command.Kind is not Command.Tray)
            return Cli.Run(command);

        AppDomain.CurrentDomain.UnhandledException += (_, e) => StartupLog.Fail(e.ExceptionObject as Exception);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                _ = new AudioSwitcher.App();
            }
            catch (Exception ex)
            {
                StartupLog.Fail(ex);
                throw;
            }
        });

        return 0;
    }
}
