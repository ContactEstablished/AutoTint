using System;

namespace AutoTint.Services;

/// <summary>
/// Decides whether saved window bounds are still usable. A monitor that was present when
/// the settings were written may be gone by the next launch -- a docked laptop unplugged,
/// a projector disconnected -- and restoring those bounds blindly puts the overlay
/// somewhere the user cannot see or reach it.
/// </summary>
internal static class BoundsValidator
{
    /// <summary>Below this the panel is too small to be worth restoring.</summary>
    internal const int MinUsableWidth = 200;

    internal const int MinUsableHeight = 100;

    /// <summary>
    /// The point that decides reachability: the centre of the tab along the bottom edge.
    /// The tint itself is inert, so what matters is whether the controls can be reached.
    /// </summary>
    internal static (int X, int Y) TabAnchor(int left, int top, int width, int height) =>
        (left + (width / 2), top + height - 1);

    /// <summary>
    /// <paramref name="isPointOnAMonitor"/> is injected so this stays a pure decision that
    /// can be tested without a display configuration.
    /// </summary>
    internal static bool IsRestorable(
        int left, int top, int width, int height, Func<int, int, bool> isPointOnAMonitor)
    {
        if (width < MinUsableWidth || height < MinUsableHeight) return false;

        (int x, int y) = TabAnchor(left, top, width, height);
        return isPointOnAMonitor(x, y);
    }
}
