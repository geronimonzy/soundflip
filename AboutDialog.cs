using System.Windows.Forms;
using System.Drawing;

static class AboutDialog
{
    public static void Show(IWin32Window? owner = null)
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

        using var form = new Form
        {
            Text = $"About {AppMetadata.ProductName}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(470, 312),
            BackColor = Theme.Content(light),
            ForeColor = Theme.Fore(light),
            Font = MakeFont("Segoe UI", 9.5F),
        };

        form.Shown += (_, _) => Win11.ApplyChrome(form, light);

        var iconImage = TrayArt.SpeakerBitmap(56);
        form.FormClosed += (_, _) =>
        {
            iconImage.Dispose();
            foreach (var font in fonts) font.Dispose();
        };

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 0),
            ColumnCount = 1,
            RowCount = 1,
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var iconBox = new PictureBox
        {
            Size = new Size(56, 56),
            Margin = new Padding(0, 2, 16, 0),
            Image = iconImage,
            SizeMode = PictureBoxSizeMode.CenterImage,
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 8,
        };

        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = MakeFont("Segoe UI Semibold", 13F),
            Text = AppMetadata.ProductName,
            Margin = new Padding(0, 0, 0, 4),
        });
        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = MakeFont("Segoe UI", 9.5F),
            Text = "Version " + AppMetadata.VersionText,
            Margin = new Padding(0, 0, 0, 12),
        });
        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = MakeFont("Segoe UI", 9.5F),
            MaximumSize = new Size(340, 0),
            Text = AppMetadata.Description,
            Margin = new Padding(0, 0, 0, 12),
        });
        body.Controls.Add(InfoLine("Publisher", AppMetadata.Company, MakeFont));
        body.Controls.Add(InfoLine("Copyright", AppMetadata.Copyright, MakeFont));
        body.Controls.Add(InfoLine("Settings", SettingsStore.SettingsPath, MakeFont));

        string notice = AppMetadata.MissingMetadataNotice;
        if (notice.Length > 0)
        {
            body.Controls.Add(new Label
            {
                AutoSize = true,
                Font = MakeFont("Segoe UI", 9F),
                MaximumSize = new Size(340, 0),
                ForeColor = Theme.Warning(light),
                Text = notice,
                Margin = new Padding(0, 12, 0, 0),
            });
        }

        header.Controls.Add(iconBox, 0, 0);
        header.Controls.Add(body, 1, 0);

        var close = new Win11Button(light) { Text = "Close", Accent = true, DialogResult = DialogResult.OK };
        var footer = DialogFooter.Create(
            light,
            close,
            ActionButton("Support", AppMetadata.SupportUrl, owner, light),
            ActionButton("Homepage", AppMetadata.HomepageUrl, owner, light));

        shell.Controls.Add(header, 0, 0);
        form.Controls.Add(shell);
        form.Controls.Add(footer);
        shell.BringToFront();
        form.AcceptButton = close;
        form.CancelButton = close;

        if (owner is null) form.ShowDialog();
        else form.ShowDialog(owner);
    }

    static Label InfoLine(string label, string value, Func<string, float, Font> makeFont) => new()
    {
        AutoSize = true,
        Font = makeFont("Segoe UI", 9F),
        MaximumSize = new Size(340, 0),
        Text = $"{label}: {ValueOrFallback(value)}",
        Margin = new Padding(0, 0, 0, 4),
    };

    static Button ActionButton(string label, string url, IWin32Window? owner, bool light)
    {
        var button = new Win11Button(light)
        {
            Text = label,
            Enabled = !string.IsNullOrWhiteSpace(url),
        };

        button.Click += (_, _) =>
        {
            try
            {
                AppMetadata.OpenUrl(url);
            }
            catch (Exception ex)
            {
                if (owner is null)
                    MessageBox.Show(ex.Message, AppMetadata.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(owner, ex.Message, AppMetadata.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        return button;
    }

    static string ValueOrFallback(string value) => string.IsNullOrWhiteSpace(value) ? "Not configured" : value;
}
