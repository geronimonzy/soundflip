namespace SoundFlip.Tests;

public sealed class TrayPinTests
{
    const string Dir = @"C:\Program Files\WindowsApps\";
    const string V0 = Dir + @"KirillFedorov.SoundFlip_1.3.5.0_x64__y3rmr8gq3s82g\soundflip.exe";
    const string V1 = Dir + @"KirillFedorov.SoundFlip_1.4.0.0_x64__y3rmr8gq3s82g\soundflip.exe";
    const string V2 = Dir + @"KirillFedorov.SoundFlip_1.4.1.0_x64__y3rmr8gq3s82g\soundflip.exe";

    static TrayPin.Entry E(string id, string path, int? promoted) => new(id, path, promoted);

    [Fact]
    public void PinnedPreviousVersion_UndecidedCurrent_PinLost()
    {
        var entries = new[] { E("old", V1, 1), E("new", V2, null) };
        Assert.True(TrayPin.PinLost(entries, V2));
    }

    [Fact]
    public void CurrentEntryDecided_NoNotice()
    {
        Assert.False(TrayPin.PinLost(new[] { E("old", V1, 1), E("new", V2, 0) }, V2));
        Assert.False(TrayPin.PinLost(new[] { E("old", V1, 1), E("new", V2, 1) }, V2));
    }

    [Fact]
    public void NothingPinnedBefore_NoNotice()
    {
        var entries = new[] { E("old", V1, null), E("other", @"C:\Tools\other.exe", 1), E("new", V2, null) };
        Assert.False(TrayPin.PinLost(entries, V2));
    }

    [Fact]
    public void OnlyNewestEarlierVersionCounts()
    {
        // Pinned in 1.3.5, notice shown in 1.4.0 and the user left it unpinned:
        // updating to 1.4.1 stays quiet.
        var entries = new[] { E("v0", V0, 1), E("v1", V1, null), E("new", V2, null) };
        Assert.False(TrayPin.PinLost(entries, V2));
    }

    [Fact]
    public void NewerVersionEntry_IsIgnored()
    {
        // Downgrade: the pinned entry is the newer version, not an earlier one.
        var entries = new[] { E("newer", V2, 1), E("cur", V1, null) };
        Assert.False(TrayPin.PinLost(entries, V1));
    }

    [Fact]
    public void CurrentEntryNotCreatedYet_NoNotice()
    {
        Assert.False(TrayPin.PinLost(new[] { E("old", V1, 1) }, V2));
    }

    [Fact]
    public void UnpackagedBuild_NoNotice()
    {
        var entries = new[] { E("old", @"C:\Tools\SoundFlip-1.4.0\soundflip.exe", 1), E("new", @"C:\Tools\SoundFlip-1.4.1\soundflip.exe", null) };
        Assert.False(TrayPin.PinLost(entries, @"C:\Tools\SoundFlip-1.4.1\soundflip.exe"));
    }

    [Fact]
    public void OtherPackageName_IsIgnored()
    {
        const string Test = Dir + @"SoundFlip.Test_1.4.0.0_x64__abc\soundflip.exe";
        Assert.False(TrayPin.PinLost(new[] { E("test", Test, 1), E("new", V2, null) }, V2));
    }

    [Fact]
    public void PathMatch_IsCaseInsensitive()
    {
        var entries = new[] { E("old", V1, 1), E("new", V2.ToUpperInvariant(), null) };
        Assert.True(TrayPin.PinLost(entries, V2));
    }

    [Fact]
    public void PackageOf_ParsesMsixFolder()
    {
        Assert.Equal(("KirillFedorov.SoundFlip", new Version(1, 4, 1, 0)), TrayPin.PackageOf(V2));
        Assert.Null(TrayPin.PackageOf(@"C:\Tools\soundflip.exe"));
        Assert.Null(TrayPin.PackageOf(Dir + @"KirillFedorov.SoundFlip_1.4.1.0_x64__hash\other.exe"));
    }
}
