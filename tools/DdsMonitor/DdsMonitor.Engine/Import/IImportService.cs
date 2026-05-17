using System.Collections.Generic;
using System.Threading;

namespace DdsMonitor.Engine.Import;

/// <summary>
/// Parses a JSON export file stream-by-stream and reconstructs
/// <see cref="SampleData"/> records without loading the full document into memory.
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Asynchronously enumerates <see cref="SampleData"/> records from a JSON
    /// export file produced by <see cref="Export.IExportService"/>.
    /// Records whose topic type cannot be resolved at runtime are silently skipped.
    /// </summary>
    /// <param name="filePath">Absolute path to the JSON export file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    IAsyncEnumerable<SampleData> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
