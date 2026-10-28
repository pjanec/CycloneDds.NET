using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;

namespace DdsMonitor.Engine.Import;

/// <summary>
/// Reconstructs <see cref="SampleData"/> records from a JSON file produced by
/// <see cref="Export.ExportService"/> without loading the whole document into memory.
///
/// Parsing strategy (DMON-038):
///   <see cref="JsonSerializer.DeserializeAsyncEnumerable{T}"/> uses an internal
///   pipe-backed <see cref="System.Text.Json.Utf8JsonReader"/> that reads the
///   <see cref="FileStream"/> in 4 KiB chunks and tokenizes the array one JSON
///   object at a time.  Only a single <see cref="SampleExportRecord"/> (≈ a few
///   hundred bytes) is kept alive per iteration step, so heap pressure is O(1)
///   regardless of file size.
///
///   Polymorphic payload reconstruction:
///   Each record carries a <c>TopicTypeName</c> (assembly-qualified CLR type string).
///   From it we derive the runtime <see cref="Type"/> at import time, then call
///   <see cref="JsonElement.Deserialize(Type, JsonSerializerOptions?)"/> on the
///   captured payload token – a single small object allocation per sample.
///   Unknown or missing types are skipped gracefully so a partially-stale export
///   file does not abort the whole import.
/// </summary>
public sealed class ImportService : IImportService
{
    private const int FileBufferSize = 65_536;

    private static readonly JsonSerializerOptions SerializerOptions = DdsJsonOptions.Import;

    /// <inheritdoc />
    public async IAsyncEnumerable<SampleData> ImportAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileBufferSize,
            useAsync: true);

        var records = await IsJsonLinesAsync(fileStream, cancellationToken)
            ? ReadJsonLinesAsync(fileStream, cancellationToken)
            : JsonSerializer.DeserializeAsyncEnumerable<SampleExportRecord>(fileStream, SerializerOptions, cancellationToken);

        await foreach (var record in records)
        {
            if (record == null)
            {
                continue;
            }

            var sample = TryReconstructSample(record);
            if (sample != null)
            {
                yield return sample;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JSON Lines (headless RecordFormat=JsonLines)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A JSON array export starts with <c>[</c>, a JSON Lines capture with <c>{</c>.
    /// Leaves the stream at its start.
    /// </summary>
    private static async Task<bool> IsJsonLinesAsync(FileStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer, cancellationToken);
        stream.Position = 0;
        int i = 0;
        if (read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            i = 3; // UTF-8 BOM
        for (; i < read; i++)
        {
            if (!char.IsWhiteSpace((char)buffer[i]))
                return buffer[i] == (byte)'{';
        }
        return false;
    }

    private static async IAsyncEnumerable<SampleExportRecord?> ReadJsonLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            SampleExportRecord? record;
            try
            {
                record = JsonSerializer.Deserialize<SampleExportRecord>(line, SerializerOptions);
            }
            catch (JsonException)
            {
                // A capture still being written (or cut off by a crash) can end in a partial line.
                continue;
            }
            yield return record;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Payload reconstruction
    // ─────────────────────────────────────────────────────────────────────────

    private static SampleData? TryReconstructSample(SampleExportRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TopicTypeName))
        {
            return null;
        }

        // Resolve the CLR topic type.  New exports use FullName; older exports used
        // AssemblyQualifiedName.  Try the direct lookup first (works for AQN); if that
        // returns null, scan every loaded assembly for a matching FullName.
        Type? topicType;
        try
        {
            topicType = Type.GetType(record.TopicTypeName, throwOnError: false);
        }
        catch
        {
            return null;
        }

        if (topicType == null)
        {
            // FullName-only lookup: search loaded assemblies.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                topicType = asm.GetType(record.TopicTypeName, throwOnError: false);
                if (topicType != null)
                {
                    break;
                }
            }
        }

        if (topicType == null)
        {
            return null;
        }

        // Build TopicMetadata – fails if the type is not decorated with [DdsTopicAttribute].
        TopicMetadata meta;
        try
        {
            meta = new TopicMetadata(topicType);
        }
        catch
        {
            return null;
        }

        // Deserialize the raw JSON token into the concrete payload type.
        object? payload;
        try
        {
            payload = record.Payload.Deserialize(topicType, SerializerOptions)
                      ?? Activator.CreateInstance(topicType);
        }
        catch
        {
            return null;
        }

        if (payload == null)
        {
            return null;
        }

        var sender = record.Sender != null
            ? new SenderIdentity
            {
                ProcessId = record.Sender.ProcessId,
                ProcessName = record.Sender.ProcessName,
                MachineName = record.Sender.MachineName,
                IpAddress = record.Sender.IpAddress,
                AppDomainId = record.Sender.AppDomainId,
                AppInstanceId = record.Sender.AppInstanceId
            }
            : null;

        // Reconstruct InstanceState from the serialized string.
        // Old Batch-24 exports lack this field; they default to Alive.
        var instanceState = DdsInstanceState.Alive;
        if (!string.IsNullOrEmpty(record.InstanceState))
        {
            Enum.TryParse(record.InstanceState, ignoreCase: true, out instanceState);
        }

        return new SampleData
        {
            Ordinal = record.Ordinal,
            Payload = payload,
            TopicMetadata = meta,
            Timestamp = record.Timestamp,
            SizeBytes = record.SizeBytes,
            Sender = sender,
            SampleInfo = new DdsApi.DdsSampleInfo { InstanceState = instanceState },
            // ME1-T07: Participant stamping fields (zero/empty for pre-T07 export files).
            DomainId = record.DomainId,
            PartitionName = record.PartitionName ?? string.Empty
        };
    }
}
