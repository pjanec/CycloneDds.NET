using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine.Export;

/// <summary>
/// Thread-safe default implementation of <see cref="IExportFormatRegistry"/>.
/// </summary>
public sealed class ExportFormatRegistry : IExportFormatRegistry
{
    private readonly List<ExportFormatEntry> _formats = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void RegisterFormat(
        string label,
        Func<IReadOnlyList<SampleData>, string, CancellationToken, Task> exportFunc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(exportFunc);

        lock (_lock)
        {
            _formats.Add(new ExportFormatEntry(label, exportFunc));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExportFormatEntry> GetFormats()
    {
        lock (_lock)
        {
            return _formats.ToArray();
        }
    }
}
