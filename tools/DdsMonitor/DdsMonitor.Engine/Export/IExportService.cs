using System;
using System.Threading;
using System.Threading.Tasks;

namespace DdsMonitor.Engine.Export;

/// <summary>
/// Streams the active <see cref="ISampleStore"/> to a JSON file on disk.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports all samples from every topic to the specified file.
    /// </summary>
    /// <param name="filePath">Absolute path of the output JSON file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ExportAllAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports only the samples belonging to the specified topic type.
    /// </summary>
    /// <param name="filePath">Absolute path of the output JSON file.</param>
    /// <param name="topicType">CLR type of the DDS topic to export.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ExportTopicAsync(string filePath, Type topicType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the provided sequence of samples to the specified file.
    /// </summary>
    /// <param name="filePath">Absolute path of the output JSON file.</param>
    /// <param name="samples">The samples to export.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ExportSamplesAsync(string filePath, System.Collections.Generic.IReadOnlyList<SampleData> samples, CancellationToken cancellationToken = default);
}
