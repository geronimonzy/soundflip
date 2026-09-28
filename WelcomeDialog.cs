using System.Windows.Forms;
using System.Drawing;

// One-time first-run window. Windows 11 puts a new app's tray icon behind the ^
// overflow, so without this a fresh install looks like it did nothing at all.
// Explains where SoundFlip lives, how to keep the icon visible, and the basics.
// Returns whether the user ticked "Start with Windows".
static class WelcomeDialog
{
    public static bool Show(string cycleHotkey, bool offerAutoStart)
    {
        bool light = Theme.IsLight;

        // Every Font created for this dialog is collected here and disposed
        // together in FormClosed — WinForms never disposes a Font assigned to a
        // control on its own.
        var fonts = new List<Font>();
        Font MakeFont(string family, float size)
        {
            var font = new Font(family, size);
            fonts.Add(font);
            return font;
        }

        int contentWidth = Dpi.S(472);

        using var form = new Form
        {
            Text = Str.WelcomeTitle.T(),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            // Unlike the other dialogs this one gets a taskbar button: on first run
            // it is the only visible sign that SoundFlip started.
            ShowInTaskbar = true,
            ClientSize = Dpi.Sz(520, 300),
            BackColor = Theme.Content(light),
            ForeColor = Theme.Fore(light),
            Font = MakeFont("Segoe UI", 9.5F),
        };

        var appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);
        if (appIcon is not null) form.Icon = appIcon;
        var iconImage = TrayArt.SpeakerBitmap(Dpi.S(40));
        form.FormClosed += (_, _) =>
        {
            iconImage.Dispose();
            appIcon?.Dispose();
            foreach (var font in fonts) font.Dispose();
        };
        form.Shown += (_, _) =>
        {
            Win11.ApplyChrome(form, light);
            form.Activate();
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = Dpi.Pad(24, 20, 24, 20),
            ColumnCount = 1,
        };

        var header = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Dpi.Pad(0, 0, 0, 12),
        };
        header.Controls.Add(new PictureBox
        {
            Size = Dpi.Sz(40, 40),
            Image = iconImage,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Margin = Dpi.Pad(0, 0, 12, 0),
        }, 0, 0);
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = MakeFont("Segoe UI Semibold", 13F),
            MaximumSize = new Size(contentWidth - Dpi.S(52), 0),
            Text = Str.WelcomeHeading.T(),
        }, 1, 0);
        body.Controls.Add(header);

        body.Controls.Add(Paragraph(Str.WelcomeTrayText.T(), contentWidth, 12));
        body.Controls.Add(Paragraph("•  " + Str.WelcomeClickTip.T(), contentWidth, 4));
        if (!string.IsNullOrWhiteSpace(cycleHotkey))
            body.Controls.Add(Paragraph("•  " + Str.WelcomeHotkeyTip.T(cycleHotkey), contentWidth, 4));

        CheckBox? startup = null;
        if (offerAutoStart)
        {
            startup = new CheckBox
            {
                AutoSize = true,
                Text = Str.WelcomeStartup.T(),
                ForeColor = Theme.Fore(light),
                Margin = Dpi.Pad(0, 12, 0, 0),
            };
            body.Controls.Add(startup);
        }

        var ok = new Win11Button(light) { Text = Str.WelcomeOk.T(), Accent = true, DialogResult = DialogResult.OK };
        var taskbar = new Win11Button(light) { Text = Str.WelcomeTaskbarSettings.T() };
        // Longer than the fixed 88px button in most languages.
        taskbar.Width = Math.Max(taskbar.Width, TextRenderer.MeasureText(taskbar.Text, form.Font).Width + Dpi.S(28));
        taskbar.Click += (_, _) =>
        {
            try
            {
                AppMetadata.OpenUrl("ms-settings:taskbar");
            }
            catch (Exception ex)
            {
                MessageBox.Show(form, ex.Message, AppMetadata.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var footer = DialogFooter.Create(light, ok, taskbar);
        form.Controls.Add(body);
        form.Controls.Add(footer);
        form.AcceptButton = ok;
        form.CancelButton = ok;

        // Text length varies a lot between languages: size the window to its content.
        form.Load += (_, _) => form.ClientSize = new Size(form.ClientSize.Width, body.PreferredSize.Height + footer.Height);

        form.ShowDialog();
        return startup?.Checked == true;
    }

    static Label Paragraph(string text, int width, int bottomMargin) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        Text = text,
        Margin = Dpi.Pad(0, 0, 0, bottomMargin),
    };
}
