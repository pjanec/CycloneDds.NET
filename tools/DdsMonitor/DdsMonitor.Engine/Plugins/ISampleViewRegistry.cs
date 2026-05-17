using Microsoft.AspNetCore.Components;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Registry that maps CLR payload types to custom Blazor viewers shown in the Detail Panel.
/// Plugins call <see cref="Register"/> during initialization to replace the default tree view
/// with a domain-specific component for a particular payload type.
/// </summary>
public interface ISampleViewRegistry
{
    /// <summary>
    /// Registers a Blazor RenderFragment that fully replaces the default tree view
    /// in the Detail Panel for samples whose payload type matches <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The exact CLR payload type to match.</param>
    /// <param name="viewer">A <see cref="RenderFragment{T}"/> that receives the full <see cref="SampleData"/>.</param>
    void Register(Type type, RenderFragment<SampleData> viewer);

    /// <summary>
    /// Returns the custom viewer for the given payload type, or <c>null</c> to fall back to
    /// the default tree. The lookup walks the type hierarchy: exact type, then base types,
    /// then interfaces.
    /// </summary>
    /// <param name="type">The runtime CLR type of the sample payload.</param>
    /// <returns>A registered viewer, or <c>null</c> if none matches.</returns>
    RenderFragment<SampleData>? GetViewer(Type type);
}
