using System.Text.Json.Serialization;

namespace AutoTint.Models;

/// <summary>
/// Everything AutoTint remembers between runs.
/// </summary>
internal sealed class AppSettings
{
    /// <summary>
    /// Window bounds in <em>physical</em> pixels. Deliberately not WPF's device-independent
    /// Left/Top/Width/Height: round-tripping those through a mixed-DPI monitor setup is a
    /// reliable way to have the window reopen at the wrong size.
    /// Null until the window has been placed once.
    /// </summary>
    public int? BoundsLeft { get; set; }

    public int? BoundsTop { get; set; }

    public int? BoundsWidth { get; set; }

    public int? BoundsHeight { get; set; }

    /// <summary>Tint strength as a percentage, matching the slider.</summary>
    public double Strength { get; set; } = 50;

    /// <summary>What the quick on/off button restores to.</summary>
    public double StrengthBeforeOff { get; set; } = 50;

    /// <summary>Id of the selected <see cref="TintPreset"/>.</summary>
    public string ColourId { get; set; } = "black";

    /// <summary>Whether the settings panel was left open.</summary>
    public bool Expanded { get; set; }

    /// <summary>On by default: the tint should dim your own view, not everyone else's.</summary>
    public bool HideFromCapture { get; set; } = true;

    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>Derived, so it has no business being written to the file.</summary>
    [JsonIgnore]
    public bool HasBounds =>
        BoundsLeft.HasValue && BoundsTop.HasValue &&
        BoundsWidth.HasValue && BoundsHeight.HasValue;
}
