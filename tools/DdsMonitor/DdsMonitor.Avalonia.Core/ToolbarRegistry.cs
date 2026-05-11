using System;
using System.Collections.Generic;

namespace DdsMonitor.Avalonia.Core;

/// <summary>
/// Thread-safe implementation of <see cref="IToolbarRegistry"/>.
/// </summary>
public sealed class ToolbarRegistry : IToolbarRegistry
{
    private readonly object _lock = new();
    private readonly List<ToolbarEntry> _entries = new();

    /// <inheritdoc/>
    public IReadOnlyList<ToolbarEntry> Entries
    {
        get
        {
            lock (_lock) return _entries.ToArray();
        }
    }

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public void Register(string id, Action onClick, string? iconKey = null, string tooltip = "")
    {
        if (id == null) throw new ArgumentNullException(nameof(id));
        if (onClick == null) throw new ArgumentNullException(nameof(onClick));

        lock (_lock)
        {
            // Replace existing entry with the same id, or append.
            var existing = _entries.FindIndex(e => e.Id == id);
            var entry = new ToolbarEntry(id, onClick, iconKey, tooltip);
            if (existing >= 0)
                _entries[existing] = entry;
            else
                _entries.Add(entry);
        }

        Changed?.Invoke();
    }
}
