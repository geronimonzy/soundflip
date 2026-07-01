using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

static class Theme
{
    public static bool IsLight
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static Color Back(bool light) => light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(44, 44, 44);
    public static Color Fore(bool light) => light ? Color.FromArgb(26, 26, 26) : Color.White;
    public static Color Hover(bool light) => light ? Color.FromArgb(24, 0, 0, 0) : Color.FromArgb(26, 255, 255, 255);
    public static Color Line(bool light) => light ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(28, 255, 255, 255);
    public static Color Disabled(bool light) => light ? Color.FromArgb(140, 140, 140) : Color.FromArgb(120, 120, 120);
    public static Color Warning(bool light) => light ? Color.FromArgb(0xB2, 0x6B, 0x00) : Color.FromArgb(0xF0, 0xC0, 0x60);
    public static Color Error(bool light) => light ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xF0, 0x70, 0x70);
    public static Color Accent => Color.FromArgb(0x2E, 0x9B, 0xF0);
    public static Color AccentHover => Color.FromArgb(0x27, 0x84, 0xCC);
    // Win11 "control fill": buttons/inputs sit slightly lighter (dark) or brighter
    // (light) than the window background.
    public static Color Card(bool light) => light ? Color.White : Color.FromArgb(58, 58, 58);
}

static class Gfx
{
    public static GraphicsPath Round(RectangleF rectangle, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

static class Win11
{
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void RoundCorners(Control control)
    {
        if (!control.IsHandleCreated) return;

        int preference = DWMWCP_ROUND;
        try
        {
            DwmSetWindowAttribute(control.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
            // Ignore on older Windows releases.
        }
    }
}

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
        using var brush = new SolidBrush(Theme.Back(_light));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => FillBack(e);

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) => FillBack(e);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Theme.Line(_light));
        var bounds = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;

        var bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Theme.Hover(_light));
        using var path = Gfx.Round(bounds, 6);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Fore(_light) : Theme.Disabled(_light);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = e.Item.Bounds;
        int y = bounds.Height / 2;
        using var pen = new Pen(Theme.Line(_light));
        e.Graphics.DrawLine(pen, bounds.Left + 8, y, bounds.Right - 8, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = e.ImageRectangle;
        using var pen = new Pen(Theme.Fore(_light), 1.6F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = bounds.X + bounds.Width * 0.18F;
        float y = bounds.Y + bounds.Height * 0.52F;
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(x, y),
            new PointF(x + bounds.Width * 0.22F, y + bounds.Height * 0.22F),
            new PointF(x + bounds.Width * 0.62F, y - bounds.Height * 0.30F),
        });
    }

    sealed class ModernColors : ProfessionalColorTable
    {
        readonly bool _light;

        public ModernColors(bool light)
        {
            _light = light;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => Theme.Back(_light);
        public override Color ImageMarginGradientBegin => Theme.Back(_light);
        public override Color ImageMarginGradientMiddle => Theme.Back(_light);
        public override Color ImageMarginGradientEnd => Theme.Back(_light);
        public override Color MenuBorder => Theme.Line(_light);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.Hover(_light);
    }
}

// A small, silent, self-dismissing pill near the bottom-center of the active
// screen. It uses a layered window for smooth corners and translucency.
sealed class ToastForm : Form
{
    readonly System.Windows.Forms.Timer _life = new() { Interval = 1800 };
    readonly string _message;
    readonly Color _foreground;
    readonly Color _background;

    const int Radius = 9;
    const int PaddingX = 28;
    const int PaddingY = 16;
    const int MaxTextWidth = 480;

    public ToastForm(string message, Color foreground, Color background)
    {
        _message = message;
        _foreground = foreground;
        _background = background;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = new Font("Segoe UI", 10.5F);

        var measured = TextRenderer.MeasureText(
            _message,
            Font,
            new Size(MaxTextWidth, 10_000),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

        Width = Math.Max(Math.Min(measured.Width + PaddingX * 2, MaxTextWidth + PaddingX * 2), 180);
        Height = Math.Max(measured.Height + PaddingY * 2, 56);

        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 14);

        _life.Tick += (_, _) =>
        {
            _life.Stop();
            Close();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x00080000;
            const int WS_EX_TOPMOST = 0x00000008;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;

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
        if (m.Msg == WM_LBUTTONUP)
        {
            Close();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _life.Dispose();
        base.Dispose(disposing);
    }

    void RenderLayered()
    {
        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var card = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
            using (var fill = new SolidBrush(_background))
            using (var path = Gfx.Round(card, Radius))
                g.FillPath(fill, path);

            using var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.LineLimit,
            };
            using var textBrush = new SolidBrush(_foreground);
            g.DrawString(
                _message,
                Font,
                textBrush,
                new RectangleF(PaddingX, PaddingY, Width - PaddingX * 2, Height - PaddingY * 2),
                stringFormat);
        }

        PushLayered(bitmap);
    }

    void PushLayered(Bitmap bitmap)
    {
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memDc, hBitmap);

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
            SelectObject(memDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    const byte AC_SRC_OVER = 0x00;
    const byte AC_SRC_ALPHA = 0x01;
    const int ULW_ALPHA = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
}

static class TrayArt
{
    public static Icon Speaker()
    {
        using var bitmap = SpeakerBitmap(32);
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public static Bitmap SpeakerBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        DrawSpeaker(g, new RectangleF(0, 0, size, size), Theme.Accent);
        return bitmap;
    }

    public static void DrawSpeaker(Graphics g, RectangleF bounds, Color color)
    {
        var state = g.Save();
        g.TranslateTransform(bounds.Left, bounds.Top);
        g.ScaleTransform(bounds.Width / 32F, bounds.Height / 32F);

        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 2.4F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        PointF[] body =
        {
            new(5, 12),
            new(11, 12),
            new(18, 6),
            new(18, 26),
            new(11, 20),
            new(5, 20),
        };
        g.FillPolygon(brush, body);
        g.DrawArc(pen, 17, 10, 8, 12, -55, 110);
        g.DrawArc(pen, 17, 5, 15, 22, -55, 110);
        g.Restore(state);
    }
}
