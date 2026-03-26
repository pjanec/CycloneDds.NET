using System;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Registry that allows plugins to contribute custom HTML tooltip content for specific
/// CLR types and values.  The portal checks this registry before rendering the default
/// JSON tooltip.
/// </summary>
public interface ITooltipProviderRegistry
{
    /// <summary>
    /// Registers a tooltip HTML provider.  The provider receives the CLR type and the
    /// current value being hovered; it should return an HTML string, or <c>null</c> to
    /// defer to the next registered provider or the default tooltip.
    /// </summary>
    /// <param name="htmlProvider">
    /// A function that accepts (<see cref="Type"/>, <see cref="object?"/>) and returns
    /// an HTML string or <c>null</c>.
    /// </param>
    void RegisterProvider(Func<Type, object?, string?> htmlProvider);

    /// <summary>
    /// Iterates all registered providers in registration order and returns the first
    /// non-null HTML string, or <c>null</c> when no provider handles the combination.
    /// </summary>
    /// <param name="type">The CLR type of the value being hovered.</param>
    /// <param name="value">The value being hovered (may be <c>null</c>).</param>
    string? GetTooltipHtml(Type type, object? value);
}
