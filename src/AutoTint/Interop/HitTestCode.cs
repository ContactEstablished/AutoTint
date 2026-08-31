namespace AutoTint.Interop;

/// <summary>
/// Return values for WM_NCHITTEST. Windows sends that message continuously to ask
/// "what is the cursor over?", and the answer decides what the OS does next --
/// which is how AutoTint gets dragging, resizing and click-through without
/// implementing any of them by hand.
/// </summary>
internal enum HitTestCode
{
    /// <summary>Pass the input to whatever window is underneath. The tint is visual only.</summary>
    Transparent = -1,

    /// <summary>Ordinary input: buttons, the slider, the swatches.</summary>
    Client = 1,

    /// <summary>Treated as the title bar, so Windows drags the window natively.</summary>
    Caption = 2,

    Left = 10,
    Right = 11,
    Top = 12,
    TopLeft = 13,
    TopRight = 14,
    Bottom = 15,
    BottomLeft = 16,
    BottomRight = 17,
}
