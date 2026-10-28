using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DdsMonitor.Engine.Recording;

/// <summary>
/// Per-topic counters of a running recording, shared by the record loop, the summary file
/// and the console status. Thread-safe: reader failures arrive on reader threads.
/// </summary>
internal sealed class RecordStats
{
    private sealed class TopicStats
    {
        public required TopicMetadata Topic { get; init; }
        public long Count;
        public DateTime? FirstUtc;
        public DateTime? LastUtc;
        public long LastOrdinal;
        public string? ReadError;
        public string? WriteError;
    }

    private readonly object _sync = new();
    private readonly Dictionary<Type, TopicStats> _topics = new();
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private IReadOnlyList<string> _files = Array.Empty<string>();
    private RecordStopReason _stopReason = RecordStopReason.None;
    private long _samples;
    private long _bytes;

    public int FailedTopicCount
    {
        get { lock (_sync) return _topics.Values.Count(t => t.ReadError != null); }
    }

    public void AddTopic(TopicMetadata topic)
    {
        lock (_sync) Get(topic);
    }

    public void SetFiles(IReadOnlyList<string> files)
    {
        lock (_sync) _files = files;
    }

    public void Record(SampleData sample, long bytesWritten)
    {
        lock (_sync)
        {
            var t = Get(sample.TopicMetadata);
            t.Count++;
            var ts = sample.Timestamp.ToUniversalTime();
            t.FirstUtc ??= ts;
            t.LastUtc = ts;
            t.LastOrdinal = sample.Ordinal;
            _samples++;
            _bytes = bytesWritten;
        }
    }

    public void MarkFailed(TopicMetadata topic, Exception ex)
    {
        lock (_sync) Get(topic).ReadError = $"{ex.GetType().Name}: {ex.Message}";
    }

    /// <summary>Returns true the first time a topic is marked (so it is logged once).</summary>
    public bool MarkSkipped(TopicMetadata topic, Exception ex)
    {
        lock (_sync)
        {
            var t = Get(topic);
            if (t.WriteError != null) return false;
            t.WriteError = $"{ex.GetType().Name}: {ex.Message}";
            return true;
        }
    }

    public void Stop(RecordStopReason reason, long bytesWritten)
    {
        lock (_sync)
        {
            _stopReason = reason;
            _bytes = bytesWritten;
        }
    }

    public byte[] SummaryJson()
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            using var buffer = new MemoryStream();
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteBoolean("Running", _stopReason == RecordStopReason.None);
                if (_stopReason == RecordStopReason.None) w.WriteNull("StopReason");
                else w.WriteString("StopReason", _stopReason.ToString());
                w.WriteString("StartedUtc", _startedUtc);
                w.WriteString("UpdatedUtc", now);
                w.WriteNumber("ElapsedSeconds", Math.Round((now - _startedUtc).TotalSeconds, 1));
                w.WriteNumber("Samples", _samples);
                w.WriteNumber("Bytes", _bytes);
                w.WriteStartArray("Files");
                foreach (var f in _files) w.WriteStringValue(f);
                w.WriteEndArray();

                w.WriteStartArray("Topics");
                foreach (var t in Ordered())
                {
                    w.WriteStartObject();
                    w.WriteString("TopicName", t.Topic.TopicName);
                    w.WriteString("TopicTypeName", t.Topic.TopicType.FullName);
                    w.WriteNumber("Count", t.Count);
                    if (t.FirstUtc is { } first) w.WriteString("FirstUtc", first); else w.WriteNull("FirstUtc");
                    if (t.LastUtc is { } last) w.WriteString("LastUtc", last); else w.WriteNull("LastUtc");
                    w.WriteNumber("LastOrdinal", t.LastOrdinal);
                    if (t.ReadError != null) w.WriteString("ReadError", t.ReadError);
                    if (t.WriteError != null) w.WriteString("WriteError", t.WriteError);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return buffer.ToArray();
        }
    }

    public string StatusLine()
    {
        lock (_sync)
        {
            var elapsed = (DateTime.UtcNow - _startedUtc).TotalSeconds;
            int receiving = _topics.Values.Count(t => t.Count > 0);
            int failed = _topics.Values.Count(t => t.ReadError != null || t.WriteError != null);
            var state = _stopReason == RecordStopReason.None ? "recording" : $"stopped ({_stopReason})";
            return $"[RECORD] {state}: {_samples} samples, {receiving}/{_topics.Count} topics with data, " +
                   $"{failed} failed, {_bytes / (1024.0 * 1024.0):F1} MB, {elapsed:F0} s";
        }
    }

    public IEnumerable<string> TopicLines()
    {
        lock (_sync)
        {
            return Ordered().Select(t =>
            {
                var error = t.ReadError ?? t.WriteError;
                return error != null
                    ? $"[RECORD]   {t.Topic.TopicName}: {t.Count} samples, FAILED: {error}"
                    : $"[RECORD]   {t.Topic.TopicName}: {t.Count} samples";
            }).ToList();
        }
    }

    private IEnumerable<TopicStats> Ordered() => _topics.Values.OrderBy(t => t.Topic.TopicName, StringComparer.Ordinal);

    private TopicStats Get(TopicMetadata topic)
    {
        if (!_topics.TryGetValue(topic.TopicType, out var t))
        {
            t = new TopicStats { Topic = topic };
            _topics.Add(topic.TopicType, t);
        }
        return t;
    }
}
