using System.Windows.Forms;
using System.Drawing;
using AudioSwitcher.AudioApi.CoreAudio;

sealed class TrayContext : ApplicationContext
{
    readonly AppSettings _settings;
    readonly CoreAudioController _controller = new();
    readonly NotifyIcon _icon;
    HotkeyWindow? _hotkey;
    ToastForm? _toast;
    AutoStartStatus _autoStart = AutoStartStatus.Loading;

    public TrayContext(AppSettings settings)
    {
        _settings = settings;

        _icon = new NotifyIcon
        {
            Icon = TrayArt.Speaker(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _icon.DoubleClick += (_, _) => SwitchNow();
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _icon.ContextMenuStrip.Opened += (_, _) => Win11.RoundCorners(_icon.ContextMenuStrip);
        UpdateTooltip();

        TryRegisterHotkey();
        BuildMenu();
        _ = RefreshAutoStartStatusAsync(rebuildMenu: true);
    }

    void TryRegisterHotkey()
    {
        _hotkey?.Dispose();
        _hotkey = null;

        if (!HotKey.TryParse(_settings.Hotkey, out uint mods, out uint vk))
        {
            Notify("Invalid hotkey", $"\"{_settings.Hotkey}\" is not a valid hotkey.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            _hotkey = new HotkeyWindow(mods, vk);
            _hotkey.Pressed += SwitchNow;
        }
        catch (Exception ex)
        {
            Notify("Hotkey unavailable", $"{_settings.Hotkey}: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    async Task RefreshAutoStartStatusAsync(bool rebuildMenu)
    {
        _autoStart = await AutoStart.GetStatusAsync();
        if (rebuildMenu) BuildMenu();
    }

    void BuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;

        bool light = Theme.IsLight;
        ToolStripManager.Renderer = new ModernMenuRenderer(light);
        menu.BackColor = Theme.Back(light);
        menu.ForeColor = Theme.Fore(light);
        menu.Items.Clear();

        var devices = _controller.GetPlaybackDevices(DeviceState.Active).OrderBy(device => device.FullName).ToList();
        string currentOutput = _controller.DefaultPlaybackDevice?.FullName ?? "No active default playback device";

        menu.Items.Add(InfoItem("Current output"));
        menu.Items.Add(InfoItem(Truncate(currentOutput, 68)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem($"Switch now  ({_settings.Hotkey})", null, (_, _) => SwitchNow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(DeviceMenu("Device 1", _settings.Device1, devices, name =>
        {
            _settings.Device1 = name;
            SettingsStore.Save(_settings);
            UpdateTooltip();
        }));
        menu.Items.Add(DeviceMenu("Device 2", _settings.Device2, devices, name =>
        {
            _settings.Device2 = name;
            SettingsStore.Save(_settings);
            UpdateTooltip();
        }));
        menu.Items.Add(new ToolStripMenuItem($"Set hotkey...  ({_settings.Hotkey})", null, (_, _) => ConfigureHotkey()));
        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Start with Windows", null, async (_, _) => await ToggleAutoStartAsync())
        {
            Checked = _autoStart.Enabled,
            Enabled = _autoStart.CanToggle,
        };
        menu.Items.Add(autostart);
        if (!string.IsNullOrWhiteSpace(_autoStart.Detail))
            menu.Items.Add(InfoItem(_autoStart.Detail));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem($"About {AppMetadata.ProductName}", null, (_, _) => AboutDialog.Show()));
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));
    }

    ToolStripMenuItem DeviceMenu(string label, string current, IReadOnlyList<CoreAudioDevice> devices, Action<string> onPick)
    {
        var resolved = string.IsNullOrWhiteSpace(current)
            ? null
            : devices.FirstOrDefault(device => device.FullName.Contains(current, StringComparison.OrdinalIgnoreCase));

        string status = resolved?.FullName
            ?? (current.Length > 0 ? current + " (not found)" : "<not set>");

        var root = new ToolStripMenuItem($"{label}:  {Truncate(status, 48)}");
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);

        foreach (var device in devices)
        {
            string name = device.FullName;
            var item = new ToolStripMenuItem(name)
            {
                Checked = resolved != null && device.Id == resolved.Id,
            };
            item.Click += (_, _) =>
            {
                onPick(name);
                Notify(label + " updated", name, ToolTipIcon.Info);
            };
            root.DropDownItems.Add(item);
        }

        if (root.DropDownItems.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem("(no active devices)") { Enabled = false });

        return root;
    }

    void ConfigureHotkey()
    {
        var picked = HotkeyDialog.Ask(_settings.Hotkey);
        if (picked is null || picked == _settings.Hotkey) return;

        _settings.Hotkey = picked;
        SettingsStore.Save(_settings);
        TryRegisterHotkey();
        BuildMenu();
        Notify("Hotkey updated", _settings.Hotkey, ToolTipIcon.Info);
    }

    async Task ToggleAutoStartAsync()
    {
        bool enable = !_autoStart.Enabled;
        _autoStart = AutoStartStatus.Loading;
        BuildMenu();

        var status = await AutoStart.SetEnabledAsync(enable);
        _autoStart = status;
        BuildMenu();

        if (enable && status.Enabled)
            Notify("Start with Windows enabled", "audsw will start when you sign in.", ToolTipIcon.Info);
        else if (!enable && !status.Enabled && status.CanToggle)
            Notify("Start with Windows disabled", "audsw will no longer start automatically.", ToolTipIcon.Info);
        else
            Notify("Autostart unavailable", status.Detail, ToolTipIcon.Warning);
    }

    void SwitchNow()
    {
        var target = Audio.DoCycle(_controller, _settings);
        if (target is null)
        {
            Notify("Set up devices first", "Choose Device 1 and Device 2 from the tray menu before switching.", ToolTipIcon.Warning);
            return;
        }

        UpdateTooltip();
        Notify("Audio output", target.FullName, ToolTipIcon.Info);
    }

    void UpdateTooltip()
    {
        string current = _controller.DefaultPlaybackDevice?.FullName ?? "unknown";
        _icon.Text = Truncate(AppMetadata.ProductName + " - " + current, 63);
    }

    void Notify(string title, string text, ToolTipIcon kind)
    {
        bool light = Theme.IsLight;
        Color foreground = kind switch
        {
            ToolTipIcon.Warning => Theme.Warning(light),
            ToolTipIcon.Error => Theme.Error(light),
            _ => Theme.Fore(light),
        };
        string message = string.IsNullOrEmpty(text) ? title : text;

        var oldToast = _toast;
        _toast = null;
        oldToast?.Close();

        var toast = new ToastForm(message, foreground, Theme.Back(light));
        _toast = toast;
        toast.Show();
    }

    void ExitApp()
    {
        _icon.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toast?.Close();
            _hotkey?.Dispose();
            _icon.Dispose();
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    static ToolStripMenuItem InfoItem(string text) => new(text) { Enabled = false };

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "...";
}
