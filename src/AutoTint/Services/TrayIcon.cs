using System;
using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace AutoTint.Services;

/// <summary>
/// The tray presence. Not a nicety: the overlay is a frameless window that stays out of
/// the taskbar and out of Alt+Tab, so without this there would be no dependable way to
/// quit it, or to get it back after it was moved somewhere awkward.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly Forms.ToolStripMenuItem _captureItem;
    private readonly Forms.ToolStripMenuItem _startupItem;

    internal TrayIcon(string version)
    {
        _toggleItem = new Forms.ToolStripMenuItem("Turn tint off");
        _toggleItem.Click += (_, _) => ToggleRequested?.Invoke();

        var resetItem = new Forms.ToolStripMenuItem("Reset size and position");
        resetItem.Click += (_, _) => ResetRequested?.Invoke();

        _captureItem = new Forms.ToolStripMenuItem("Hide from screen sharing")
        {
            CheckOnClick = true,
            Checked = true,
        };
        _captureItem.CheckedChanged += (_, _) => CaptureProtectionChanged?.Invoke(_captureItem.Checked);

        _startupItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
        };
        _startupItem.CheckedChanged += (_, _) => StartWithWindowsChanged?.Invoke(_startupItem.Checked);

        var quitItem = new Forms.ToolStripMenuItem("Quit AutoTint");
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(resetItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_captureItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(quitItem);

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = $"AutoTint {version}",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.DoubleClick += (_, _) => ToggleRequested?.Invoke();
    }

    internal event Action? ToggleRequested;

    internal event Action? ResetRequested;

    internal event Action? QuitRequested;

    internal event Action<bool>? CaptureProtectionChanged;

    internal event Action<bool>? StartWithWindowsChanged;

    /// <summary>Keeps the menu wording honest about what clicking it will do.</summary>
    internal void SetTintOn(bool on)
    {
        _toggleItem.Text = on ? "Turn tint off" : "Turn tint on";
    }

    internal void SetStartWithWindows(bool enabled)
    {
        if (_startupItem.Checked != enabled) _startupItem.Checked = enabled;
    }

    internal void SetCaptureProtection(bool enabled)
    {
        // Assigning Checked re-raises CheckedChanged, which would loop back into the
        // window; only touch it when the value actually differs.
        if (_captureItem.Checked != enabled) _captureItem.Checked = enabled;
    }

    /// <summary>
    /// Used to report a hotkey that could not be registered. A balloon is the right weight
    /// for this -- worth telling the user, not worth a modal dialog on startup.
    /// </summary>
    internal void Notify(string title, string message)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(5000);
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null && File.Exists(exe))
            {
                Icon? extracted = Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null) return extracted;
            }
        }
        catch (Exception e) when (e is IOException or ArgumentException)
        {
            // Fall through to the stock icon rather than failing to show a tray entry.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
