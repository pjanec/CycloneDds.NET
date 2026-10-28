using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DdsMonitor.Engine.Recording;

/// <summary>Where a headless recording writes its samples.</summary>
internal interface ICaptureSink : IDisposable
{
    /// <summary>The files written so far (empty when writing to stdout).</summary>
    IReadOnlyList<string> Files { get; }

    /// <summary>Bytes written so far, across all files.</summary>
    long BytesWritten { get; }

    void Write(SampleData sample, JsonElement payload);

    /// <summary>Pushes everything written so far to the file, so a reader sees it.</summary>
    void Flush();

    /// <summary>Completes the capture (closing the JSON array, if any) and releases the file.</summary>
    void Close();
}

internal static class CaptureRecord
{
    /// <summary>
    /// Writes one sample record. <paramref name="includeTopicName"/> adds the DDS topic name,
    /// which JSON Lines carries so each line can be matched without knowing the CLR type.
    /// </summary>
    public static void Write(Utf8JsonWriter writer, SampleData sample, JsonElement payload, bool includeTopicName)
    {
        writer.WriteStartObject();

        writer.WriteNumber("Ordinal", sample.Ordinal);
        if (includeTopicName)
            writer.WriteString("TopicName", sample.TopicMetadata.TopicName);
        writer.WriteString("TopicTypeName", sample.TopicMetadata.TopicType.FullName);
        writer.WriteString("Timestamp", sample.Timestamp.ToUniversalTime());
        writer.WriteNumber("DomainId", sample.DomainId);
        writer.WriteString("PartitionName", sample.PartitionName);

        if (sample.Sender != null)
        {
            writer.WriteStartObject("Sender");
            writer.WriteNumber("ProcessId", sample.Sender.ProcessId);
            if (sample.Sender.MachineName != null)
                writer.WriteString("MachineName", sample.Sender.MachineName);
            if (sample.Sender.IpAddress != null)
                writer.WriteString("IpAddress", sample.Sender.IpAddress);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("Sender");
        }

        writer.WriteString("InstanceState", sample.SampleInfo.InstanceState.ToString());

        writer.WritePropertyName("Payload");
        payload.WriteTo(writer);

        writer.WriteEndObject();
    }

    public static Stream Open(string? path, int bufferSize = 65_536) => path == null
        ? Console.OpenStandardOutput()
        : new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize);
}

/// <summary>The original capture format: one indented JSON array.</summary>
internal sealed class JsonArraySink : ICaptureSink
{
    private readonly Stream _stream;
    private readonly Utf8JsonWriter _writer;
    private bool _closed;

    public JsonArraySink(string? path)
    {
        _stream = CaptureRecord.Open(path);
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions { Indented = true });
        Files = path == null ? Array.Empty<string>() : new[] { path };
        _writer.WriteStartArray();
    }

    public IReadOnlyList<string> Files { get; }

    public long BytesWritten => _writer.BytesCommitted + _writer.BytesPending;

    public void Write(SampleData sample, JsonElement payload) =>
        CaptureRecord.Write(_writer, sample, payload, includeTopicName: false);

    public void Flush() => _writer.Flush();

    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _writer.WriteEndArray();
        _writer.Flush();
        _stream.Flush();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}

/// <summary>
/// JSON Lines: one compact record per line, flushed in whole lines only. Optionally rolls over
/// to numbered files (<c>name.0001.jsonl</c>, <c>name.0002.jsonl</c>, ...) at a size limit.
/// </summary>
internal sealed class JsonLinesSink : ICaptureSink
{
    private const int PendingLimit = 65_536;

    private readonly string? _path;
    private readonly long _maxFileBytes;
    private readonly ArrayBufferWriter<byte> _pending = new(PendingLimit * 2);
    private readonly Utf8JsonWriter _lineWriter;
    private readonly List<string> _files = new();
    private Stream _stream;
    private long _fileBytes;
    private long _totalBytes;

    public JsonLinesSink(string? path, long maxFileBytes)
    {
        _path = path;
        _maxFileBytes = maxFileBytes;
        _lineWriter = new Utf8JsonWriter(_pending);
        _stream = OpenNext();
    }

    public IReadOnlyList<string> Files => _files;

    public long BytesWritten => _totalBytes;

    public void Write(SampleData sample, JsonElement payload)
    {
        // Lines are collected here and handed to the (unbuffered) file only as whole lines,
        // so a reader of the growing file never sees a partial line other than the one being written.
        int before = _pending.WrittenCount;
        _lineWriter.Reset(_pending);
        CaptureRecord.Write(_lineWriter, sample, payload, includeTopicName: true);
        _lineWriter.Flush();
        _pending.GetSpan(1)[0] = (byte)'\n';
        _pending.Advance(1);
        int lineBytes = _pending.WrittenCount - before;

        if (_maxFileBytes > 0 && _fileBytes > 0 && _fileBytes + lineBytes > _maxFileBytes)
        {
            // This line starts the next file: the lines before it finish the current one.
            WritePending(_pending.WrittenSpan.Slice(0, before));
            _stream.Dispose();
            _stream = OpenNext();
            var line = _pending.WrittenSpan.Slice(before).ToArray();
            _pending.Clear();
            _pending.Write(line);
        }

        _fileBytes += lineBytes;
        _totalBytes += lineBytes;

        if (_pending.WrittenCount >= PendingLimit)
            Flush();
    }

    public void Flush()
    {
        WritePending(_pending.WrittenSpan);
        _pending.Clear();
        _stream.Flush();
    }

    public void Close() => Flush();

    public void Dispose()
    {
        _lineWriter.Dispose();
        _stream.Dispose();
    }

    private void WritePending(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.IsEmpty)
            _stream.Write(bytes);
    }

    private Stream OpenNext()
    {
        _fileBytes = 0;
        if (_path == null)
            return CaptureRecord.Open(null);

        var file = _maxFileBytes > 0 ? NumberedPath(_path, _files.Count + 1) : _path;
        _files.Add(file);
        return CaptureRecord.Open(file, bufferSize: 0);
    }

    /// <summary><c>dir/cap.jsonl</c>, 3 → <c>dir/cap.0003.jsonl</c>.</summary>
    internal static string NumberedPath(string path, int index)
    {
        var ext = Path.GetExtension(path);
        var stem = path.Substring(0, path.Length - ext.Length);
        return $"{stem}.{index:D4}{(ext.Length > 0 ? ext : ".jsonl")}";
    }
}
