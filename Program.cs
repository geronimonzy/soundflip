using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;

// audsw -- minimal audio device switcher for Windows.
//
//   audsw list           list active playback devices
//   audsw set <name>     set default playback device (substring match)
//   audsw cycle          toggle between device1/device2 from audsw.cfg
//   audsw daemon         run in the tray; cycle on the hotkey, pick devices from the menu
//
// The switching default is the playback (render) device; both the "default"
// and "default communications" roles are set together.

return Run(args);

static int Run(string[] args)
{
    string cmd = (args.Length > 0 ? args[0] : "help").ToLowerInvariant();
    try
    {
        switch (cmd)
        {
            case "list":   return List();
            case "set":    return Set(args);
            case "cycle":  return Cycle();
            case "daemon": return Daemon();
            default:       return Help();
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("error: " + ex.Message);
        return 1;
    }
}

static int Help()
{
    Console.WriteLine(
        """
        audsw -- minimal audio device switcher

          audsw list           list active playback devices
          audsw set <name>     set default playback device (substring match)
          audsw cycle          toggle between the two devices in audsw.cfg
          audsw daemon         run in the tray; hotkey + menu to switch devices

        Config file: audsw.cfg (next to the .exe)
        """);
    return 0;
}

static int List()
{
    using var c = new CoreAudioController();
    foreach (var d in c.GetPlaybackDevices(DeviceState.Active).OrderBy(d => d.FullName))
        Console.WriteLine($"{(d.IsDefaultDevice ? "* " : "  ")}{d.FullName}");
    return 0;
}

static int Set(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: audsw set <name>");
        return 2;
    }
    using var c = new CoreAudioController();
    var dev = Audio.Resolve(c, args[1]);
    if (dev is null)
    {
        Console.Error.WriteLine($"no active playback device matching \"{args[1]}\"");
        return 1;
    }
    Audio.MakeDefault(dev);
    Console.WriteLine("-> " + dev.FullName);
    return 0;
}

static int Cycle()
{
    var cfg = Config.Load();
    using var c = new CoreAudioController();
    var target = Audio.DoCycle(c, cfg);
    if (target is null)
    {
        Console.Error.WriteLine(
            $"could not resolve both devices (device1=\"{cfg.Device1}\", device2=\"{cfg.Device2}\"). " +
            "Run `audsw list` and fix audsw.cfg.");
        return 1;
    }
    Console.WriteLine("-> " + target.FullName);
    return 0;
}

// ---------------------------------------------------------------------------
// daemon: tray icon + global hotkey, all on a single STA UI thread.
// ---------------------------------------------------------------------------

static int Daemon()
{
    var cfg = Config.Load();
    HideOwnConsoleWindow();

    var t = new Thread(() =>
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.Run(new TrayContext(cfg));
    });
    t.SetApartmentState(ApartmentState.STA);
    t.Start();
    t.Join();
    return 0;
}

static void HideOwnConsoleWindow()
{
    // Hide the console only when we own it alone (double-click / vbs launch);
    // leave an interactive terminal we were started from untouched.
    uint[] buf = new uint[2];
    uint count = GetConsoleProcessList(buf, (uint)buf.Length);
    if (count <= 1)
    {
        IntPtr hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero) ShowWindow(hwnd, SW_HIDE);
    }
}

// ---------------------------------------------------------------------------
// audio helpers shared by the CLI and the tray
// ---------------------------------------------------------------------------

static class Audio
{
    // Core toggle. Returns the device switched to, or null if either configured
    // device can't be resolved.
    public static CoreAudioDevice? DoCycle(CoreAudioController c, Config cfg)
    {
        var d1 = Resolve(c, cfg.Device1);
        var d2 = Resolve(c, cfg.Device2);
        if (d1 is null || d2 is null) return null;

        var cur = c.DefaultPlaybackDevice;
        var target = (cur != null && cur.Id == d1.Id) ? d2 : d1;
        MakeDefault(target);
        return target;
    }

    public static CoreAudioDevice? Resolve(CoreAudioController c, string query) =>
        string.IsNullOrWhiteSpace(query) ? null :
        c.GetPlaybackDevices(DeviceState.Active)
         .FirstOrDefault(d => d.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));

    public static void MakeDefault(CoreAudioDevice dev)
    {
        dev.SetAsDefault();
        dev.SetAsDefaultCommunications();
    }
}

