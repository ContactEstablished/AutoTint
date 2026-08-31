using System;

namespace AutoTint.Interop;

/// <summary>
/// Alt+Shift+T from anywhere, so the tint can be dropped without hunting for the tab --
/// which matters when the thing you want to see is behind the panel.
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    /// <summary>Any value unique within this window.</summary>
    private const int HotkeyId = 0xA71;

    private readonly IntPtr _hwnd;
    private bool _registered;

    internal GlobalHotkey(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    internal string Description => "Alt+Shift+T";

    /// <summary>
    /// Returns false when another application already owns the combination. That is a
    /// normal outcome, not an error worth stopping startup over.
    /// </summary>
    internal bool TryRegister()
    {
        if (_registered) return true;

        _registered = NativeMethods.RegisterHotKey(
            _hwnd,
            HotkeyId,
            NativeMethods.MOD_ALT | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_T);

        return _registered;
    }

    /// <summary>True when the message was this hotkey firing.</summary>
    internal bool Matches(int msg, IntPtr wParam) =>
        msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId;

    public void Dispose()
    {
        if (!_registered) return;

        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }
}
