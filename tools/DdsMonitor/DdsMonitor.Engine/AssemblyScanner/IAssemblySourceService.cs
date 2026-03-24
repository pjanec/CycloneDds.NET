using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine.AssemblyScanner;

/// <summary>
/// Manages the list of user-configured external DLL assemblies that DDS Monitor
/// should inspect for topic types.
/// </summary>
public interface IAssemblySourceService
{
    /// <summary>
    /// Raised whenever the assembly source list changes (add, remove, reorder).
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// When <c>true</c> the assembly source list was set via CLI (<c>--AppSettings:TopicSources</c>)
    /// and any changes made during the session are not persisted to <c>assembly-sources.json</c>.
    /// </summary>
    bool IsCliOverride { get; }

    /// <summary>
    /// Gets the current ordered list of assembly source entries.
    /// </summary>
    IReadOnlyList<AssemblySourceEntry> Entries { get; }

    /// <summary>
    /// Adds a DLL path, persists the list, and scans the assembly for topic types.
    /// Does nothing if the path is already present.
    /// </summary>
    void Add(string dllPath);

    /// <summary>
    /// Removes the entry at the given index, persists, and raises <see cref="Changed"/>.
    /// </summary>
    void Remove(int index);

    /// <summary>
    /// Moves the entry at <paramref name="index"/> one position toward the front of the list.
    /// </summary>
    void MoveUp(int index);

    /// <summary>
    /// Moves the entry at <paramref name="index"/> one position toward the end of the list.
    /// </summary>
    void MoveDown(int index);

    /// <summary>
    /// Returns the subset of all registered topics that originate from the entry at
    /// <paramref name="entryIndex"/>.
    /// </summary>
    IReadOnlyList<TopicMetadata> GetTopicsForEntry(int entryIndex);
}
