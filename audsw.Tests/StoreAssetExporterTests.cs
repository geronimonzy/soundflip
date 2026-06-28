namespace audsw.Tests;

public sealed class StoreAssetExporterTests
{
    [Fact]
    public void AssetPlan_HasExpectedFilesAndSizes()
    {
        var plan = StoreAssetExporter.AssetPlan;

        Assert.Equal(5, plan.Count);
        Assert.Equal(new StoreAssetSpec("Square44x44Logo.png", 44, 44, false), plan[0]);
        Assert.Equal(new StoreAssetSpec("StoreLogo.png", 50, 50, false), plan[1]);
        Assert.Equal(new StoreAssetSpec("Square150x150Logo.png", 150, 150, false), plan[2]);
        Assert.Equal(new StoreAssetSpec("Wide310x150Logo.png", 310, 150, true), plan[3]);
        Assert.Equal(new StoreAssetSpec("Square310x310Logo.png", 310, 310, true), plan[4]);
    }

    [Fact]
    public void Export_CreatesAllPlannedFiles()
    {
        using var temp = new TempDirectory();

        StoreAssetExporter.Export(temp.Path);

        foreach (var asset in StoreAssetExporter.AssetPlan)
        {
            string path = System.IO.Path.Combine(temp.Path, asset.FileName);
            Assert.True(File.Exists(path), $"Expected exported asset {asset.FileName} to exist.");
            Assert.True(new FileInfo(path).Length > 0, $"Expected exported asset {asset.FileName} to be non-empty.");
        }
    }
}
