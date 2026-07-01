using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

// Hidden message-only window that hosts any number of global hotkeys, each mapped
// to its own action. Bindings are registered independently so one conflict does
// not disable the rest.
sealed class HotkeyManager : NativeWindow, IDisposable
{
    const int WM_HOTKEY = 0x0312;

    readonly Dictionary<int, Action> _actions = new();
    int _nextId = 1;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams { Parent = (IntPtr)(-3) }); // HWND_MESSAGE
    }

    // Register one binding. Returns false (without throwing) if the spec is invalid
    // or the combo is already taken by another app, so callers can warn and move on.
    public bool Register(string spec, Action onPressed)
    {
        if (!HotKey.TryParse(spec, out uint mods, out uint vk)) return false;

        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(Handle, id, mods | NativeMethods.MOD_NOREPEAT, vk))
            return false;

        _actions[id] = onPressed;
        return true;
    }

    // Unregister every binding (used before re-applying changed settings).
    public void Clear()
    {
        foreach (int id in _actions.Keys)
            NativeMethods.UnregisterHotKey(Handle, id);
        _actions.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _actions.TryGetValue((int)m.WParam, out var action))
            action();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            Clear();
            DestroyHandle();
        }
    }
}

static class HotKey
{
    const uint MOD_ALT = 0x0001;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint MOD_WIN = 0x0008;

    internal readonly record struct HotkeySpec(uint Modifiers, uint VirtualKey);

    public static bool TryParse(string spec, out HotkeySpec hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        uint mods = 0;
        uint vk = 0;
        bool sawKey = false;

        foreach (var rawPart in spec.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim().ToLowerInvariant();
            switch (part)
            {
                case "ctrl":
                case "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": mods |= MOD_WIN; break;
                default:
                    if (sawKey || !TryKey(part, out vk)) return false;
                    sawKey = true;
                    break;
            }
        }

        if (!sawKey || mods == 0) return false;

        hotkey = new HotkeySpec(mods, vk);
        return true;
    }

    public static bool TryParse(string spec, out uint mods, out uint vk)
    {
        if (TryParse(spec, out HotkeySpec hotkey))
        {
            mods = hotkey.Modifiers;
            vk = hotkey.VirtualKey;
            return true;
        }

        mods = 0;
        vk = 0;
        return false;
    }

    public static string Format(HotkeySpec hotkey)
    {
        string? key = Token((Keys)hotkey.VirtualKey);
        if (key is null) throw new ArgumentOutOfRangeException(nameof(hotkey), "Unsupported virtual key.");

        var parts = new List<string>(5);
        if ((hotkey.Modifiers & MOD_CONTROL) != 0) parts.Add("ctrl");
        if ((hotkey.Modifiers & MOD_ALT) != 0) parts.Add("alt");
        if ((hotkey.Modifiers & MOD_SHIFT) != 0) parts.Add("shift");
        if ((hotkey.Modifiers & MOD_WIN) != 0) parts.Add("win");
        parts.Add(key);
        return string.Join("+", parts);
    }

    public static string? FromKeyEvent(Keys keyCode, bool ctrl, bool alt, bool shift, bool win)
    {
        string? key = Token(keyCode);
        if (key is null) return null;

        var parts = new List<string>(4);
        if (ctrl) parts.Add("ctrl");
        if (alt) parts.Add("alt");
        if (shift) parts.Add("shift");
        if (win) parts.Add("win");
        if (parts.Count == 0) return null;

        parts.Add(key);
        return TryParse(string.Join("+", parts), out HotkeySpec hotkey) ? Format(hotkey) : null;
    }

    static bool TryKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1)
        {
            char ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = ch;
                return true;
            }

            return false;
        }

        if (key.Length is 2 or 3 && key[0] == 'f' && int.TryParse(key[1..], out int number) && number is >= 1 and <= 12)
        {
            vk = (uint)(0x70 + (number - 1));
            return true;
        }

        return false;
    }

    static string? Token(Keys key) => key switch
    {
        >= Keys.A and <= Keys.Z => char.ToLowerInvariant((char)key).ToString(),
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => ((char)('0' + (key - Keys.NumPad0))).ToString(),
        >= Keys.F1 and <= Keys.F12 => "f" + (key - Keys.F1 + 1),
        _ => null,
    };
}

