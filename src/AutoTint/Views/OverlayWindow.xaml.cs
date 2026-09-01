using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AutoTint.Interop;
using AutoTint.Models;
using AutoTint.Services;

namespace AutoTint.Views;

public partial class OverlayWindow : Window
{
    /// <summary>Restored when the tint is switched back on from zero.</summary>
    private const double FallbackStrength = 50;

    /// <summary>How far one notch of the scroll wheel moves the tint.</summary>
    private const double WheelStep = 5;

    private readonly AppSettings _settings;
    private readonly SettingsStore _store;

    private HwndSource? _source;
    private ClickThroughController? _clickThrough;
    private SnapController? _snap;
    private AutoLevelController? _autoLevel;
    private GlobalHotkey? _hotkey;
    private IntPtr _hwnd;
    private IntPtr _moveCursor;

    private double _strengthBeforeOff = FallbackStrength;
    private bool _captureProtected = true;
    private bool _restoring = true;
    private bool _shuttingDown;
    private bool _switchingMode;

    /// <summary>
    /// Explicit on/off state. Under auto-adjust the slider holds a comfort target rather
    /// than an opacity, so "is the tint on" can no longer be read off the slider.
    /// </summary>
    private bool _tintEnabled = true;

    internal OverlayWindow(AppSettings settings, SettingsStore store)
    {
        _settings = settings;
        _store = store;

        InitializeComponent();

        PowerButton.Click += (_, _) => ToggleTint();
        ExpandButton.Click += (_, _) => SetExpanded(SettingsPanel.Visibility != Visibility.Visible);
        AutoButton.Click += (_, _) => SetAutoLevel(!IsAutoOn);
        SnapButton.Click += (_, _) => SetAutoSnap(!(_snap?.IsEnabled ?? false));
        CaptureButton.Click += (_, _) => SetCaptureProtection(!_captureProtected);
        MenuButton.Click += (_, _) => ShowMenu();

        OpacitySlider.ValueChanged += (_, e) => OnSliderChanged(e.NewValue);

        SwatchBlack.Checked += (_, _) => ApplyColour(TintPreset.Black);
        SwatchWarm.Checked += (_, _) => ApplyColour(TintPreset.Warm);
        SwatchGrey.Checked += (_, _) => ApplyColour(TintPreset.Grey);

        // Scrolling anywhere on the tab nudges the tint, so small adjustments do not
        // require opening the panel and aiming at the slider.
        TabSurface.PreviewMouseWheel += OnTabMouseWheel;

        LocationChanged += (_, _) => Persist();
        SizeChanged += (_, _) => Persist();

        ApplySettings();
    }

    /// <summary>Raised when the tint is switched on or off, so the tray label can follow.</summary>
    internal event Action<bool>? TintStateChanged;

    internal bool IsTintOn => IsAutoOn ? _tintEnabled : OpacitySlider.Value > 0;

    private bool IsAutoOn => _autoLevel?.IsEnabled == true;

    internal bool HotkeyRegistered { get; private set; }

    internal string HotkeyDescription => _hotkey?.Description ?? "Alt+Shift+T";

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        _hwnd = _source.Handle;
        _source.AddHook(WndProc);
        _moveCursor = NativeMethods.LoadCursor(IntPtr.Zero, new IntPtr(NativeMethods.IDC_SIZEALL));

        // WS_EX_TOOLWINDOW keeps the overlay out of Alt+Tab. WS_EX_NOACTIVATE stops it
        // stealing focus when the tab is clicked -- reaching for the opacity slider should
        // not pull focus out of the meeting window behind it.
        NativeMethods.AddExtendedStyle(
            _hwnd,
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);

        RestoreSavedBounds();

        _clickThrough = new ClickThroughController(_hwnd, IsInteractiveAt);
        _clickThrough.Start();

        _snap = new SnapController(_hwnd, PanelBoundsPx, TabHeightPx, MinimumSizePx, ApplyBoundsPx);
        _snap.NoTargetFound += FlashSnapButton;

        _autoLevel = new AutoLevelController(
            _hwnd, TintRegionPx, CurrentTintLuma, CurrentTargetLuma, ApplyComputedOpacity);

