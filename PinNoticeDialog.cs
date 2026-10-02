using System.Windows.Forms;
using System.Drawing;

// Shown once after a Store update that lost the user's tray pin (see TrayPin).
// Windows treats each MSIX version as a new icon, so the speaker the user kept on
// the taskbar is back behind the ^ overflow; this says so and points at Taskbar
// settings instead of silently re-pinning.
static class PinNoticeDialog
{
    public static void Show(string version)
    {
        bool light = Theme.IsLight;
        int contentWidth = Dpi.S(432);

        var fonts = new List<Font>();
        Font MakeFont(string family, float size)
        {
            var font = new Font(family, size);
            fonts.Add(font);
            return font;
        }

        using var form = new Form
        {
            Text = Str.PinNoticeTitle.T(),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            // Usually appears at sign-in with nothing else of SoundFlip on screen.
            ShowInTaskbar = true,
            ClientSize = Dpi.Sz(480, 220),
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
            Text = Str.PinNoticeHeading.T(version),
        }, 1, 0);
        body.Controls.Add(header);

        body.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(contentWidth, 0),
            Text = Str.PinNoticeText.T(),
        });

        var ok = new Win11Button(light) { Accent = true, DialogResult = DialogResult.OK, Text = Str.WelcomeOk.T() };
        var taskbar = new Win11Button(light);
        taskbar.Width = Math.Max(taskbar.Width,
            TextRenderer.MeasureText(Str.WelcomeTaskbarSettings.T(), form.Font).Width + Dpi.S(28));
        taskbar.Text = Str.WelcomeTaskbarSettings.T();
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
        form.Load += (_, _) => form.ClientSize = new Size(form.ClientSize.Width, body.PreferredSize.Height + footer.Height);

        form.ShowDialog();
    }
}
