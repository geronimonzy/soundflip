using System.Drawing;
using System.Windows.Forms;
using AudioSwitcher.AudioApi.CoreAudio;

// One window to manage the whole config: the output and input rings, the IO pairs,
// and every hotkey. Works on a clone so Cancel discards. Returns the updated
// settings on OK, or null on Cancel.
static class SettingsWindow
{
    public static AppSettings? Edit(CoreAudioController controller, AppSettings current)
    {
        var working = Clone(current);
        bool light = Theme.IsLight;

        var outputs = ActiveNames(controller, AudioKind.Output);
        var inputs = ActiveNames(controller, AudioKind.Input);

        using var form = new Form
        {
            Text = $"{AppMetadata.ProductName} settings",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(540, 460),
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            Font = new Font("Segoe UI", 9.5F),
            TopMost = true,
        };
        form.Shown += (_, _) => Win11.RoundCorners(form);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        tabs.TabPages.Add(RingTab("Outputs", AudioKind.Output, working.Outputs, outputs, light,
            () => working.CycleOutputs, value => working.CycleOutputs = value));
        tabs.TabPages.Add(RingTab("Inputs", AudioKind.Input, working.Inputs, inputs, light,
            () => working.CycleInputs, value => working.CycleInputs = value));
        tabs.TabPages.Add(PairsTab(working, outputs, inputs, light));

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Anchor = AnchorStyles.Right };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 12),
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        form.Controls.Add(tabs);
        form.Controls.Add(buttons);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK ? working : null;
    }

    // --- A ring tab: list of devices in cycle order + their hotkeys ---

    static TabPage RingTab(
        string title,
        AudioKind kind,
        List<DeviceEntry> ring,
        IReadOnlyList<string> active,
        bool light,
        Func<string> getCycle,
        Action<string> setCycle)
    {
        var page = new TabPage(title) { BackColor = Theme.Back(light), ForeColor = Theme.Fore(light), Padding = new Padding(10) };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
        };
        void Refresh()
        {
            int keep = list.SelectedIndex;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in ring) list.Items.Add(Describe(entry));
            list.EndUpdate();
            if (keep >= 0 && keep < list.Items.Count) list.SelectedIndex = keep;
            else if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1;
        }

        DeviceEntry? Selected() => list.SelectedIndex >= 0 ? ring[list.SelectedIndex] : null;

        var picker = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
        };
        foreach (var name in active) picker.Items.Add(name);

        var add = Btn("Add", light, () =>
        {
            string match = picker.Text.Trim();
            if (match.Length == 0) return;
            ring.Add(new DeviceEntry { Match = match });
            picker.Text = "";
            Refresh();
        });

        var remove = Btn("Remove", light, () =>
        {
            if (list.SelectedIndex < 0) return;
            ring.RemoveAt(list.SelectedIndex);
            Refresh();
        });

        var up = Btn("Up", light, () => Move(ring, list, -1, Refresh));
        var down = Btn("Down", light, () => Move(ring, list, +1, Refresh));

        var setHotkey = Btn("Hotkey...", light, () =>
        {
            var entry = Selected();
            if (entry is null) return;
            string? picked = HotkeyDialog.Ask(entry.Hotkey);
            if (picked is not null) { entry.Hotkey = picked; Refresh(); }
        });

        var clearHotkey = Btn("Clear hotkey", light, () =>
        {
            var entry = Selected();
            if (entry is null) return;
            entry.Hotkey = "";
            Refresh();
        });

        var cycleButton = Btn(CycleButtonText(getCycle()), light, null);
        cycleButton.AutoSize = true;
        cycleButton.Click += (_, _) =>
        {
            string? picked = HotkeyDialog.Ask(getCycle());
            if (picked is not null) { setCycle(picked); cycleButton.Text = CycleButtonText(picked); }
        };
        var cycleClear = Btn("Clear", light, () => { setCycle(""); cycleButton.Text = CycleButtonText(""); });

        Refresh();

        // Layout: list on top, an add row, a per-entry button row, and a cycle row.
        var addRow = Row(picker, add);
        var actionRow = Row(remove, up, down, setHotkey, clearHotkey);
        var cycleRow = Row(new Label
        {
            Text = "Cycle hotkey:",
            AutoSize = true,
            ForeColor = Theme.Fore(light),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 6, 0),
        }, cycleButton, cycleClear);

        var root = Stack(list, addRow, actionRow, cycleRow);
        page.Controls.Add(root);
        return page;
    }

    // --- The pairs tab ---

    static TabPage PairsTab(AppSettings working, IReadOnlyList<string> outputs, IReadOnlyList<string> inputs, bool light)
    {
        var page = new TabPage("Pairs") { BackColor = Theme.Back(light), ForeColor = Theme.Fore(light), Padding = new Padding(10) };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
        };
        void Refresh()
        {
            int keep = list.SelectedIndex;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var pair in working.Pairs) list.Items.Add(Describe(pair));
            list.EndUpdate();
            if (keep >= 0 && keep < list.Items.Count) list.SelectedIndex = keep;
            else if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1;
        }

        PairEntry? Selected() => list.SelectedIndex >= 0 ? working.Pairs[list.SelectedIndex] : null;

        var add = Btn("Add...", light, () =>
        {
            var created = PairDialog.Edit(new PairEntry(), outputs, inputs, light);
            if (created is not null) { working.Pairs.Add(created); Refresh(); }
        });

        var edit = Btn("Edit...", light, () =>
        {
            var entry = Selected();
            if (entry is null) return;
            var updated = PairDialog.Edit(entry, outputs, inputs, light);
            if (updated is not null)
            {
                entry.Name = updated.Name;
                entry.Output = updated.Output;
                entry.Input = updated.Input;
                Refresh();
            }
        });

        var remove = Btn("Remove", light, () =>
        {
            if (list.SelectedIndex < 0) return;
            working.Pairs.RemoveAt(list.SelectedIndex);
            Refresh();
        });

        var up = Btn("Up", light, () => Move(working.Pairs, list, -1, Refresh));
        var down = Btn("Down", light, () => Move(working.Pairs, list, +1, Refresh));

        var setHotkey = Btn("Hotkey...", light, () =>
        {
            var entry = Selected();
            if (entry is null) return;
            string? picked = HotkeyDialog.Ask(entry.Hotkey);
            if (picked is not null) { entry.Hotkey = picked; Refresh(); }
        });

        var clearHotkey = Btn("Clear hotkey", light, () =>
        {
            var entry = Selected();
            if (entry is null) return;
            entry.Hotkey = "";
            Refresh();
        });

        var cycleButton = Btn(CycleButtonText(working.CyclePairs), light, null);
        cycleButton.AutoSize = true;
        cycleButton.Click += (_, _) =>
        {
            string? picked = HotkeyDialog.Ask(working.CyclePairs);
            if (picked is not null) { working.CyclePairs = picked; cycleButton.Text = CycleButtonText(picked); }
        };
        var cycleClear = Btn("Clear", light, () => { working.CyclePairs = ""; cycleButton.Text = CycleButtonText(""); });

        Refresh();

        var actionRow = Row(add, edit, remove, up, down);
        var hotkeyRow = Row(setHotkey, clearHotkey);
        var cycleRow = Row(new Label
        {
            Text = "Cycle hotkey:",
            AutoSize = true,
            ForeColor = Theme.Fore(light),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 6, 6, 0),
        }, cycleButton, cycleClear);

        var root = Stack(list, actionRow, hotkeyRow, cycleRow);
        page.Controls.Add(root);
        return page;
    }

    // --- Small helpers ---

    static string Describe(DeviceEntry entry) =>
        entry.Match + (string.IsNullOrWhiteSpace(entry.Hotkey) ? "" : "    [" + entry.Hotkey + "]");

    static string Describe(PairEntry pair)
    {
        string name = string.IsNullOrWhiteSpace(pair.Name) ? "" : pair.Name + ":  ";
        string combo = $"{Show(pair.Output)} + {Show(pair.Input)}";
        string hotkey = string.IsNullOrWhiteSpace(pair.Hotkey) ? "" : "    [" + pair.Hotkey + "]";
        return name + combo + hotkey;

        static string Show(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    static string CycleButtonText(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "Set..." : hotkey;

    static void Move<T>(List<T> items, ListBox list, int delta, Action refresh)
    {
        int i = list.SelectedIndex;
        int j = i + delta;
        if (i < 0 || j < 0 || j >= items.Count) return;
        (items[i], items[j]) = (items[j], items[i]);
        list.SelectedIndex = j;
        refresh();
    }

    static IReadOnlyList<string> ActiveNames(CoreAudioController controller, AudioKind kind) =>
        Audio.Devices(controller, kind).Select(device => device.FullName).OrderBy(name => name).ToList();

    static Button Btn(string text, bool light, Action? onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 6, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
        };
        button.FlatAppearance.BorderColor = Theme.Line(light);
        button.FlatAppearance.MouseOverBackColor = Theme.Hover(light);
        if (onClick is not null) button.Click += (_, _) => onClick();
        return button;
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
        };
        foreach (var control in controls)
        {
            if (control is ComboBox) control.Width = 240;
            panel.Controls.Add(control);
        }
        return panel;
    }

    // Stack a fill control (first) above a series of top-docked rows. Docking order
    // means rows are added before the fill so the fill takes the remaining space.
    static Control Stack(Control fill, params Control[] rows)
    {
        var host = new Panel { Dock = DockStyle.Fill };
        for (int i = rows.Length - 1; i >= 0; i--)
        {
            rows[i].Dock = DockStyle.Bottom;
            host.Controls.Add(rows[i]);
        }
        fill.Dock = DockStyle.Fill;
        host.Controls.Add(fill);
        fill.BringToFront();
        return host;
    }

    static AppSettings Clone(AppSettings source) => new()
    {
        Outputs = source.Outputs.Select(e => new DeviceEntry { Match = e.Match, Hotkey = e.Hotkey }).ToList(),
        Inputs = source.Inputs.Select(e => new DeviceEntry { Match = e.Match, Hotkey = e.Hotkey }).ToList(),
        Pairs = source.Pairs.Select(p => new PairEntry { Name = p.Name, Output = p.Output, Input = p.Input, Hotkey = p.Hotkey }).ToList(),
        CycleOutputs = source.CycleOutputs,
        CycleInputs = source.CycleInputs,
        CyclePairs = source.CyclePairs,
    };
}