// ---------------------------------------------------------------------------
// tray application
// ---------------------------------------------------------------------------

sealed class TrayContext : ApplicationContext
{
    readonly Config _cfg;
    readonly CoreAudioController _controller = new();
    readonly NotifyIcon _icon;
    HotkeyWindow? _hotkey;
    ToastForm? _toast;

    public TrayContext(Config cfg)
    {
        _cfg = cfg;

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
    }

    void TryRegisterHotkey()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        if (!HotKey.TryParse(_cfg.Hotkey, out uint mods, out uint vk))
        {
            Notify("Invalid hotkey", $"\"{_cfg.Hotkey}\" is not a valid hotkey.", ToolTipIcon.Warning);
            return;
        }
        try
        {
            _hotkey = new HotkeyWindow(mods, vk);
            _hotkey.Pressed += SwitchNow;
        }
        catch (Exception ex)
        {
            Notify("Hotkey unavailable", $"{_cfg.Hotkey}: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    void BuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;

        // Re-theme each open so it tracks the current Windows light/dark setting.
        bool light = Theme.IsLight;
        ToolStripManager.Renderer = new ModernMenuRenderer(light);
        menu.BackColor = Theme.Back(light);
        menu.ForeColor = Theme.Fore(light);

        menu.Items.Clear();

        // Snapshot the device list once; both sub-menus and their checks reuse it.
        var devices = _controller.GetPlaybackDevices(DeviceState.Active).OrderBy(d => d.FullName).ToList();

        menu.Items.Add(new ToolStripMenuItem($"Switch now  ({_cfg.Hotkey})", null, (_, _) => SwitchNow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(DeviceMenu("Device 1", _cfg.Device1, devices, name => { _cfg.Device1 = name; _cfg.Save(); }));
        menu.Items.Add(DeviceMenu("Device 2", _cfg.Device2, devices, name => { _cfg.Device2 = name; _cfg.Save(); }));
        menu.Items.Add(new ToolStripMenuItem($"Set hotkey…  ({_cfg.Hotkey})", null, (_, _) => ConfigureHotkey()));
        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutoStart())
        {
            Checked = AutoStart.IsEnabled,
        };
        menu.Items.Add(autostart);
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));
    }

    void ConfigureHotkey()
    {
        var picked = HotkeyDialog.Ask(_cfg.Hotkey);
        if (picked is null || picked == _cfg.Hotkey) return;
        _cfg.Hotkey = picked;
        _cfg.Save();
        TryRegisterHotkey();
    }

    ToolStripMenuItem DeviceMenu(string label, string current, IReadOnlyList<CoreAudioDevice> devices, Action<string> onPick)
    {
        var resolved = string.IsNullOrWhiteSpace(current) ? null
            : devices.FirstOrDefault(d => d.FullName.Contains(current, StringComparison.OrdinalIgnoreCase));
        var root = new ToolStripMenuItem($"{label}:  {resolved?.FullName ?? (current.Length > 0 ? current + " (not found)" : "<not set>")}");
        root.DropDownOpened += (_, _) => Win11.RoundCorners(root.DropDown);

        foreach (var d in devices)
        {
            string name = d.FullName;
            var item = new ToolStripMenuItem(name)
            {
                Checked = resolved != null && d.Id == resolved.Id,
            };
            item.Click += (_, _) => { onPick(name); UpdateTooltip(); };
            root.DropDownItems.Add(item);
        }
        if (root.DropDownItems.Count == 0)
            root.DropDownItems.Add(new ToolStripMenuItem("(no active devices)") { Enabled = false });
        return root;
    }

    void SwitchNow()
    {
        var target = Audio.DoCycle(_controller, _cfg);
        if (target is null)
        {
            Notify("Set up devices first", "Right-click the icon and choose Device 1 and Device 2.", ToolTipIcon.Warning);
            return;
        }
        UpdateTooltip();
        Notify("Audio output", target.FullName, ToolTipIcon.Info);
    }

    void ToggleAutoStart()
    {
        try
        {
            if (AutoStart.IsEnabled) AutoStart.Disable();
            else AutoStart.Enable();
        }
        catch (Exception ex)
        {
            Notify("Autostart failed", ex.Message, ToolTipIcon.Error);
        }
    }

