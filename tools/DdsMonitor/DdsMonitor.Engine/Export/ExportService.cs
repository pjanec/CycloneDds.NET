using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DdsMonitor.Engine;

namespace DdsMonitor.Engine.Export;

/// <summary>
/// Streams the <see cref="ISampleStore"/> to a JSON file using <see cref="Utf8JsonWriter"/>
/// so that the entire serialised payload is never held in memory at once.
///
/// Memory-safety guarantee (DMON-037):
///   The writer operates directly on a <see cref="FileStream"/>; each call to
///   <see cref="Utf8JsonWriter.WriteStartObject"/> / <see cref="Utf8JsonWriter.WriteEndObject"/>
///   produces bytes that are written (or buffered by the OS page-cache) immediately.
///   We flush the internal 4 KiB <see cref="Utf8JsonWriter"/> buffer to the 64 KiB
///   <see cref="FileStream"/> buffer every <see cref="FlushEveryN"/> records, and
///   the stream itself is flushed to disk on disposal.  No <c>List&lt;string&gt;</c>,
///   <c>StringBuilder</c>, or intermediate JSON DOM ever accumulates the full output,
///   making an <see cref="OutOfMemoryException"/> O(1) in peak allocation regardless
///   of how many records exist in the store.
/// </summary>
public sealed class ExportService : IExportService
{
    private const int FileBufferSize = 65_536;
    private const int FlushEveryN = 500;

    private static readonly JsonSerializerOptions PayloadSerializerOptions = DdsJsonOptions.Export;

    private readonly ISampleStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportService"/>.
    /// </summary>
    public ExportService(ISampleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task ExportAllAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var snapshot = _store.AllSamples;
        return WriteAsync(filePath, snapshot, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ExportTopicAsync(string filePath, Type topicType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        var topicSamples = _store.GetTopicSamples(topicType);
        await WriteAsync(filePath, topicSamples.Samples, cancellationToken);
    }

    /// <inheritdoc />
    public Task ExportSamplesAsync(string filePath, IReadOnlyList<SampleData> samples, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (samples == null)
        {
            throw new ArgumentNullException(nameof(samples));
        }

        return WriteAsync(filePath, samples, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core streaming writer
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task WriteAsync(
        string filePath,
        IReadOnlyList<SampleData> samples,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileBufferSize,
            useAsync: true);

        await using var writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });

        writer.WriteStartArray();

        var written = 0;
        foreach (var sample in samples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteSampleRecord(writer, sample);
            written++;

            // Flush the Utf8JsonWriter buffer to the FileStream periodically.
            // This keeps the writer's internal allocation constant at ~4 KiB
            // regardless of total record count.
            if (written % FlushEveryN == 0)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
        await writer.FlushAsync(cancellationToken);
    }

    private static void WriteSampleRecord(Utf8JsonWriter writer, SampleData sample)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Ordinal", sample.Ordinal);
        writer.WriteString("TopicTypeName", sample.TopicMetadata.TopicType.FullName);
        writer.WriteString("Timestamp", sample.Timestamp.ToUniversalTime());

        // ME1-T07: Participant stamping fields.
        writer.WriteNumber("DomainId", sample.DomainId);
        writer.WriteString("PartitionName", sample.PartitionName);

        // Sender
        if (sample.Sender != null)
        {
            writer.WriteStartObject("Sender");
            writer.WriteNumber("ProcessId", sample.Sender.ProcessId);

            if (sample.Sender.MachineName != null)
            {
                writer.WriteString("MachineName", sample.Sender.MachineName);
            }

            if (sample.Sender.IpAddress != null)
            {
                writer.WriteString("IpAddress", sample.Sender.IpAddress);
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("Sender");
        }

        // Instance state – needed to restore Alive/Disposed/NoWriters on import.
        writer.WriteString("InstanceState", sample.SampleInfo.InstanceState.ToString());

        // Payload – serialised inline as a nested JSON object so no intermediate
        // string allocation spans the entire payload tree.
        writer.WritePropertyName("Payload");
        var payloadElement = JsonSerializer.SerializeToElement(
            sample.Payload,
            sample.Payload.GetType(),
            PayloadSerializerOptions);
        payloadElement.WriteTo(writer);

        writer.WriteEndObject();
    }
}
