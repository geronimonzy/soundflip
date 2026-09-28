using System.Windows.Forms;
using System.Drawing;

// One-time first-run window. Windows 11 puts a new app's tray icon behind the ^
// overflow, so without this a fresh install looks like it did nothing at all.
// Explains where SoundFlip lives, how to keep the icon visible, and the basics,
// and lets the user fix the two things worth deciding up front: the UI language
// (Windows' display language is only a guess) and Start with Windows.
// A language pick is applied and saved immediately through onLanguageChanged and
// the window re-renders in it. Returns whether "Start with Windows" was ticked.
static class WelcomeDialog
{
    public static bool Show(string language, string cycleHotkey, bool offerAutoStart, Action<string> onLanguageChanged)
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
        var heading = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = MakeFont("Segoe UI Semibold", 13F),
            MaximumSize = new Size(contentWidth - Dpi.S(52), 0),
        };
        header.Controls.Add(heading, 1, 0);
        body.Controls.Add(header);

        var trayText = Paragraph(contentWidth, 12);
        var clickTip = Paragraph(contentWidth, 4);
        var hotkeyTip = Paragraph(contentWidth, 4);
        body.Controls.Add(trayText);
        body.Controls.Add(clickTip);
        if (!string.IsNullOrWhiteSpace(cycleHotkey)) body.Controls.Add(hotkeyTip);

        // Language row: caption + drop-down with the same choices as the tray
        // Language submenu (system default, then each language in its own name).
        var languageRow = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Dpi.Pad(0, 14, 0, 0),
        };
        var languageLabel = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = Dpi.Pad(0, 0, 12, 0),
        };
        var languageBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = Dpi.S(200),
            BackColor = Theme.Content(light),
            ForeColor = Theme.Fore(light),
            Margin = Padding.Empty,
        };
        languageRow.Controls.Add(languageLabel, 0, 0);
        languageRow.Controls.Add(languageBox, 1, 0);
        body.Controls.Add(languageRow);

        CheckBox? startup = null;
        if (offerAutoStart)
        {
            startup = new CheckBox
            {
                AutoSize = true,
                ForeColor = Theme.Fore(light),
                Margin = Dpi.Pad(0, 10, 0, 0),
            };
            body.Controls.Add(startup);
        }

        var ok = new Win11Button(light) { Accent = true, DialogResult = DialogResult.OK };
        var taskbar = new Win11Button(light);
        int minButtonWidth = taskbar.Width;
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

        // All visible text, so a language change can simply re-run it.
        bool fillingLanguages = false;
        void ApplyTexts()
        {
            form.Text = Str.WelcomeTitle.T();
            heading.Text = Str.WelcomeHeading.T();
            trayText.Text = Str.WelcomeTrayText.T();
            clickTip.Text = "•  " + Str.WelcomeClickTip.T();
            hotkeyTip.Text = "•  " + Str.WelcomeHotkeyTip.T(cycleHotkey);
            languageLabel.Text = Str.Language.T();
            if (startup is not null) startup.Text = Str.WelcomeStartup.T();
            ok.Text = Str.WelcomeOk.T();
            taskbar.Text = Str.WelcomeTaskbarSettings.T();
            // Longer than the fixed-width button in most languages.
            taskbar.Width = Math.Max(minButtonWidth, TextRenderer.MeasureText(taskbar.Text, form.Font).Width + Dpi.S(28));

            // "System default" is itself translated, so refill the list in place.
            fillingLanguages = true;
            string selected = (languageBox.SelectedItem as LanguageChoice)?.Code ?? Loc.Normalize(language);
            languageBox.Items.Clear();
            languageBox.Items.Add(new LanguageChoice(Loc.Auto, Str.LanguageSystem.T()));
            foreach (var code in Loc.Supported) languageBox.Items.Add(new LanguageChoice(code, Loc.NativeName(code)));
            languageBox.SelectedItem = languageBox.Items.Cast<LanguageChoice>().First(choice => choice.Code == selected);
            fillingLanguages = false;

            // Text length varies a lot between languages: size the window to its content.
            if (form.IsHandleCreated)
                form.ClientSize = new Size(form.ClientSize.Width, body.PreferredSize.Height + footer.Height);
        }

        languageBox.SelectedIndexChanged += (_, _) =>
        {
            if (fillingLanguages || languageBox.SelectedItem is not LanguageChoice choice) return;
            onLanguageChanged(choice.Code);
            ApplyTexts();
        };

        ApplyTexts();
        form.Load += (_, _) => form.ClientSize = new Size(form.ClientSize.Width, body.PreferredSize.Height + footer.Height);

        form.ShowDialog();
        return startup?.Checked == true;
    }

    sealed record LanguageChoice(string Code, string Label)
    {
        public override string ToString() => Label;
    }

    static Label Paragraph(int width, int bottomMargin) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(width, 0),
        Margin = Dpi.Pad(0, 0, 0, bottomMargin),
    };
}
