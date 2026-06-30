namespace audsw.Tests;

// Pure ring-advance math. The device-touching paths need real Windows audio COM
// and are covered by the manual checklist in TESTING.md, not here.
public sealed class AudioTests
{
    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(1, 2, 0)]   // wraps
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]   // wraps
    [InlineData(-1, 3, 0)]  // current not in ring -> start at first
    public void NextIndex_AdvancesAndWraps(int current, int count, int expected)
    {
        Assert.Equal(expected, Audio.NextIndex(current, count));
    }

    [Fact]
    public void NextIndex_EmptyRing_IsZero()
    {
        Assert.Equal(0, Audio.NextIndex(-1, 0));
    }
}
