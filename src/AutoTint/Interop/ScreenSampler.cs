using System;
using System.Runtime.InteropServices;

namespace AutoTint.Interop;

/// <summary>
/// Reads the screen under a rectangle, shrunk to a fixed grid of samples.
///
/// This is safe to point at the region the overlay is covering precisely because the
/// overlay sets <c>WDA_EXCLUDEFROMCAPTURE</c>: our own tint is left out of our own capture,
/// so measuring does not measure the dimming we already applied. Without that, reading the
/// screen to decide how much to dim it would oscillate.
/// </summary>
internal sealed class ScreenSampler : IDisposable
{
    /// <summary>
    /// 64,000 samples. Measured: the cost is dominated by reading the source region, not by
    /// the destination size (4.16 ms at 96x64 versus 4.21 ms here), so the larger grid is
    /// effectively free and estimates the distribution better.
    /// </summary>
    internal const int SampleWidth = 320;

    internal const int SampleHeight = 200;

    private const int BytesPerPixel = 4;

    private readonly byte[] _pixels = new byte[SampleWidth * SampleHeight * BytesPerPixel];

    private IntPtr _screenDc;
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _previousBitmap;
    private IntPtr _bits;
    private bool _ready;

    internal ScreenSampler()
    {
        _ready = Initialise();
    }

    /// <summary>The most recent sample, as BGRA rows, top-down.</summary>
    internal ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>
    /// Fills <see cref="Pixels"/> from the given screen rectangle, in physical pixels.
    /// Returns false rather than throwing; a caller that fails to sample should hold its
    /// previous reading rather than treating the failure as "the screen went dark".
    /// </summary>
    internal bool TrySample(NativeMethods.RECT region)
    {
        if (!_ready || region.Width <= 0 || region.Height <= 0) return false;

        bool copied = NativeMethods.StretchBlt(
            _memoryDc, 0, 0, SampleWidth, SampleHeight,
            _screenDc, region.Left, region.Top, region.Width, region.Height,
            NativeMethods.SRCCOPY);

        if (!copied) return false;

        Marshal.Copy(_bits, _pixels, 0, _pixels.Length);
        return true;
    }

    private bool Initialise()
    {
        // GetDC(NULL) yields a device context over the whole virtual desktop, so regions on
        // secondary monitors -- including ones at negative coordinates -- work unchanged.
        _screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero) return false;

        _memoryDc = NativeMethods.CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero) return false;

        var header = new NativeMethods.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            biWidth = SampleWidth,
            biHeight = -SampleHeight,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };

        _bitmap = NativeMethods.CreateDIBSection(
            _screenDc, ref header, NativeMethods.DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);

        if (_bitmap == IntPtr.Zero || _bits == IntPtr.Zero) return false;

        _previousBitmap = NativeMethods.SelectObject(_memoryDc, _bitmap);
        NativeMethods.SetStretchBltMode(_memoryDc, NativeMethods.STRETCH_COLORONCOLOR);
        return true;
    }

    public void Dispose()
    {
        if (_previousBitmap != IntPtr.Zero) NativeMethods.SelectObject(_memoryDc, _previousBitmap);
        if (_bitmap != IntPtr.Zero) NativeMethods.DeleteObject(_bitmap);
        if (_memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(_memoryDc);
        if (_screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, _screenDc);

        _previousBitmap = _bitmap = _memoryDc = _screenDc = _bits = IntPtr.Zero;
        _ready = false;
    }
}
