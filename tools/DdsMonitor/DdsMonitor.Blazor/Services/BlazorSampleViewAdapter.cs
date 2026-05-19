using System;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.AspNetCore.Components;

namespace DdsMonitor.Services;

/// <summary>
/// Blazor-specific adapter that wraps <see cref="ISampleViewRegistry"/> and provides
/// <see cref="RenderFragment{SampleData}"/> for registered payload types.
/// </summary>
public sealed class BlazorSampleViewAdapter
{
    private readonly ISampleViewRegistry _registry;

    public BlazorSampleViewAdapter(ISampleViewRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Returns a Blazor <see cref="RenderFragment{SampleData}"/> for the given payload type,
    /// or <c>null</c> to fall back to the default tree view.
    /// </summary>
    public RenderFragment<SampleData>? GetBlazorViewer(Type type)
    {
        var factory = _registry.GetViewer(type);
        if (factory == null) return null;
        return sample =>
        {
            var viewer = factory(sample);
            return viewer as RenderFragment
                ?? throw new InvalidOperationException(
                    $"Sample viewer for '{type.FullName}' did not return a Blazor RenderFragment.");
        };
    }
}
