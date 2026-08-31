using System;
using System.IO;
using AutoTint.Models;
using AutoTint.Services;

namespace AutoTint.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "autotint-tests-" + Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    [Fact]
    public void MissingFileYieldsDefaults()
    {
        AppSettings settings = SettingsStore.LoadFrom(_path);

        Assert.Equal(50, settings.Strength);
        Assert.Equal("black", settings.ColourId);
        Assert.True(settings.HideFromCapture);
        Assert.False(settings.HasBounds);
    }

    [Fact]
    public void CorruptJsonYieldsDefaultsRatherThanThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not json");

        AppSettings settings = SettingsStore.LoadFrom(_path);

        Assert.Equal(50, settings.Strength);
    }

    [Fact]
    public void FileContainingOnlyNullYieldsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "null");

        Assert.Equal("black", SettingsStore.LoadFrom(_path).ColourId);
    }

    [Fact]
    public void SavedSettingsSurviveARoundTrip()
    {
        using var store = new SettingsStore(_path);
        store.Save(new AppSettings
        {
            Strength = 72,
            StrengthBeforeOff = 72,
            ColourId = "warm",
            Expanded = true,
            HideFromCapture = false,
            BoundsLeft = -1200,
            BoundsTop = 340,
            BoundsWidth = 800,
            BoundsHeight = 500,
        });
        store.Flush();

        AppSettings loaded = SettingsStore.LoadFrom(_path);

        Assert.Equal(72, loaded.Strength);
        Assert.Equal("warm", loaded.ColourId);
        Assert.True(loaded.Expanded);
        Assert.False(loaded.HideFromCapture);
        Assert.True(loaded.HasBounds);

        // Negative coordinates are ordinary for a monitor left of the primary.
        Assert.Equal(-1200, loaded.BoundsLeft);
    }

    [Fact]
    public void FlushCreatesTheDirectoryOnFirstRun()
    {
        using var store = new SettingsStore(_path);
        store.Save(new AppSettings { Strength = 30 });
        store.Flush();

        Assert.True(File.Exists(_path));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup is best effort.
        }

        GC.SuppressFinalize(this);
    }
}
