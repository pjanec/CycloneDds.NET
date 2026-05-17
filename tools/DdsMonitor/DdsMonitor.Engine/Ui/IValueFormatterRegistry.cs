using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Tier 1 formatter registry.  Manages a collection of <see cref="IValueFormatter"/>
/// instances and exposes discovery methods used by the UI to build context-menu options
/// and to auto-apply high-score formatters.
/// </summary>
public interface IValueFormatterRegistry
{
    /// <summary>Registers a <see cref="IValueFormatter"/> with the registry.</summary>
    void Register(IValueFormatter formatter);

    /// <summary>
    /// Returns all formatters whose <see cref="IValueFormatter.GetScore"/> for the given
    /// <paramref name="type"/> and <paramref name="value"/> is greater than zero, ordered
    /// by descending score.
    /// </summary>
    IReadOnlyList<IValueFormatter> GetFormatters(Type type, object? value);

    /// <summary>
    /// Returns the best-matching formatter whose score is <c>&gt;= 0.8</c>, or <c>null</c>
    /// when no formatter meets the auto-apply threshold.
    /// </summary>
    IValueFormatter? GetAutoFormatter(Type type, object? value);
}
