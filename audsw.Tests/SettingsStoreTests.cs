namespace audsw.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTripsFullModel()
    {
        var settings = new AppSettings
        {
            Outputs =
            {
                new DeviceEntry { Match = "Speakers" },
                new DeviceEntry { Match = "Headphones" },
            },
            Inputs = { new DeviceEntry { Match = "Yeti" } },
            CycleOutputs = "ctrl+alt+o",
            CycleInputs = "ctrl+alt+i",
        };

        var actual = SettingsStore.Deserialize(SettingsStore.Serialize(settings));

        Assert.Equal(2, actual.Outputs.Count);
        Assert.Equal("Speakers", actual.Outputs[0].Match);
        Assert.Equal("Headphones", actual.Outputs[1].Match);
        Assert.Single(actual.Inputs);
        Assert.Equal("Yeti", actual.Inputs[0].Match);
        Assert.Equal("ctrl+alt+i", actual.CycleInputs);
    }

    // Pre-1.1.2 settings carried pairs, cycle-pair hotkeys, and per-device hotkeys;
    // those must load cleanly (and be dropped) rather than fail deserialization.
    [Fact]
    public void Deserialize_IgnoresRetiredPairAndHotkeyProperties()
    {
        string json = """
            {
              "outputs": [ { "match": "Speakers", "hotkey": "ctrl+alt+1" } ],
              "inputs": [ { "match": "Yeti", "hotkey": "ctrl+alt+2" } ],
              "pairs": [ { "name": "Desk", "output": "Speakers", "input": "Yeti", "hotkey": "ctrl+alt+3" } ],
              "cycleOutputs": "ctrl+alt+o",
              "cycleInputs": "ctrl+alt+i",
              "cyclePairs": "ctrl+alt+p"
            }
            """;

        var actual = SettingsStore.Deserialize(json);

        Assert.Single(actual.Outputs);
        Assert.Equal("Speakers", actual.Outputs[0].Match);
        Assert.Single(actual.Inputs);
        Assert.Equal("Yeti", actual.Inputs[0].Match);
        Assert.Equal("ctrl+alt+o", actual.CycleOutputs);
        Assert.Equal("ctrl+alt+i", actual.CycleInputs);
    }

    [Fact]
    public void Serialize_UsesCamelCaseJson()
    {
        string json = SettingsStore.Serialize(new AppSettings());

        Assert.Contains("\"outputs\"", json);
        Assert.Contains("\"cycleOutputs\"", json);
    }

    [Fact]
    public void SaveAndLoad_RoundTripThroughProvidedPath()
    {
        using var temp = new TempDirectory();
        string path = System.IO.Path.Combine(temp.Path, "nested", "audsw.json");
        var expected = new AppSettings
        {
            Outputs = { new DeviceEntry { Match = "USB DAC" } },
            CycleOutputs = "ctrl+alt+0",
        };

        SettingsStore.Save(expected, path);
        var actual = SettingsStore.Load(path);

        Assert.Single(actual.Outputs);
        Assert.Equal("USB DAC", actual.Outputs[0].Match);
        Assert.Equal("ctrl+alt+0", actual.CycleOutputs);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var settings = SettingsStore.Load(System.IO.Path.Combine(temp.Path, "missing.json"));

        Assert.Empty(settings.Outputs);
        Assert.Empty(settings.Inputs);
        Assert.Equal("ctrl+alt+o", settings.CycleOutputs);
    }

    [Fact]
    public void Migrate_MapsLegacyDevicesToOutputRingAndCycleHotkey()
    {
        string legacy = """
            # comment
            device1 = Speakers
            DEVICE2 = Headphones
            hotkey = ctrl+shift+f10
            """;

        var settings = LegacyCfg.Migrate(legacy);

        Assert.Equal(2, settings.Outputs.Count);
        Assert.Equal("Speakers", settings.Outputs[0].Match);
        Assert.Equal("Headphones", settings.Outputs[1].Match);
        Assert.Equal("ctrl+shift+f10", settings.CycleOutputs);
        Assert.Empty(settings.Inputs);
    }

    [Fact]
    public void Load_NoJsonButLegacyPresent_MigratesAndWritesJson()
    {
        using var temp = new TempDirectory();
        string jsonPath = System.IO.Path.Combine(temp.Path, "audsw.json");
        string legacyPath = System.IO.Path.Combine(temp.Path, "audsw.cfg");
        System.IO.File.WriteAllText(legacyPath, "device1 = Speakers\nhotkey = ctrl+alt+u\n");

        var settings = SettingsStore.Load(jsonPath, legacyPath);

        Assert.Single(settings.Outputs);
        Assert.Equal("Speakers", settings.Outputs[0].Match);
        Assert.Equal("ctrl+alt+u", settings.CycleOutputs);
        Assert.True(System.IO.File.Exists(jsonPath), "migration should persist a JSON file");
    }
}