        // Sampling a hidden window would measure whatever is behind it, to no purpose.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) _autoLevel?.Resume();
            else _autoLevel?.Suspend();
        };

        SetCaptureProtection(_settings.HideFromCapture);

        // Deferred: snapping and sampling both need the tab's measured height, which layout
        // has not produced yet.
        //
        // Read the wanted values now rather than inside the callback. Persist() rewrites
        // these fields from live UI state, and the layout pass raises SizeChanged -- and so
        // a Persist -- before this callback runs, which would overwrite both flags with the
        // "off" they currently hold and silently discard what was restored from disk.
        bool restoreAutoSnap = _settings.AutoSnap;
        bool restoreAutoLevel = _settings.AutoLevel;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                SetAutoSnap(restoreAutoSnap);
                SetAutoLevel(restoreAutoLevel);
            }));

        if (_settings.HotkeyEnabled)
        {
            _hotkey = new GlobalHotkey(_hwnd);
            HotkeyRegistered = _hotkey.TryRegister();
        }

        Diagnostics.DumpWindowState(_hwnd, this);
        _restoring = false;
    }

    /// <summary>
    /// Alt+F4 or a stray WM_CLOSE would otherwise destroy the only window while the app
    /// keeps running from the tray, leaving nothing for "show" to bring back. Closing
    /// hides instead; only an explicit quit ends the process.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_shuttingDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>Lets the real close through when the application is exiting.</summary>
    internal void PrepareForShutdown() => _shuttingDown = true;

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        _autoLevel?.Dispose();
        _snap?.Dispose();
        _clickThrough?.Dispose();
        _source?.RemoveHook(WndProc);
        base.OnClosed(e);
    }

    // ---- Settings ---------------------------------------------------------------------

    private void ApplySettings()
    {
        _strengthBeforeOff = _settings.StrengthBeforeOff;

        TintPreset preset = TintPreset.FromId(_settings.ColourId);
        ApplyColour(preset);
        if (preset.Id == "warm") SwatchWarm.IsChecked = true;
        else if (preset.Id == "grey") SwatchGrey.IsChecked = true;
        else SwatchBlack.IsChecked = true;

        OpacitySlider.Value = Math.Clamp(
            _settings.Strength, OpacitySlider.Minimum, OpacitySlider.Maximum);
        ApplyStrength(OpacitySlider.Value);

        // Only the visibility here. Saved bounds already account for the taller tab, and
        // SetExpanded would add that height a second time.
        SettingsPanel.Visibility = _settings.Expanded ? Visibility.Visible : Visibility.Collapsed;
        if (_settings.Expanded && !_settings.HasBounds) SetExpanded(true);
    }

    /// <summary>
    /// Puts the window back where it was, in physical pixels, but only if that place still
    /// exists -- a monitor may have been disconnected since the settings were written.
    /// </summary>
    private void RestoreSavedBounds()
    {
        if (!_settings.HasBounds) return;

        int left = _settings.BoundsLeft!.Value;
        int top = _settings.BoundsTop!.Value;
        int width = _settings.BoundsWidth!.Value;
        int height = _settings.BoundsHeight!.Value;

        if (!BoundsValidator.IsRestorable(left, top, width, height, NativeMethods.IsPointOnAMonitor))
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero, left, top, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void Persist()
    {
        if (_restoring || _hwnd == IntPtr.Zero) return;

        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT r))
        {
            _settings.BoundsLeft = r.Left;
            _settings.BoundsTop = r.Top;
            _settings.BoundsWidth = r.Width;
            _settings.BoundsHeight = r.Height;
        }

        if (!IsAutoOn) _settings.Strength = OpacitySlider.Value;
        _settings.StrengthBeforeOff = _strengthBeforeOff;
        _settings.Expanded = SettingsPanel.Visibility == Visibility.Visible;
        _settings.HideFromCapture = _captureProtected;
        _settings.AutoSnap = _snap?.IsEnabled ?? false;
        _settings.AutoLevel = IsAutoOn;

        // The slider means different things in the two modes; do not let one overwrite
        // the other's saved value.
        if (IsAutoOn) _settings.AutoTarget = OpacitySlider.Value;
        _settings.ColourId = CurrentColourId();

        _store.Save(_settings);
    }

    private string CurrentColourId()
    {
        if (SwatchWarm.IsChecked == true) return "warm";
        if (SwatchGrey.IsChecked == true) return "grey";
        return "black";
    }

    // ---- Tint -------------------------------------------------------------------------

    private void OnSliderChanged(double value)
    {
        if (_switchingMode) return;

        if (IsAutoOn)
        {
            // The slider is the comfort target here, not the opacity.
            _autoLevel!.Recompute();
            Persist();
            return;
        }

        ApplyStrength(value);
    }

    private void ApplyStrength(double percent)
    {
        TintSurface.Opacity = percent / 100.0;
        OpacityReadout.Text = Math.Round(percent).ToString("0'%'", CultureInfo.CurrentCulture);
        PowerButton.Opacity = percent > 0 ? 1.0 : 0.55;

        TintStateChanged?.Invoke(percent > 0);
        Persist();
    }

    private void ApplyColour(TintPreset preset)
    {
        TintSurface.Background = new SolidColorBrush(preset.Colour);
        Persist();
    }

    /// <summary>
    /// The quick on/off. Switching off remembers the strength so switching back on returns
    /// to exactly where it was rather than to some default.
    /// </summary>
    internal void ToggleTint()
    {
        if (IsAutoOn)
        {
            // Auto must not immediately undo a deliberate switch-off, so it stands down
            // rather than carrying on sampling.
            _tintEnabled = !_tintEnabled;

            if (_tintEnabled)
            {
                _autoLevel!.Resume();
                _autoLevel.RequestImmediateUpdate();
            }
            else
            {
                _autoLevel!.Suspend();
                ApplyComputedOpacity(0);
            }

            TintStateChanged?.Invoke(_tintEnabled);
            return;
        }

        if (OpacitySlider.Value > 0)
        {
            _strengthBeforeOff = OpacitySlider.Value;
            OpacitySlider.Value = 0;
        }
        else
        {
            OpacitySlider.Value = _strengthBeforeOff > 0 ? _strengthBeforeOff : FallbackStrength;
        }
    }

    private void OnTabMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = e.Delta > 0 ? WheelStep : -WheelStep;
        OpacitySlider.Value = Math.Clamp(
            OpacitySlider.Value + step, OpacitySlider.Minimum, OpacitySlider.Maximum);
        e.Handled = true;
    }

    // ---- Tab --------------------------------------------------------------------------

    /// <summary>
    /// Grows or shrinks the window by whatever height the settings panel adds, so opening
    /// the panel extends the tab downwards instead of eating into the tint.
    /// </summary>
    private void SetExpanded(bool expanded)
    {
        double before = TabSurface.ActualHeight;
        SettingsPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        TabSurface.UpdateLayout();
        Height += TabSurface.ActualHeight - before;
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu
        {
            PlacementTarget = MenuButton,
            Placement = PlacementMode.Top,
        };

        var reset = new MenuItem { Header = "Reset size and position" };
        reset.Click += (_, _) => ResetBounds();
        menu.Items.Add(reset);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit AutoTint" };
        quit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(quit);

        menu.IsOpen = true;
    }

    internal void ResetBounds()
    {
        Width = 640;
        Height = 400;
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
    }

    /// <summary>
    /// Hides the overlay from screen captures and shared screens while leaving it visible
    /// on the physical monitor -- the tint should dim your view, not everyone else's.
    /// </summary>
    internal bool SetCaptureProtection(bool enabled)
    {
        if (_hwnd == IntPtr.Zero) return false;

        uint affinity = enabled ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE;
        if (!NativeMethods.SetWindowDisplayAffinity(_hwnd, affinity)) return false;

        _captureProtected = enabled;
        CaptureButton.Content = enabled ? "" : "";
        CaptureButton.ToolTip = enabled
            ? "Hidden from screen sharing and screenshots"
            : "Visible to screen sharing and screenshots";
        Persist();
        return true;
    }

    // ---- Auto-adjust ------------------------------------------------------------------

    private void SetAutoLevel(bool enabled)
    {
        if (_autoLevel is null || _autoLevel.IsEnabled == enabled) return;

        _switchingMode = true;
        try
        {
            OpacitySlider.Value = Math.Clamp(
                enabled ? _settings.AutoTarget : _settings.Strength,
                OpacitySlider.Minimum,
                OpacitySlider.Maximum);

            if (!enabled)
            {
                // Release the animation, or later direct writes to Opacity are ignored
                // because the held animation keeps winning.
                TintSurface.BeginAnimation(OpacityProperty, null);
            }
        }
        finally
        {
            _switchingMode = false;
        }

        _tintEnabled = true;
        _autoLevel.SetEnabled(enabled);

        if (!enabled) ApplyStrength(OpacitySlider.Value);

        AutoButton.Background = enabled
            ? (Brush)FindResource("TabSunkenBrush")
            : Brushes.Transparent;
        AutoButton.ToolTip = enabled
            ? "Auto-adjust on -- the slider sets how bright to leave things"
            : "Auto-adjust off -- the slider sets tint strength directly";
        OpacitySlider.ToolTip = enabled ? "How bright to leave the result" : "Tint strength";

        Persist();
    }

    /// <summary>
    /// Applies an opacity chosen by auto-adjust. Deliberately does not touch the slider or
    /// persist: this is derived state, not a preference, and writing it every sample would
    /// turn the settings debounce into a disk write twice a second.
    /// </summary>
    private void ApplyComputedOpacity(double percent)
    {
        TintSurface.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = percent / 100.0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });

        OpacityReadout.Text = _tintEnabled
            ? string.Create(CultureInfo.CurrentCulture, $"auto {Math.Round(percent)}%")
            : "off";
        PowerButton.Opacity = _tintEnabled ? 1.0 : 0.55;
    }

    /// <summary>The region the tint actually covers: the window less the tab below it.</summary>
    private NativeMethods.RECT TintRegionPx()
    {
        NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT bounds);
        bounds.Bottom -= TabHeightPx();
        return bounds;
    }

    private double CurrentTintLuma() =>
        TintSurface.Background is SolidColorBrush brush
            ? AutoLevelMath.LumaOf(brush.Color)
            : 0;

    private double CurrentTargetLuma() => AutoLevelMath.TargetForSlider(OpacitySlider.Value);

    // ---- Auto-snap --------------------------------------------------------------------

    private void SetAutoSnap(bool enabled)
    {
        if (_snap is null) return;

        _snap.SetEnabled(enabled);
        SnapButton.Background = enabled
            ? (Brush)FindResource("TabSunkenBrush")
            : Brushes.Transparent;
        SnapButton.ToolTip = enabled
            ? "Auto-snap on -- the panel follows the window beneath it"
            : "Auto-snap off -- position the panel yourself";

        Persist();
    }

    /// <summary>
    /// A snap was attempted with nothing suitable underneath. Blink the button so the
    /// absence of movement reads as "looked, found nothing" rather than as a dead control.
    /// </summary>
    private void FlashSnapButton()
    {
        SnapButton.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 1.0,
            To = 0.2,
            Duration = TimeSpan.FromMilliseconds(130),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
            FillBehavior = FillBehavior.Stop,
        });
    }

    private NativeMethods.RECT PanelBoundsPx()
    {
        NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT bounds);
        return bounds;
    }

    private int TabHeightPx() =>
        (int)Math.Round(TabSurface.ActualHeight * VisualTreeHelper.GetDpi(this).DpiScaleY);

    private (int Width, int Height) MinimumSizePx()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        return ((int)Math.Round(MinWidth * dpi.DpiScaleX), (int)Math.Round(MinHeight * dpi.DpiScaleY));
    }

    private void ApplyBoundsPx(SnapBounds bounds) =>
        NativeMethods.SetWindowPos(
            _hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

    // ---- Win32 ------------------------------------------------------------------------

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey is not null && _hotkey.Matches(msg, wParam))
        {
            ToggleTint();
            handled = true;
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case NativeMethods.WM_NCHITTEST:
                handled = true;
                return new IntPtr((int)HitTest(NativeMethods.PointFromLParam(lParam)));

            case NativeMethods.WM_SETCURSOR:
                // Windows draws the plain arrow over a caption area. The grip is a drag
                // handle, so give it the four-way move cursor to say so.
                if ((lParam.ToInt64() & 0xFFFF) == (int)HitTestCode.Caption && _moveCursor != IntPtr.Zero)
                {
                    NativeMethods.SetCursor(_moveCursor);
                    handled = true;
                    return new IntPtr(1);
                }

                return IntPtr.Zero;

            case NativeMethods.WM_ENTERSIZEMOVE:
                _clickThrough?.SuspendForSizeMove();
                _snap?.Suspend();
                _autoLevel?.Suspend();
                return IntPtr.Zero;

            case NativeMethods.WM_EXITSIZEMOVE:
                _clickThrough?.ResumeAfterSizeMove();
                // The panel has just been dropped somewhere new, so it looks for a new
                // window to line up with rather than returning to its previous one.
                _snap?.ResumeAndRetarget();
                _autoLevel?.Resume();
                _autoLevel?.RequestImmediateUpdate();
                return IntPtr.Zero;

            default:
                return IntPtr.Zero;
        }
    }

    private bool IsInteractiveAt(NativeMethods.POINT screenPx) =>
        HitTest(screenPx) != HitTestCode.Transparent;

    private HitTestCode HitTest(NativeMethods.POINT screenPx)
    {
        Point p;
        try
        {
            p = PointFromScreen(new Point(screenPx.X, screenPx.Y));
        }
        catch (InvalidOperationException)
        {
            // No presentation source yet; nothing sensible to report.
            return HitTestCode.Transparent;
        }

        return HitTestResolver.Resolve(
            RectOf(TintSurface),
            RectOf(TabSurface),
            RectOf(GripSurface),
            p);
    }

    /// <summary>
    /// Bounds of an element in window-relative device-independent units, or
    /// <see cref="Rect.Empty"/> before layout has run.
    /// </summary>
    private Rect RectOf(FrameworkElement element)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        Point origin = element.TranslatePoint(new Point(0, 0), this);
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }
}
