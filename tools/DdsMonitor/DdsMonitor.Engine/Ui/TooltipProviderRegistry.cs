using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Thread-safe implementation of <see cref="ITooltipProviderRegistry"/>.
/// Iterates providers in registration order; returns the first non-null HTML result.
/// </summary>
public sealed class TooltipProviderRegistry : ITooltipProviderRegistry
{
    private readonly List<Func<Type, object?, string?>> _providers = new();
    private readonly object _sync = new();

    /// <inheritdoc />
    public void RegisterProvider(Func<Type, object?, string?> htmlProvider)
    {
        ArgumentNullException.ThrowIfNull(htmlProvider);
        lock (_sync)
        {
            _providers.Add(htmlProvider);
        }
    }

    /// <inheritdoc />
    public string? GetTooltipHtml(Type type, object? value)
    {
        ArgumentNullException.ThrowIfNull(type);

        List<Func<Type, object?, string?>> snapshot;
        lock (_sync)
        {
            snapshot = new List<Func<Type, object?, string?>>(_providers);
        }

        foreach (var provider in snapshot)
        {
            var result = provider(type, value);
            if (result != null)
                return result;
        }

        return null;
    }
}
