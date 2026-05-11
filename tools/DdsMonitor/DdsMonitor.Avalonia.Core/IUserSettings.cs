using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Persists user-level settings across sessions.
/// </summary>
public interface IUserSettings
{
    /// <summary>Gets a setting value, returning <paramref name="defaultValue"/> if not set.</summary>
    T Get<T>(string section, string key, T defaultValue);

    /// <summary>Sets a setting value in memory.</summary>
    void Set<T>(string section, string key, T value);

    /// <summary>
    /// Persists current settings to disk asynchronously.
    /// Calls within 500 ms of each other are debounced (only the last write happens).
    /// </summary>
    Task SaveAsync();
}
