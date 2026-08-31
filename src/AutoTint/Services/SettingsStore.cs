using System;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using AutoTint.Models;

namespace AutoTint.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under %APPDATA%\AutoTint.
///
/// Saving is debounced because the settings that change most (tint strength, window
/// bounds) change continuously while a slider or a window edge is being dragged, and
/// writing the file on every step would mean hundreds of writes per gesture.
/// </summary>
internal sealed class SettingsStore : IDisposable
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly DispatcherTimer _debounce;
    private readonly string _path;
    private AppSettings _pending = new();

    /// <summary>
    /// <paramref name="path"/> defaults to the real per-user location; tests pass a
    /// temporary file so they never touch the user's actual settings.
    /// </summary>
    internal SettingsStore(string? path = null)
    {
        _path = path ?? FilePath;
        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = SaveDelay };
        _debounce.Tick += (_, _) => Flush();
    }

    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoTint");

    internal static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    /// <summary>
    /// Reads the saved settings. Any problem -- missing file, unreadable directory,
    /// truncated or hand-edited JSON -- yields defaults rather than an exception: failing
    /// to start because a preferences file got corrupted would be a poor trade.
    /// </summary>
    internal static AppSettings LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Reads the settings this store is bound to.</summary>
    internal AppSettings Load() => LoadFrom(_path);

    /// <summary>Queues a save, restarting the debounce window.</summary>
    internal void Save(AppSettings settings)
    {
        _pending = settings;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Writes immediately. Called on shutdown so nothing pending is lost.</summary>
    internal void Flush()
    {
        _debounce.Stop();

        try
        {
            string? parent = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(_path, JsonSerializer.Serialize(_pending, SerializerOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing preferences is not worth taking the app down for.
        }
    }

    public void Dispose()
    {
        if (_debounce.IsEnabled) Flush();
        _debounce.Stop();
    }
}
