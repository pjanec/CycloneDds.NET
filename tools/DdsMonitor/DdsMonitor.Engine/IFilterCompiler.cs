using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Compiles user filter expressions into executable predicates.
/// </summary>
public interface IFilterCompiler
{
    /// <summary>
    /// Compiles the provided expression into a predicate.
    /// </summary>
    FilterResult Compile(string expression, TopicMetadata? topicMeta);

    /// <summary>
    /// Compiles the provided expression into a predicate, binding extra parameter values
    /// referenced as <c>@0</c>, <c>@1</c>, … in the expression (used by string-method
    /// operators such as <c>StartsWith</c> / <c>EndsWith</c> / <c>Contains</c>).
    /// </summary>
    FilterResult Compile(string expression, TopicMetadata? topicMeta, IReadOnlyList<object?>? paramValues);
}

/// <summary>
/// Result of a filter compilation.
/// </summary>
public sealed record FilterResult(bool IsValid, Func<SampleData, bool>? Predicate, string? ErrorMessage);
