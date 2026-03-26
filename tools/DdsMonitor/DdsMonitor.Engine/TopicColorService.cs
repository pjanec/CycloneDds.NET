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
/// User overrides: persisted to <c>workspace.json</c> under <c>PluginSettings["TopicColors"]</c>
/// via the workspace save/load event mechanism.  A one-time migration from the legacy
/// <c>topic-colors.json</c> sidecar file is performed if the workspace does not yet contain
/// the section.
/// </summary>
public sealed class TopicColorService
{
    private const int PaletteSize = 12;
    private const string WorkspaceKey = "TopicColors";
    private const string LegacyFileName = "topic-colors.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IEventBroker? _eventBroker;
    private readonly string _legacyFilePath;
    private readonly Dictionary<string, string> _userOverrides;
    private readonly List<Func<string, string?>> _programmaticRules = new();
    private readonly object _rulesLock = new();

    private IDisposable? _savingSub;
    private IDisposable? _loadedSub;

    /// <summary>
    /// Raised when any color override is changed.
    /// </summary>
    public event Action? OnChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="TopicColorService"/>.
    /// </summary>
    /// <param name="workspaceState">Provides the workspace file path for initial load and migration.</param>
    /// <param name="eventBroker">
    /// When provided, the service subscribes to <see cref="WorkspaceSavingEvent"/> and
    /// <see cref="WorkspaceLoadedEvent"/> so that color overrides are persisted inside
    /// <c>workspace.json</c>.  When <c>null</c> (e.g. unit tests) the service operates
    /// purely in-memory.
    /// </param>
    public TopicColorService(IWorkspaceState workspaceState, IEventBroker? eventBroker = null)
    {
        if (workspaceState == null) throw new ArgumentNullException(nameof(workspaceState));

        var dir = Path.GetDirectoryName(workspaceState.WorkspaceFilePath) ?? string.Empty;
        _legacyFilePath = Path.Combine(dir, LegacyFileName);
        _eventBroker = eventBroker;

        // Eagerly load initial state: prefer workspace.json, fall back to legacy file.
        _userOverrides = LoadFromWorkspace(workspaceState.WorkspaceFilePath)
                         ?? LoadFromLegacy()
                         ?? new Dictionary<string, string>(StringComparer.Ordinal);

        if (eventBroker != null)
        {
            _savingSub = eventBroker.Subscribe<WorkspaceSavingEvent>(OnWorkspaceSaving);
            _loadedSub = eventBroker.Subscribe<WorkspaceLoadedEvent>(OnWorkspaceLoaded);
        }
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
    /// Registers a programmatic color rule. The rule function receives the topic's
    /// <paramref name="shortName"/> and returns a CSS color string, or <c>null</c> to
    /// defer to the next rule or the auto-palette. Rules are evaluated after user overrides
    /// but before the auto-palette. Thread-safe.
    /// </summary>
    public void RegisterColorRule(Func<string, string?> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        lock (_rulesLock)
        {
            _programmaticRules.Add(rule);
        }
    }

    /// <summary>
    /// Returns the effective color for <paramref name="shortName"/> as a CSS value:
    /// user override wins first, then the first matching programmatic rule, then the
    /// auto-assigned CSS variable.
    /// </summary>
    public string GetEffectiveColor(string shortName)
    {
        // 1. User override takes top priority.
        var userColor = GetUserColor(shortName);
        if (userColor != null)
        {
            return userColor;
        }

        // 2. Programmatic rules in registration order.
        List<Func<string, string?>> snapshot;
        lock (_rulesLock)
        {
            snapshot = new List<Func<string, string?>>(_programmaticRules);
        }

        foreach (var rule in snapshot)
        {
            var result = rule(shortName);
            if (result != null)
            {
                return result;
            }
        }

        // 3. Auto-palette fallback.
        return GetAutoColorVar(shortName);
    }

    /// <summary>
    /// Returns the effective color for <paramref name="shortName"/> as a CSS value:
    /// the user override if one is set, otherwise the auto-assigned CSS variable.
    /// </summary>
    /// <remarks>Preserved for backward compatibility. Delegates to <see cref="GetEffectiveColor"/>.</remarks>
    public string GetColorValue(string shortName)
        => GetEffectiveColor(shortName);

    /// <summary>
    /// Returns an inline <c>color</c> style string ready to embed in a
    /// <c>style</c> HTML attribute.
    /// </summary>
    public string GetColorStyle(string shortName)
        => $"color: {GetEffectiveColor(shortName)};";

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

        _eventBroker?.Publish(new WorkspaceSaveRequestedEvent());
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Removes any user-defined color override for <paramref name="shortName"/>,
    /// reverting the topic to auto-assigned color.
    /// </summary>
    public void ResetUserColor(string shortName)
    {
        _userOverrides.Remove(shortName);
        _eventBroker?.Publish(new WorkspaceSaveRequestedEvent());
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Returns a snapshot of all user-defined color overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllOverrides()
        => _userOverrides;

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnWorkspaceSaving(WorkspaceSavingEvent e)
    {
        try
        {
            e.PluginSettings[WorkspaceKey] = new Dictionary<string, string>(_userOverrides);
        }
        catch { }
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent e)
    {
        if (e.PluginSettings.TryGetValue(WorkspaceKey, out var raw))
        {
            ApplyFromObject(raw);
        }
        else
        {
            TryMigrateFromLegacy();
        }
    }

    // ── Initial load helpers ──────────────────────────────────────────────────

    private Dictionary<string, string>? LoadFromWorkspace(string workspaceFilePath)
    {
        try
        {
            if (!File.Exists(workspaceFilePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(workspaceFilePath));
            if (!doc.RootElement.TryGetProperty("PluginSettings", out var ps)) return null;
            if (!ps.TryGetProperty(WorkspaceKey, out var colorsEl)) return null;
            var result = colorsEl.Deserialize<Dictionary<string, string>>(JsonOptions);
            return result is { Count: > 0 } ? result : null;
        }
        catch { return null; }
    }

    private Dictionary<string, string>? LoadFromLegacy()
    {
        try
        {
            if (!File.Exists(_legacyFilePath)) return null;
            var json = File.ReadAllText(_legacyFilePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return loaded is { Count: > 0 } ? loaded : null;
        }
        catch { return null; }
    }

    private void ApplyFromObject(object raw)
    {
        try
        {
            Dictionary<string, string>? dict;
            if (raw is JsonElement elem)
                dict = elem.Deserialize<Dictionary<string, string>>(JsonOptions);
            else
            {
                var json = JsonSerializer.Serialize(raw, JsonOptions);
                dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            }

            if (dict == null) return;
            _userOverrides.Clear();
            foreach (var kv in dict) _userOverrides[kv.Key] = kv.Value;
        }
        catch { }
    }

    private void TryMigrateFromLegacy()
    {
        var migrated = LoadFromLegacy();
        if (migrated == null) return;
        _userOverrides.Clear();
        foreach (var kv in migrated) _userOverrides[kv.Key] = kv.Value;
        try { File.Delete(_legacyFilePath); } catch { }
    }
}