// Small modal to create/edit one pair: a name plus an output and input device.
static class PairDialog
{
    public static PairEntry? Edit(PairEntry source, IReadOnlyList<string> outputs, IReadOnlyList<string> inputs, bool light)
    {
        using var form = new Form
        {
            Text = "Pair",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowIcon = false,
            ShowInTaskbar = false,
            ClientSize = new Size(420, 200),
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            Font = new Font("Segoe UI", 9.5F),
            TopMost = true,
        };
        form.Shown += (_, _) => Win11.RoundCorners(form);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var name = new TextBox { Dock = DockStyle.Fill, Text = source.Name, BackColor = Theme.Back(light), ForeColor = Theme.Fore(light) };
        var output = Combo(outputs, source.Output, light);
        var input = Combo(inputs, source.Input, light);

        layout.Controls.Add(Caption("Name", light), 0, 0);
        layout.Controls.Add(name, 1, 0);
        layout.Controls.Add(Caption("Output", light), 0, 1);
        layout.Controls.Add(output, 1, 1);
        layout.Controls.Add(Caption("Input", light), 0, 2);
        layout.Controls.Add(input, 1, 2);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Anchor = AnchorStyles.Right };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Anchor = AnchorStyles.Right };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 3);

        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog() != DialogResult.OK) return null;

        return new PairEntry
        {
            Name = name.Text.Trim(),
            Output = output.Text.Trim(),
            Input = input.Text.Trim(),
            Hotkey = source.Hotkey,
        };
    }

    static ComboBox Combo(IReadOnlyList<string> items, string value, bool light)
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDown,
            BackColor = Theme.Back(light),
            ForeColor = Theme.Fore(light),
            Text = value,
        };
        foreach (var item in items) combo.Items.Add(item);
        return combo;
    }

    static Label Caption(string text, bool light) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Theme.Fore(light),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 6, 0, 0),
    };
}
