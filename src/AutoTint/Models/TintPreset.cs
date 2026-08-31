using System.Windows.Media;

namespace AutoTint.Models;

/// <summary>
/// A tint colour the panel can be filled with. Strength stays on the slider; this is
/// only the hue the dimming is made of.
/// </summary>
internal readonly record struct TintPreset(string Id, Color Colour)
{
    internal static readonly TintPreset Black = new("black", Color.FromRgb(0x00, 0x00, 0x00));
    internal static readonly TintPreset Warm = new("warm", Color.FromRgb(0x3B, 0x24, 0x00));
    internal static readonly TintPreset Grey = new("grey", Color.FromRgb(0x5A, 0x60, 0x68));

    internal static TintPreset FromId(string? id) => id switch
    {
        "warm" => Warm,
        "grey" => Grey,
        _ => Black,
    };
}
