using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine.Export;

/// <summary>
/// Registry for custom export formats that plugins can contribute.
/// Plugins call <see cref="RegisterFormat"/> during initialization; the
/// <c>SamplesPanel</c> reads <see cref="GetFormats"/> to populate the export dropdown.
/// </summary>
public interface IExportFormatRegistry
{
    /// <summary>
    /// Registers a named export format.
    /// </summary>
    /// <param name="label">Short display label shown in the export dropdown.</param>
    /// <param name="exportFunc">
    /// Async function that receives the current filtered samples, the user-chosen
    /// file path, and a <see cref="CancellationToken"/>. The function is responsible
    /// for writing the file.
    /// </param>
    void RegisterFormat(
        string label,
        System.Func<IReadOnlyList<SampleData>, string, CancellationToken, Task> exportFunc);

    /// <summary>Returns all registered export formats in registration order.</summary>
    IReadOnlyList<ExportFormatEntry> GetFormats();
}
