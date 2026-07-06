using System.Windows.Forms;

namespace SoundFlip.Tests;

public sealed class HotKeyTests
{
    [Fact]
    public void TryParse_ParsesCanonicalShortcut()
    {
        bool ok = HotKey.TryParse("ctrl+alt+f12", out HotKey.HotkeySpec hotkey);

        Assert.True(ok);
        Assert.Equal((uint)0x0002 | 0x0001u, hotkey.Modifiers);
        Assert.Equal(0x7Bu, hotkey.VirtualKey);
        Assert.Equal("ctrl+alt+f12", HotKey.Format(hotkey));
    }

    [Fact]
    public void TryParse_RejectsBareKeyWithoutModifier()
    {
        Assert.False(HotKey.TryParse("o", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void TryParse_RejectsMultipleKeys()
    {
        Assert.False(HotKey.TryParse("ctrl+a+b", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void TryParse_RejectsUnsupportedKey()
    {
        Assert.False(HotKey.TryParse("ctrl+space", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void FromKeyEvent_CanonicalizesModifierOrder()
    {
        string? spec = HotKey.FromKeyEvent(Keys.F4, ctrl: false, alt: true, shift: true, win: true);

        Assert.Equal("alt+shift+win+f4", spec);
    }

    [Fact]
    public void TryParse_ParsesNumpadDigit()
    {
        bool ok = HotKey.TryParse("ctrl+num5", out HotKey.HotkeySpec hotkey);

        Assert.True(ok);
        Assert.Equal(0x65u, hotkey.VirtualKey);
        Assert.Equal("ctrl+num5", HotKey.Format(hotkey));
    }

    [Fact]
    public void FromKeyEvent_NumpadDigitStaysOnNumpad()
    {
        string? spec = HotKey.FromKeyEvent(Keys.NumPad5, ctrl: true, alt: false, shift: false, win: false);

        Assert.Equal("ctrl+num5", spec);
    }

    [Fact]
    public void TryParse_ParsesTopRowDigit()
    {
        bool ok = HotKey.TryParse("ctrl+5", out HotKey.HotkeySpec hotkey);

        Assert.True(ok);
        Assert.Equal(0x35u, hotkey.VirtualKey);
        Assert.Equal("ctrl+5", HotKey.Format(hotkey));
    }

    [Fact]
    public void TryParse_RejectsEmptyToken()
    {
        Assert.False(HotKey.TryParse("ctrl++a", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void TryParse_RejectsDuplicateModifier()
    {
        Assert.False(HotKey.TryParse("ctrl+ctrl+a", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void TryParse_RejectsZeroPaddedFKey()
    {
        Assert.False(HotKey.TryParse("ctrl+f01", out HotKey.HotkeySpec _));
    }

    [Fact]
    public void TryParse_RejectsTrailingSeparator()
    {
        Assert.False(HotKey.TryParse("ctrl+a+", out HotKey.HotkeySpec _));
    }
}
