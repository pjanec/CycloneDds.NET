using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DdsMonitor.Engine.Ui;

/// <summary>
/// Default implementation of <see cref="IValueFormatterRegistry"/>.
/// Thread-safe — formatters may be registered from any thread (e.g. during startup).
/// </summary>
public sealed class ValueFormatterRegistry : IValueFormatterRegistry
{
    private const float AutoApplyThreshold = 0.8f;

    private readonly ConcurrentBag<IValueFormatter> _formatters = new();

    /// <inheritdoc />
    public void Register(IValueFormatter formatter)
    {
        if (formatter == null) throw new ArgumentNullException(nameof(formatter));
        _formatters.Add(formatter);
    }

    /// <inheritdoc />
    public IReadOnlyList<IValueFormatter> GetFormatters(Type type, object? value)
    {
        return _formatters
            .Select(f => (Formatter: f, Score: f.GetScore(type, value)))
            .Where(t => t.Score > 0f)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Formatter)
            .ToList();
    }

    /// <inheritdoc />
    public IValueFormatter? GetAutoFormatter(Type type, object? value)
    {
        return _formatters
            .Select(f => (Formatter: f, Score: f.GetScore(type, value)))
            .Where(t => t.Score >= AutoApplyThreshold)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Formatter)
            .FirstOrDefault();
    }
}
