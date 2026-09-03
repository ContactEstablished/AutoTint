using System;
using System.Threading;
using System.Windows;
using AutoTint.Interop;
using AutoTint.Models;
using AutoTint.Services;
using AutoTint.Views;

namespace AutoTint;

public partial class App : System.Windows.Application
{
    // "Local\" scopes these to the current logon session, which is the right granularity:
    // one tint per desktop, and no interference between users on a shared machine.
    private const string MutexName = @"Local\AutoTint.SingleInstance";
    private const string RevealEventName = @"Local\AutoTint.Reveal";

    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private EventWaitHandle? _revealEvent;
    private RegisteredWaitHandle? _revealRegistration;

    private SettingsStore? _store;
    private TrayIcon? _tray;
    private OverlayWindow? _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!ClaimSingleInstance())
        {
            Shutdown();
            return;
        }

        _store = new SettingsStore();
        AppSettings settings = _store.Load();

        _overlay = new OverlayWindow(settings, _store);
        _overlay.Show();

        _tray = new TrayIcon(OverlayWindow.AppVersion);
        _tray.ToggleRequested += () => _overlay.ToggleTint();
        _tray.ResetRequested += () => _overlay.ResetBounds();
        _tray.FillMonitorRequested += () => _overlay.FillCurrentMonitor();
        _tray.QuitRequested += Shutdown;
        _tray.CaptureProtectionChanged += enabled => _overlay.SetCaptureProtection(enabled);

        _tray.StartWithWindowsChanged += enabled => _overlay.SetRunAtLogon(enabled);

        _overlay.TintStateChanged += on => _tray.SetTintOn(on);
        _overlay.RunAtLogonChanged += enabled => _tray.SetStartWithWindows(enabled);
        _tray.SetTintOn(_overlay.IsTintOn);
        _tray.SetCaptureProtection(settings.HideFromCapture);
        _tray.SetStartWithWindows(_overlay.RunsAtLogon);

        if (settings.HotkeyEnabled && !_overlay.HotkeyRegistered)
        {
            _tray.Notify(
                "AutoTint",
                $"{_overlay.HotkeyDescription} is already in use by another app, so the " +
                "shortcut is off. The tint can still be toggled from the tab or this icon.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlay?.PrepareForShutdown();
        _revealRegistration?.Unregister(null);
        _revealEvent?.Dispose();
        _tray?.Dispose();

        // Dispose flushes anything still sitting in the debounce window.
        _store?.Dispose();

        // Only the instance that created the mutex holds it. Releasing one this process
        // never owned throws, which would turn a tidy "already running, bowing out" into a
        // crash on the way out.
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Returns false when AutoTint is already running. In that case the existing instance
    /// is told to show itself, since a second launch almost always means "where did it
    /// go?" rather than "give me another one".
    /// </summary>
    private bool ClaimSingleInstance()
    {
        _instanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);
        _ownsInstanceMutex = isFirstInstance;

        if (!isFirstInstance)
        {
            if (EventWaitHandle.TryOpenExisting(RevealEventName, out EventWaitHandle? existing))
            {
                existing.Set();
                existing.Dispose();
            }

            return false;
        }

        _revealEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RevealEventName);
        _revealRegistration = ThreadPool.RegisterWaitForSingleObject(
            _revealEvent,
            (_, _) => Dispatcher.Invoke(Reveal),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        return true;
    }

    private bool BoundsAreReachable()
    {
        if (_overlay is null) return true;

        return BoundsValidator.IsRestorable(
            (int)_overlay.Left, (int)_overlay.Top,
            (int)_overlay.Width, (int)_overlay.Height,
            NativeMethods.IsPointOnAMonitor);
    }

    private void Reveal()
    {
        if (_overlay is null) return;

        // The window may have been hidden rather than closed; Show brings it back either way.
        _overlay.Show();
        _overlay.Topmost = true;

        if (!BoundsAreReachable()) _overlay.ResetBounds();
    }
}