    void UpdateTooltip()
    {
        string cur = _controller.DefaultPlaybackDevice?.FullName ?? "unknown";
        // NotifyIcon.Text is capped at 63 chars.
        _icon.Text = Trunc("audsw - " + cur, 63);
    }

    static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    void Notify(string title, string text, ToolTipIcon kind)
    {
        bool light = Theme.IsLight;
        Color fg = kind switch
        {
            ToolTipIcon.Warning => Theme.Warning(light),
            ToolTipIcon.Error   => Theme.Error(light),
            _                   => Theme.Fore(light),
        };
        string message = string.IsNullOrEmpty(text) ? title : text;

        // Replace any visible toast so they never stack.
        var old = _toast;
        _toast = null;
        old?.Close();

        var toast = new ToastForm(message, fg, Theme.Back(light));
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
}

// ---------------------------------------------------------------------------
// theme + drawing helpers
// ---------------------------------------------------------------------------

static class Theme
{
    // Windows "apps" light/dark setting. Defaults to dark if unreadable.
    public static bool IsLight
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return k?.GetValue("AppsUseLightTheme") is int v && v != 0;
            }
            catch { return false; }
        }
    }

    public static Color Back(bool light) => light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(44, 44, 44);
    public static Color Fore(bool light) => light ? Color.FromArgb(26, 26, 26) : Color.White;
    public static Color Hover(bool light) => light ? Color.FromArgb(24, 0, 0, 0) : Color.FromArgb(26, 255, 255, 255);
    public static Color Line(bool light) => light ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(28, 255, 255, 255);
    public static Color Disabled(bool light) => light ? Color.FromArgb(140, 140, 140) : Color.FromArgb(120, 120, 120);
    public static Color Warning(bool light) => light ? Color.FromArgb(0xB2, 0x6B, 0x00) : Color.FromArgb(0xF0, 0xC0, 0x60);
    public static Color Error(bool light) => light ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xF0, 0x70, 0x70);
}

static class Gfx
{
    public static GraphicsPath Round(RectangleF r, float radius)
    {
        float d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

static class Win11
{
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // Asks DWM to round the window's corners. No-op on Windows 10.
    public static void RoundCorners(Control c)
    {
        if (!c.IsHandleCreated) return;
        int pref = DWMWCP_ROUND;
        try { DwmSetWindowAttribute(c.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int)); }
        catch { /* older Windows: ignore */ }
    }
}

// A flat, theme-aware menu renderer that mimics the Windows 11 menu look:
// solid background, rounded inset highlight, no 3-D borders.
sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    readonly bool _light;

    public ModernMenuRenderer(bool light) : base(new ModernColors(light))
    {
        _light = light;
        RoundedEdges = false;
    }

    void FillBack(ToolStripRenderEventArgs e)
    {
        using var b = new SolidBrush(Theme.Back(_light));
        e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => FillBack(e);

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) => FillBack(e);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Line(_light));
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var r = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(Theme.Hover(_light));
        using var path = Gfx.Round(r, 6);
        e.Graphics.FillPath(b, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Fore(_light) : Theme.Disabled(_light);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var r = e.Item.Bounds;
        int y = r.Height / 2;
        using var pen = new Pen(Theme.Line(_light));
        e.Graphics.DrawLine(pen, r.Left + 8, y, r.Right - 8, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // Draw a simple check glyph in the foreground color.
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        using var pen = new Pen(Theme.Fore(_light), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = r.X + r.Width * 0.18f, y = r.Y + r.Height * 0.52f;
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(x, y),
            new PointF(x + r.Width * 0.22f, y + r.Height * 0.22f),
            new PointF(x + r.Width * 0.62f, y - r.Height * 0.30f),
        });
    }

    sealed class ModernColors : ProfessionalColorTable
    {
        readonly bool _light;
        public ModernColors(bool light) { _light = light; UseSystemColors = false; }
        public override Color ToolStripDropDownBackground => Theme.Back(_light);
        public override Color ImageMarginGradientBegin => Theme.Back(_light);
        public override Color ImageMarginGradientMiddle => Theme.Back(_light);
        public override Color ImageMarginGradientEnd => Theme.Back(_light);
        public override Color MenuBorder => Theme.Line(_light);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.Hover(_light);
    }
}

