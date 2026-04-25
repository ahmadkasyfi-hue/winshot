// File: Services/SettingsService.cs
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WinShot.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> as JSON under
/// <c>%APPDATA%\WinShot\settings.json</c>. Load failures fall back to defaults
/// so a bad settings file never prevents the app from starting — the old file
/// is kept on disk as <c>settings.json.bak</c> so the user can recover by hand.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; } = new();
    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinShot");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");

        Load();
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            _logger.LogInformation("No settings file — using defaults ({Path}).", _path);
            Save(); // Write defaults so user can find & edit it.
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, s_json);
            if (loaded is not null) Current = loaded;
            _logger.LogInformation("Loaded settings from {Path}", _path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings; keeping defaults and backing up bad file.");
            try
            {
                File.Copy(_path, _path + ".bak", overwrite: true);
            }
            catch { /* best effort */ }
            Save();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, s_json);
            File.WriteAllText(_path, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _path);
            throw;
        }
    }
}
