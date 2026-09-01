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

    /// <summary>Whether the panel lines itself up with the window beneath it.</summary>
    public bool AutoSnap { get; set; }

    /// <summary>Whether the tint strength follows the brightness of what it covers.</summary>
    public bool AutoLevel { get; set; }

    /// <summary>
    /// Slider position while auto-adjust is on, where it means "how bright will I tolerate
    /// the result being" rather than an opacity. Kept apart from <see cref="Strength"/> so
    /// switching modes does not overwrite the manual setting with a target, or vice versa.
    /// </summary>
    public double AutoTarget { get; set; } = 50;

    /// <summary>Derived, so it has no business being written to the file.</summary>
    [JsonIgnore]
    public bool HasBounds =>
        BoundsLeft.HasValue && BoundsTop.HasValue &&
        BoundsWidth.HasValue && BoundsHeight.HasValue;
}
