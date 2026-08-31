using AutoTint.Services;

namespace AutoTint.Tests;

public class SnapGeometryTests
{
    private const int TabHeight = 60;
    private const int MinWidth = 280;
    private const int MinHeight = 120;

    private static SnapBounds For(int left, int top, int width, int height) =>
        SnapGeometry.ForTarget(left, top, width, height, TabHeight, MinWidth, MinHeight);

    [Fact]
    public void TintCoversTheTargetAndTheTabHangsBelowIt()
    {
        SnapBounds b = For(400, 300, 900, 600);

        Assert.Equal(400, b.Left);
        Assert.Equal(300, b.Top);
        Assert.Equal(900, b.Width);

        // Window is the target plus the tab, so the tint alone matches the target exactly.
        Assert.Equal(660, b.Height);
        Assert.Equal(600, b.Height - TabHeight);
    }

    [Fact]
    public void TopEdgeAlwaysMatchesTheTarget()
    {
        // Surplus height goes downward; the top is the edge that has to line up.
        Assert.Equal(300, For(400, 300, 900, 600).Top);
        Assert.Equal(0, For(0, 0, 400, 50).Top);
    }

    [Fact]
    public void NarrowTargetsLeaveThePanelCentredOnThem()
    {
        // A 200px target cannot be matched by a panel with a 280px minimum, so the 80px
        // surplus is split evenly rather than hanging off one side.
        SnapBounds b = For(1000, 200, 200, 400);

        Assert.Equal(MinWidth, b.Width);
        Assert.Equal(960, b.Left);

        int targetCentre = 1000 + (200 / 2);
        int panelCentre = b.Left + (b.Width / 2);
        Assert.Equal(targetCentre, panelCentre);
    }

    [Fact]
    public void ShortTargetsAreRaisedToTheMinimumHeight()
    {
        SnapBounds b = For(0, 0, 900, 40);

        Assert.Equal(MinHeight, b.Height);
    }

    [Fact]
    public void NegativeCoordinatesArePreserved()
    {
        // A monitor to the left of the primary produces negative coordinates, and a target
        // there must not be dragged back onto the primary.
        SnapBounds b = For(-1600, -200, 800, 500);

        Assert.Equal(-1600, b.Left);
        Assert.Equal(-200, b.Top);
    }
}

public class SnapTargetPolicyTests
{
    [Theory]
    [InlineData("Progman")]          // the desktop
    [InlineData("WorkerW")]          // wallpaper host
    [InlineData("Shell_TrayWnd")]    // taskbar
    [InlineData("shell_traywnd")]    // matching is case-insensitive
    public void ShellSurfacesAreNotTargets(string className)
    {
        // Snapping to the desktop would balloon the panel to the whole screen.
        Assert.False(SnapTargetPolicy.IsEligibleClass(className));
    }

    [Theory]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("Windows.UI.Core.CoreWindow")]
    [InlineData("ApplicationFrameWindow")]
    public void OrdinaryApplicationWindowsAreTargets(string className)
    {
        Assert.True(SnapTargetPolicy.IsEligibleClass(className));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingClassNamesAreRejected(string? className)
    {
        Assert.False(SnapTargetPolicy.IsEligibleClass(className));
    }

    [Fact]
    public void TinyWindowsAreRejected()
    {
        // Tooltips and badges sit under the cursor but are never what someone means.
        Assert.False(SnapTargetPolicy.IsUsableSize(80, 400));
        Assert.False(SnapTargetPolicy.IsUsableSize(400, 40));
        Assert.True(SnapTargetPolicy.IsUsableSize(400, 300));
    }
}
