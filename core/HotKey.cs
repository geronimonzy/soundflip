// Parses and formats hotkey specs like "ctrl+alt+o". Virtual keys are raw Win32
// VK codes (which match Windows.System.VirtualKey), so this stays UI-framework
// free and is shared by the tray app and tests.
static class HotKey
{
    const uint MOD_ALT = 0x0001;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint MOD_WIN = 0x0008;

    internal readonly record struct HotkeySpec(uint Modifiers, uint VirtualKey);

    public static bool TryParse(string spec, out HotkeySpec hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(spec)) return false;

        uint mods = 0;
        uint vk = 0;
        bool sawKey = false;

        foreach (var rawPart in spec.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.Trim().ToLowerInvariant();
            switch (part)
            {
                case "ctrl":
                case "control": mods |= MOD_CONTROL; break;
                case "alt": mods |= MOD_ALT; break;
                case "shift": mods |= MOD_SHIFT; break;
                case "win": mods |= MOD_WIN; break;
                default:
                    if (sawKey || !TryKey(part, out vk)) return false;
                    sawKey = true;
                    break;
            }
        }

        if (!sawKey || mods == 0) return false;

        hotkey = new HotkeySpec(mods, vk);
        return true;
    }

    public static bool TryParse(string spec, out uint mods, out uint vk)
    {
        if (TryParse(spec, out HotkeySpec hotkey))
        {
            mods = hotkey.Modifiers;
            vk = hotkey.VirtualKey;
            return true;
        }

        mods = 0;
        vk = 0;
        return false;
    }

    public static string Format(HotkeySpec hotkey)
    {
        string? key = Token(hotkey.VirtualKey);
        if (key is null) throw new ArgumentOutOfRangeException(nameof(hotkey), "Unsupported virtual key.");

        var parts = new List<string>(5);
        if ((hotkey.Modifiers & MOD_CONTROL) != 0) parts.Add("ctrl");
        if ((hotkey.Modifiers & MOD_ALT) != 0) parts.Add("alt");
        if ((hotkey.Modifiers & MOD_SHIFT) != 0) parts.Add("shift");
        if ((hotkey.Modifiers & MOD_WIN) != 0) parts.Add("win");
        parts.Add(key);
        return string.Join("+", parts);
    }

    // Build a spec from a key event. vk is a raw Win32 VK code; modifiers are the
    // live key states captured at the time of the press.
    public static string? FromKeyEvent(uint vk, bool ctrl, bool alt, bool shift, bool win)
    {
        string? key = Token(vk);
        if (key is null) return null;

        var parts = new List<string>(4);
        if (ctrl) parts.Add("ctrl");
        if (alt) parts.Add("alt");
        if (shift) parts.Add("shift");
        if (win) parts.Add("win");
        if (parts.Count == 0) return null;

        parts.Add(key);
        return TryParse(string.Join("+", parts), out HotkeySpec hotkey) ? Format(hotkey) : null;
    }

    static bool TryKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1)
        {
            char ch = char.ToUpperInvariant(key[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = ch;
                return true;
            }

            return false;
        }

        if (key.Length is 2 or 3 && key[0] == 'f' && int.TryParse(key[1..], out int number) && number is >= 1 and <= 12)
        {
            vk = (uint)(0x70 + (number - 1));
            return true;
        }

        return false;
    }

    // Reverse map a VK code to its canonical token, or null if unsupported.
    static string? Token(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => char.ToLowerInvariant((char)vk).ToString(),       // A-Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),                            // 0-9
        >= 0x60 and <= 0x69 => ((char)('0' + (vk - 0x60))).ToString(),           // NumPad0-9
        >= 0x70 and <= 0x7B => "f" + (vk - 0x70 + 1),                            // F1-F12
        _ => null,
    };
}