// Modal dialog that captures the next modifier+key combo as a hotkey string.
static class HotkeyDialog
{
    public static string? Ask(string current)
    {
        bool light = Theme.IsLight;
        using var f = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Set hotkey",
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(340, 130),
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            KeyPreview = true,
            TopMost = true,
        };
        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F),
            Text = $"Current:  {current}\n\nPress a new shortcut\n(modifier + letter / digit / F-key).\nEsc to cancel.",
        };
        f.Controls.Add(label);

        string? result = null;
        f.KeyDown += (s, e) =>
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; return; }
            if (e.KeyCode is Keys.Menu or Keys.ShiftKey or Keys.ControlKey or Keys.LWin or Keys.RWin)
                return; // a bare modifier; wait for the real key

            // HotKey owns the grammar; the Win key isn't in KeyEventArgs so we poll it.
            string? spec = HotKey.FromKeyEvent(e.KeyCode, e.Control, e.Alt, e.Shift, WinDown());
            if (spec is null)
            {
                label.Text = "Need a modifier (Ctrl/Alt/Shift/Win) plus a\nletter, digit, or F-key. Try again.";
                return;
            }
            result = spec;
            f.DialogResult = DialogResult.OK;
        };

        return f.ShowDialog() == DialogResult.OK ? result : null;
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    static bool WinDown() => (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
}

// A small, silent, self-dismissing pill near the bottom-center of the screen,
// styled like the Windows virtual-desktop switch indicator. It's an ordinary
// borderless Form, so it makes no sound and never stacks in the Action Center.
sealed class ToastForm : Form
{
    readonly System.Windows.Forms.Timer _life = new() { Interval = 1800 };
    readonly string _message;
    readonly Color _fg;
    readonly Color _back;
    const int Radius = 9;     // squarish, like the Windows desktop-switch indicator
    const int PadX = 30, PadY = 16;

    public ToastForm(string message, Color fg, Color back)
    {
        _message = message;
        _fg = fg;
        _back = back;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = new Font("Segoe UI", 11F);

        var text = TextRenderer.MeasureText(_message, Font);
        Size = new Size(Math.Max(text.Width + PadX * 2, 120), Math.Max(text.Height + PadY * 2, 46));

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - 12);

        _life.Tick += (_, _) => { _life.Stop(); Close(); };
    }

    // Render the pill into a 32bpp bitmap and push it to the layered window,
    // giving per-pixel alpha (smooth corners + translucency).
    void RenderLayered()
    {
        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var fill = new SolidBrush(_back))
            using (var path = Gfx.Round(rect, Radius))
                g.FillPath(fill, path);

            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            using var textBrush = new SolidBrush(_fg);
            g.DrawString(_message, Font, textBrush, new RectangleF(0, 0, Width, Height), sf);
        }
        PushLayered(bmp);
    }

    void PushLayered(Bitmap bmp)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBmp = SelectObject(memDc, hBmp);
        try
        {
            var size = new SIZE { cx = Width, cy = Height };
            var src = new POINT { x = 0, y = 0 };
            var pos = new POINT { x = Left, y = Top };
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    // Don't steal focus from the foreground app when shown.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x00080000, WS_EX_TOPMOST = 0x08,
                      WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderLayered();
        _life.Start();
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_LBUTTONUP = 0x0202;
        if (m.Msg == WM_LBUTTONUP) { Close(); return; }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _life.Dispose();
        base.Dispose(disposing);
    }

    // ---- layered-window interop ----
    const byte AC_SRC_OVER = 0x00, AC_SRC_ALPHA = 0x01;
    const int ULW_ALPHA = 0x02;

    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
}

// Draws a simple speaker icon at runtime so we don't ship an .ico file.
static class TrayArt
{
    public static Icon Speaker()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(0x2E, 0x9B, 0xF0));
            using var pen = new Pen(brush.Color, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            Point[] body =
            {
                new(5, 12), new(11, 12), new(18, 6), new(18, 26), new(11, 20), new(5, 20),
            };
            g.FillPolygon(brush, body);
            g.DrawArc(pen, 17, 10, 8, 12, -55, 110);
            g.DrawArc(pen, 17, 5, 15, 22, -55, 110);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}

// Hidden message-only window that receives the global hotkey.
sealed class HotkeyWindow : NativeWindow, IDisposable
{
    public event Action? Pressed;
    const int WM_HOTKEY = 0x0312;
    const int ID = 1;

