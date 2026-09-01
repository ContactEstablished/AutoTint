using System;
using System.Windows.Media;

namespace AutoTint.Services;

/// <summary>
/// Works out how much tint the content underneath actually needs.
///
/// The opacity is derived rather than guessed. Alpha compositing gives
/// <c>result = source × (1 − a) + tint × a</c>, so solving for the <c>a</c> that brings a
/// measured brightness down to a comfort target is ordinary algebra -- which also means it
/// self-corrects for the tint colour, and can be tested exactly.
/// </summary>
internal static class AutoLevelMath
{
    /// <summary>Matches the slider's ceiling; the panel never goes fully opaque.</summary>
    internal const double MaxOpacityPercent = 90;

    /// <summary>Comfort target when the slider is at its gentlest.</summary>
    private const double LightestTarget = 235;

    /// <summary>Comfort target when the slider is at its most aggressive.</summary>
    private const double DarkestTarget = 90;

    /// <summary>
    /// Slider position (0–90) to the brightness you are willing to be left looking at.
    /// Left: only tame the genuinely blown-out. Right: keep everything fairly dark.
    /// </summary>
    internal static double TargetForSlider(double sliderPercent)
    {
        double t = Math.Clamp(sliderPercent, 0, MaxOpacityPercent) / MaxOpacityPercent;
        return LightestTarget - (t * (LightestTarget - DarkestTarget));
    }

    /// <summary>Perceived brightness of a colour, in the same gamma space as the measurement.</summary>
    internal static double LumaOf(Color colour) =>
        ((0.299 * colour.R) + (0.587 * colour.G) + (0.114 * colour.B));

    /// <summary>
    /// The opacity, as a percentage, that brings <paramref name="measuredLuma"/> down to
    /// <paramref name="targetLuma"/> using a tint of brightness <paramref name="tintLuma"/>.
    ///
    /// Returns zero when the content is already comfortable, and when the tint is not darker
    /// than the content -- a pale tint laid over something darker cannot dim it, and piling
    /// on opacity would only wash it out.
    /// </summary>
    internal static double OpacityFor(double measuredLuma, double tintLuma, double targetLuma)
    {
        if (measuredLuma <= targetLuma) return 0;
        if (measuredLuma <= tintLuma) return 0;

        double alpha = (measuredLuma - targetLuma) / (measuredLuma - tintLuma);
        return Math.Clamp(alpha * 100.0, 0, MaxOpacityPercent);
    }

    /// <summary>
    /// Exponential smoothing. Applied to the measured brightness rather than to the opacity,
    /// so what is being averaged is a physical reading rather than a decision.
    /// </summary>
    internal static double Smooth(double previous, double latest, double weight) =>
        previous + (Math.Clamp(weight, 0, 1) * (latest - previous));

    /// <summary>
    /// Whether a new opacity is different enough to be worth applying. Without a dead-band
    /// the tint would creep constantly by fractions of a percent, which reads as the panel
    /// quietly breathing.
    /// </summary>
    internal static bool ShouldApply(double current, double candidate, double deadBand) =>
        Math.Abs(candidate - current) >= deadBand;
}
