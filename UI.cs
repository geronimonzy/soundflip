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
    // Win11 dialog surfaces: the content area sits on Content, the bottom action
    // strip on the slightly darker Footer (WinUI ContentDialog pattern).
    public static Color Content(bool light) => light ? Color.White : Color.FromArgb(41, 41, 41);
    public static Color Footer(bool light) => light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
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
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
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

    // Dialog chrome: rounded corners plus a title bar that follows the app theme
    // (WinForms otherwise keeps a light title bar even when the content is dark).
    public static void ApplyChrome(Form form, bool light)
    {
        if (!form.IsHandleCreated) return;

        int dark = light ? 0 : 1;
        try
        {
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch
        {
            // Ignore on older Windows releases.
        }

        RoundCorners(form);
    }
}

// Custom-painted Win11-style button: smooth anti-aliased rounded corners with
// control-fill (default) or accent (primary action) coloring. Plain WinForms —
// no WinUI involved.
sealed class Win11Button : Button
{
    readonly bool _light;
    bool _hover, _down;

    public bool Accent { get; init; }

    public Win11Button(bool light)
    {
        _light = light;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(88, 32);
        Margin = new Padding(8, 0, 0, 0);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Content(_light));
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5F, 0.5F, Width - 1, Height - 1);
        using var path = Gfx.Round(rect, 4F);
        using (var fill = new SolidBrush(FillColor()))
            g.FillPath(fill, path);

        if (!Accent)
        {
            using var pen = new Pen(Focused ? Theme.Accent : Theme.Line(_light));
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor(),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    Color FillColor()
    {
        if (Accent)
        {
            if (!Enabled) return Theme.Disabled(_light);
            return _down || _hover ? Theme.AccentHover : Theme.Accent;
        }

        if (_down) return _light ? Color.FromArgb(240, 240, 240) : Color.FromArgb(39, 39, 39);
        if (_hover) return _light ? Color.FromArgb(246, 246, 246) : Color.FromArgb(52, 52, 52);
        return _light ? Color.FromArgb(251, 251, 251) : Color.FromArgb(45, 45, 45);
    }

    Color TextColor() =>
        !Enabled ? Theme.Disabled(_light) : Accent ? Color.White : Theme.Fore(_light);
}

// Bottom action strip of a Win11-style dialog: a slightly darker band with a
// hairline on top and buttons flowing in from the right.
static class DialogFooter
{
    public const int Height = 60;

