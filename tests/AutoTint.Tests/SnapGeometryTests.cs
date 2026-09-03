using AutoTint.Services;

namespace AutoTint.Tests;

public class SnapGeometryTests
{
    private const int TabHeight = 60;
    private const int MinWidth = 280;
    private const int MinHeight = 120;

    private static SnapBounds For(int left, int top, int width, int height) =>
        SnapGeometry.ForTarget(left, top, width, height, TabHeight, MinWidth, MinHeight);

    private static SnapBounds ForMonitor(int left, int top, int width, int height) =>
        SnapGeometry.ForMonitor(left, top, width, height, MinWidth, MinHeight);

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

    [Fact]
    public void FillingAMonitorTakesTheWholeWorkArea()
    {
        // 1920x1080 with a 48px taskbar along the bottom.
        SnapBounds b = ForMonitor(0, 0, 1920, 1032);

        Assert.Equal(0, b.Left);
        Assert.Equal(0, b.Top);
        Assert.Equal(1920, b.Width);
        Assert.Equal(1032, b.Height);
    }

    [Fact]
    public void FillingLeavesTheTabInsideTheWorkArea()
    {
        // The tab hangs off the bottom of the window, so the window has to stop where the
        // work area does. Fill the display itself and the controls land under the taskbar
        // or off the screen, with the tray icon as the only way back to them.
        SnapBounds b = ForMonitor(0, 0, 1920, 1032);

        Assert.Equal(1032, b.Top + b.Height);

        (_, int tabY) = BoundsValidator.TabAnchor(b.Left, b.Top, b.Width, b.Height);
        Assert.InRange(tabY, 0, 1031);
    }

    [Fact]
    public void FillingFollowsATaskbarDockedToTheSide()
    {
        // The work area is inset on whichever edge the taskbar occupies, so a fill starts
        // there rather than at the monitor's own origin.
        SnapBounds b = ForMonitor(72, 0, 1848, 1080);

        Assert.Equal(72, b.Left);
        Assert.Equal(1848, b.Width);
    }

    [Fact]
    public void FillingASecondaryMonitorStaysOnIt()
    {
        // A display above and to the left of the primary; the fill must not be pulled back.
        SnapBounds b = ForMonitor(-1920, -120, 1920, 1032);

        Assert.Equal(-1920, b.Left);
        Assert.Equal(-120, b.Top);
        Assert.Equal(1032, b.Height);
    }

    [Fact]
    public void ImplausiblySmallWorkAreasStillLeaveAUsablePanel()
    {
        SnapBounds b = ForMonitor(0, 0, 200, 90);

        Assert.Equal(MinWidth, b.Width);
        Assert.Equal(MinHeight, b.Height);
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
