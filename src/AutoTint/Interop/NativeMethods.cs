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

    /// <summary>GetAncestor: walk up to the top-level owner window.</summary>
    internal const uint GA_ROOT = 2;

    /// <summary>
    /// The window rectangle as drawn, excluding the invisible resize border that
    /// GetWindowRect reports (roughly 7px per side on Windows 10 and 11).
    /// </summary>
    internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    /// <summary>Non-zero when a window is cloaked -- present but not really shown.</summary>
    internal const int DWMWA_CLOAKED = 14;

    /// <summary>MonitorFromPoint returns NULL when the point is on no monitor at all.</summary>
    internal const uint MONITOR_DEFAULTTONULL = 0x00000000;

    /// <summary>
    /// StretchBlt sub-sampling. Measured against HALFTONE it is twice as fast and, for a
    /// statistical reading, more faithful: HALFTONE averages fine bright detail away, while
    /// dropping pixels preserves the distribution the percentile is taken over.
    /// </summary>
    internal const int STRETCH_COLORONCOLOR = 3;

    internal const int SRCCOPY = 0x00CC0020;
    internal const uint DIB_RGB_COLORS = 0;

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
    internal struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;

        /// <summary>Negative means a top-down bitmap, which keeps row order intuitive.</summary>
        public int biHeight;

        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
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

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

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

    [LibraryImport("user32.dll")]
    internal static partial IntPtr WindowFromPoint(POINT Point);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetDC(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateCompatibleDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(IntPtr hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    internal static partial int SetStretchBltMode(IntPtr hdc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool StretchBlt(
        IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, int rop);

    [LibraryImport("gdi32.dll")]
    internal static partial IntPtr CreateDIBSection(
        IntPtr hdc, ref BITMAPINFOHEADER pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    private static unsafe partial int GetClassNameRaw(IntPtr hWnd, char* lpClassName, int nMaxCount);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmGetWindowAttribute(
        IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    internal static partial int DwmGetWindowAttributeInt(
        IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    internal static unsafe string GetClassNameOf(IntPtr hWnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = GetClassNameRaw(hWnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

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
