using System;
using AutoTint.Interop;

namespace AutoTint.Tests;

public class PointFromLParamTests
{
    /// <summary>Packs coordinates the way Windows does for WM_NCHITTEST.</summary>
    private static IntPtr Pack(int x, int y) =>
        new(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    [Fact]
    public void UnpacksOrdinaryCoordinates()
    {
        NativeMethods.POINT p = NativeMethods.PointFromLParam(Pack(1280, 720));

        Assert.Equal(1280, p.X);
        Assert.Equal(720, p.Y);
    }

    [Theory]
    [InlineData(-1200, 400)]
    [InlineData(300, -150)]
    [InlineData(-1920, -1080)]
    public void UnpacksNegativeCoordinates(int x, int y)
    {
        // Monitors positioned left of or above the primary produce negative screen
        // coordinates. Masking without sign-extending turns those into huge positives,
        // which would put every hit-test region in the wrong place on such a setup.
        NativeMethods.POINT p = NativeMethods.PointFromLParam(Pack(x, y));

        Assert.Equal(x, p.X);
        Assert.Equal(y, p.Y);
    }

    [Fact]
    public void HandlesTheSignBoundary()
    {
        NativeMethods.POINT positive = NativeMethods.PointFromLParam(Pack(32767, 32767));
        Assert.Equal(32767, positive.X);
        Assert.Equal(32767, positive.Y);

        NativeMethods.POINT negative = NativeMethods.PointFromLParam(Pack(-32768, -32768));
        Assert.Equal(-32768, negative.X);
        Assert.Equal(-32768, negative.Y);
    }
}
