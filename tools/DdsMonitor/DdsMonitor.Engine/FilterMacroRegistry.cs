using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Thread-safe implementation of <see cref="IFilterMacroRegistry"/>.
/// </summary>
public sealed class FilterMacroRegistry : IFilterMacroRegistry
{
    private readonly Dictionary<string, Func<object?[], object?>> _macros =
        new(StringComparer.Ordinal);

    private readonly object _sync = new();

    /// <inheritdoc />
    public void RegisterMacro(string name, Func<object?[], object?> impl)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(impl);

        lock (_sync)
        {
            _macros[name] = impl;
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, Func<object?[], object?>> GetMacros()
    {
        lock (_sync)
        {
            return new Dictionary<string, Func<object?[], object?>>(_macros, StringComparer.Ordinal);
        }
    }
}
