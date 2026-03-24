using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime;
using DdsMonitor.Engine.Export;
using DdsMonitor.Engine.Import;
using DdsMonitor.Engine.Replay;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for BATCH-24: Export/Import Tooling &amp; Streaming Extensibility.
/// DMON-037 – ExportService streaming JSON write
/// DMON-038 – ImportService token parsing and payload reconstruction
/// DMON-039 – ReplayEngine state machine and routing
/// </summary>
public sealed class Batch24Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // DMON-037: ExportService
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportService_ExportAll_WritesValidJsonArray()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 1 }, 1));
        store.Append(MakeSample(meta, new SampleTopic { Id = 2 }, 2));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(2, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportService_ExportAll_RecordsContainExpectedFields()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 42 }, ordinal: 7));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);

            var element = doc.RootElement[0];
            Assert.Equal(7L, element.GetProperty("Ordinal").GetInt64());
            Assert.True(element.TryGetProperty("TopicTypeName", out _));
            Assert.True(element.TryGetProperty("Payload", out _));
            Assert.True(element.TryGetProperty("Timestamp", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportService_ExportTopic_OnlyWritesSamplesForThatTopic()
    {
        var store = new SampleStore();
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));
        store.Append(MakeSample(metaA, new SampleTopic { Id = 1 }, 1));
        store.Append(MakeSample(metaB, new SimpleType { Count = 99 }, 2));
        store.Append(MakeSample(metaA, new SampleTopic { Id = 3 }, 3));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportTopicAsync(path, typeof(SampleTopic));

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(2, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportService_ExportAll_EmptyStore_WritesEmptyArray()
    {
        var store = new SampleStore();
        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(0, doc.RootElement.GetArrayLength());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportService_ExportAll_DoesNotThrow()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 5 }, 1));

        var path = Path.GetTempFileName();
        try
        {
            var ex = await Record.ExceptionAsync(() =>
                new ExportService(store).ExportAllAsync(path));

            Assert.Null(ex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportService_ExportAll_WriterSenderBlock_WhenSenderPresent()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 1 }, 1,
            sender: new SenderIdentity { ProcessId = 9, MachineName = "SRV", IpAddress = "1.2.3.4" }));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);

            var senderEl = doc.RootElement[0].GetProperty("Sender");
            Assert.Equal(JsonValueKind.Object, senderEl.ValueKind);
            Assert.Equal(9u, senderEl.GetProperty("ProcessId").GetUInt32());
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-038: ImportService – roundtrip accuracy
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportService_Import_ReconstructsSampleData_FromExportedFile()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 77 }, ordinal: 5));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Single(imported);
            Assert.Equal(5L, imported[0].Ordinal);
            Assert.Equal(typeof(SampleTopic), imported[0].Payload.GetType());
            Assert.Equal(77, ((SampleTopic)imported[0].Payload).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportService_Import_SkipsRecordsWithUnresolvableType()
    {
        const string json = "[{\"Ordinal\":1,\"TopicTypeName\":\"Does.Not.Exist, NoAssembly\","
                          + "\"Timestamp\":\"2025-01-01T00:00:00Z\",\"SizeBytes\":0,\"Sender\":null,\"Payload\":{}}]";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Empty(imported);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportService_Import_ReconstructsSender_WhenPresent()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 1 }, 1,
            sender: new SenderIdentity { ProcessId = 1234, MachineName = "PC01", IpAddress = "10.0.0.1" }));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Single(imported);
            Assert.NotNull(imported[0].Sender);
            Assert.Equal(1234u, imported[0].Sender!.ProcessId);
            Assert.Equal("PC01", imported[0].Sender.MachineName);
            Assert.Equal("10.0.0.1", imported[0].Sender.IpAddress);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportService_Import_ReconstructsNullSender_WhenAbsent()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        store.Append(MakeSample(meta, new SampleTopic { Id = 1 }, 1, sender: null));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Single(imported);
            Assert.Null(imported[0].Sender);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportImport_Roundtrip_PreservesAllCoreFields()
    {
        var store = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var ts = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var sample = new SampleData
        {
            Ordinal = 99,
            Payload = new SampleTopic { Id = 55 },
            TopicMetadata = meta,
            Timestamp = ts,
            Sender = new SenderIdentity { ProcessId = 42, MachineName = "TESTPC", IpAddress = "192.168.1.50" }
        };
        store.Append(sample);

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Single(imported);
            var r = imported[0];
            Assert.Equal(99L, r.Ordinal);
            Assert.Equal(ts, r.Timestamp.ToUniversalTime());
            Assert.Equal(42u, r.Sender!.ProcessId);
            Assert.Equal("TESTPC", r.Sender.MachineName);
            Assert.Equal(55, ((SampleTopic)r.Payload).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportImport_Roundtrip_MultipleTopics_PreservesAll()
    {
        var store = new SampleStore();
        var metaA = new TopicMetadata(typeof(SampleTopic));
        var metaB = new TopicMetadata(typeof(SimpleType));
        store.Append(MakeSample(metaA, new SampleTopic { Id = 1 }, 1));
        store.Append(MakeSample(metaB, new SimpleType { Count = 7 }, 2));
        store.Append(MakeSample(metaA, new SampleTopic { Id = 3 }, 3));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(store).ExportAllAsync(path);

            var imported = await CollectAsync(new ImportService().ImportAsync(path));

            Assert.Equal(3, imported.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DMON-039: ReplayEngine – state machine
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReplayEngine_InitialState_IsIdle()
    {
        using var engine = BuildReplayEngine(out _, out _);

        Assert.Equal(ReplayStatus.Idle, engine.Status);
        Assert.Equal(0, engine.TotalSamples);
        Assert.Equal(0, engine.CurrentIndex);
    }

    [Fact]
    public async Task ReplayEngine_LoadAsync_PopulatesSamplesAndResetsIndex()
    {
        var exportStore = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        exportStore.Append(MakeSample(meta, new SampleTopic { Id = 1 }, 1));
        exportStore.Append(MakeSample(meta, new SampleTopic { Id = 2 }, 2));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(exportStore).ExportAllAsync(path);

            using var engine = BuildReplayEngine(out _, out _);
            await engine.LoadAsync(path);

            Assert.Equal(2, engine.TotalSamples);
            Assert.Equal(0, engine.CurrentIndex);
            Assert.Equal(ReplayStatus.Idle, engine.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Play_TransitionsToPlayingStatus()
    {
        var path = await ExportSingleSampleAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            engine.SpeedMultiplier = 1000.0;
            await engine.LoadAsync(path);

            engine.Play(ReplayTarget.LocalStore);

            Assert.Equal(ReplayStatus.Playing, engine.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Pause_TransitionsToPausedStatus()
    {
        // Export 2 samples with a 60-second timestamp gap.
        // At 1× speed the engine will await ~60 s between them; Pause() cancels
        // that delay instantly, ensuring the status is reliably Paused.
        var path = await ExportTwoSamplesWithLongDelayAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            // Default speed (1×) means the engine will block in Delay(60 s).
            await engine.LoadAsync(path);
            engine.Play(ReplayTarget.LocalStore);
            engine.Pause();

            Assert.Equal(ReplayStatus.Paused, engine.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Stop_ResetsToIdleAndZeroIndex()
    {
        var path = await ExportSingleSampleAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            await engine.LoadAsync(path);

            engine.Play(ReplayTarget.LocalStore);
            engine.Stop();

            Assert.Equal(ReplayStatus.Idle, engine.Status);
            Assert.Equal(0, engine.CurrentIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Route_LocalStore_AppendsSamplesToStore()
    {
        var exportStore = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        exportStore.Append(MakeSample(meta, new SampleTopic { Id = 11 }, 1));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(exportStore).ExportAllAsync(path);

            var targetStore = new SampleStore();
            var fakeBridge = new FakeDdsBridge();
            using var engine = new ReplayEngine(new ImportService(), targetStore, fakeBridge);
            engine.SpeedMultiplier = 1000.0;
            await engine.LoadAsync(path);

            engine.Play(ReplayTarget.LocalStore);

            await WaitForAsync(() => engine.Status == ReplayStatus.Idle, timeoutMs: 5000);

            Assert.Single(targetStore.AllSamples);
            Assert.Equal(11, ((SampleTopic)targetStore.AllSamples[0].Payload).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Route_DdsNetwork_CallsWriterWrite()
    {
        var exportStore = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        exportStore.Append(MakeSample(meta, new SampleTopic { Id = 99 }, 1));

        var path = Path.GetTempFileName();
        try
        {
            await new ExportService(exportStore).ExportAllAsync(path);

            var fakeBridge = new FakeDdsBridge();
            using var engine = new ReplayEngine(new ImportService(), new SampleStore(), fakeBridge);
            engine.SpeedMultiplier = 1000.0;
            await engine.LoadAsync(path);

            engine.Play(ReplayTarget.DdsNetwork);

            await WaitForAsync(() => engine.Status == ReplayStatus.Idle, timeoutMs: 5000);

            Assert.Single(fakeBridge.WrittenPayloads);
            Assert.Equal(99, ((SampleTopic)fakeBridge.WrittenPayloads[0]).Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_Play_NoOp_WhenAlreadyPlaying()
    {
        var path = await ExportSingleSampleAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            await engine.LoadAsync(path);

            engine.Play(ReplayTarget.LocalStore);
            engine.Play(ReplayTarget.LocalStore); // should not throw

            Assert.Equal(ReplayStatus.Playing, engine.Status);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReplayEngine_Play_NoOp_WhenNoSamplesLoaded()
    {
        using var engine = BuildReplayEngine(out _, out _);

        engine.Play(ReplayTarget.LocalStore); // should not throw

        Assert.Equal(ReplayStatus.Idle, engine.Status);
    }

    [Fact]
    public void ReplayEngine_SpeedMultiplier_DefaultIsOne()
    {
        using var engine = BuildReplayEngine(out _, out _);

        Assert.Equal(1.0, engine.SpeedMultiplier);
    }

    [Fact]
    public void ReplayEngine_Loop_DefaultIsFalse()
    {
        using var engine = BuildReplayEngine(out _, out _);

        Assert.False(engine.Loop);
    }

    [Fact]
    public async Task ReplayEngine_StateChanged_RaisedOnPlay()
    {
        var path = await ExportSingleSampleAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            engine.SpeedMultiplier = 1000.0;
            await engine.LoadAsync(path);

            var raised = false;
            engine.StateChanged += () => raised = true;
            engine.Play(ReplayTarget.LocalStore);

            Assert.True(raised);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReplayEngine_StateChanged_RaisedOnStop()
    {
        var path = await ExportSingleSampleAsync();
        try
        {
            using var engine = BuildReplayEngine(out _, out _);
            engine.SpeedMultiplier = 1000.0;
            await engine.LoadAsync(path);
            engine.Play(ReplayTarget.LocalStore);

            var raised = false;
            engine.StateChanged += () => raised = true;
            engine.Stop();

            Assert.True(raised);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static SampleData MakeSample(
        TopicMetadata meta,
        object payload,
        long ordinal,
        SenderIdentity? sender = null)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = meta,
            Timestamp = DateTime.UtcNow,
            SizeBytes = 64,
            Sender = sender
        };
    }

    private static ReplayEngine BuildReplayEngine(out SampleStore store, out FakeDdsBridge bridge)
    {
        store = new SampleStore();
        bridge = new FakeDdsBridge();
        return new ReplayEngine(new ImportService(), store, bridge);
    }

    private static async Task<string> ExportSingleSampleAsync()
    {
        var exportStore = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        exportStore.Append(MakeSample(meta, new SampleTopic { Id = 1 }, ordinal: 1));
        var path = Path.GetTempFileName();
        await new ExportService(exportStore).ExportAllAsync(path);
        return path;
    }

    private static async Task<string> ExportTwoSamplesWithLongDelayAsync()
    {
        var exportStore = new SampleStore();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(60); // 60-minute gap → 3600 s delay at 1× speed
        exportStore.Append(new SampleData
        {
            Ordinal = 1,
            Payload = new SampleTopic { Id = 1 },
            TopicMetadata = meta,
            Timestamp = t0,
            SizeBytes = 64
        });
        exportStore.Append(new SampleData
        {
            Ordinal = 2,
            Payload = new SampleTopic { Id = 2 },
            TopicMetadata = meta,
            Timestamp = t1,
            SizeBytes = 64
        });
        var path = Path.GetTempFileName();
        await new ExportService(exportStore).ExportAllAsync(path);
        return path;
    }

    private static async Task<List<SampleData>> CollectAsync(
        IAsyncEnumerable<SampleData> source)
    {
        var list = new List<SampleData>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fakes
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class FakeDdsBridge : IDdsBridge
    {
        public List<object> WrittenPayloads { get; } = new();

        public DdsParticipant Participant => throw new NotSupportedException();

        public string? CurrentPartition => null;

        public IReadOnlyList<DdsParticipant> Participants => Array.Empty<DdsParticipant>();

        public IReadOnlyList<ParticipantConfig> ParticipantConfigs => Array.Empty<ParticipantConfig>();

        public bool IsPaused { get; set; }

        public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders =>
            new Dictionary<Type, IDynamicReader>();

        public IReadOnlySet<Type> ExplicitlyUnsubscribedTopicTypes =>
            new HashSet<Type>();

        public void InitializeExplicitlyUnsubscribed(IEnumerable<Type> types) { }

        public event Action? ReadersChanged;

        public IDynamicReader Subscribe(TopicMetadata meta) =>
            throw new NotSupportedException();

        public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? errorMessage)
        {
            reader = null;
            errorMessage = "Fake bridge does not support subscribe.";
            return false;
        }

        public void Unsubscribe(TopicMetadata meta) { }

        public IDynamicWriter GetWriter(TopicMetadata meta) =>
            new FakeDynamicWriter(meta.TopicType, WrittenPayloads);

        public void ChangePartition(string? newPartition) { }

        public void AddParticipant(uint domainId, string partitionName) { }

        public void RemoveParticipant(int participantIndex) { }

        public void ResetAll() { }

        public void Dispose() { }
    }

    private sealed class FakeDynamicWriter : IDynamicWriter
    {
        private readonly List<object> _sink;

        public FakeDynamicWriter(Type topicType, List<object> sink)
        {
            TopicType = topicType;
            _sink = sink;
        }

        public Type TopicType { get; }

        public void Write(object payload)
        {
            lock (_sink)
            {
                _sink.Add(payload);
            }
        }

        public void DisposeInstance(object payload) { }

        public void Dispose() { }
    }
}
