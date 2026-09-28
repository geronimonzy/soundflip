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

// Every pixel constant in the UI is authored at 96 DPI (100% scaling). The process
// is system-DPI-aware and forms are built in code with AutoScaleMode.None, so fonts
// (sized in points) follow the display scale but raw pixel sizes don't: route
// every size, padding and margin through S() so layouts grow with the text.
static class Dpi
{
    static readonly float Factor = ReadFactor();

    static float ReadFactor()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96F;
    }

    public static int S(int px) => (int)Math.Round(px * Factor);
    public static float S(float px) => px * Factor;
    public static Size Sz(int width, int height) => new(S(width), S(height));
    public static Padding Pad(int left, int top, int right, int bottom) => new(S(left), S(top), S(right), S(bottom));
    public static Padding Pad(int all) => new(S(all));
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

    // Drop-down look: left-aligned text plus a chevron on the right. Pair with a
    // ContextMenuStrip (see Win11DropDown) for a Win11-style selector.
    public bool Chevron { get; init; }

    public Win11Button(bool light)
    {
        _light = light;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = Dpi.Sz(88, 32);
        Margin = Dpi.Pad(8, 0, 0, 0);
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
        using var path = Gfx.Round(rect, Dpi.S(4F));
        using (var fill = new SolidBrush(FillColor()))
            g.FillPath(fill, path);

        if (!Accent)
        {
            using var pen = new Pen(Focused ? Theme.Accent : Theme.Line(_light));
            g.DrawPath(pen, path);
        }

        if (!Chevron)
        {
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, TextColor(),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return;
        }

        int pad = Dpi.S(11);
        int glyph = Dpi.S(10);
        var textRect = new Rectangle(pad, 0, Width - pad * 2 - glyph - Dpi.S(6), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, TextColor(),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Chevron-down, stroked like the Fluent "ChevronDown" glyph.
        float cx = Width - pad - glyph / 2F;
        float cy = Height / 2F;
        float half = glyph / 2F;
        using var chevron = new Pen(TextColor(), Dpi.S(1.3F)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(chevron, new[]
        {
            new PointF(cx - half, cy - half / 2F),
            new PointF(cx, cy + half / 2F),
            new PointF(cx + half, cy - half / 2F),
        });
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

// Win11-style selector: a chevron button that opens a menu (drawn by the shared
// ModernMenuRenderer, so it matches the tray menu) listing the choices, with a
// check on the selected one. Raises SelectionChanged with the chosen value.
sealed class Win11DropDown<T>
{
    readonly List<(T Value, string Label)> _items = new();
    readonly ContextMenuStrip _menu = new();

    public Win11Button Button { get; }
    public T? Selected { get; private set; }
    public event Action<T>? SelectionChanged;

    public Win11DropDown(bool light, int width)
    {
        Button = new Win11Button(light) { Chevron = true, Width = width, Margin = Padding.Empty };
        _menu.BackColor = Theme.Back(light);
        _menu.ForeColor = Theme.Fore(light);
        _menu.Opened += (_, _) => Win11.RoundCorners(_menu);
        Button.Click += (_, _) =>
        {
            _menu.MinimumSize = new Size(Button.Width, 0);
            _menu.Show(Button, new Point(0, Button.Height + Dpi.S(2)));
        };
        Button.Disposed += (_, _) => _menu.Dispose();
    }

    // Replace the choices (e.g. after a language change re-translates a label).
    public void SetItems(IEnumerable<(T Value, string Label)> items, T selected)
    {
        _items.Clear();
        _items.AddRange(items);
        foreach (var item in _menu.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        _menu.Items.Clear();
        foreach (var (value, label) in _items)
        {
            var entry = new ToolStripMenuItem(label);
            entry.Click += (_, _) => Select(value, raise: true);
            _menu.Items.Add(entry);
        }
        Select(selected, raise: false);
    }

    void Select(T value, bool raise)
    {
        Selected = value;
        for (int i = 0; i < _items.Count; i++)
        {
            bool match = EqualityComparer<T>.Default.Equals(_items[i].Value, value);
            ((ToolStripMenuItem)_menu.Items[i]).Checked = match;
            if (match) Button.Text = _items[i].Label;
        }
        if (raise) SelectionChanged?.Invoke(value);
    }
}

// Bottom action strip of a Win11-style dialog: a slightly darker band with a
// hairline on top and buttons flowing in from the right.
static class DialogFooter
{
    public static Panel Create(bool light, params Button[] buttonsRightToLeft)
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = Dpi.S(60),
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
            Padding = Dpi.Pad(8, 14, 24, 0),
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

        var bounds = new Rectangle(Dpi.S(4), 1, e.Item.Width - Dpi.S(8), e.Item.Height - 2);
        var oldSmoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Theme.Hover(_light));
        using var path = Gfx.Round(bounds, Dpi.S(6F));
        e.Graphics.FillPath(brush, path);
        e.Graphics.SmoothingMode = oldSmoothing;
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
        e.Graphics.DrawLine(pen, bounds.Left + Dpi.S(8), y, bounds.Right - Dpi.S(8), y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var oldSmoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = e.ImageRectangle;
        using var pen = new Pen(Theme.Fore(_light), Dpi.S(1.6F)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = bounds.X + bounds.Width * 0.18F;
        float y = bounds.Y + bounds.Height * 0.52F;
        e.Graphics.DrawLines(pen, new[]
        {
            new PointF(x, y),
            new PointF(x + bounds.Width * 0.22F, y + bounds.Height * 0.22F),
            new PointF(x + bounds.Width * 0.62F, y - bounds.Height * 0.30F),
        });
        e.Graphics.SmoothingMode = oldSmoothing;
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
// screen: a semibold title line (normal text color for info, amber for warnings,
// red for errors) over an optional body line. It uses a layered window for smooth
// corners and translucency.
sealed class ToastForm : Form
{
    // Shared across every toast for the app's lifetime: WinForms never disposes a
    // Font assigned to a control, so per-instance Fonts here would leak GDI
    // handles on every notification.
    static readonly Font TitleFont = new("Segoe UI Semibold", 10.5F);
    static readonly Font BodyFont = new("Segoe UI", 10.5F);

    readonly System.Windows.Forms.Timer _life;
    readonly string _title;
    readonly string _body;
    readonly Color _titleColor;
    readonly Color _bodyColor;
    readonly Color _background;
    readonly SizeF _titleSize;
    readonly SizeF _bodySize;

    static readonly int Radius = Dpi.S(9);
    static readonly int PaddingX = Dpi.S(28);
    static readonly int PaddingY = Dpi.S(14);
    static readonly int LineGap = Dpi.S(2);
    static readonly int MaxTextWidth = Dpi.S(480);

    public ToastForm(string title, string body, Color titleColor, Color bodyColor, Color background, int lifetimeMs)
    {
        _title = title;
        _body = body;
        _titleColor = titleColor;
        _bodyColor = bodyColor;
        _background = background;
        _life = new System.Windows.Forms.Timer { Interval = lifetimeMs };

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = BodyFont;

        // Measure with the same GDI+ engine and StringFormat used for drawing.
        // GDI (TextRenderer) wraps at different points than GDI+ (DrawString), so
        // measuring with one and drawing with the other clipped long messages.
        using var format = TextFormat();
        using var probe = Graphics.FromHwnd(IntPtr.Zero);
        probe.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        _titleSize = probe.MeasureString(_title, TitleFont, MaxTextWidth, format);
        _bodySize = _body.Length == 0 ? SizeF.Empty : probe.MeasureString(_body, BodyFont, MaxTextWidth, format);

        float textWidth = Math.Max(_titleSize.Width, _bodySize.Width);
        float textHeight = _titleSize.Height + (_body.Length == 0 ? 0 : LineGap + _bodySize.Height);
        Width = Math.Max((int)MathF.Ceiling(textWidth) + PaddingX * 2 + 2, Dpi.S(180));
        Height = Math.Max((int)MathF.Ceiling(textHeight) + PaddingY * 2 + 2, Dpi.S(56));

        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - Dpi.S(14));

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
        LineAlignment = StringAlignment.Near,
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
            // The text block is centered vertically; each line is centered
            // horizontally by the StringFormat.
            using var stringFormat = TextFormat();
            float textHeight = _titleSize.Height + (_body.Length == 0 ? 0 : LineGap + _bodySize.Height);
            float top = (Height - textHeight) / 2F;
            float width = Width - PaddingX * 2;

            using (var titleBrush = new SolidBrush(_titleColor))
                g.DrawString(_title, TitleFont, titleBrush,
                    new RectangleF(PaddingX, top, width, _titleSize.Height), stringFormat);

            if (_body.Length > 0)
            {
                using var bodyBrush = new SolidBrush(_bodyColor);
                g.DrawString(_body, BodyFont, bodyBrush,
                    new RectangleF(PaddingX, top + _titleSize.Height + LineGap, width, _bodySize.Height), stringFormat);
            }
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
        // Render at the size the tray actually shows (16px at 100%, 40px at 250%)
        // so the shell never rescales a fixed-size bitmap.
        using var bitmap = SpeakerBitmap(SystemInformation.SmallIconSize.Width);
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

    // Tray glyph: Fluent UI System Icons (Microsoft, MIT licensed,
    // https://github.com/microsoft/fluentui-system-icons) "Speaker 2" (filled, 24px)
    // scaled to 88%, with a ring knocked out around an "Arrow Sync Circle" badge
    // (filled, 24px, at 52% in the bottom-right). The badge is what sets it apart
    // from the Windows volume icon next to it. GDI+ has no anti-aliased boolean
    // ops, so the combined outline was computed once (WPF Geometry.Combine) and is
    // baked in here as bezier/line segments in the native 24-unit space.
    static GraphicsPath SpeakerPath()
    {
        var path = new GraphicsPath(FillMode.Winding);
        path.StartFigure();
        path.AddBezier(15.77f, 18.24f, 15.5546f, 18.24f, 15.38f, 18.4146f, 15.38f, 18.63f);
        path.AddLine(15.38f, 18.63f, 15.38f, 20.19f);
        path.AddBezier(15.38f, 20.19f, 15.38f, 20.4054f, 15.5546f, 20.58f, 15.77f, 20.58f);
        path.AddBezier(15.77f, 20.58f, 15.9854f, 20.58f, 16.16f, 20.4054f, 16.16f, 20.19f);
        path.AddLine(16.16f, 20.19f, 16.16f, 19.8003f);
        path.AddBezier(16.16f, 19.8003f, 16.6344f, 20.4316f, 17.3895f, 20.84f, 18.24f, 20.84f);
        path.AddBezier(18.24f, 20.84f, 19.0335f, 20.84f, 19.7443f, 20.4841f, 20.2206f, 19.9244f);
        path.AddBezier(20.2206f, 19.9244f, 20.3602f, 19.7604f, 20.3404f, 19.5143f, 20.1764f, 19.3747f);
        path.AddBezier(20.1764f, 19.3747f, 20.0124f, 19.2351f, 19.7663f, 19.2549f, 19.6266f, 19.4189f);
        path.AddBezier(19.6266f, 19.4189f, 19.2922f, 19.8118f, 18.7953f, 20.06f, 18.24f, 20.06f);
        path.AddBezier(18.24f, 20.06f, 17.514f, 20.06f, 16.8872f, 19.6349f, 16.5951f, 19.02f);
        path.AddLine(16.5951f, 19.02f, 17.33f, 19.02f);
        path.AddBezier(17.33f, 19.02f, 17.5454f, 19.02f, 17.72f, 18.8454f, 17.72f, 18.63f);
        path.AddBezier(17.72f, 18.63f, 17.72f, 18.4146f, 17.5454f, 18.24f, 17.33f, 18.24f);
        path.AddLine(17.33f, 18.24f, 15.77f, 18.24f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(18.24f, 15.64f, 17.4461f, 15.64f, 16.7351f, 15.9963f, 16.2588f, 16.5563f);
        path.AddBezier(16.2588f, 16.5563f, 16.1192f, 16.7203f, 16.1391f, 16.9665f, 16.3032f, 17.106f);
        path.AddBezier(16.3032f, 17.106f, 16.4672f, 17.2456f, 16.7134f, 17.2257f, 16.8529f, 17.0616f);
        path.AddBezier(16.8529f, 17.0616f, 17.1874f, 16.6684f, 17.6845f, 16.42f, 18.24f, 16.42f);
        path.AddBezier(18.24f, 16.42f, 18.966f, 16.42f, 19.5927f, 16.8451f, 19.8849f, 17.46f);
        path.AddLine(19.8849f, 17.46f, 19.15f, 17.46f);
        path.AddBezier(19.15f, 17.46f, 18.9346f, 17.46f, 18.76f, 17.6346f, 18.76f, 17.85f);
        path.AddBezier(18.76f, 17.85f, 18.76f, 18.0654f, 18.9346f, 18.24f, 19.15f, 18.24f);
        path.AddLine(19.15f, 18.24f, 20.71f, 18.24f);
        path.AddBezier(20.71f, 18.24f, 20.9254f, 18.24f, 21.1f, 18.0654f, 21.1f, 17.85f);
        path.AddLine(21.1f, 17.85f, 21.1f, 16.29f);
        path.AddBezier(21.1f, 16.29f, 21.1f, 16.0746f, 20.9254f, 15.9f, 20.71f, 15.9f);
        path.AddBezier(20.71f, 15.9f, 20.4946f, 15.9f, 20.32f, 16.0746f, 20.32f, 16.29f);
        path.AddLine(20.32f, 16.29f, 20.32f, 16.6798f);
        path.AddBezier(20.32f, 16.6798f, 19.8457f, 16.0484f, 19.0905f, 15.64f, 18.24f, 15.64f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(18.24f, 13.04f, 21.1119f, 13.04f, 23.44f, 15.3681f, 23.44f, 18.24f);
        path.AddBezier(23.44f, 18.24f, 23.44f, 21.1119f, 21.1119f, 23.44f, 18.24f, 23.44f);
        path.AddBezier(18.24f, 23.44f, 15.3681f, 23.44f, 13.04f, 21.1119f, 13.04f, 18.24f);
        path.AddBezier(13.04f, 18.24f, 13.04f, 15.3681f, 15.3681f, 13.04f, 18.24f, 13.04f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(15.5883f, 7.7133f, 15.7502f, 7.7614f, 15.8937f, 7.8714f, 15.9804f, 8.0317f);
        path.AddBezier(15.9804f, 8.0317f, 16.4523f, 8.9034f, 16.72f, 9.9016f, 16.72f, 10.9604f);
        path.AddLine(16.72f, 10.9604f, 16.599f, 11.9232f);
        path.AddLine(16.599f, 11.9232f, 15.631f, 12.1187f);
        path.AddLine(15.631f, 12.1187f, 15.0005f, 12.5437f);
        path.AddLine(15.0005f, 12.5437f, 15.4f, 10.9604f);
        path.AddBezier(15.4f, 10.9604f, 15.4f, 10.1265f, 15.1896f, 9.3436f, 14.8196f, 8.66f);
        path.AddBezier(14.8196f, 8.66f, 14.646f, 8.3395f, 14.7653f, 7.939f, 15.0858f, 7.7654f);
        path.AddBezier(15.0858f, 7.7654f, 15.2461f, 7.6787f, 15.4264f, 7.6651f, 15.5883f, 7.7133f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(17.2027f, 5.4677f, 17.3697f, 5.4925f, 17.5273f, 5.5812f, 17.6358f, 5.7276f);
        path.AddBezier(17.6358f, 5.7276f, 18.7191f, 7.1902f, 19.36f, 9.0015f, 19.36f, 10.9604f);
        path.AddLine(19.36f, 10.9604f, 19.2224f, 11.8064f);
        path.AddLine(19.2224f, 11.8064f, 18.2f, 11.6f);
        path.AddLine(18.2f, 11.6f, 17.9271f, 11.6551f);
        path.AddLine(17.9271f, 11.6551f, 18.04f, 10.9604f);
        path.AddBezier(18.04f, 10.9604f, 18.04f, 9.2939f, 17.4957f, 7.7563f, 16.5751f, 6.5133f);
        path.AddBezier(16.5751f, 6.5133f, 16.3581f, 6.2204f, 16.4197f, 5.807f, 16.7126f, 5.5901f);
        path.AddBezier(16.7126f, 5.5901f, 16.859f, 5.4816f, 17.0356f, 5.4428f, 17.2027f, 5.4677f);
        path.CloseFigure();
        path.StartFigure();
        path.AddBezier(11.9545f, 3.0473f, 12.5691f, 2.9673f, 13.2f, 3.4285f, 13.2f, 4.1404f);
        path.AddLine(13.2f, 4.1404f, 13.2f, 14.0271f);
        path.AddLine(13.2f, 14.0271f, 12.1187f, 15.631f);
        path.AddBezier(12.1187f, 15.631f, 11.7847f, 16.4206f, 11.6f, 17.2887f, 11.6f, 18.2f);
        path.AddLine(11.6f, 18.2f, 11.7126f, 18.7577f);
        path.AddLine(11.7126f, 18.7577f, 11.3691f, 18.5986f);
        path.AddLine(11.3691f, 18.5986f, 7.4166f, 15.0841f);
        path.AddBezier(7.4166f, 15.0841f, 7.2958f, 14.9767f, 7.1397f, 14.9174f, 6.978f, 14.9174f);
        path.AddLine(6.978f, 14.9174f, 3.74f, 14.9174f);
        path.AddBezier(3.74f, 14.9174f, 2.6465f, 14.9174f, 1.76f, 14.0308f, 1.76f, 12.9374f);
        path.AddLine(1.76f, 12.9374f, 1.76f, 8.9792f);
        path.AddBezier(1.76f, 8.9792f, 1.76f, 7.8857f, 2.6465f, 6.9992f, 3.74f, 6.9992f);
        path.AddLine(3.74f, 6.9992f, 6.9781f, 6.9992f);
        path.AddBezier(6.9781f, 6.9992f, 7.1397f, 6.9992f, 7.2958f, 6.9399f, 7.4166f, 6.8324f);
        path.AddLine(7.4166f, 6.8324f, 11.3692f, 3.3184f);
        path.AddBezier(11.3692f, 3.3184f, 11.5465f, 3.1607f, 11.7496f, 3.0739f, 11.9545f, 3.0473f);
        path.CloseFigure();
        return path;
    }
}
