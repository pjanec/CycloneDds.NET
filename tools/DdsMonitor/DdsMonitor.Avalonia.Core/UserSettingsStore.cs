using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Persists user settings to <c>%APPDATA%\DdsMonitor\settings.json</c>.
/// Saves are debounced: if <see cref="SaveAsync"/> is called again within 500 ms the
/// previous pending write is cancelled and replaced by the new one.
/// </summary>
public sealed class UserSettingsStore : IUserSettings
{
    private readonly string _filePath;
    private readonly Dictionary<string, Dictionary<string, JsonElement>> _data = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    private CancellationTokenSource? _pendingSave;

    /// <summary>
    /// Initialises a new instance with the default path:
    /// <c>%APPDATA%\DdsMonitor\settings.json</c>.
    /// </summary>
    public UserSettingsStore() : this(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DdsMonitor",
            "settings.json"))
    {
    }

    /// <summary>Initialises a new instance with a custom file path (for testing).</summary>
    public UserSettingsStore(string filePath)
    {
        _filePath = filePath;
        TryLoad();
    }

    /// <inheritdoc/>
    public T Get<T>(string section, string key, T defaultValue)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var el))
            {
                try { return el.Deserialize<T>() ?? defaultValue; }
                catch { /* fall through */ }
            }
        }
        return defaultValue;
    }

    /// <inheritdoc/>
    public void Set<T>(string section, string key, T value)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(section, out var sec))
                _data[section] = sec = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            sec[key] = JsonSerializer.SerializeToElement(value);
        }
    }

    /// <inheritdoc/>
    public Task SaveAsync()
    {
        CancellationTokenSource cts;
        lock (_lock)
        {
            _pendingSave?.Cancel();
            _pendingSave = cts = new CancellationTokenSource();
        }

        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return; // replaced by a later call
            }

            Dictionary<string, Dictionary<string, JsonElement>> snapshot;
            lock (_lock) snapshot = Copy(_data);

            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
        }, CancellationToken.None);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private void TryLoad()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
            if (loaded == null) return;
            lock (_lock)
            {
                foreach (var (section, values) in loaded)
                    _data[section] = values;
            }
        }
        catch { /* ignore corrupt file */ }
    }

    private static Dictionary<string, Dictionary<string, JsonElement>> Copy(
        Dictionary<string, Dictionary<string, JsonElement>> src)
    {
        var copy = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
        foreach (var (section, values) in src)
            copy[section] = new Dictionary<string, JsonElement>(values, StringComparer.Ordinal);
        return copy;
    }
}
