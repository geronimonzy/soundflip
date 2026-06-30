using System.Collections.ObjectModel;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

// Manages the whole config (output ring, input ring, IO pairs, and every hotkey).
// Built in code with native WinUI controls so it follows the system theme. Works on
// a clone, calling back with the result only when the user saves.
sealed class SettingsWindow : Window
{
    readonly CoreAudioController _controller;
    readonly Action<AppSettings> _onSaved;
    readonly AppSettings _working;
    readonly Grid _root;

    public SettingsWindow(CoreAudioController controller, AppSettings current, Action<AppSettings> onSaved)
    {
        _controller = controller;
        _onSaved = onSaved;
        _working = Clone(current);

        Title = $"{AppMetadata.ProductName} settings";
        AppWindow.Resize(new SizeInt32(660, 620));

        var outputs = ActiveNames(AudioKind.Output);
        var inputs = ActiveNames(AudioKind.Input);

        var pivot = new Pivot { Margin = new Thickness(12, 8, 12, 0) };
        pivot.Items.Add(RingTab("Outputs", AudioKind.Output, _working.Outputs, outputs,
            () => _working.CycleOutputs, value => _working.CycleOutputs = value));
        pivot.Items.Add(RingTab("Inputs", AudioKind.Input, _working.Inputs, inputs,
            () => _working.CycleInputs, value => _working.CycleInputs = value));
        pivot.Items.Add(PairsTab(outputs, inputs));

        var save = new Button { Content = "Save", Style = AccentStyle() };
        save.Click += (_, _) => { _onSaved(_working); Close(); };
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => Close();

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(12, 8, 12, 12),
        };
        commands.Children.Add(cancel);
        commands.Children.Add(save);

        _root = new Grid();
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(pivot, 0);
        Grid.SetRow(commands, 1);
        _root.Children.Add(pivot);
        _root.Children.Add(commands);

