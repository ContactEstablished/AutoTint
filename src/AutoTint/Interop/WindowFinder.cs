using System;
using System.Runtime.InteropServices;
using AutoTint.Services;

namespace AutoTint.Interop;

/// <summary>
/// Finds the window sitting under a point, and measures it the way it actually looks
/// on screen.
/// </summary>
internal static class WindowFinder
{
    /// <summary>
    /// Returns the top-level window under the given screen point, or
    /// <see cref="IntPtr.Zero"/> if there is nothing worth snapping to.
    ///
    /// The overlay is made click-through for the duration of the probe. Without that, a
    /// hit test taken while the cursor is over the tab -- which is exactly when the snap
    /// button gets pressed -- would find the overlay itself and nothing else.
    /// </summary>
    internal static IntPtr FindTargetUnder(IntPtr self, int screenX, int screenY)
    {
        bool alreadyClickThrough =
            NativeMethods.HasExtendedStyle(self, NativeMethods.WS_EX_TRANSPARENT);

        if (!alreadyClickThrough)
        {
            NativeMethods.AddExtendedStyle(self, NativeMethods.WS_EX_TRANSPARENT);
        }

        try
        {
            IntPtr hit = NativeMethods.WindowFromPoint(
                new NativeMethods.POINT { X = screenX, Y = screenY });

            if (hit == IntPtr.Zero) return IntPtr.Zero;

            IntPtr root = NativeMethods.GetAncestor(hit, NativeMethods.GA_ROOT);
            if (root == IntPtr.Zero) root = hit;

            return IsEligible(root) ? root : IntPtr.Zero;
        }
        finally
        {
            if (!alreadyClickThrough)
            {
                NativeMethods.RemoveExtendedStyle(self, NativeMethods.WS_EX_TRANSPARENT);
            }
        }
    }

    /// <summary>True while the window is still a live, visible, un-minimised target.</summary>
    internal static bool IsStillUsable(IntPtr hwnd) =>
        hwnd != IntPtr.Zero
        && NativeMethods.IsWindow(hwnd)
        && NativeMethods.IsWindowVisible(hwnd)
        && !NativeMethods.IsIconic(hwnd)
        && !IsCloaked(hwnd);

    /// <summary>
    /// The window's bounds as drawn. GetWindowRect includes an invisible resize border --
    /// around 7px per side on Windows 10 and 11 -- so snapping to it would leave the tint
    /// visibly overhanging the window on three sides. DWM knows the real figure.
    /// </summary>
    internal static bool TryGetFrameBounds(IntPtr hwnd, out NativeMethods.RECT bounds)
    {
        int hr = NativeMethods.DwmGetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out bounds,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr == 0 && bounds.Width > 0 && bounds.Height > 0) return true;

        // Pre-DWM or a window that declines to answer; the padded rectangle beats nothing.
        return NativeMethods.GetWindowRect(hwnd, out bounds);
    }

    private static bool IsEligible(IntPtr hwnd)
    {
        if (!IsStillUsable(hwnd)) return false;

        // Anything belonging to AutoTint -- the overlay, its menus, its tooltips -- is never
        // a target. Comparing by process catches all of them, not just the main window.
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == (uint)Environment.ProcessId) return false;

        if (!SnapTargetPolicy.IsEligibleClass(NativeMethods.GetClassNameOf(hwnd))) return false;

        if (!TryGetFrameBounds(hwnd, out NativeMethods.RECT bounds)) return false;

        return SnapTargetPolicy.IsUsableSize(bounds.Width, bounds.Height);
    }

    private static bool IsCloaked(IntPtr hwnd)
    {
        int hr = NativeMethods.DwmGetWindowAttributeInt(
            hwnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int));

        return hr == 0 && cloaked != 0;
    }
}
