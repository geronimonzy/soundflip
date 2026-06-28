using System.Windows.Forms;
using System.Drawing;

static class AboutDialog
{
    public static void Show(IWin32Window? owner = null)
    {
        bool light = Theme.IsLight;

        using var form = new Form
        {
            Text = $"About {AppMetadata.ProductName}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(470, 300),
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
        };

        form.Shown += (_, _) => Win11.RoundCorners(form);

        var iconImage = TrayArt.SpeakerBitmap(56);
        form.FormClosed += (_, _) => iconImage.Dispose();

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 2,
        };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

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
            Font = new Font("Segoe UI Semibold", 13F),
            Text = AppMetadata.ProductName,
            Margin = new Padding(0, 0, 0, 4),
        });
        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            Text = "Version " + AppMetadata.VersionText,
            Margin = new Padding(0, 0, 0, 12),
        });
        body.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            MaximumSize = new Size(340, 0),
            Text = AppMetadata.Description,
            Margin = new Padding(0, 0, 0, 12),
        });
        body.Controls.Add(InfoLine("Publisher", AppMetadata.Company));
        body.Controls.Add(InfoLine("Copyright", AppMetadata.Copyright));
        body.Controls.Add(InfoLine("Settings", SettingsStore.SettingsPath));

        string notice = AppMetadata.MissingMetadataNotice;
        if (notice.Length > 0)
        {
            body.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                MaximumSize = new Size(340, 0),
                ForeColor = Theme.Warning(light),
                Text = notice,
                Margin = new Padding(0, 12, 0, 0),
            });
        }

        header.Controls.Add(iconBox, 0, 0);
        header.Controls.Add(body, 1, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 14, 0, 0),
        };

        var close = new Button
        {
            Text = "Close",
            AutoSize = true,
            DialogResult = DialogResult.OK,
        };
        buttons.Controls.Add(close);

        buttons.Controls.Add(ActionButton("Support", AppMetadata.SupportUrl, owner));
        buttons.Controls.Add(ActionButton("Homepage", AppMetadata.HomepageUrl, owner));

        shell.Controls.Add(header, 0, 0);
        shell.Controls.Add(buttons, 0, 1);
        form.Controls.Add(shell);
        form.AcceptButton = close;
        form.CancelButton = close;

        if (owner is null) form.ShowDialog();
        else form.ShowDialog(owner);
    }

    static Label InfoLine(string label, string value) => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 9F),
        MaximumSize = new Size(340, 0),
        Text = $"{label}: {ValueOrFallback(value)}",
        Margin = new Padding(0, 0, 0, 4),
    };

    static Button ActionButton(string label, string url, IWin32Window? owner)
    {
        var button = new Button
        {
            Text = label,
            AutoSize = true,
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
