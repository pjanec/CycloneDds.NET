using System;

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
}

/// <summary>
/// Result of a filter compilation.
/// </summary>
public sealed record FilterResult(bool IsValid, Func<SampleData, bool>? Predicate, string? ErrorMessage);
