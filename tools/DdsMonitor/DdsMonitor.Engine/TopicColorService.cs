using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DdsMonitor.Engine;

/// <summary>
/// Manages deterministic auto-assigned topic colors and persisted user color overrides.
///
/// Auto-assignment: a stable hash of the topic's ShortName selects an index from a
/// 12-color CSS-variable palette defined in app.css. Both light and dark palettes are
/// defined so colors remain readable in either theme.
///
/// User overrides: stored as a dictionary of ShortName→hex-color, persisted to a
/// JSON file alongside workspace.json in the DdsMonitor application data directory.
/// </summary>
public sealed class TopicColorService
{
    private const int PaletteSize = 12;
    private const string OverridesFileName = "topic-colors.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _persistPath;
    private readonly Dictionary<string, string> _userOverrides;

    /// <summary>
    /// Raised when any color override is changed.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="TopicColorService"/>.
    /// </summary>
    public TopicColorService(IWorkspaceState workspaceState)
    {
        if (workspaceState == null) throw new ArgumentNullException(nameof(workspaceState));

        var dir = Path.GetDirectoryName(workspaceState.WorkspaceFilePath) ?? string.Empty;
        _persistPath = Path.Combine(dir, OverridesFileName);
        _userOverrides = Load();
    }

    /// <summary>
    /// Gets the user-configured color override for <paramref name="shortName"/>, or
    /// <c>null</c> if no override has been set (auto mode).
    /// </summary>
    public string? GetUserColor(string shortName)
    {
        return _userOverrides.TryGetValue(shortName, out var color) ? color : null;
    }

    /// <summary>
    /// Returns the 0-based palette index deterministically computed from
    /// <paramref name="shortName"/>.
    /// </summary>
    public int GetAutoColorIndex(string shortName)
    {
        var hash = 0;
        foreach (var ch in shortName)
        {
            hash = hash * 31 + ch;
        }

        return Math.Abs(hash) % PaletteSize;
    }

    /// <summary>
    /// Returns a CSS <c>var(--topic-color-N)</c> expression for the auto-assigned palette
    /// slot of <paramref name="shortName"/>.
    /// </summary>
    public string GetAutoColorVar(string shortName)
        => $"var(--topic-color-{GetAutoColorIndex(shortName)})";

    /// <summary>
    /// Returns the effective color for <paramref name="shortName"/> as a CSS value:
    /// the user override if one is set, otherwise the auto-assigned CSS variable.
    /// </summary>
    public string GetColorValue(string shortName)
    {
        var userColor = GetUserColor(shortName);
        return userColor ?? GetAutoColorVar(shortName);
    }

    /// <summary>
    /// Returns an inline <c>color</c> style string ready to embed in a
    /// <c>style</c> HTML attribute.
    /// </summary>
    public string GetColorStyle(string shortName)
        => $"color: {GetColorValue(shortName)};";

    /// <summary>
    /// Sets a user-defined color override for <paramref name="shortName"/>.
    /// Pass <c>null</c> or call <see cref="ResetUserColor"/> to revert to auto.
    /// </summary>
    public void SetUserColor(string shortName, string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            _userOverrides.Remove(shortName);
        }
        else
        {
            _userOverrides[shortName] = colorHex;
        }

        Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Removes any user-defined color override for <paramref name="shortName"/>,
    /// reverting the topic to auto-assigned color.
    /// </summary>
    public void ResetUserColor(string shortName)
    {
        _userOverrides.Remove(shortName);
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Returns a snapshot of all user-defined color overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllOverrides()
        => _userOverrides;

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(_persistPath))
            {
                var json = File.ReadAllText(_persistPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
                return loaded ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
        catch
        {
            // Return empty on any I/O or parse error.
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_userOverrides, SerializerOptions);
            File.WriteAllText(_persistPath, json);
        }
        catch
        {
            // Ignore persistence errors.
        }
    }
}
