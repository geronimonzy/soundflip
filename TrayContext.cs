using System.Windows.Forms;
using System.Drawing;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

sealed class TrayContext : ApplicationContext
{
    AppSettings _settings;
    readonly CoreAudioController _controller = new();
    readonly NotifyIcon _icon;
    readonly HotkeyManager _hotkeys = new();
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
        _icon.DoubleClick += (_, _) => CycleOutputs();
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _icon.ContextMenuStrip.Opened += (_, _) => Win11.RoundCorners(_icon.ContextMenuStrip);
        UpdateTooltip();

        RebindHotkeys();
        BuildMenu();
        _ = RefreshAutoStartStatusAsync(rebuildMenu: true);
    }

    // Re-apply every hotkey from current settings: the three cycle commands plus
    // each device/pair direct-jump binding. One warning summarizes any conflicts.
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
            Notify("Some hotkeys are in use",
                "Could not register: " + string.Join(", ", failed.Distinct()),
                ToolTipIcon.Warning);
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

        string currentOutput = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? "No default output";
        string currentInput = Audio.CurrentDefault(_controller, AudioKind.Input)?.FullName ?? "No default input";

        menu.Items.Add(InfoItem("Output:  " + Truncate(currentOutput, 60)));
        menu.Items.Add(InfoItem("Input:   " + Truncate(currentInput, 60)));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem(CycleLabel("Cycle output", _settings.CycleOutputs), null, (_, _) => CycleOutputs()));
        menu.Items.Add(new ToolStripMenuItem(CycleLabel("Cycle input", _settings.CycleInputs), null, (_, _) => CycleInputs()));
        menu.Items.Add(new ToolStripMenuItem(CycleLabel("Cycle pair", _settings.CyclePairs), null, (_, _) => CyclePairs()));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(DeviceMenu("Output", AudioKind.Output));
        menu.Items.Add(DeviceMenu("Input", AudioKind.Input));
        menu.Items.Add(PairsMenu());
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => OpenSettings()));
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

    // Live list of active devices of one kind; checking the current default. Picking
    // one sets it as default+communications immediately.
    ToolStripMenuItem DeviceMenu(string label, AudioKind kind)
    {
        var devices = Audio.Devices(_controller, kind).OrderBy(device => device.FullName).ToList();
        var current = Audio.CurrentDefault(_controller, kind);

        var root = new ToolStripMenuItem(label);
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);

        foreach (var device in devices)
        {
            string name = device.FullName;
            var item = new ToolStripMenuItem(name)
            {
                Checked = current != null && device.Id == current.Id,
            };
            item.Click += (_, _) => JumpTo(kind, name);
            root.DropDownItems.Add(item);
        }

        if (root.DropDownItems.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem("(no active devices)") { Enabled = false });

        return root;
    }

    ToolStripMenuItem PairsMenu()
    {
        var root = new ToolStripMenuItem("Pairs");
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);

        foreach (var pair in _settings.Pairs)
        {
            string label = string.IsNullOrWhiteSpace(pair.Name)
                ? $"{pair.Output} + {pair.Input}"
                : pair.Name;
            var item = new ToolStripMenuItem(Truncate(label, 48));
            item.Click += (_, _) => ApplyPair(pair);
            root.DropDownItems.Add(item);
        }

        if (root.DropDownItems.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem("(no pairs - add in Settings)") { Enabled = false });

        return root;
    }

    void OpenSettings()
    {
        var updated = SettingsWindow.Edit(_controller, _settings);
        if (updated is null) return;

        _settings = updated;
        SettingsStore.Save(_settings);
        RebindHotkeys();
        UpdateTooltip();
        BuildMenu();
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

    // --- Switch actions ---

    void CycleOutputs() => CycleRing(AudioKind.Output, "outputs");
    void CycleInputs() => CycleRing(AudioKind.Input, "inputs");

    void CycleRing(AudioKind kind, string what)
    {
        var ring = (kind == AudioKind.Output ? _settings.Outputs : _settings.Inputs)
            .Select(entry => entry.Match).ToList();

        if (ring.Count == 0)
        {
            Notify("No " + what + " configured", "Add devices to the " + what + " ring in Settings.", ToolTipIcon.Warning);
            return;
        }

        var target = Audio.CycleRing(_controller, kind, ring);
        if (target is null)
        {
            Notify("Nothing to switch to", "None of the configured " + what + " are currently active.", ToolTipIcon.Warning);
            return;
        }

        UpdateTooltip();
        Notify(kind == AudioKind.Output ? "Audio output" : "Audio input", target.FullName, ToolTipIcon.Info);
    }

    void CyclePairs()
    {
        var pair = Audio.NextPair(_controller, _settings.Pairs);
        if (pair is null)
        {
            Notify("No pairs configured", "Create output+input pairs in Settings.", ToolTipIcon.Warning);
            return;
        }

        ApplyPair(pair);
    }

    void ApplyPair(PairEntry pair)
    {
        var result = Audio.SetPair(_controller, pair.Output, pair.Input);
        if (!result.Any)
        {
            Notify("Pair unavailable", "Neither device in this pair is currently active.", ToolTipIcon.Warning);
            return;
        }

        UpdateTooltip();
        string detail = string.Join(
            "\n",
            new[] { result.Output?.FullName, result.Input?.FullName }.Where(name => name is not null));
        Notify(string.IsNullOrWhiteSpace(pair.Name) ? "Pair switched" : pair.Name, detail, ToolTipIcon.Info);
    }

    void JumpTo(AudioKind kind, string match)
    {
        var device = Audio.SetTo(_controller, kind, match);
        if (device is null)
        {
            Notify("Device unavailable", $"\"{match}\" is not currently active.", ToolTipIcon.Warning);
            return;
        }

        UpdateTooltip();
        Notify(kind == AudioKind.Output ? "Audio output" : "Audio input", device.FullName, ToolTipIcon.Info);
    }

    void UpdateTooltip()
    {
        string current = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? "unknown";
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
            _hotkeys.Dispose();
            _icon.Dispose();
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    static ToolStripMenuItem InfoItem(string text) => new(text) { Enabled = false };

    static string CycleLabel(string label, string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? label : $"{label}  ({hotkey})";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "...";
}
