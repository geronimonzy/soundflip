using Microsoft.UI.Xaml;

namespace AudioSwitcher;

// WinUI 3 Application (App.xaml provides the XAML metadata + control resources). It
// owns the tray app and lives for the lifetime of the process; there is no main
// window. Startup failures are surfaced instead of silently killing the process.
sealed partial class App : Application
{
    TrayApp? _tray;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            StartupLog.Fail(e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _tray = new TrayApp();
        }
        catch (Exception ex)
        {
            StartupLog.Fail(ex);
            Exit();
        }
    }
}
