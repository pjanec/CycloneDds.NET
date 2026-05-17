using System;
using System.Collections.Generic;
using CycloneDDS.Schema.Formatting;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Tier 1 value formatter contract.  Implementations provide custom text and token-based
/// rendering for a specific CLR type, allowing rich inline previews in the DDS Monitor UI.
/// </summary>
public interface IValueFormatter
{
    /// <summary>A short human-readable name shown in the context menu (e.g. "DIS Entity ID").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Returns a score in [0, 1] indicating how well this formatter applies to the given
    /// <paramref name="type"/> / <paramref name="value"/> combination.
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>&gt;= 0.8</c> — formatter is auto-applied as the default.
    ///   </description></item>
    ///   <item><description>
    ///     <c>&gt; 0</c> and <c>&lt; 0.8</c> — formatter is available as an alternative view.
    ///   </description></item>
    ///   <item><description>
    ///     <c>&lt;= 0</c> — formatter is not applicable.
    ///   </description></item>
    /// </list>
    /// </summary>
    float GetScore(Type type, object? value);

    /// <summary>Returns a plain-text representation of <paramref name="value"/>.</summary>
    string FormatText(object? value);

    /// <summary>Returns a syntax-highlighted token sequence for <paramref name="value"/>.</summary>
    IEnumerable<FormattedToken> FormatTokens(object? value);
}