static class HotkeyDialog
{
    public static string? Ask(string current)
    {
        bool light = Theme.IsLight;

        using var form = new Form
        {
            Text = "Set hotkey",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(430, 226),
            BackColor = Theme.Content(light),
            ForeColor = Theme.Fore(light),
            KeyPreview = true,
        };

        form.Shown += (_, _) => Win11.ApplyChrome(form, light);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 0),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F),
            Text = "Press a new shortcut",
            Margin = new Padding(0, 0, 0, 8),
        };

        var currentLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            Text = $"Current: {current}",
            Margin = new Padding(0, 0, 0, 8),
        };

        var hint = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Text = "Use Ctrl, Alt, Shift, or Win together with a letter, digit, or F1-F12. Press Esc to cancel.",
            MaximumSize = new Size(380, 0),
            Margin = new Padding(0, 0, 0, 12),
        };

        var status = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Text = "Waiting for a valid shortcut...",
            ForeColor = Theme.Fore(light),
            Margin = new Padding(0, 6, 0, 0),
        };

        var cancel = new Win11Button(light) { Text = "Cancel", DialogResult = DialogResult.Cancel };

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(currentLabel, 0, 1);
        root.Controls.Add(hint, 0, 2);
        root.Controls.Add(status, 0, 3);
        form.Controls.Add(root);
        form.Controls.Add(DialogFooter.Create(light, cancel));
        root.BringToFront();
        form.CancelButton = cancel;

        string? result = null;
        form.KeyDown += (_, e) =>
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape)
            {
                form.DialogResult = DialogResult.Cancel;
                return;
            }

            if (e.KeyCode is Keys.Menu or Keys.ShiftKey or Keys.ControlKey or Keys.LWin or Keys.RWin)
                return;

            string? spec = HotKey.FromKeyEvent(e.KeyCode, e.Control, e.Alt, e.Shift, WinDown());
            if (spec is null)
            {
                status.ForeColor = Theme.Warning(light);
                status.Text = "Need a modifier plus a letter, digit, or F1-F12. Try again.";
                return;
            }

            result = spec;
            form.DialogResult = DialogResult.OK;
        };

        return form.ShowDialog() == DialogResult.OK ? result : null;
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    static bool WinDown() =>
        (GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
        (GetAsyncKeyState(0x5C) & 0x8000) != 0;
}

// One small window to edit both cycle hotkeys at once. Works on local copies so
// Cancel discards; on Save the new values are written into the settings object.
// Returns true when the settings were changed. Only one instance can be open:
// asking again focuses the existing window instead of stacking a duplicate.
static class HotkeysWindow
{
    static Form? _open;

    public static bool Edit(AppSettings settings)
    {
        if (_open is not null)
        {
            _open.Activate();
            return false;
        }

        bool light = Theme.IsLight;
        string cycleOutputs = settings.CycleOutputs;
        string cycleInputs = settings.CycleInputs;

        using var form = new Form
        {
            Text = "Hotkeys",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(440, 178),
            BackColor = Theme.Content(light),
            ForeColor = Theme.Fore(light),
            Font = new Font("Segoe UI", 9.5F),
        };
        form.Shown += (_, _) => Win11.ApplyChrome(form, light);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 0),
            ColumnCount = 3,
            RowCount = 2,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        AddRow(layout, 0, "Cycle outputs", light, () => cycleOutputs, value => cycleOutputs = value);
        AddRow(layout, 1, "Cycle inputs", light, () => cycleInputs, value => cycleInputs = value);

        var save = new Win11Button(light) { Text = "Save", Accent = true, DialogResult = DialogResult.OK };
        var cancel = new Win11Button(light) { Text = "Cancel", DialogResult = DialogResult.Cancel };

        form.Controls.Add(layout);
        form.Controls.Add(DialogFooter.Create(light, save, cancel));
        layout.BringToFront();
        form.AcceptButton = save;
        form.CancelButton = cancel;

        _open = form;
        try
        {
            if (form.ShowDialog() != DialogResult.OK) return false;
        }
        finally
        {
            _open = null;
        }

        if (cycleOutputs == settings.CycleOutputs && cycleInputs == settings.CycleInputs) return false;

        settings.CycleOutputs = cycleOutputs;
        settings.CycleInputs = cycleInputs;
        return true;
    }

    static void AddRow(TableLayoutPanel layout, int row, string caption, bool light, Func<string> get, Action<string> set)
    {
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Theme.Fore(light),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 8, 10),
        };

        var pick = new Win11Button(light) { Text = Show(get()), Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 10) };
        pick.Click += (_, _) =>
        {
            string? picked = HotkeyDialog.Ask(get());
            if (picked is not null) { set(picked); pick.Text = Show(picked); }
        };

        var clear = new Win11Button(light) { Text = "Clear", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
        clear.Click += (_, _) => { set(""); pick.Text = Show(""); };

        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(pick, 1, row);
        layout.Controls.Add(clear, 2, row);
    }

    static string Show(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "(none)" : hotkey;
}
