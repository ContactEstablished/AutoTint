using AutoTint.Services;

namespace AutoTint.Tests;

public class BoundsValidatorTests
{
    /// <summary>Stands in for a single 1920x1080 monitor at the origin.</summary>
    private static bool OnPrimaryMonitor(int x, int y) =>
        x >= 0 && x < 1920 && y >= 0 && y < 1080;

    [Fact]
    public void AnchorIsTheCentreOfTheBottomEdge()
    {
        (int x, int y) = BoundsValidator.TabAnchor(100, 200, 640, 400);

        Assert.Equal(420, x);
        Assert.Equal(599, y);
    }

    [Fact]
    public void BoundsFullyOnAMonitorAreRestorable()
    {
        Assert.True(BoundsValidator.IsRestorable(100, 100, 640, 400, OnPrimaryMonitor));
    }

    [Fact]
    public void BoundsOnAMonitorThatIsGoneAreNotRestorable()
    {
        // Saved while a second monitor sat to the right; that monitor is now unplugged.
        Assert.False(BoundsValidator.IsRestorable(2600, 300, 640, 400, OnPrimaryMonitor));
    }

    [Fact]
    public void BoundsWhoseTabHangsOffTheBottomAreNotRestorable()
    {
        // The tint would still be visible, but the tab -- every control -- would not be.
        Assert.False(BoundsValidator.IsRestorable(100, 900, 640, 400, OnPrimaryMonitor));
    }

    [Theory]
    [InlineData(10, 400)]
    [InlineData(640, 10)]
    public void DegenerateSizesAreRejected(int width, int height)
    {
        Assert.False(BoundsValidator.IsRestorable(100, 100, width, height, OnPrimaryMonitor));
    }
}
