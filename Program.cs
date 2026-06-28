using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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
        menu.Items.Clear();

        menu.Items.Add(new ToolStripMenuItem($"Switch now  ({_cfg.Hotkey})", null, (_, _) => SwitchNow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(DeviceMenu("Device 1", _cfg.Device1, name => { _cfg.Device1 = name; _cfg.Save(); }));
        menu.Items.Add(DeviceMenu("Device 2", _cfg.Device2, name => { _cfg.Device2 = name; _cfg.Save(); }));
        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutoStart())
        {
            Checked = AutoStart.IsEnabled,
        };
        menu.Items.Add(autostart);
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));
    }

    ToolStripMenuItem DeviceMenu(string label, string current, Action<string> onPick)
    {
        var resolved = Audio.Resolve(_controller, current);
        var root = new ToolStripMenuItem($"{label}:  {resolved?.FullName ?? (current.Length > 0 ? current + " (not found)" : "<not set>")}");

        foreach (var d in _controller.GetPlaybackDevices(DeviceState.Active).OrderBy(d => d.FullName))
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
        Color fg = kind switch
        {
            ToolTipIcon.Warning => Color.FromArgb(0xF0, 0xC0, 0x60),
            ToolTipIcon.Error   => Color.FromArgb(0xF0, 0x70, 0x70),
            _                   => Color.White,
        };
        string message = string.IsNullOrEmpty(text) ? title : text;

        // Replace any visible toast so they never stack.
        var old = _toast;
        _toast = null;
        old?.Close();

        var toast = new ToastForm(message, fg);
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

// A small, silent, self-dismissing pill near the bottom-center of the screen,
// styled like the Windows virtual-desktop switch indicator. It's an ordinary
// borderless Form, so it makes no sound and never stacks in the Action Center.
sealed class ToastForm : Form
{
    readonly System.Windows.Forms.Timer _life = new() { Interval = 1800 };
    readonly string _message;
    readonly Color _fg;
    const int Radius = 20;

    public ToastForm(string message, Color fg)
    {
        _message = message;
        _fg = fg;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(0x26, 0x26, 0x26);
        Font = new Font("Segoe UI", 11F);

        var text = TextRenderer.MeasureText(_message, Font);
        Size = new Size(Math.Max(text.Width + 64, 130), Math.Max(text.Height + 32, 52));

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - 12);
        Region = new Region(Rounded(new Rectangle(0, 0, Width, Height), Radius));

        Click += (_, _) => Close();
        _life.Tick += (_, _) => { _life.Stop(); Close(); };
    }

    static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Faint border for definition against dark backgrounds.
        using (var path = Rounded(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
        using (var pen = new Pen(Color.FromArgb(0x3C, 0x3C, 0x3C)))
            e.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(e.Graphics, _message, Font, ClientRectangle, _fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    // Don't steal focus from the foreground app when shown.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x08, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _life.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _life.Dispose();
        base.Dispose(disposing);
    }
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
