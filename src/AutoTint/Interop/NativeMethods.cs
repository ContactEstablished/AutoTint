using System;
using System.Runtime.InteropServices;

namespace AutoTint.Interop;

/// <summary>
/// The Win32 surface AutoTint needs. Kept in one place so the window code stays readable.
/// </summary>
internal static partial class NativeMethods
{
    // ---- Messages -------------------------------------------------------------------

    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_SETCURSOR = 0x0020;
    internal const int WM_ENTERSIZEMOVE = 0x0231;
    internal const int WM_EXITSIZEMOVE = 0x0232;
    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_DPICHANGED = 0x02E0;

    // ---- Window styles --------------------------------------------------------------

    internal const int GWL_EXSTYLE = -20;

    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;

    // ---- Display affinity (hide from screen capture) ---------------------------------

    internal const uint WDA_NONE = 0x00000000;

    /// <summary>
    /// Window stays visible on the monitor but is omitted from screen captures and
    /// shared screens. Requires Windows 10 version 2004 or later.
    /// </summary>
    internal const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // ---- Hotkey modifiers -------------------------------------------------------------

    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_WIN = 0x0008;
    internal const uint MOD_NOREPEAT = 0x4000;

    internal const int VK_LBUTTON = 0x01;
    internal const uint VK_T = 0x54;

    /// <summary>The four-way move cursor.</summary>
    internal const int IDC_SIZEALL = 32646;

    /// <summary>MonitorFromPoint returns NULL when the point is on no monitor at all.</summary>
    internal const uint MONITOR_DEFAULTTONULL = 0x00000000;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
    internal static partial IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr SetCursor(IntPtr hCursor);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>True when the given screen pixel falls on some connected monitor.</summary>
    internal static bool IsPointOnAMonitor(int x, int y) =>
        MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONULL) != IntPtr.Zero;

    /// <summary>True while the physical left mouse button is held down.</summary>
    internal static bool IsLeftButtonDown() => (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Adds extended window style bits, returning the previous style.
    /// </summary>
    internal static void AddExtendedStyle(IntPtr hWnd, int bits)
    {
        long current = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(current | (long)(uint)bits));
    }

    internal static void RemoveExtendedStyle(IntPtr hWnd, int bits)
    {
        long current = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(current & ~(long)(uint)bits));
    }

    internal static bool HasExtendedStyle(IntPtr hWnd, int bits)
    {
        long current = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        return (current & bits) == bits;
    }

    /// <summary>
    /// WM_NCHITTEST packs screen coordinates into lParam as two <em>signed</em> 16-bit
    /// values. Naive masking breaks on any monitor positioned left of or above the
    /// primary, where the coordinates go negative.
    /// </summary>
    internal static POINT PointFromLParam(IntPtr lParam)
    {
        int raw = (int)(lParam.ToInt64() & 0xFFFFFFFF);
        return new POINT
        {
            X = unchecked((short)(raw & 0xFFFF)),
            Y = unchecked((short)((raw >> 16) & 0xFFFF)),
        };
    }
}