    public static Panel Create(bool light, params Button[] buttonsRightToLeft)
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = Height,
            BackColor = Theme.Footer(light),
        };
        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line(light));
            e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 14, 24, 0),
        };
        foreach (var button in buttonsRightToLeft) flow.Controls.Add(button);
        footer.Controls.Add(flow);
        return footer;
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

        // Measure with the same GDI+ engine and StringFormat used for drawing.
        // GDI (TextRenderer) wraps at different points than GDI+ (DrawString), so
        // measuring with one and drawing with the other clipped long messages.
        using var format = TextFormat();
        using var probe = Graphics.FromHwnd(IntPtr.Zero);
        probe.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var measured = probe.MeasureString(_message, Font, MaxTextWidth, format);

        Width = Math.Max((int)MathF.Ceiling(measured.Width) + PaddingX * 2 + 2, 180);
        Height = Math.Max((int)MathF.Ceiling(measured.Height) + PaddingY * 2 + 2, 56);

        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - 14);

        _life.Tick += (_, _) =>
        {
            _life.Stop();
            Close();
        };
    }

    // One definition of the wrapping/alignment behavior, shared by the size
    // measurement and the actual draw so they can never disagree.
    static StringFormat TextFormat() => new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center,
    };

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

            // No LineLimit: the window was sized from an exact measurement, and
            // LineLimit would drop the whole last line on a 1px rounding shortfall.
            using var stringFormat = TextFormat();
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
        g.ScaleTransform(bounds.Width / 24F, bounds.Height / 24F);

        using var brush = new SolidBrush(color);
        using var path = SpeakerPath();
        g.FillPath(brush, path);
        g.Restore(state);
    }

    // "Speaker 2" (filled, 24px) from Microsoft's Fluent UI System Icons, MIT
    // licensed: https://github.com/microsoft/fluentui-system-icons
    // The SVG path is baked in as bezier/line segments in its native 24-unit space.
    static GraphicsPath SpeakerPath()
    {
        var path = new GraphicsPath(FillMode.Winding);
        path.StartFigure();
        path.AddLine(15f, 4.25049f, 15f, 19.7461f);
        path.AddBezier(15f, 19.7461f, 15f, 20.8247f, 13.7255f, 21.397f, 12.9194f, 20.6802f);
        path.AddLine(12.9194f, 20.6802f, 8.42793f, 16.6865f);
        path.AddBezier(8.42793f, 16.6865f, 8.29063f, 16.5644f, 8.11329f, 16.497f, 7.92956f, 16.497f);
        path.AddLine(7.92956f, 16.497f, 4.25f, 16.497f);
        path.AddBezier(4.25f, 16.497f, 3.00736f, 16.497f, 2f, 15.4896f, 2f, 14.247f);
        path.AddLine(2f, 14.247f, 2f, 9.74907f);
        path.AddBezier(2f, 9.74907f, 2f, 8.50643f, 3.00736f, 7.49907f, 4.25f, 7.49907f);
        path.AddLine(4.25f, 7.49907f, 7.92961f, 7.49907f);
        path.AddBezier(7.92961f, 7.49907f, 8.11333f, 7.49907f, 8.29065f, 7.43165f, 8.42794f, 7.30958f);
        path.AddLine(8.42794f, 7.30958f, 12.9195f, 3.31631f);
        path.AddBezier(12.9195f, 3.31631f, 13.7255f, 2.59964f, 15f, 3.17187f, 15f, 4.25049f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(18.9916f, 5.89782f, 19.3244f, 5.65128f, 19.7941f, 5.72126f, 20.0407f, 6.05411f);
        path.AddBezier(20.0407f, 6.05411f, 21.2717f, 7.71619f, 22f, 9.77439f, 22f, 12.0005f);
        path.AddBezier(22f, 12.0005f, 22f, 14.2266f, 21.2717f, 16.2848f, 20.0407f, 17.9469f);
        path.AddBezier(20.0407f, 17.9469f, 19.7941f, 18.2798f, 19.3244f, 18.3497f, 18.9916f, 18.1032f);
        path.AddBezier(18.9916f, 18.1032f, 18.6587f, 17.8567f, 18.5888f, 17.387f, 18.8353f, 17.0541f);
        path.AddBezier(18.8353f, 17.0541f, 19.8815f, 15.6416f, 20.5f, 13.8943f, 20.5f, 12.0005f);
        path.AddBezier(20.5f, 12.0005f, 20.5f, 10.1067f, 19.8815f, 8.35945f, 18.8353f, 6.9469f);
        path.AddBezier(18.8353f, 6.9469f, 18.5888f, 6.61404f, 18.6587f, 6.14435f, 18.9916f, 5.89782f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(17.143f, 8.36982f, 17.5072f, 8.17262f, 17.9624f, 8.30806f, 18.1596f, 8.67233f);
        path.AddBezier(18.1596f, 8.67233f, 18.6958f, 9.66294f, 19f, 10.7973f, 19f, 12.0005f);
        path.AddBezier(19f, 12.0005f, 19f, 13.2037f, 18.6958f, 14.338f, 18.1596f, 15.3287f);
        path.AddBezier(18.1596f, 15.3287f, 17.9624f, 15.6929f, 17.5072f, 15.8284f, 17.143f, 15.6312f);
        path.AddBezier(17.143f, 15.6312f, 16.7787f, 15.434f, 16.6432f, 14.9788f, 16.8404f, 14.6146f);
        path.AddBezier(16.8404f, 14.6146f, 17.2609f, 13.8378f, 17.5f, 12.9482f, 17.5f, 12.0005f);
        path.AddBezier(17.5f, 12.0005f, 17.5f, 11.0528f, 17.2609f, 10.1632f, 16.8404f, 9.38642f);
        path.AddBezier(16.8404f, 9.38642f, 16.6432f, 9.02216f, 16.7787f, 8.56701f, 17.143f, 8.36982f);
        path.CloseFigure();
        return path;
    }
}
