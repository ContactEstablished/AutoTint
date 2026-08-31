using System;
using System.Windows.Threading;
using AutoTint.Interop;

namespace AutoTint.Services;

/// <summary>
/// Auto-snap: line the tint up with the window underneath it, and keep it there.
///
/// The panel latches onto a target when it is dropped, and follows that window as it is
/// moved or resized until either the target goes away or the user drags the panel
/// somewhere else, at which point it looks for a new one.
/// </summary>
internal sealed class SnapController : IDisposable
{
    /// <summary>
    /// Four times a second. Fast enough that following a dragged window looks attached
    /// rather than laggy, slow enough to cost nothing when nothing is moving.
    /// </summary>
    private static readonly TimeSpan FollowInterval = TimeSpan.FromMilliseconds(250);

    private readonly IntPtr _self;
    private readonly Func<NativeMethods.RECT> _panelBounds;
    private readonly Func<int> _tabHeight;
    private readonly Func<(int Width, int Height)> _minimumSize;
    private readonly Action<SnapBounds> _applyBounds;
    private readonly DispatcherTimer _follow;

    private IntPtr _target;
    private NativeMethods.RECT _targetBounds;
    private bool _enabled;
    private bool _suspended;

    internal SnapController(
        IntPtr self,
        Func<NativeMethods.RECT> panelBounds,
        Func<int> tabHeight,
        Func<(int Width, int Height)> minimumSize,
        Action<SnapBounds> applyBounds)
    {
        _self = self;
        _panelBounds = panelBounds;
        _tabHeight = tabHeight;
        _minimumSize = minimumSize;
        _applyBounds = applyBounds;

        _follow = new DispatcherTimer(DispatcherPriority.Background) { Interval = FollowInterval };
        _follow.Tick += OnFollowTick;
    }

    /// <summary>
    /// Raised when a snap was attempted and nothing suitable was underneath, so the UI can
    /// say so rather than leaving the silence to be read as a bug.
    /// </summary>
    internal event Action? NoTargetFound;

    internal bool IsEnabled => _enabled;

    /// <summary>True while the panel is latched onto a window.</summary>
    internal bool HasTarget => _target != IntPtr.Zero;

    internal void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;

        _enabled = enabled;

        if (enabled)
        {
            // Turning it on should act immediately; waiting for the next drag would look broken.
            SnapToWindowUnderPanel();
            _follow.Start();
        }
        else
        {
            _follow.Stop();
            _target = IntPtr.Zero;
        }
    }

    /// <summary>Called on WM_ENTERSIZEMOVE: leave the user alone while they drag.</summary>
    internal void Suspend() => _suspended = true;

    /// <summary>
    /// Called on WM_EXITSIZEMOVE. The panel has just been dropped somewhere new, so it
    /// re-targets rather than snapping back to whatever it was on before.
    /// </summary>
    internal void ResumeAndRetarget()
    {
        _suspended = false;
        if (_enabled) SnapToWindowUnderPanel();
    }

    /// <summary>
    /// Finds the window under the middle of the tint and lines the panel up with it.
    /// </summary>
    internal void SnapToWindowUnderPanel()
    {
        NativeMethods.RECT panel = _panelBounds();
        int tab = _tabHeight();

        // Probe the centre of the tinted area rather than of the whole window: the tab
        // hangs below the tint and would drag the sample point off the target.
        int centreX = panel.Left + (panel.Width / 2);
        int centreY = panel.Top + ((panel.Height - tab) / 2);

        IntPtr found = WindowFinder.FindTargetUnder(_self, centreX, centreY);
        if (found == IntPtr.Zero || !WindowFinder.TryGetFrameBounds(found, out NativeMethods.RECT bounds))
        {
            _target = IntPtr.Zero;
            NoTargetFound?.Invoke();
            return;
        }

        _target = found;
        _targetBounds = bounds;
        Apply(bounds, tab);
    }

    private void OnFollowTick(object? sender, EventArgs e)
    {
        if (_suspended || !_enabled || _target == IntPtr.Zero) return;

        if (!WindowFinder.IsStillUsable(_target))
        {
            // Closed, minimised or hidden. Let go and leave the panel where it is rather
            // than chasing a window that is no longer on screen.
            _target = IntPtr.Zero;
            return;
        }

        if (!WindowFinder.TryGetFrameBounds(_target, out NativeMethods.RECT bounds)) return;
        if (SameRect(bounds, _targetBounds)) return;

        _targetBounds = bounds;
        Apply(bounds, _tabHeight());
    }

    private void Apply(NativeMethods.RECT target, int tabHeight)
    {
        (int minWidth, int minHeight) = _minimumSize();

        _applyBounds(SnapGeometry.ForTarget(
            target.Left, target.Top, target.Width, target.Height,
            tabHeight, minWidth, minHeight));
    }

    private static bool SameRect(NativeMethods.RECT a, NativeMethods.RECT b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    public void Dispose()
    {
        _follow.Stop();
        _follow.Tick -= OnFollowTick;
    }
}