    public HotkeyWindow(uint mods, uint vk)
    {
        CreateHandle(new CreateParams { Parent = (IntPtr)(-3) }); // HWND_MESSAGE
        if (!Program.RegisterHotKey(Handle, ID, mods | Program.MOD_NOREPEAT, vk))
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
            Program.UnregisterHotKey(Handle, ID);
            DestroyHandle();
        }
    }
}

// "Start with Windows" via a windowless launcher in the user's Startup folder.
static class AutoStart
{
    static string VbsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "audsw-daemon.vbs");

    public static bool IsEnabled => File.Exists(VbsPath);

    public static void Enable()
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("cannot locate exe");
        string dir = Path.GetDirectoryName(exe)!;
        string vbs =
            "Set sh = CreateObject(\"WScript.Shell\")\r\n" +
            $"sh.CurrentDirectory = \"{dir}\"\r\n" +
            $"sh.Run \"\"\"{exe}\"\" daemon\", 0, False\r\n";
        File.WriteAllText(VbsPath, vbs);
    }

    public static void Disable()
    {
        if (File.Exists(VbsPath)) File.Delete(VbsPath);
    }
}

// ---------------------------------------------------------------------------
// config
// ---------------------------------------------------------------------------

sealed class Config
{
    public string Device1 = "";
    public string Device2 = "";
    public string Hotkey  = "ctrl+alt+o";

    public static Config Load()
    {
        string path = Paths.Beside("audsw.cfg");
        var cfg = new Config();
        if (File.Exists(path))
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line[..eq].Trim().ToLowerInvariant();
                string val = line[(eq + 1)..].Trim();
                switch (key)
                {
                    case "device1": cfg.Device1 = val; break;
                    case "device2": cfg.Device2 = val; break;
                    case "hotkey":  cfg.Hotkey  = val; break;
                }
            }
        }
        return cfg;
    }

    public void Save()
    {
        string path = Paths.Beside("audsw.cfg");
        string text =
            "# audsw config -- managed from the tray menu, but safe to edit by hand.\n" +
            "# Device names are matched case-insensitively as a substring.\n\n" +
            $"device1 = {Device1}\n" +
            $"device2 = {Device2}\n\n" +
            "# Hotkey for the daemon. Modifiers: ctrl, alt, shift, win.\n" +
            "# Key: a letter, a digit, or f1..f12.\n" +
            $"hotkey  = {Hotkey}\n";
        File.WriteAllText(path, text);
    }
}

static class Paths
{
    public static string Beside(string file) =>
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", file);
}

// ---------------------------------------------------------------------------
// hotkey parsing
// ---------------------------------------------------------------------------

static class HotKey
{
    const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

    public static bool TryParse(string spec, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        foreach (var partRaw in spec.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = partRaw.Trim().ToLowerInvariant();
            switch (part)
            {
                case "ctrl":
                case "control": mods |= MOD_CONTROL; break;
                case "alt":     mods |= MOD_ALT;     break;
                case "shift":   mods |= MOD_SHIFT;   break;
                case "win":     mods |= MOD_WIN;     break;
                default:
                    if (!TryKey(part, out vk)) return false;
                    break;
            }
        }
        return vk != 0;
    }

    static bool TryKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1)
        {
            char ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9') { vk = ch; return true; }
            return false;
        }
        if (key.Length is 2 or 3 && key[0] == 'f' &&
            int.TryParse(key[1..], out int n) && n is >= 1 and <= 12)
        {
            vk = (uint)(0x70 + (n - 1)); // VK_F1 = 0x70
            return true;
        }
        return false;
    }

    // Build a canonical spec ("ctrl+alt+o") from a key event, or null if the key
    // isn't supported or no modifier is held. Output is always TryParse-able.
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
        return string.Join("+", parts);
    }

    static string? Token(Keys k) => k switch
    {
        >= Keys.A and <= Keys.Z => char.ToLowerInvariant((char)k).ToString(),
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (k - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => ((char)('0' + (k - Keys.NumPad0))).ToString(),
        >= Keys.F1 and <= Keys.F12 => "f" + (k - Keys.F1 + 1),
        _ => null,
    };
}

// ---------------------------------------------------------------------------
// Win32 interop
// ---------------------------------------------------------------------------

partial class Program
{
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const int  SW_HIDE      = 0;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetConsoleProcessList(uint[] lpdwProcessList, uint dwProcessCount);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
