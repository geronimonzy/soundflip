namespace SoundFlip.Tests;

public sealed class TrayPinTests
{
    const string V1 = @"C:\Program Files\WindowsApps\KirillFedorov.SoundFlip_1.4.0.0_x64__y3rmr8gq3s82g\soundflip.exe";
    const string V2 = @"C:\Program Files\WindowsApps\KirillFedorov.SoundFlip_1.4.1.0_x64__y3rmr8gq3s82g\soundflip.exe";

    static TrayPin.Entry E(string id, string path, int? promoted) => new(id, path, promoted);

    [Fact]
    public void PinnedOlderVersion_PromotesUndecidedCurrentEntry()
    {
        var entries = new[] { E("old", V1, 1), E("new", V2, null) };
        Assert.Equal("new", TrayPin.EntryToPromote(entries, V2));
    }

    [Fact]
    public void ExplicitUnpinOnCurrentEntry_IsRespected()
    {
        var entries = new[] { E("old", V1, 1), E("new", V2, 0) };
        Assert.Null(TrayPin.EntryToPromote(entries, V2));
    }

    [Fact]
    public void NothingPinnedBefore_NeverPins()
    {
        var entries = new[] { E("old", V1, null), E("other", @"C:\Tools\other.exe", 1), E("new", V2, null) };
        Assert.Null(TrayPin.EntryToPromote(entries, V2));
    }

    [Fact]
    public void CurrentEntryNotCreatedYet_DoesNothing()
    {
        var entries = new[] { E("old", V1, 1) };
        Assert.Null(TrayPin.EntryToPromote(entries, V2));
    }

    [Fact]
    public void PathMatch_IsCaseInsensitive()
    {
        var entries = new[] { E("old", V1, 1), E("new", V2.ToUpperInvariant(), null) };
        Assert.Equal("new", TrayPin.EntryToPromote(entries, V2));
    }
}
