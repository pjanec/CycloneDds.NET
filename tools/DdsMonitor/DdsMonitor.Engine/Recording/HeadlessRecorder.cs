using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DdsMonitor.Engine.Recording;

/// <summary>How a headless recording ended.</summary>
public enum RecordStopReason
{
    /// <summary>Still recording.</summary>
    None,

    /// <summary>Ctrl+C / SIGTERM: the host stopped the recording.</summary>
    Interrupted,

    /// <summary><see cref="DdsSettings.RecordDurationSeconds"/> elapsed.</summary>
    Duration,

    /// <summary><see cref="DdsSettings.RecordMaxSamples"/> reached.</summary>
    MaxSamples,

    /// <summary>The sample channel was completed.</summary>
    Completed,
}

/// <summary>The outcome of a headless recording.</summary>
public sealed record RecordResult(long Samples, RecordStopReason StopReason, int FailedTopics, IReadOnlyList<string> Files);

/// <summary>
/// Streams samples from the bridge channel to a capture file (or stdout) for headless
/// <see cref="HeadlessMode.Record"/>, honouring the <c>Record*</c> options of <see cref="DdsSettings"/>.
/// By default: JSON Lines readable while recording, a silent console, until interrupted.
/// </summary>
public sealed class HeadlessRecorder
{
    /// <summary>The <see cref="DdsSettings.HeadlessFilePath"/> value that means stdout.</summary>
    public const string StdoutPath = "-";

    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(1);
    private const int StatusEverySummaries = 5;

    private readonly DdsSettings _settings;
    private readonly IDdsBridge _bridge;
    private readonly IEnumerable<TopicMetadata> _subscribedTopics;
    private readonly ChannelReader<SampleData> _channel;
    private readonly ILogger _logger;
    private readonly RecordStats _stats = new();
    private readonly TextWriter _console;

    public HeadlessRecorder(
        DdsSettings settings,
        IDdsBridge bridge,
        IEnumerable<TopicMetadata> subscribedTopics,
        ChannelReader<SampleData> channel,
        ILogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _subscribedTopics = subscribedTopics ?? throw new ArgumentNullException(nameof(subscribedTopics));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // When the capture itself goes to stdout, every console line must go to stderr.
        _console = IsStdout || settings.RecordConsole != RecordConsoleOutput.PerSample ? Console.Error : Console.Out;
    }

    private bool IsStdout => _settings.HeadlessFilePath == StdoutPath;

    /// <summary>Records until interrupted or until a configured stop condition is met.</summary>
    public async Task<RecordResult> RunAsync(CancellationToken stoppingToken)
    {
        foreach (var topic in _subscribedTopics)
            _stats.AddTopic(topic);

        _bridge.ReaderFailed += OnReaderFailed;
        using var sink = CreateSink();
        _stats.SetFiles(sink.Files);
        _logger.LogInformation("Headless Record → {FilePath} ({Format})", IsStdout ? "stdout" : _settings.HeadlessFilePath, _settings.RecordFormat);

        using var recordCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        if (_settings.RecordDurationSeconds > 0)
            recordCts.CancelAfter(TimeSpan.FromSeconds(_settings.RecordDurationSeconds));

        using var reportCts = new CancellationTokenSource();
        var reportTask = ReportLoopAsync(reportCts.Token);

        var payloadOptions = _settings.RecordBytesAsBase64 ? DdsJsonOptions.ExportBase64Bytes : DdsJsonOptions.Export;
        var stopReason = RecordStopReason.Completed;
        long count = 0;
        var sinceFlush = Stopwatch.StartNew();

        try
        {
            while (await _channel.WaitToReadAsync(recordCts.Token).ConfigureAwait(false))
            {
                while (_channel.TryRead(out var sample))
                {
                    if (!TrySerializePayload(sample, payloadOptions, out var payload))
                        continue;

                    sink.Write(sample, payload);
                    count++;
                    _stats.Record(sample, sink.BytesWritten);

                    if (_settings.RecordConsole == RecordConsoleOutput.PerSample)
                        _console.WriteLine($"[RECORD] Ordinal={sample.Ordinal} Topic={sample.TopicMetadata.ShortName}");

                    if (_settings.RecordMaxSamples > 0 && count >= _settings.RecordMaxSamples)
                    {
                        stopReason = RecordStopReason.MaxSamples;
                        return Finish();
                    }

                    if (sinceFlush.Elapsed >= FlushInterval)
                    {
                        sink.Flush();
                        sinceFlush.Restart();
                    }
                }

                // The queue is drained: make everything written so far readable.
                sink.Flush();
                sinceFlush.Restart();
            }
        }
        catch (OperationCanceledException) when (recordCts.IsCancellationRequested)
        {
            stopReason = stoppingToken.IsCancellationRequested ? RecordStopReason.Interrupted : RecordStopReason.Duration;
        }
        finally
        {
            if (stopReason != RecordStopReason.MaxSamples)
                Finish();
        }

        return new RecordResult(count, stopReason, _stats.FailedTopicCount, sink.Files);

        RecordResult Finish()
        {
            _bridge.ReaderFailed -= OnReaderFailed;

            // Always close the capture properly, whatever ended the recording.
            sink.Close();
            reportCts.Cancel();
            try { reportTask.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }

            _stats.Stop(stopReason, sink.BytesWritten);
            WriteSummaryFile();
            if (_settings.RecordConsole == RecordConsoleOutput.Status)
                WriteFinalConsoleSummary();

            _logger.LogInformation("Headless Record complete ({Reason}) – {Count} samples written to {Files}.",
                stopReason, count, sink.Files.Count == 0 ? "stdout" : string.Join(", ", sink.Files));
            return new RecordResult(count, stopReason, _stats.FailedTopicCount, sink.Files);
        }
    }

