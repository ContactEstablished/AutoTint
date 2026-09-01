using System;
using AutoTint.Models;
using AutoTint.Services;

namespace AutoTint.Tests;

public class AutoLevelMathTests
{
    private static readonly double BlackLuma = AutoLevelMath.LumaOf(TintPreset.Black.Colour);
    private static readonly double WarmLuma = AutoLevelMath.LumaOf(TintPreset.Warm.Colour);
    private static readonly double GreyLuma = AutoLevelMath.LumaOf(TintPreset.Grey.Colour);

    /// <summary>What the screen will actually show once the tint is composited over it.</summary>
    private static double Composite(double source, double tint, double opacityPercent)
    {
        double a = opacityPercent / 100.0;
        return (source * (1 - a)) + (tint * a);
    }

    [Theory]
    [InlineData(250, 180, 28.0)]
    [InlineData(200, 180, 10.0)]
    [InlineData(220, 110, 50.0)]
    public void BlackTintOpacityFollowsTheCompositingSolve(
        double measured, double target, double expectedPercent)
    {
        double opacity = AutoLevelMath.OpacityFor(measured, BlackLuma, target);

        Assert.Equal(expectedPercent, opacity, precision: 1);
    }

    [Theory]
    [InlineData(250, 180)]
    [InlineData(200, 150)]
    [InlineData(255, 90)]
    public void TheChosenOpacityActuallyLandsOnTheTarget(double measured, double target)
    {
        // The point of deriving rather than guessing: compositing at the chosen opacity
        // should produce the requested brightness.
        foreach (double tint in new[] { BlackLuma, WarmLuma, GreyLuma })
        {
            double opacity = AutoLevelMath.OpacityFor(measured, tint, target);
            if (opacity <= 0 || opacity >= AutoLevelMath.MaxOpacityPercent) continue;

            Assert.Equal(target, Composite(measured, tint, opacity), precision: 6);
        }
    }

    [Fact]
    public void APaleTintNeedsMoreOpacityThanBlackForTheSameResult()
    {
        double black = AutoLevelMath.OpacityFor(250, BlackLuma, 180);
        double warm = AutoLevelMath.OpacityFor(250, WarmLuma, 180);

        // Warm amber is not as dark as black, so more of it is required.
        Assert.True(warm > black);
    }

    [Fact]
    public void ContentAlreadyComfortableGetsNoTint()
    {
        Assert.Equal(0, AutoLevelMath.OpacityFor(170, BlackLuma, 180));
        Assert.Equal(0, AutoLevelMath.OpacityFor(180, BlackLuma, 180));
    }

    [Fact]
    public void ATintNoDarkerThanTheContentIsRefused()
    {
        // Soft grey over something darker than the grey cannot dim it; piling on opacity
        // would only wash the content out.
        Assert.Equal(0, AutoLevelMath.OpacityFor(GreyLuma - 5, GreyLuma, 40));
    }

    [Fact]
    public void OpacityNeverExceedsTheSliderCeiling()
    {
        double opacity = AutoLevelMath.OpacityFor(255, BlackLuma, 1);

        Assert.Equal(AutoLevelMath.MaxOpacityPercent, opacity);
    }

    [Fact]
    public void SliderSpansGentleToAggressiveTargets()
    {
        Assert.Equal(235, AutoLevelMath.TargetForSlider(0), precision: 6);
        Assert.Equal(90, AutoLevelMath.TargetForSlider(90), precision: 6);
        Assert.Equal(162.5, AutoLevelMath.TargetForSlider(45), precision: 6);
    }

    [Fact]
    public void HigherSliderMeansMoreTintForTheSameContent()
    {
        double gentle = AutoLevelMath.OpacityFor(250, BlackLuma, AutoLevelMath.TargetForSlider(20));
        double firm = AutoLevelMath.OpacityFor(250, BlackLuma, AutoLevelMath.TargetForSlider(70));

        Assert.True(firm > gentle);
    }

    [Fact]
    public void SmoothingConvergesTowardTheNewReading()
    {
        double value = 0;
        for (int i = 0; i < 6; i++) value = AutoLevelMath.Smooth(value, 100, 0.4);

        // Around 95% of the way there after six samples, and never overshooting.
        Assert.InRange(value, 90, 100);
    }

    [Fact]
    public void DeadBandSuppressesTinyChanges()
    {
        Assert.False(AutoLevelMath.ShouldApply(30, 31, 1.5));
        Assert.True(AutoLevelMath.ShouldApply(30, 32, 1.5));
    }
}

public class LuminanceStatsTests
{
    /// <summary>Grey pixels, where the luma weights sum to exactly the input value.</summary>
    private static byte[] Buffer(params (int Luma, int Count)[] groups)
    {
        int total = 0;
        foreach ((_, int count) in groups) total += count;

        var bytes = new byte[total * 4];
        int i = 0;
        foreach ((int luma, int count) in groups)
        {
            for (int n = 0; n < count; n++)
            {
                bytes[i] = bytes[i + 1] = bytes[i + 2] = (byte)luma;
                bytes[i + 3] = 255;
                i += 4;
            }
        }

        return bytes;
    }

    [Fact]
    public void FlatContentReadsAsItsOwnBrightness()
    {
        LuminanceStats stats = LuminanceStats.From(Buffer((220, 1000)));

        Assert.Equal(220, stats.Mean, precision: 6);
        Assert.Equal(220, stats.Percentile(0.90));
    }

    [Fact]
    public void PercentileSeesGlareThatAnAverageWouldHide()
    {
        // Half black, half blazing. This is the whole reason the metric is a percentile:
        // the mean calls it a comfortable mid-grey while half the screen is searing.
        LuminanceStats stats = LuminanceStats.From(Buffer((0, 500), (250, 500)));

        Assert.Equal(125, stats.Mean, precision: 6);
        Assert.Equal(250, stats.Percentile(0.90));
    }

    [Fact]
    public void ASmallBrightPatchOnADarkWindowStillRegisters()
    {
        // 15% of the area blown out -- exactly the "dark app, one white document" case.
        LuminanceStats stats = LuminanceStats.From(Buffer((30, 850), (250, 150)));

        Assert.True(stats.Mean < 70);
        Assert.Equal(250, stats.Percentile(0.90));
    }

    [Fact]
    public void AFewStrayBrightPixelsDoNotDriveTheReading()
    {
        // A white cursor or a highlight should not make the panel slam shut.
        LuminanceStats stats = LuminanceStats.From(Buffer((40, 990), (255, 10)));

        Assert.Equal(40, stats.Percentile(0.90));
    }

    [Fact]
    public void FractionAboveCountsTheBrightShare()
    {
        LuminanceStats stats = LuminanceStats.From(Buffer((10, 750), (240, 250)));

        Assert.Equal(0.25, stats.FractionAbove(200), precision: 6);
    }

    [Fact]
    public void AnEmptyBufferIsHarmless()
    {
        LuminanceStats stats = LuminanceStats.From(Array.Empty<byte>());

        Assert.True(stats.IsEmpty);
        Assert.Equal(0, stats.Percentile(0.90));
        Assert.Equal(0, stats.FractionAbove(128));
    }
}
