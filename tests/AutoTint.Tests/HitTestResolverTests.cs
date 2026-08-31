using System.Windows;
using AutoTint.Interop;

namespace AutoTint.Tests;

/// <summary>
/// The resolver decides, for every pixel of the overlay, whether Windows should pass the
/// click to the app underneath, drag the window, resize it, or treat it as a normal
/// control. Getting a region wrong is the difference between a usable overlay and one
/// that either blocks the meeting window or cannot be grabbed at all.
/// </summary>
public class HitTestResolverTests
{
    // A 640x400 tint with a 140x50 tab centred beneath it, and a 40x14 grip on top of
    // the tab -- the real layout, at the default window size.
    private static readonly Rect Tint = new(0, 0, 640, 350);
    private static readonly Rect Tab = new(250, 350, 140, 50);
    private static readonly Rect Grip = new(300, 350, 40, 14);

    private static HitTestCode Resolve(double x, double y) =>
        HitTestResolver.Resolve(Tint, Tab, Grip, new Point(x, y));

    [Fact]
    public void TintInteriorPassesInputThrough()
    {
        Assert.Equal(HitTestCode.Transparent, Resolve(320, 175));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(200, 0)]
    [InlineData(639, 349)]
    public void EdgesAreNeverTransparent(double x, double y)
    {
        Assert.NotEqual(HitTestCode.Transparent, Resolve(x, y));
    }

    // Expressed as the raw Win32 values a public signature can carry: HTLEFT..HTBOTTOMRIGHT.
    [Theory]
    [InlineData(1, 175, 10)]
    [InlineData(638, 175, 11)]
    [InlineData(320, 1, 12)]
    [InlineData(320, 348, 15)]
    [InlineData(1, 1, 13)]
    [InlineData(638, 1, 14)]
    [InlineData(1, 348, 16)]
    [InlineData(638, 348, 17)]
    public void EachEdgeAndCornerResolvesToItsResizeCode(double x, double y, int expected)
    {
        Assert.Equal(expected, (int)Resolve(x, y));
    }

    [Fact]
    public void CornersReachFurtherAlongTheEdgeThanTheBandIsThick()
    {
        // 10px in from the corner along the top edge is still a corner grab, otherwise the
        // diagonal target is a 6x6 square and effectively unhittable.
        Assert.Equal(HitTestCode.TopLeft, Resolve(10, 2));
        Assert.Equal(HitTestCode.TopLeft, Resolve(2, 10));

        // Far enough along and it becomes a plain edge again.
        Assert.Equal(HitTestCode.Top, Resolve(100, 2));
    }

    [Fact]
    public void GripDragsTheWindow()
    {
        Assert.Equal(HitTestCode.Caption, Resolve(320, 356));
    }

    [Fact]
    public void TabOutsideTheGripTakesNormalInput()
    {
        Assert.Equal(HitTestCode.Client, Resolve(320, 380));
        Assert.Equal(HitTestCode.Client, Resolve(260, 356));
    }

    [Fact]
    public void MarginBesideTheTabPassesInputThrough()
    {
        // The tab is centred, so the window corners level with it are empty space and must
        // not steal clicks from whatever is behind them.
        Assert.Equal(HitTestCode.Transparent, Resolve(50, 380));
        Assert.Equal(HitTestCode.Transparent, Resolve(600, 380));
    }

    [Fact]
    public void OnASmallPanelTheBandsShrinkRatherThanMeetingInTheMiddle()
    {
        // A 40x40 tint with a 6px band on each side would leave nothing click-through.
        var tiny = new Rect(0, 0, 40, 40);
        HitTestCode centre = HitTestResolver.Resolve(tiny, Rect.Empty, Rect.Empty, new Point(20, 20));

        Assert.Equal(HitTestCode.Transparent, centre);
    }

    [Fact]
    public void EmptyRectanglesBeforeLayoutAreHarmless()
    {
        HitTestCode code = HitTestResolver.Resolve(
            Rect.Empty, Rect.Empty, Rect.Empty, new Point(100, 100));

        Assert.Equal(HitTestCode.Transparent, code);
    }
}
