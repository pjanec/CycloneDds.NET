using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using DdsMonitor.Engine.Import;
using DdsMonitor.Engine.Recording;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// The opt-in headless record options meant for scripts and AI agents: JSON Lines readable while
/// recording, stop conditions, the summary file, rotation, base64 byte buffers and the exit code.
/// </summary>
public sealed class HeadlessRecordOptionsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ddsmon-rec-" + Guid.NewGuid().ToString("N"));
    private readonly Channel<SampleData> _channel = Channel.CreateUnbounded<SampleData>();
    private readonly TopicMetadata _keyed = new(typeof(KeyedType));
    private readonly TopicMetadata _blob = new(typeof(SumoLikeWireFrame));
    // A domain of its own, so samples other tests publish on domain 0 never reach these recordings.
    private readonly uint _domain = (uint)Random.Shared.Next(100, 230);
    private long _ordinal;

    public HeadlessRecordOptionsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string PathOf(string name) => Path.Combine(_dir, name);

    [Fact(Timeout = 30000)]
    public async Task JsonLines_IsReadableWhileRecording_OneCompleteRecordPerLine()
    {
        var file = PathOf("cap.jsonl");
        await using var run = Start(new DdsSettings { RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file });

        Send(_keyed, 3);
        var lines = await WaitForLines(file, 3);

        // Still recording, and every line already parses on its own.
        Assert.False(run.Service.ExecuteTask!.IsCompleted);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal(_keyed.TopicName, doc.RootElement.GetProperty("TopicName").GetString());
            Assert.True(doc.RootElement.GetProperty("Ordinal").GetInt64() > 0);
        }

        await run.Service.StopAsync(CancellationToken.None);
        Assert.Equal(3, File.ReadAllLines(file).Length);
        Assert.Equal(RecordStopReason.Interrupted, run.Service.RecordResult!.StopReason);
    }

    [Fact(Timeout = 30000)]
    public async Task Defaults_RecordJsonLines_WithBase64Blobs_AndASilentConsole()
    {
        var defaults = new DdsSettings();
        Assert.Equal(RecordFormat.JsonLines, defaults.RecordFormat);
        Assert.Equal(RecordConsoleOutput.Quiet, defaults.RecordConsole);
        Assert.True(defaults.RecordBytesAsBase64);

        var file = PathOf("cap.jsonl");
        var stdout = new StringWriter();
        var original = Console.Out;
        Console.SetOut(stdout);
        try
        {
            await using var run = Start(new DdsSettings { HeadlessFilePath = file, RecordMaxSamples = 1 });
            Send(_blob, 1);
            await run.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            Console.SetOut(original);
        }

        using var doc = JsonDocument.Parse(File.ReadAllLines(file).Single());
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("Payload").GetProperty("Payload").ValueKind);
        Assert.DoesNotContain("[RECORD]", stdout.ToString());
    }

    [Fact(Timeout = 30000)]
    public async Task MaxSamples_StopsTheRecordingByItself()
    {
        var file = PathOf("cap.jsonl");
        await using var run = Start(new DdsSettings
        {
            RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file, RecordMaxSamples = 2
        });

        Send(_keyed, 5);
        await run.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, File.ReadAllLines(file).Length);
        Assert.Equal(RecordStopReason.MaxSamples, run.Service.RecordResult!.StopReason);
        Assert.Equal(1, run.Lifetime.StopRequests);
        Assert.Equal(0, run.Service.ExitCode);
    }

    [Fact(Timeout = 30000)]
    public async Task Duration_StopsTheRecordingByItself_AndTheJsonArrayIsClosed()
    {
        var file = PathOf("cap.json");
        await using var run = Start(new DdsSettings { RecordFormat = RecordFormat.Json, HeadlessFilePath = file, RecordDurationSeconds = 1 });

        Send(_keyed, 2);
        await run.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(RecordStopReason.Duration, run.Service.RecordResult!.StopReason);
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    [Fact(Timeout = 30000)]
    public async Task SummaryFile_ListsEverySubscribedTopic_AndTheFinalState()
    {
        var file = PathOf("cap.jsonl");
        var summary = PathOf("summary.json");
        await using var run = Start(
            new DdsSettings { RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file, RecordSummaryFile = summary },
            _keyed, _blob);

        Send(_keyed, 4);
        await WaitUntil(() => ReadSummary(summary) is { } s && s.GetProperty("Samples").GetInt64() == 4);

        var running = ReadSummary(summary)!.Value;
        Assert.True(running.GetProperty("Running").GetBoolean());
        var topics = running.GetProperty("Topics").EnumerateArray().ToDictionary(t => t.GetProperty("TopicName").GetString()!);
        Assert.Equal(4, topics[_keyed.TopicName].GetProperty("Count").GetInt64());
        Assert.Equal(0, topics[_blob.TopicName].GetProperty("Count").GetInt64()); // subscribed, nothing arrived

        await run.Service.StopAsync(CancellationToken.None);
        var final = ReadSummary(summary)!.Value;
        Assert.False(final.GetProperty("Running").GetBoolean());
        Assert.Equal("Interrupted", final.GetProperty("StopReason").GetString());
        Assert.Equal(file, final.GetProperty("Files")[0].GetString());
    }

    [Fact(Timeout = 30000)]
    public async Task MaxFileSize_RollsOverToNumberedFiles_WithoutSplittingALine()
    {
        var file = PathOf("cap.jsonl");
        await using var run = Start(new DdsSettings
        {
            RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file, RecordMaxFileSizeMB = 1, RecordMaxSamples = 8,
            RecordBytesAsBase64 = false
        });

        // A 64 KiB blob written as a number array is about 128 KiB of JSON: 8 of them need two files.
        Send(_blob, 8);
        await run.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(20));

        var files = run.Service.RecordResult!.Files;
        Assert.Equal(new[] { PathOf("cap.0001.jsonl"), PathOf("cap.0002.jsonl") }, files);
        Assert.False(File.Exists(file));
        var all = files.SelectMany(File.ReadAllLines).ToList();
        Assert.Equal(8, all.Count);
        Assert.All(all, line => JsonDocument.Parse(line).Dispose());
        Assert.All(files, f => Assert.True(new FileInfo(f).Length <= 1024 * 1024));
        Assert.Equal(Enumerable.Range(1, 8).Select(i => (long)i),
            all.Select(l => JsonDocument.Parse(l).RootElement.GetProperty("Ordinal").GetInt64()));
    }

    [Fact(Timeout = 30000)]
    public async Task BytesAsBase64_WritesTheBlobAsOneString_AndImportReadsItBack()
    {
        var file = PathOf("cap.jsonl");
        await using var run = Start(new DdsSettings
        {
            RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file, RecordBytesAsBase64 = true, RecordMaxSamples = 1
        });

        var payloadField = _blob.AllFields.Single(f => f.StructuredName == "Payload");
        object frame = new SumoLikeWireFrame();
        var bytes = new byte[65536];
        bytes[0] = 0x11;
        bytes[65535] = 0x22;
        payloadField.Setter(frame, bytes);
        Send(_blob, frame);
        await run.Service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        var line = File.ReadAllLines(file).Single();
        using (var doc = JsonDocument.Parse(line))
            Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("Payload").GetProperty("Payload").ValueKind);
        Assert.True(line.Length < 100_000, $"a base64 line should be ~88 KB, was {line.Length}");

        var imported = await ToList(new ImportService().ImportAsync(file));
        var got = Assert.IsType<byte[]>(payloadField.Getter(Assert.Single(imported).Payload));
        Assert.Equal(0x11, got[0]);
        Assert.Equal(0x22, got[65535]);
    }

    [Fact]
    public async Task Import_ReadsJsonLines_AndIgnoresAPartialLastLine()
    {
        var file = PathOf("cut.jsonl");
        var record = $"{{\"Ordinal\":1,\"TopicName\":\"{_keyed.TopicName}\",\"TopicTypeName\":\"{typeof(KeyedType).FullName}\"," +
                     "\"Timestamp\":\"2026-01-01T00:00:00Z\",\"DomainId\":0,\"PartitionName\":\"\",\"Sender\":null," +
                     "\"InstanceState\":\"Alive\",\"Payload\":{\"Id\":7}}";
        File.WriteAllText(file, record + "\n" + record.Replace("\"Ordinal\":1", "\"Ordinal\":2") + "\n" + record.Substring(0, 40));

        var imported = await ToList(new ImportService().ImportAsync(file));

        Assert.Equal(new long[] { 1, 2 }, imported.Select(s => s.Ordinal));
    }

    [Fact(Timeout = 30000)]
    public async Task ATopicThatCannotBeRead_IsInTheSummary_AndTheExitCode()
    {
        var file = PathOf("cap.jsonl");
        var summary = PathOf("summary.json");
        var bridge = NewBridge();
        // The bridge-wide filter runs on each reader thread: make reading one topic fail.
        bridge.SetFilter(s => s.TopicMetadata.TopicType == typeof(KeyedType) ? throw new InvalidOperationException("unreadable") : true);
        await using var run = Start(
            new DdsSettings { RecordFormat = RecordFormat.JsonLines, HeadlessFilePath = file, RecordSummaryFile = summary },
            bridge, _keyed);

        using var participant = new DdsParticipant(_domain);
        using var writer = new DdsWriter<KeyedType>(participant, _keyed.TopicName);
        await WaitUntil(() =>
        {
            writer.Write(new KeyedType { Id = 1 });
            return ReadSummary(summary) is { } s && s.GetProperty("Topics")[0].TryGetProperty("ReadError", out _);
        });

        await run.Service.StopAsync(CancellationToken.None);
        Assert.Equal(HeadlessRunnerService.ExitCodeTopicFailed, run.Service.ExitCode);
        Assert.Contains("unreadable", ReadSummary(summary)!.Value.GetProperty("Topics")[0].GetProperty("ReadError").GetString());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private Run Start(DdsSettings settings, params TopicMetadata[] topics) =>
        Start(settings, NewBridge(), topics);

    private DdsBridge NewBridge() =>
        new(_channel.Writer, new[] { new ParticipantConfig { DomainId = _domain } }, null, null, null, null);

    private Run Start(DdsSettings settings, DdsBridge bridge, params TopicMetadata[] topics)
    {
        settings.HeadlessMode = HeadlessMode.Record;
        var registry = new TopicRegistry();
        foreach (var t in topics) registry.Register(t);
        var provider = new ServiceCollection().BuildServiceProvider();
        var lifetime = new TestLifetime();
        var service = new HeadlessRunnerService(settings, new AppSettings(), registry, bridge,
            provider.GetRequiredService<IServiceScopeFactory>(), lifetime,
            NullLogger<HeadlessRunnerService>.Instance, _channel.Reader);
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new Run(service, lifetime, bridge, provider);
    }

    private void Send(TopicMetadata topic, int count)
    {
        for (int i = 0; i < count; i++)
            Send(topic, Activator.CreateInstance(topic.TopicType)!);
    }

    private void Send(TopicMetadata topic, object payload) =>
        _channel.Writer.TryWrite(new SampleData
        {
            Ordinal = Interlocked.Increment(ref _ordinal),
            Payload = payload,
            TopicMetadata = topic,
            Timestamp = DateTime.UtcNow
        });

    private static async Task<string[]> WaitForLines(string file, int count)
    {
        string[] lines = Array.Empty<string>();
        await WaitUntil(() =>
        {
            if (!File.Exists(file)) return false;
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.Length >= count;
        });
        return lines;
    }

    private static JsonElement? ReadSummary(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200; i++)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        Assert.Fail("condition not reached within 10 s");
    }

    private static async Task<List<SampleData>> ToList(IAsyncEnumerable<SampleData> source)
    {
        var list = new List<SampleData>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    private sealed record Run(HeadlessRunnerService Service, TestLifetime Lifetime, DdsBridge Bridge, ServiceProvider Provider) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Service.StopAsync(CancellationToken.None);
            Service.Dispose();
            Bridge.Dispose();
            await Provider.DisposeAsync();
        }
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public int StopRequests;
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => Interlocked.Increment(ref StopRequests);
    }
}
