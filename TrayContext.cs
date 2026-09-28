using System.Windows.Forms;
using System.Drawing;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

sealed class TrayContext : ApplicationContext
{
    readonly AppSettings _settings;
    readonly CoreAudioController _controller = new();
    readonly NotifyIcon _icon;
    readonly HotkeyManager _hotkeys = new();
    ToastForm? _toast;
    AutoStartStatus _autoStart = AutoStartStatus.Loading;
    bool? _rendererLight;

    public TrayContext(AppSettings settings, bool firstRun = false)
    {
        _settings = settings;

        _icon = new NotifyIcon
        {
            Icon = TrayArt.Speaker(),
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };
        // Left-click opens the same menu as right-click: a click that does nothing
        // reads as broken. (This replaces double-click-to-cycle, which could not
        // coexist with it and was undiscoverable anyway.)
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMenu(); };
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _icon.ContextMenuStrip.Opened += (_, _) => Win11.RoundCorners(_icon.ContextMenuStrip);
        UpdateTooltip();

        RebindHotkeys();
        BuildMenu();
        _ = RefreshAutoStartStatusAsync(rebuildMenu: true);

        if (firstRun)
        {
            // Persist right away so the welcome shows exactly once, however the
            // app is closed afterwards.
            SettingsStore.Save(_settings);

            // Deferred until the message loop runs (and the tray icon exists).
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                await ShowWelcomeAsync();
            };
            timer.Start();
        }
    }

    async Task ShowWelcomeAsync()
    {
        _autoStart = await AutoStart.GetStatusAsync();
        bool offerAutoStart = _autoStart.CanToggle && !_autoStart.Enabled;

        if (WelcomeDialog.Show(_settings.Language, _settings.CycleOutputs, offerAutoStart, SetLanguage))
            await ToggleAutoStartAsync();
        else if (!_icon.ContextMenuStrip!.Visible)
            BuildMenu();
    }

    // Re-apply the two cycle hotkeys from current settings. One warning summarizes
    // any conflicts.
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

        if (failed.Count > 0)
            Notify(Str.HotkeysInUseTitle.T(),
                Str.HotkeysInUseText.T(string.Join(", ", failed.Distinct())),
                ToolTipIcon.Warning);
    }

    async Task RefreshAutoStartStatusAsync(bool rebuildMenu)
    {
        _autoStart = await AutoStart.GetStatusAsync();
        if (rebuildMenu && !_icon.ContextMenuStrip!.Visible) BuildMenu();
    }

    void BuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;

        bool light = Theme.IsLight;
        if (_rendererLight != light)
        {
            ToolStripManager.Renderer = new ModernMenuRenderer(light);
            _rendererLight = light;
        }
        menu.BackColor = Theme.Back(light);
        menu.ForeColor = Theme.Fore(light);

        var oldItems = menu.Items.Cast<ToolStripItem>().ToArray();
        foreach (var item in oldItems) item.Dispose();
        menu.Items.Clear();

        string currentOutput = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? Str.NoDefaultOutput.T();
        string currentInput = Audio.CurrentDefault(_controller, AudioKind.Input)?.FullName ?? Str.NoDefaultInput.T();

        menu.Items.Add(InfoItem(Str.MenuOutputCurrent.T(Truncate(currentOutput, 60))));
        menu.Items.Add(InfoItem(Str.MenuInputCurrent.T(Truncate(currentInput, 60))));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem(CycleLabel(Str.CycleOutput.T(), _settings.CycleOutputs), null, (_, _) => CycleOutputs()));
        menu.Items.Add(new ToolStripMenuItem(CycleLabel(Str.CycleInput.T(), _settings.CycleInputs), null, (_, _) => CycleInputs()));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(DeviceMenu(Str.Output.T(), AudioKind.Output));
        menu.Items.Add(DeviceMenu(Str.Input.T(), AudioKind.Input));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem(Str.Hotkeys.T(), null, (_, _) => EditHotkeys()));
        menu.Items.Add(LanguageMenu());
        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem(Str.StartWithWindows.T(), null, async (_, _) => await ToggleAutoStartAsync())
        {
            Checked = _autoStart.Enabled,
            Enabled = _autoStart.CanToggle,
        };
        menu.Items.Add(autostart);
        if (!string.IsNullOrWhiteSpace(_autoStart.Detail))
            menu.Items.Add(InfoItem(_autoStart.Detail));

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(Str.About.T(AppMetadata.ProductName), null, (_, _) => AboutDialog.Show()));
        menu.Items.Add(new ToolStripMenuItem(Str.Exit.T(), null, (_, _) => ExitApp()));
    }

    // Radio-style language picker. "System default" follows the Windows display
    // language; a concrete choice is saved immediately and the menu, tooltip and
    // any dialog opened afterwards use it. Language names are shown in their own
    // language and never translated.
    ToolStripMenuItem LanguageMenu()
    {
        var root = new ToolStripMenuItem(Str.Language.T());
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);

        string selected = Loc.Normalize(_settings.Language);
        void Add(string code, string label)
        {
            var item = new ToolStripMenuItem(label) { Checked = selected == code };
            item.Click += (_, _) => SetLanguage(code);
            root.DropDownItems.Add(item);
        }

        Add(Loc.Auto, Str.LanguageSystem.T());
        root.DropDownItems.Add(new ToolStripSeparator());
        foreach (var code in Loc.Supported) Add(code, Loc.NativeName(code));

        return root;
    }

    void SetLanguage(string code)
    {
        _settings.Language = code;
        SettingsStore.Save(_settings);
        Loc.Apply(code);
        UpdateTooltip();
        if (!_icon.ContextMenuStrip!.Visible) BuildMenu();
    }

    // Live checklist of active devices of one kind: the check marks membership in
    // the cycle ring, a "●" prefix marks the current default. Clicking toggles ring
    // membership (and keeps the menu open for multi-select).
    ToolStripMenuItem DeviceMenu(string label, AudioKind kind)
    {
        var devices = Audio.Devices(_controller, kind).OrderBy(device => device.FullName).ToList();
        var current = Audio.CurrentDefault(_controller, kind);
        var ring = kind == AudioKind.Output ? _settings.Outputs : _settings.Inputs;

        var root = new ToolStripMenuItem(label);
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);
        root.DropDown.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };

        foreach (var device in devices)
        {
            string name = device.FullName;
            bool isDefault = current != null && device.Id == current.Id;
            var item = new ToolStripMenuItem((isDefault ? "● " : "") + name)
            {
                Checked = InRing(ring, name),
            };
            item.Click += (_, _) =>
            {
                item.Checked = ToggleRing(ring, name);
                SettingsStore.Save(_settings);
            };
            root.DropDownItems.Add(item);
        }

        if (root.DropDownItems.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem(Str.NoActiveDevices.T()) { Enabled = false });
        else if (ring.Count == 0)
        {
            // Nothing ticked means the cycle walks every active device; say so, or
            // an all-unticked list reads as "nothing will happen".
            root.DropDownItems.Insert(0, InfoItem(Str.CycleAllHint.T()));
            root.DropDownItems.Insert(1, new ToolStripSeparator());
        }

        return root;
    }

    // NotifyIcon's own (non-public) ShowContextMenu does what a tray menu needs:
    // foreground activation so the menu closes when clicking elsewhere, and
    // placement next to the tray. Fall back to a plain Show if it ever moves.
    void ShowMenu()
    {
        var method = typeof(NotifyIcon).GetMethod("ShowContextMenu",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method is not null)
            method.Invoke(_icon, null);
        else
            _icon.ContextMenuStrip!.Show(Cursor.Position);
    }

    static bool InRing(List<DeviceEntry> ring, string deviceName) =>
        ring.Any(entry => Matches(entry, deviceName));

    // Add the device to the ring, or drop every entry that resolves to it. Returns
    // the new membership state.
    static bool ToggleRing(List<DeviceEntry> ring, string deviceName)
    {
        if (InRing(ring, deviceName))
        {
            ring.RemoveAll(entry => Matches(entry, deviceName));
            return false;
        }

        ring.Add(new DeviceEntry { Match = deviceName });
        return true;
    }

    // Same substring semantics as Audio.Resolve, so hand-edited partial matches
    // still show up checked.
    static bool Matches(DeviceEntry entry, string deviceName) =>
        !string.IsNullOrWhiteSpace(entry.Match)
        && deviceName.Contains(entry.Match, StringComparison.OrdinalIgnoreCase);

    void EditHotkeys()
    {
        if (!HotkeysWindow.Edit(_settings)) return;

        SettingsStore.Save(_settings);
        RebindHotkeys();
        BuildMenu();
    }

    async Task ToggleAutoStartAsync()
    {
        bool enable = !_autoStart.Enabled;
        _autoStart = AutoStartStatus.Loading;
        if (!_icon.ContextMenuStrip!.Visible) BuildMenu();

        try
        {
            var status = await AutoStart.SetEnabledAsync(enable);
            _autoStart = status;
            if (!_icon.ContextMenuStrip!.Visible) BuildMenu();

            if (enable && status.Enabled)
                Notify(Str.AutostartEnabledTitle.T(), Str.AutostartEnabledText.T(AppMetadata.ProductName), ToolTipIcon.Info);
            else if (!enable && !status.Enabled && status.CanToggle)
                Notify(Str.AutostartDisabledTitle.T(), Str.AutostartDisabledText.T(AppMetadata.ProductName), ToolTipIcon.Info);
            else
                Notify(Str.AutostartUnavailable.T(), status.Detail, ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            try
            {
                _autoStart = await AutoStart.GetStatusAsync();
            }
            catch
            {
                _autoStart = AutoStartStatus.Error(Str.StartupStatusUnavailable);
            }

            Notify(Str.AutostartUnavailable.T(), ex.Message, ToolTipIcon.Warning);
            if (!_icon.ContextMenuStrip!.Visible) BuildMenu();
            return;
        }
    }

    // --- Switch actions ---

    void CycleOutputs() => CycleRing(AudioKind.Output);
    void CycleInputs() => CycleRing(AudioKind.Input);

    void CycleRing(AudioKind kind)
    {
        bool output = kind == AudioKind.Output;
        var ring = (output ? _settings.Outputs : _settings.Inputs)
            .Select(entry => entry.Match).ToList();

        var before = Audio.CurrentDefault(_controller, kind);
        var target = Audio.CycleRing(_controller, kind, ring);
        if (target is null)
        {
            string detail = ring.Count == 0 ? "" : (output ? Str.NoOutputsActive : Str.NoInputsActive).T();
            Notify(Str.NothingToSwitch.T(), detail, ToolTipIcon.Warning);
            return;
        }

        UpdateTooltip();

        // Only one ring device is available and it is already the default: say so
        // instead of flashing its name as if a switch had happened.
        if (before is not null && target.Id == before.Id)
        {
            Notify((output ? Str.OnlyOneOutput : Str.OnlyOneInput).T(), target.FullName, ToolTipIcon.Info);
            return;
        }

        Notify((output ? Str.AudioOutput : Str.AudioInput).T(), target.FullName, ToolTipIcon.Info);
    }

    void UpdateTooltip()
    {
        string current = Audio.CurrentDefault(_controller, AudioKind.Output)?.FullName ?? Str.Unknown.T();
        _icon.Text = Truncate(AppMetadata.ProductName + " - " + current, 63);
    }

    void Notify(string title, string text, ToolTipIcon kind)
    {
        bool light = Theme.IsLight;
        Color titleColor = kind switch
        {
            ToolTipIcon.Warning => Theme.Warning(light),
            ToolTipIcon.Error => Theme.Error(light),
            _ => Theme.Fore(light),
        };
        // Switch confirmations are glanceable; warnings are sentences to read.
        int lifetimeMs = kind == ToolTipIcon.Info ? 2200 : 4000;

        var oldToast = _toast;
        _toast = null;
        oldToast?.Close();

        var toast = new ToastForm(title, text, titleColor, Theme.Fore(light), Theme.Back(light), lifetimeMs);
        toast.FormClosed += (_, _) => { if (ReferenceEquals(_toast, toast)) _toast = null; };
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
            var icon = _icon.Icon;
            _icon.Dispose();
            icon?.Dispose();
            _controller.Dispose();
        }

        base.Dispose(disposing);
    }

    static ToolStripMenuItem InfoItem(string text) => new(text) { Enabled = false };

    static string CycleLabel(string label, string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? label : $"{label}  ({hotkey})";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 3)] + "...";
}
