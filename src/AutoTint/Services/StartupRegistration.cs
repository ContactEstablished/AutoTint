using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace AutoTint.Services;

/// <summary>
/// Starting with Windows, via the per-user Run key.
///
/// Chosen over a scheduled task or a Startup-folder shortcut because it is what the rest of
/// the desktop already does -- Slack, Discord, Spotify and Teams all register here -- which
/// means AutoTint shows up in Task Manager's Startup tab next to them and can be disabled
/// from the place people already look.
///
/// The registry is the single source of truth. This state is deliberately not mirrored into
/// settings.json: the user can turn it off from Task Manager without the app running, and a
/// cached copy would then be a lie.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutoTint";

    /// <summary>Path of the running executable, or null if it cannot be determined.</summary>
    internal static string? ExecutablePath
    {
        get
        {
            string? path = Environment.ProcessPath;
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }
    }

    internal static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    internal static bool SetEnabled(bool enabled)
    {
        string? executable = ExecutablePath;
        if (executable is null) return false;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return false;

            if (enabled)
            {
                key.SetValue(ValueName, StartupCommand.For(executable), RegistryValueKind.String);
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Repoints the entry if it is enabled but refers to a different copy of the app -- which
    /// happens after installing over a copy that was being run from somewhere else. Without
    /// this the app would keep launching the old executable at logon.
    /// </summary>
    internal static void RepairIfStale()
    {
        string? executable = ExecutablePath;
        if (executable is null) return;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string existing) return;
            if (StartupCommand.PointsAt(existing, executable)) return;

            key.SetValue(ValueName, StartupCommand.For(executable), RegistryValueKind.String);
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Not being able to tidy the entry is not worth interrupting startup over.
        }
    }
}
