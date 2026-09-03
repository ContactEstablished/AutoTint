using System;

namespace AutoTint.Services;

/// <summary>Window bounds in physical pixels.</summary>
internal readonly record struct SnapBounds(int Left, int Top, int Width, int Height);

/// <summary>
/// Turns a target window's rectangle into bounds for the overlay. Pure, and separated from
/// the snapping machinery so the arithmetic can be tested directly.
/// </summary>
internal static class SnapGeometry
{
    /// <summary>
    /// The <em>tint</em> is what must line up with the target, not the whole window -- the
    /// tab hangs below the tint, so the window is that much taller and the tab ends up just
    /// under the target's bottom edge.
    ///
    /// When the target is smaller than the overlay's own minimum, the surplus is spread
    /// evenly either side so the panel stays centred on what it is covering rather than
    /// hanging off to the right.
    /// </summary>
    internal static SnapBounds ForTarget(
        int targetLeft,
        int targetTop,
        int targetWidth,
        int targetHeight,
        int tabHeight,
        int minWidth,
        int minHeight)
    {
        int width = Math.Max(targetWidth, minWidth);
        int left = targetLeft - ((width - targetWidth) / 2);

        int desiredHeight = targetHeight + tabHeight;
        int height = Math.Max(desiredHeight, minHeight);

        // Extra height goes downward: the top edge is the one that has to match.
        return new SnapBounds(left, targetTop, width, height);
    }

    /// <summary>
    /// Bounds that spread the panel over one whole monitor. The window takes the work area
    /// exactly, which leaves the tab in the strip along its bottom and the tint over
    /// everything above.
    ///
    /// Filling the <em>work area</em> rather than the monitor is deliberate twice over: the
    /// taskbar is not a glare source worth dimming, and covering the display outright would
    /// push the tab off the bottom edge, leaving the tray icon as the only way back to the
    /// controls.
    /// </summary>
    internal static SnapBounds ForMonitor(
        int workLeft,
        int workTop,
        int workWidth,
        int workHeight,
        int minWidth,
        int minHeight) =>
        new(workLeft, workTop, Math.Max(workWidth, minWidth), Math.Max(workHeight, minHeight));
}
