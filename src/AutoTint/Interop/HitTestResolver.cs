using System;
using System.Windows;

namespace AutoTint.Interop;

/// <summary>
/// Maps a point inside the overlay window to the hit-test code Windows should act on.
///
/// Deliberately free of window state and Win32 types so the trickiest geometry in the
/// app can be unit tested without a UI thread. All rectangles and the point are in
/// device-independent units, relative to the top-left of the window.
/// </summary>
internal static class HitTestResolver
{
    /// <summary>Thickness of the grab band along each edge of the tint.</summary>
    internal const double DefaultBorderThickness = 6.0;

    /// <summary>
    /// Corners extend further along each axis than the edge bands are thick, otherwise the
    /// diagonal resize targets are a 6x6 pixel square and effectively unhittable.
    /// </summary>
    private const double CornerReach = 2.5;

    internal static HitTestCode Resolve(
        Rect tint,
        Rect tab,
        Rect grip,
        Point p,
        double border = DefaultBorderThickness)
    {
        // Resize bands sit just *inside* the tint's edges rather than in the invisible
        // margin around it. A band you can see is a band you can aim at, and it keeps the
        // window's outer bounds irrelevant to the geometry.
        if (!tint.IsEmpty && tint.Contains(p))
        {
            // On a small panel the bands would otherwise meet in the middle and leave
            // nothing click-through at all.
            double b = Math.Min(border, Math.Min(tint.Width, tint.Height) / 4.0);
            double corner = b * CornerReach;

            bool left = p.X < tint.Left + b;
            bool right = p.X >= tint.Right - b;
            bool top = p.Y < tint.Top + b;
            bool bottom = p.Y >= tint.Bottom - b;

            bool nearLeft = p.X < tint.Left + corner;
            bool nearRight = p.X >= tint.Right - corner;
            bool nearTop = p.Y < tint.Top + corner;
            bool nearBottom = p.Y >= tint.Bottom - corner;

            if ((top && nearLeft) || (left && nearTop)) return HitTestCode.TopLeft;
            if ((top && nearRight) || (right && nearTop)) return HitTestCode.TopRight;
            if ((bottom && nearLeft) || (left && nearBottom)) return HitTestCode.BottomLeft;
            if ((bottom && nearRight) || (right && nearBottom)) return HitTestCode.BottomRight;
            if (left) return HitTestCode.Left;
            if (right) return HitTestCode.Right;
            if (top) return HitTestCode.Top;
            if (bottom) return HitTestCode.Bottom;
        }

        // The grip is checked before the tab as a whole, since it sits inside it.
        if (!grip.IsEmpty && grip.Contains(p)) return HitTestCode.Caption;
        if (!tab.IsEmpty && tab.Contains(p)) return HitTestCode.Client;

        // The tinted interior, and the transparent margin either side of the tab.
        return HitTestCode.Transparent;
    }
}