        Content = _root;
    }

    // --- A ring tab: ordered device list, actions, and a cycle-hotkey row ---

    PivotItem RingTab(string header, AudioKind kind, List<DeviceEntry> ring, IReadOnlyList<string> active,
        Func<string> getCycle, Action<string> setCycle)
    {
        var rows = new ObservableCollection<string>();
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single };
        void Refresh()
        {
            int keep = list.SelectedIndex;
            rows.Clear();
            foreach (var entry in ring) rows.Add(Describe(entry.Match, entry.Hotkey));
            list.SelectedIndex = keep >= 0 && keep < rows.Count ? keep : rows.Count - 1;
        }
        DeviceEntry? Selected() => list.SelectedIndex >= 0 ? ring[list.SelectedIndex] : null;

        var picker = new ComboBox { PlaceholderText = "Choose a device", MinWidth = 280 };
        foreach (var name in active) picker.Items.Add(name);

        var add = Btn("Add", () =>
        {
            if (picker.SelectedItem is string name && !ring.Any(e => string.Equals(e.Match, name, StringComparison.OrdinalIgnoreCase)))
            {
                ring.Add(new DeviceEntry { Match = name });
                Refresh();
            }
        });

        var remove = Btn("Remove", () => { if (list.SelectedIndex >= 0) { ring.RemoveAt(list.SelectedIndex); Refresh(); } });
        var up = Btn("Up", () => Move(ring, list, -1, Refresh));
        var down = Btn("Down", () => Move(ring, list, +1, Refresh));
        var setHotkey = Btn("Set hotkey", async () =>
        {
            var entry = Selected();
            if (entry is null) return;
            string? picked = await CaptureHotkeyAsync(entry.Hotkey);
            if (picked is not null) { entry.Hotkey = picked; Refresh(); }
        });
        var clearHotkey = Btn("Clear hotkey", () => { var entry = Selected(); if (entry is not null) { entry.Hotkey = ""; Refresh(); } });

        var addRow = Horizontal(picker, add);
        var actions = Horizontal(remove, up, down, setHotkey, clearHotkey);
        var cycleRow = HotkeyRow(header == "Outputs" ? "Cycle outputs" : "Cycle inputs", getCycle, setCycle);

        Refresh();
        return new PivotItem { Header = header, Content = Compose(list, addRow, actions, cycleRow) };
    }

    // --- The pairs tab ---

    PivotItem PairsTab(IReadOnlyList<string> outputs, IReadOnlyList<string> inputs)
    {
        var rows = new ObservableCollection<string>();
        var list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single };
        void Refresh()
        {
            int keep = list.SelectedIndex;
            rows.Clear();
            foreach (var pair in _working.Pairs) rows.Add(Describe(PairLabel(pair), pair.Hotkey));
            list.SelectedIndex = keep >= 0 && keep < rows.Count ? keep : rows.Count - 1;
        }
        PairEntry? Selected() => list.SelectedIndex >= 0 ? _working.Pairs[list.SelectedIndex] : null;

        var add = Btn("Add", async () =>
        {
            var created = await EditPairAsync(new PairEntry(), outputs, inputs);
            if (created is not null) { _working.Pairs.Add(created); Refresh(); }
        });
        var edit = Btn("Edit", async () =>
        {
            var entry = Selected();
            if (entry is null) return;
            var updated = await EditPairAsync(entry, outputs, inputs);
            if (updated is not null) { entry.Name = updated.Name; entry.Output = updated.Output; entry.Input = updated.Input; Refresh(); }
        });
        var remove = Btn("Remove", () => { if (list.SelectedIndex >= 0) { _working.Pairs.RemoveAt(list.SelectedIndex); Refresh(); } });
        var up = Btn("Up", () => Move(_working.Pairs, list, -1, Refresh));
        var down = Btn("Down", () => Move(_working.Pairs, list, +1, Refresh));
        var setHotkey = Btn("Set hotkey", async () =>
        {
            var entry = Selected();
            if (entry is null) return;
            string? picked = await CaptureHotkeyAsync(entry.Hotkey);
            if (picked is not null) { entry.Hotkey = picked; Refresh(); }
        });
        var clearHotkey = Btn("Clear hotkey", () => { var entry = Selected(); if (entry is not null) { entry.Hotkey = ""; Refresh(); } });

        var actions = Horizontal(add, edit, remove, up, down, setHotkey, clearHotkey);
        var cycleRow = HotkeyRow("Cycle pairs", () => _working.CyclePairs, value => _working.CyclePairs = value);

        Refresh();
        return new PivotItem { Header = "Pairs", Content = Compose(list, actions, cycleRow) };
    }

    // --- Shared building blocks ---

    Grid HotkeyRow(string label, Func<string> get, Action<string> set)
    {
        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        var value = new Button { Content = ShortcutText(get()) };
        value.Click += async (_, _) => { string? picked = await CaptureHotkeyAsync(get()); if (picked is not null) { set(picked); value.Content = ShortcutText(picked); } };
        var clear = Btn("Clear", () => { set(""); value.Content = ShortcutText(""); });

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(value);
        right.Children.Add(clear);

        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(caption, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(caption);
        grid.Children.Add(right);
        return grid;
    }

    static Grid Compose(FrameworkElement list, params FrameworkElement[] belowRows)
    {
        var grid = new Grid { Margin = new Thickness(4, 12, 4, 4) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        foreach (var _ in belowRows) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(list, 0);
        grid.Children.Add(list);
        for (int i = 0; i < belowRows.Length; i++)
        {
            Grid.SetRow(belowRows[i], i + 1);
            grid.Children.Add(belowRows[i]);
        }
        return grid;
    }

    static StackPanel Horizontal(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    static Button Btn(string text, Action onClick)
    {
        var button = new Button { Content = text };
        button.Click += (_, _) => onClick();
        return button;
    }

    static Style AccentStyle() =>
        (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"];

    // --- Hotkey capture + pair editor (ContentDialogs) ---

    async Task<string?> CaptureHotkeyAsync(string current)
    {
        string? result = null;
        var status = new TextBlock { Text = "Press a shortcut (modifier + key)…", Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Current: " + (string.IsNullOrWhiteSpace(current) ? "none" : current) });
        panel.Children.Add(status);

        var dialog = new ContentDialog
        {
            Title = "Set hotkey",
            Content = panel,
            CloseButtonText = "Cancel",
            XamlRoot = _root.XamlRoot,
        };
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or VirtualKey.LeftWindows or VirtualKey.RightWindows)
                return;

            string? spec = HotKey.FromKeyEvent((uint)e.Key, IsDown(VirtualKey.Control), IsDown(VirtualKey.Menu), IsDown(VirtualKey.Shift),
                IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows));
            e.Handled = true;
            if (spec is null)
            {
                status.Text = "Need a modifier plus a letter, digit, or F1-F12.";
                return;
            }

            result = spec;
            dialog.Hide();
        };

        await dialog.ShowAsync();
        return result;
    }

    async Task<PairEntry?> EditPairAsync(PairEntry source, IReadOnlyList<string> outputs, IReadOnlyList<string> inputs)
    {
        var name = new TextBox { Header = "Name", Text = source.Name };
        var output = new ComboBox { Header = "Output", MinWidth = 320, PlaceholderText = "Choose an output" };
        foreach (var item in outputs) output.Items.Add(item);
        if (!string.IsNullOrWhiteSpace(source.Output)) output.SelectedItem = source.Output;
        var input = new ComboBox { Header = "Input", MinWidth = 320, PlaceholderText = "Choose an input" };
        foreach (var item in inputs) input.Items.Add(item);
        if (!string.IsNullOrWhiteSpace(source.Input)) input.SelectedItem = source.Input;

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(name);
        panel.Children.Add(output);
        panel.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = "Pair",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _root.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        return new PairEntry
        {
            Name = name.Text.Trim(),
            Output = output.SelectedItem as string ?? "",
            Input = input.SelectedItem as string ?? "",
            Hotkey = source.Hotkey,
        };
    }

    static bool IsDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    // --- Helpers ---

    static void Move<T>(List<T> items, ListView list, int delta, Action refresh)
    {
        int i = list.SelectedIndex;
        int j = i + delta;
        if (i < 0 || j < 0 || j >= items.Count) return;
        (items[i], items[j]) = (items[j], items[i]);
        refresh();
        list.SelectedIndex = j;
    }

    IReadOnlyList<string> ActiveNames(AudioKind kind) =>
        Audio.Devices(_controller, kind).Select(device => device.FullName).OrderBy(name => name).ToList();

    static string Describe(string primary, string hotkey) =>
        string.IsNullOrWhiteSpace(hotkey) ? primary : $"{primary}    [{hotkey}]";

    static string PairLabel(PairEntry pair)
    {
        string combo = $"{Show(pair.Output)}  →  {Show(pair.Input)}";
        return string.IsNullOrWhiteSpace(pair.Name) ? combo : $"{pair.Name}   ·   {combo}";

        static string Show(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    static string ShortcutText(string hotkey) => string.IsNullOrWhiteSpace(hotkey) ? "Set shortcut" : hotkey;

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
