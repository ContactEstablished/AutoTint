using System;
using System.Windows.Threading;
using AutoTint.Interop;

namespace AutoTint.Services;

/// <summary>
/// Watches how bright the content under the panel is and sets the tint to suit it.
///
/// Each tick samples the covered region, reduces it to a brightness histogram, and solves
/// for the opacity that brings the bright parts down to the comfort target. Readings are
/// smoothed and changes are held back by a dead-band, because a tint that visibly breathes
/// during a video call is worse than one that is slightly wrong.
/// </summary>
internal sealed class AutoLevelController : IDisposable
{
    /// <summary>Twice a second.</summary>
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Weight given to each new reading. At this cadence the tint settles about 90% of the
    /// way to a change in roughly two and a half seconds.
    /// </summary>
    private const double SmoothingWeight = 0.4;

    /// <summary>Percentage points of change worth acting on.</summary>
    private const double DeadBand = 1.5;

    /// <summary>Glare is about how bright the bright parts are, not the average.</summary>
    private const double MeasuredPercentile = 0.90;

    /// <summary>Below this the region is too small to say anything useful about.</summary>
    private const int MinimumRegionEdge = 16;

    private readonly IntPtr _self;
    private readonly Func<NativeMethods.RECT> _tintRegion;
    private readonly Func<double> _tintLuma;
    private readonly Func<double> _targetLuma;
    private readonly Action<double> _applyOpacity;
    private readonly DispatcherTimer _timer;
    private readonly ScreenSampler _sampler = new();

    private double _smoothedLuma;
    private double _appliedOpacity;
    private bool _hasReading;
    private bool _enabled;
    private bool _suspended;
    private bool _bypassDeadBandOnce;

    internal AutoLevelController(
        IntPtr self,
        Func<NativeMethods.RECT> tintRegion,
        Func<double> tintLuma,
        Func<double> targetLuma,
        Action<double> applyOpacity)
    {
        _self = self;
        _tintRegion = tintRegion;
        _tintLuma = tintLuma;
        _targetLuma = targetLuma;
        _applyOpacity = applyOpacity;

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = SampleInterval };
        _timer.Tick += OnTick;
    }

    internal bool IsEnabled => _enabled;

    /// <summary>The opacity most recently chosen, as a percentage.</summary>
    internal double AppliedOpacity => _appliedOpacity;

    internal void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;

        _enabled = enabled;

        if (enabled)
        {
            _hasReading = false;
            _bypassDeadBandOnce = true;
            _timer.Start();
            Sample();          // act at once; waiting half a second reads as broken
        }
        else
        {
            _timer.Stop();
        }
    }

    /// <summary>
    /// Stops sampling while the tint is off, the window is hidden, or the user is dragging.
    /// </summary>
    internal void Suspend() => _suspended = true;

    internal void Resume() => _suspended = false;

    /// <summary>
    /// Forces the next reading through the dead-band. Used after an auto-snap, where the
    /// covered content has changed wholesale rather than drifted.
    /// </summary>
    internal void RequestImmediateUpdate()
    {
        _bypassDeadBandOnce = true;
        if (_enabled && !_suspended) Sample();
    }

    /// <summary>The comfort target changed, so recompute from the reading already held.</summary>
    internal void Recompute()
    {
        if (!_enabled || !_hasReading) return;

        _bypassDeadBandOnce = true;
        ApplyFrom(_smoothedLuma);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_suspended || !_enabled) return;
        Sample();
    }

    private void Sample()
    {
        NativeMethods.RECT region = _tintRegion();
        if (region.Width < MinimumRegionEdge || region.Height < MinimumRegionEdge) return;

        if (!TrySampleUnprotected(region)) return;    // hold the previous reading

        LuminanceStats stats = LuminanceStats.From(_sampler.Pixels);
        if (stats.IsEmpty) return;

        double measured = stats.Percentile(MeasuredPercentile);

        _smoothedLuma = _hasReading
            ? AutoLevelMath.Smooth(_smoothedLuma, measured, SmoothingWeight)
            : measured;
        _hasReading = true;

        ApplyFrom(_smoothedLuma);
    }

    /// <summary>
    /// Samples with the overlay guaranteed to be excluded from capture, so the reading is of
    /// the content rather than of our own tint. Toggling display affinity costs about a
    /// microsecond, so doing it per sample is cheaper than requiring the user to leave
    /// screen-share hiding switched on.
    /// </summary>
    private bool TrySampleUnprotected(NativeMethods.RECT region)
    {
        bool alreadyExcluded =
            NativeMethods.GetWindowDisplayAffinity(_self, out uint affinity)
            && affinity == NativeMethods.WDA_EXCLUDEFROMCAPTURE;

        if (!alreadyExcluded)
        {
            NativeMethods.SetWindowDisplayAffinity(_self, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        }

        try
        {
            return _sampler.TrySample(region);
        }
        finally
        {
            if (!alreadyExcluded)
            {
                NativeMethods.SetWindowDisplayAffinity(_self, NativeMethods.WDA_NONE);
            }
        }
    }

    private void ApplyFrom(double luma)
    {
        double opacity = AutoLevelMath.OpacityFor(luma, _tintLuma(), _targetLuma());

        if (!_bypassDeadBandOnce && !AutoLevelMath.ShouldApply(_appliedOpacity, opacity, DeadBand))
        {
            return;
        }

        _bypassDeadBandOnce = false;
        _appliedOpacity = opacity;
        _applyOpacity(opacity);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _sampler.Dispose();
    }
}
