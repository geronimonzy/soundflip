namespace SoundFlip.Tests;

public sealed class StringsTests
{
    static IEnumerable<Str> AllKeys => Enum.GetValues<Str>();

    [Fact]
    public void EveryLanguageHasEveryKey()
    {
        foreach (string code in Loc.Supported)
        {
            var table = Loc.Table(code);
            foreach (Str key in AllKeys)
            {
                Assert.True(table.TryGetValue(key, out string? text), $"{code} is missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(text), $"{code}.{key} is blank");
            }
        }
    }

    // A translation that drops or adds a "{0}" would either lose the device /
    // product name or throw FormatException at runtime.
    [Fact]
    public void PlaceholdersMatchEnglish()
    {
        static int Count(string text) => text.Split("{0}").Length - 1;

        foreach (string code in Loc.Supported)
        {
            foreach (Str key in AllKeys)
            {
                int expected = Count(Loc.Get("en", key));
                int actual = Count(Loc.Get(code, key));
                Assert.True(expected == actual, $"{code}.{key}: expected {expected} placeholder(s), found {actual}");
                Assert.DoesNotContain("{1}", Loc.Get(code, key));
            }
        }
    }

    [Fact]
    public void Get_FallsBackToEnglishForUnknownLanguage()
    {
        Assert.Equal(Loc.Get("en", Str.Exit), Loc.Get("xx", Str.Exit));
    }

    [Fact]
    public void Get_ReturnsTranslatedText()
    {
        Assert.Equal("Beenden", Loc.Get("de", Str.Exit));
        Assert.Equal("Выход", Loc.Get("ru", Str.Exit));
        Assert.NotEqual(Loc.Get("en", Str.Exit), Loc.Get("fr", Str.Exit));
        Assert.NotEqual(Loc.Get("en", Str.Exit), Loc.Get("es", Str.Exit));
    }

    [Theory]
    [InlineData((ushort)0x0409, "en")] // en-US
    [InlineData((ushort)0x0809, "en")] // en-GB
    [InlineData((ushort)0x0407, "de")] // de-DE
    [InlineData((ushort)0x0C07, "de")] // de-AT
    [InlineData((ushort)0x0C0A, "es")] // es-ES
    [InlineData((ushort)0x080A, "es")] // es-MX
    [InlineData((ushort)0x040C, "fr")] // fr-FR
    [InlineData((ushort)0x0C0C, "fr")] // fr-CA
    [InlineData((ushort)0x0419, "ru")] // ru-RU
    [InlineData((ushort)0x0411, "en")] // ja-JP: unsupported -> English
    [InlineData((ushort)0x0000, "en")]
    public void FromLangId_MapsPrimaryLanguage(ushort langId, string expected)
    {
        Assert.Equal(expected, Loc.FromLangId(langId));
    }

    [Theory]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("auto", "auto")]
    [InlineData("AUTO", "auto")]
    [InlineData("klingon", "auto")]
    [InlineData("ru", "ru")]
    [InlineData(" De ", "de")]
    public void Normalize_AcceptsOnlySupportedCodes(string? setting, string expected)
    {
        Assert.Equal(expected, Loc.Normalize(setting));
    }

    [Theory]
    [InlineData("auto", "ru", "ru")]
    [InlineData(null, "de", "de")]
    [InlineData("auto", "en", "en")]
    [InlineData("auto", "xx", "en")]
    [InlineData("fr", "ru", "fr")]
    [InlineData("FR", "ru", "fr")]
    [InlineData("klingon", "es", "es")]
    public void Resolve_PrefersExplicitChoiceThenSystemLanguage(string? setting, string system, string expected)
    {
        Assert.Equal(expected, Loc.Resolve(setting, system));
    }

    [Fact]
    public void NativeName_IsNeverTranslated()
    {
        Assert.Equal("English", Loc.NativeName("en"));
        Assert.Equal("Deutsch", Loc.NativeName("de"));
        Assert.Equal("Español", Loc.NativeName("es"));
        Assert.Equal("Français", Loc.NativeName("fr"));
        Assert.Equal("Русский", Loc.NativeName("ru"));
    }

    [Fact]
    public void SupportedLanguagesAreUniqueAndHaveTables()
    {
        Assert.Equal(Loc.Supported.Length, Loc.Supported.Distinct().Count());
        Assert.Contains(Loc.Default, Loc.Supported);
        foreach (string code in Loc.Supported)
            Assert.NotEmpty(Loc.Table(code));
    }

    // Settings written before the language setting existed must load as "auto".
    [Fact]
    public void Settings_DefaultLanguageIsAuto()
    {
        Assert.Equal("auto", new AppSettings().Language);

        var loaded = SettingsStore.Deserialize("""{ "outputs": [], "inputs": [], "cycleOutputs": "ctrl+alt+o" }""");
        Assert.Equal("auto", loaded.Language);
    }

    [Fact]
    public void Settings_LanguageRoundTrips()
    {
        var settings = new AppSettings { Language = "ru" };
        string json = SettingsStore.Serialize(settings);

        Assert.Contains("\"language\": \"ru\"", json);
        Assert.Equal("ru", SettingsStore.Deserialize(json).Language);
    }
}
