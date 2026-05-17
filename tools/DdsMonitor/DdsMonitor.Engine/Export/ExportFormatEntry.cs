using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine.Export;

/// <summary>
/// Represents a single named export format registered with <see cref="IExportFormatRegistry"/>.
/// </summary>
/// <param name="Label">Short display label shown in the export dropdown.</param>
/// <param name="ExportFunc">
/// Async function that writes the current filtered samples to the specified file path.
/// </param>
public sealed record ExportFormatEntry(
    string Label,
    System.Func<IReadOnlyList<SampleData>, string, CancellationToken, Task> ExportFunc);
