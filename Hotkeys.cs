using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;

// Hidden message-only window that receives the global hotkey.
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    public event Action? Pressed;

    const int WM_HOTKEY = 0x0312;
    const int ID = 1;

    public HotkeyWindow(uint mods, uint vk)
    {
        CreateHandle(new CreateParams { Parent = (IntPtr)(-3) }); // HWND_MESSAGE
        if (!NativeMethods.RegisterHotKey(Handle, ID, mods | NativeMethods.MOD_NOREPEAT, vk))
            throw new InvalidOperationException("hotkey already in use");
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(Handle, ID);
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
            ClientSize = new Size(430, 220),
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            KeyPreview = true,
            TopMost = true,
        };

        form.Shown += (_, _) => Win11.RoundCorners(form);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            MaximumSize = new Size(390, 0),
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

        var cancel = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Right,
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(currentLabel, 0, 1);
        root.Controls.Add(hint, 0, 2);
        root.Controls.Add(status, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        form.Controls.Add(root);
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
