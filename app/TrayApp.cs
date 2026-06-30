using AudioSwitcher.AudioApi.CoreAudio;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;

// Owns the tray icon, its context menu, the global hotkeys, and the switch actions.
sealed class TrayApp : IDisposable
{
    AppSettings _settings;
    readonly CoreAudioController _controller = new();
    readonly TaskbarIcon _icon;
    readonly HotkeyManager _hotkeys = new();
    SettingsWindow? _settingsWindow;

    public TrayApp()
    {
        _settings = SettingsStore.Load();

        _icon = new TaskbarIcon
        {
            ToolTipText = AppMetadata.ProductName,
            IconSource = new GeneratedIconSource { Text = "", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets") },
            NoLeftClickDelay = true,
            DoubleClickCommand = new RelayCommand(CycleOutputs),
        };

        var menu = new MenuFlyout();
        menu.Opening += (_, _) => BuildMenu(menu);
        _icon.ContextFlyout = menu;
        _icon.ForceCreate();

        RebindHotkeys();
        UpdateTooltip();
    }

    void RebindHotkeys()
    {
        _hotkeys.Clear();
        var failed = new List<string>();

        void Bind(string spec, Action action)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            if (!_hotkeys.Register(spec, action)) failed.Add(spec);
        }

        Bind(_settings.CycleOutputs, CycleOutputs);
        Bind(_settings.CycleInputs, CycleInputs);
        Bind(_settings.CyclePairs, CyclePairs);

        foreach (var entry in _settings.Outputs)
            Bind(entry.Hotkey, () => JumpTo(AudioKind.Output, entry.Match));
        foreach (var entry in _settings.Inputs)
            Bind(entry.Hotkey, () => JumpTo(AudioKind.Input, entry.Match));
        foreach (var pair in _settings.Pairs)
            Bind(pair.Hotkey, () => ApplyPair(pair));

        if (failed.Count > 0)
            Notify("Some hotkeys are in use", "Could not register: " + string.Join(", ", failed.Distinct()));
    }

    void BuildMenu(MenuFlyout menu)
    {
        menu.Items.Clear();

        string currentOutput = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? "No default output";
        string currentInput = Audio.CurrentDefault(_controller, AudioKind.Input)?.FullName ?? "No default input";

        menu.Items.Add(Info("Output:  " + Truncate(currentOutput, 48)));
        menu.Items.Add(Info("Input:   " + Truncate(currentInput, 48)));
        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(Item(CycleLabel("Cycle output", _settings.CycleOutputs), CycleOutputs));
        menu.Items.Add(Item(CycleLabel("Cycle input", _settings.CycleInputs), CycleInputs));
        menu.Items.Add(Item(CycleLabel("Cycle pair", _settings.CyclePairs), CyclePairs));
        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(DeviceSubMenu("Output", AudioKind.Output));
        menu.Items.Add(DeviceSubMenu("Input", AudioKind.Input));
        menu.Items.Add(PairsSubMenu());
        menu.Items.Add(new MenuFlyoutSeparator());

        menu.Items.Add(Item("Settings...", OpenSettings));
        menu.Items.Add(Item("Exit", Exit));
    }

    MenuFlyoutSubItem DeviceSubMenu(string label, AudioKind kind)
    {
        var root = new MenuFlyoutSubItem { Text = label };
        var devices = Audio.Devices(_controller, kind).OrderBy(device => device.FullName).ToList();
        var current = Audio.CurrentDefault(_controller, kind);

        foreach (var device in devices)
        {
            string name = device.FullName;
            var item = new ToggleMenuFlyoutItem { Text = name, IsChecked = current != null && device.Id == current.Id };
            item.Click += (_, _) => JumpTo(kind, name);
            root.Items.Add(item);
        }

        if (root.Items.Count == 0)
            root.Items.Add(new MenuFlyoutItem { Text = "(no active devices)", IsEnabled = false });

        return root;
    }

