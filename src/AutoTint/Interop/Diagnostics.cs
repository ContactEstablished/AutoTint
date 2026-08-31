using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace AutoTint.Interop;

/// <summary>
/// Opt-in dump of the window's real Win32 state, so assumptions about what WPF applied
/// can be checked against reality rather than trusted. Enabled with AUTOTINT_DIAG=1.
/// </summary>
internal static class Diagnostics
{
    private const int GWL_STYLE = -16;
    private const int WS_THICKFRAME = 0x00040000;

    internal static string LogPath =>
        Path.Combine(Path.GetTempPath(), "autotint-diag.log");

    internal static void DumpWindowState(IntPtr hwnd, Window window)
    {
        if (Environment.GetEnvironmentVariable("AUTOTINT_DIAG") != "1") return;

        var sb = new StringBuilder();
        long style = NativeMethods.GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect);
        DpiScale dpi = VisualTreeHelper.GetDpi(window);

        sb.AppendLine(CultureInfo.InvariantCulture, $"hwnd            = 0x{hwnd.ToInt64():X}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"style           = 0x{style:X8}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  WS_THICKFRAME = {(style & WS_THICKFRAME) != 0}   <-- required for native resize");
        sb.AppendLine(CultureInfo.InvariantCulture, $"exStyle         = 0x{exStyle:X8}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  WS_EX_LAYERED     = {(exStyle & NativeMethods.WS_EX_LAYERED) != 0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  WS_EX_TRANSPARENT = {(exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  WS_EX_TOOLWINDOW  = {(exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"windowRect(px)  = {rect.Left},{rect.Top} {rect.Width}x{rect.Height}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"wpfSize(dip)    = {window.ActualWidth}x{window.ActualHeight}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"dpiScale        = {dpi.DpiScaleX}x{dpi.DpiScaleY}");

        // Does client-area origin coincide with the window rect? If PointFromScreen and
        // GetWindowRect disagree, every hit-test region would be offset.
        Point clientOrigin = window.PointToScreen(new Point(0, 0));
        sb.AppendLine(CultureInfo.InvariantCulture, $"clientOrigin(px)= {clientOrigin.X},{clientOrigin.Y}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"originDelta     = {clientOrigin.X - rect.Left},{clientOrigin.Y - rect.Top}");

        File.WriteAllText(LogPath, sb.ToString());
    }
}
