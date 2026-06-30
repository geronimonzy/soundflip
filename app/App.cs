using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// Code-only WinUI 3 Application (no App.xaml). It owns the tray app and lives for
// the lifetime of the process; there is no main window.
sealed class App : Application
{
    TrayApp? _tray;

    public App()
    {
        Resources.MergedDictionaries.Add(new XamlControlsResources());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _tray = new TrayApp();
    }
}
