namespace audsw.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Parse_IgnoresCommentsBlankLinesAndUnknownKeys()
    {
        string text = """
            # comment

            device1 = Speakers
            unused = ignore me
            DEVICE2 = Headphones
            hotkey = ctrl+shift+f10
            """;

        var settings = SettingsStore.Parse(text);

        Assert.Equal("Speakers", settings.Device1);
        Assert.Equal("Headphones", settings.Device2);
        Assert.Equal("ctrl+shift+f10", settings.Hotkey);
    }

    [Fact]
    public void Serialize_ProducesStableConfigText()
    {
        var settings = new AppSettings
        {
            Device1 = "USB DAC",
            Device2 = "HDMI",
            Hotkey = "ctrl+alt+o",
        };

        string text = SettingsStore.Serialize(settings, "\n");

        Assert.Contains("device1 = USB DAC\n", text);
        Assert.Contains("device2 = HDMI\n", text);
        Assert.Contains("hotkey  = ctrl+alt+o\n", text);
        Assert.DoesNotContain("\r\n", text);
    }

    [Fact]
    public void SaveAndLoad_RoundTripThroughProvidedPath()
    {
        using var temp = new TempDirectory();
        string path = System.IO.Path.Combine(temp.Path, "nested", "audsw.cfg");
        var expected = new AppSettings
        {
            Device1 = "Speakers",
            Device2 = "Headphones",
            Hotkey = "ctrl+alt+9",
        };

        SettingsStore.Save(expected, path);
        var actual = SettingsStore.Load(path);

        Assert.Equal(expected.Device1, actual.Device1);
        Assert.Equal(expected.Device2, actual.Device2);
        Assert.Equal(expected.Hotkey, actual.Hotkey);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var settings = SettingsStore.Load(System.IO.Path.Combine(temp.Path, "missing.cfg"));

        Assert.Equal(string.Empty, settings.Device1);
        Assert.Equal(string.Empty, settings.Device2);
        Assert.Equal("ctrl+alt+o", settings.Hotkey);
    }
}