    private ICaptureSink CreateSink()
    {
        if (_settings.RecordFormat == RecordFormat.JsonLines)
        {
            long maxBytes = _settings.RecordMaxFileSizeMB > 0 ? _settings.RecordMaxFileSizeMB * 1024L * 1024L : 0;
            if (IsStdout && maxBytes > 0)
            {
                _logger.LogWarning("RecordMaxFileSizeMB is ignored when recording to stdout.");
                maxBytes = 0;
            }
            return new JsonLinesSink(IsStdout ? null : _settings.HeadlessFilePath, maxBytes);
        }

        if (_settings.RecordMaxFileSizeMB > 0)
            _logger.LogWarning("RecordMaxFileSizeMB applies to RecordFormat=JsonLines only; writing a single file.");
        return new JsonArraySink(IsStdout ? null : _settings.HeadlessFilePath);
    }

    private bool TrySerializePayload(SampleData sample, JsonSerializerOptions options, out JsonElement payload)
    {
        try
        {
            payload = JsonSerializer.SerializeToElement(sample.Payload, sample.Payload.GetType(), options);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_stats.MarkSkipped(sample.TopicMetadata, ex))
            {
                _logger.LogError(ex,
                    "Cannot record samples of topic {TopicName} ({TopicType}); they are left out of the capture.",
                    sample.TopicMetadata.TopicName, sample.TopicMetadata.TopicType.FullName);
            }
            payload = default;
            return false;
        }
    }

    private void OnReaderFailed(TopicMetadata topic, Exception ex) => _stats.MarkFailed(topic, ex);

    private async Task ReportLoopAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_settings.RecordSummaryFile) && _settings.RecordConsole != RecordConsoleOutput.Status)
            return;

        using var timer = new PeriodicTimer(SummaryInterval);
        int ticks = 0;
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            WriteSummaryFile();
            if (_settings.RecordConsole == RecordConsoleOutput.Status && ++ticks % StatusEverySummaries == 0)
                _console.WriteLine(_stats.StatusLine());
        }
    }

    private void WriteSummaryFile()
    {
        var path = _settings.RecordSummaryFile;
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            // Write next to the target and move it over, so a reader never sees a half-written file.
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, _stats.SummaryJson());
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write the record summary {Path}.", path);
        }
    }

    private void WriteFinalConsoleSummary()
    {
        _console.WriteLine(_stats.StatusLine());
        foreach (var line in _stats.TopicLines())
            _console.WriteLine(line);
    }
}
