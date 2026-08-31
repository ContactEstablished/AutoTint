using System;

namespace AutoTint.Services;

/// <summary>
/// The rules for what counts as a window worth snapping to. Pure, so the judgement can be
/// tested without conjuring up real windows.
/// </summary>
internal static class SnapTargetPolicy
{
    /// <summary>
    /// Below this, a window is a tooltip, a badge or some other transient scrap rather than
    /// something a person wants dimmed.
    /// </summary>
    internal const int MinTargetWidth = 120;

    internal const int MinTargetHeight = 100;

    /// <summary>
    /// Shell surfaces that technically sit under the cursor but are never the intended
    /// target. Snapping to the desktop would balloon the panel to the whole screen.
    /// </summary>
    private static readonly string[] ExcludedClasses =
    {
        "Progman",                  // the desktop itself
        "WorkerW",                  // wallpaper host behind the desktop icons
        "Shell_TrayWnd",            // taskbar
        "Shell_SecondaryTrayWnd",   // taskbar on additional monitors
        "Shell_ChargeBar",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "ForegroundStaging",
        "XamlExplorerHostIslandWindow",
    };

    internal static bool IsEligibleClass(string? className)
    {
        if (string.IsNullOrWhiteSpace(className)) return false;

        foreach (string excluded in ExcludedClasses)
        {
            if (string.Equals(className, excluded, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool IsUsableSize(int width, int height) =>
        width >= MinTargetWidth && height >= MinTargetHeight;
}
