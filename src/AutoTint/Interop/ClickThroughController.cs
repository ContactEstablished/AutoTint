using System;
using System.Windows.Threading;

namespace AutoTint.Interop;

/// <summary>
/// Makes the overlay ignore the mouse everywhere except its own controls.
///
/// Returning HTTRANSPARENT from WM_NCHITTEST is not enough: Windows only forwards those
/// hits to other windows <em>on the same thread</em>, so with a Teams window underneath
/// the clicks are swallowed rather than passed on. The mechanism that works across
/// processes is the WS_EX_TRANSPARENT extended style, which applies to the whole window.
///
/// So the style is toggled instead: click-through by default, switched off while the
/// cursor sits over an interactive region. WM_NCHITTEST still runs the moment the window
/// is live, which is what keeps dragging and resizing native rather than hand-rolled.
/// </summary>
internal sealed class ClickThroughController : IDisposable
{
    /// <summary>
    /// ~60Hz. The work per tick is one GetCursorPos plus a handful of rectangle tests,
    /// so the cost is immaterial and the response feels immediate.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);

    private readonly IntPtr _hwnd;
    private readonly Func<NativeMethods.POINT, bool> _isInteractive;
    private readonly DispatcherTimer _timer;
    private readonly bool _forceInteractive;

    private bool _clickThrough;
    private bool _suspended;

    internal ClickThroughController(IntPtr hwnd, Func<NativeMethods.POINT, bool> isInteractive)
    {
        _hwnd = hwnd;
        _isInteractive = isInteractive;
        _forceInteractive = Environment.GetEnvironmentVariable("AUTOTINT_FORCE_INTERACTIVE") == "1";

        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = PollInterval };
        _timer.Tick += OnTick;
    }

    /// <summary>True when the window is currently ignoring the mouse.</summary>
    internal bool IsClickThrough => _clickThrough;

    internal void Start()
    {
        SetClickThrough(!_forceInteractive);
        _timer.Start();
    }

    /// <summary>
    /// Called on WM_ENTERSIZEMOVE. During a native drag or resize the cursor routinely
    /// leaves the grab band -- flipping the window back to click-through mid-gesture would
    /// drop the operation, so the watcher stands down until the loop ends.
    /// </summary>
    internal void SuspendForSizeMove() => _suspended = true;

    /// <summary>Called on WM_EXITSIZEMOVE.</summary>
    internal void ResumeAfterSizeMove() => _suspended = false;

    private void OnTick(object? sender, EventArgs e)
    {
        if (_suspended || _forceInteractive) return;
        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT cursor)) return;

        bool wantsInput = _isInteractive(cursor);
        if (wantsInput == !_clickThrough) return;

        // Never hand the mouse back mid-click. Dragging the opacity slider takes the cursor
        // outside the tab, and going click-through there would abandon the drag.
        if (!wantsInput && NativeMethods.IsLeftButtonDown()) return;

        SetClickThrough(!wantsInput);
    }

    private void SetClickThrough(bool enabled)
    {
        if (enabled)
        {
            NativeMethods.AddExtendedStyle(_hwnd, NativeMethods.WS_EX_TRANSPARENT);
        }
        else
        {
            NativeMethods.RemoveExtendedStyle(_hwnd, NativeMethods.WS_EX_TRANSPARENT);
        }

        _clickThrough = enabled;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
