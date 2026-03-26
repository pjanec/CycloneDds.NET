using System.Linq;
using Microsoft.AspNetCore.Components;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Thread-safe implementation of <see cref="ISampleViewRegistry"/>.
/// Type-hierarchy lookup order: exact type → base types (most-derived first) → interfaces.
/// <para>
/// <strong>Interface resolution rule (deterministic):</strong> when the queried type
/// implements multiple registered interfaces, the interface whose
/// <see cref="Type.FullName"/> sorts first alphabetically (ordinal) is selected.
/// This guarantees a stable winner regardless of CLR reflection ordering.
/// </para>
/// </summary>
public sealed class SampleViewRegistry : ISampleViewRegistry
{
    private readonly Dictionary<Type, RenderFragment<SampleData>> _viewers = new();
    private readonly object _sync = new();

    /// <inheritdoc />
    public void Register(Type type, RenderFragment<SampleData> viewer)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(viewer);

        lock (_sync)
        {
            _viewers[type] = viewer;
        }
    }

    /// <inheritdoc />
    public RenderFragment<SampleData>? GetViewer(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        Dictionary<Type, RenderFragment<SampleData>> snapshot;
        lock (_sync)
        {
            snapshot = new Dictionary<Type, RenderFragment<SampleData>>(_viewers);
        }

        // Walk hierarchy: exact type first, then base types (most-derived first), then interfaces.
        var candidate = type;
        while (candidate != null)
        {
            if (snapshot.TryGetValue(candidate, out var viewer))
                return viewer;
            candidate = candidate.BaseType;
        }

        // Interface walk: interfaces are sorted by FullName for deterministic resolution
        // when multiple registered interfaces could match (e.g. two interfaces both
        // implemented by the queried type).  The interface whose FullName sorts first
        // alphabetically wins.
        foreach (var iface in type.GetInterfaces().OrderBy(i => i.FullName, StringComparer.Ordinal))
        {
            if (snapshot.TryGetValue(iface, out var viewer))
                return viewer;
        }

        return null;
    }
}
