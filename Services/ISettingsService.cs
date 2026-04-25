// File: Services/ISettingsService.cs
using System;

namespace WinShot.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Persist <see cref="Current"/> to disk.</summary>
    void Save();

    /// <summary>Fires after <see cref="Save"/> so open windows can refresh bindings.</summary>
    event EventHandler? SettingsChanged;
}