    MenuFlyoutSubItem PairsSubMenu()
    {
        var root = new MenuFlyoutSubItem { Text = "Pairs" };

        foreach (var pair in _settings.Pairs)
        {
            string label = string.IsNullOrWhiteSpace(pair.Name) ? $"{pair.Output} + {pair.Input}" : pair.Name;
            var item = new MenuFlyoutItem { Text = Truncate(label, 40) };
            item.Click += (_, _) => ApplyPair(pair);
            root.Items.Add(item);
        }

        if (root.Items.Count == 0)
            root.Items.Add(new MenuFlyoutItem { Text = "(no pairs - add in Settings)", IsEnabled = false });

        return root;
    }

    void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var window = new SettingsWindow(_controller, _settings, OnSettingsSaved);
        _settingsWindow = window;
        window.Closed += (_, _) => _settingsWindow = null;
        window.Activate();
    }

    void OnSettingsSaved(AppSettings updated)
    {
        _settings = updated;
        SettingsStore.Save(_settings);
        RebindHotkeys();
        UpdateTooltip();
    }

    // --- Switch actions ---

    void CycleOutputs() => CycleRing(AudioKind.Output, "outputs");
    void CycleInputs() => CycleRing(AudioKind.Input, "inputs");

    void CycleRing(AudioKind kind, string what)
    {
        var ring = (kind == AudioKind.Output ? _settings.Outputs : _settings.Inputs).Select(entry => entry.Match).ToList();
        if (ring.Count == 0)
        {
            Notify("No " + what + " configured", "Add devices to the " + what + " ring in Settings.");
            return;
        }

        var target = Audio.CycleRing(_controller, kind, ring);
        if (target is null)
        {
            Notify("Nothing to switch to", "None of the configured " + what + " are currently active.");
            return;
        }

        UpdateTooltip();
        Notify(kind == AudioKind.Output ? "Audio output" : "Audio input", target.FullName);
    }

    void CyclePairs()
    {
        var pair = Audio.NextPair(_controller, _settings.Pairs);
        if (pair is null)
        {
            Notify("No pairs configured", "Create output+input pairs in Settings.");
            return;
        }

        ApplyPair(pair);
    }

    void ApplyPair(PairEntry pair)
    {
        var result = Audio.SetPair(_controller, pair.Output, pair.Input);
        if (!result.Any)
        {
            Notify("Pair unavailable", "Neither device in this pair is currently active.");
            return;
        }

        UpdateTooltip();
        string detail = string.Join(" + ", new[] { result.Output?.FullName, result.Input?.FullName }.Where(name => name is not null));
        Notify(string.IsNullOrWhiteSpace(pair.Name) ? "Pair switched" : pair.Name, detail);
    }

    void JumpTo(AudioKind kind, string match)
    {
        var device = Audio.SetTo(_controller, kind, match);
        if (device is null)
        {
            Notify("Device unavailable", $"\"{match}\" is not currently active.");
            return;
        }

        UpdateTooltip();
        Notify(kind == AudioKind.Output ? "Audio output" : "Audio input", device.FullName);
    }

    void UpdateTooltip()
    {
        string current = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? "unknown";
        _icon.ToolTipText = Truncate(AppMetadata.ProductName + " - " + current, 63);
    }

    void Notify(string title, string message)
    {
        try
        {
            _icon.ShowNotification(title: title, message: message);
        }
        catch
        {
            // Notifications are best-effort; never let a toast failure break switching.
        }
    }

    void Exit()
    {
        Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    public void Dispose()
    {
        _hotkeys.Dispose();
        _icon.Dispose();
        _controller.Dispose();
    }

    static MenuFlyoutItem Item(string text, Action onClick)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => onClick();
        return item;
    }

    static MenuFlyoutItem Info(string text) => new() { Text = text, IsEnabled = false };

    static string CycleLabel(string label, string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? label : $"{label}  ({hotkey})";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "...";
}
