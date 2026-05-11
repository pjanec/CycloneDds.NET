using System;

namespace DdsMonitor.Engine.Plugins;

/// <summary>
/// Registry that maps CLR payload types to UI-agnostic viewer factories shown in the Detail Panel.
/// Plugins call <see cref="Register"/> during initialization to replace the default tree view
/// with a domain-specific component for a particular payload type.
/// The concrete control type (Blazor <c>RenderFragment</c>, Avalonia <c>Control</c>, etc.)
/// is determined by the host UI layer.
/// </summary>
public interface ISampleViewRegistry
{
    /// <summary>
    /// Registers a viewer factory that fully replaces the default tree view
    /// in the Detail Panel for samples whose payload type matches <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The exact CLR payload type to match.</param>
    /// <param name="viewer">A factory that receives the full <see cref="SampleData"/> and returns the UI control object.</param>
    void Register(Type type, Func<SampleData, object?> viewer);

    /// <summary>
    /// Returns the viewer factory for the given payload type, or <c>null</c> to fall back to
    /// the default tree. The lookup walks the type hierarchy: exact type, then base types,
    /// then interfaces.
    /// </summary>
    /// <param name="type">The runtime CLR type of the sample payload.</param>
    /// <returns>A registered viewer factory, or <c>null</c> if none matches.</returns>
    Func<SampleData, object?>? GetViewer(Type type);
}
