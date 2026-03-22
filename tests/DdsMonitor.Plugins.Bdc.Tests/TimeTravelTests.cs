using System;
using System.Collections.Generic;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using DdsMonitor.Engine;
using DdsMonitor.Plugins.Bdc;

namespace DdsMonitor.Plugins.Bdc.Tests;

/// <summary>
/// Unit tests for the <see cref="TimeTravelEngine"/> (DMON-049).
///
/// All tests operate with a <see cref="StubSampleStore"/> populated with manually
/// crafted chronological samples and a fresh <see cref="BdcSettings"/> instance —
/// no live DDS bus is required.
/// </summary>
public sealed class TimeTravelTests
{
    private readonly StubSampleStore _sampleStore;
    private readonly BdcSettings     _settings;
    private readonly TimeTravelEngine _engine;

    // ── Common timestamps ─────────────────────────────────────────────────────

    private static readonly DateTime T1 = new(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2024, 1, 1, 0, 0, 2, DateTimeKind.Utc);
    private static readonly DateTime T3 = new(2024, 1, 1, 0, 0, 3, DateTimeKind.Utc);
    private static readonly DateTime T4 = new(2024, 1, 1, 0, 0, 4, DateTimeKind.Utc);
    private static readonly DateTime T5 = new(2024, 1, 1, 0, 0, 5, DateTimeKind.Utc);

    public TimeTravelTests()
    {
        _sampleStore = new StubSampleStore();
        _settings = new BdcSettings
        {
            NamespacePrefix    = "company.BDC",
            EntityIdPattern    = @"(?i)\bEntityId\b",
            PartIdPattern      = @"(?i)\bPartId\b",
            MasterTopicPattern = @"Master$"
        };
        _engine = new TimeTravelEngine(_sampleStore, _settings);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-049 success condition 1:
    //   TimeTravel_FindsCorrectDescriptorsAtTimestamp
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_FindsCorrectDescriptorsAtTimestamp()
    {
        // Arrange: three updates to the Master topic for entity 10 at T1, T2, T3.
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 10, Name = "v1" }, T1));
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 10, Name = "v2" }, T2));
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 10, Name = "v3" }, T3));

        // Query halfway between T2 and T3 — should see v2.
        var result = _engine.GetHistoricalState(10, T2.AddMilliseconds(500));

        Assert.Equal(EntityState.Alive, result.EntityState);
        Assert.Single(result.Descriptors);
        var found = Assert.Single(result.Descriptors).Value;
        Assert.Equal(T2, found.Timestamp);
        Assert.Equal("v2", ((BdcEntityMasterTopic)found.Payload).Name);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-049 success condition 2:
    //   TimeTravel_ExcludesDisposedDescriptors
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_ExcludesDisposedDescriptors()
    {
        // Arrange: Info descriptor added at T1, disposed at T=1.5, query at T2.
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 20 }, T1));
        _sampleStore.Add(MakeAliveSample<BdcEntityInfoTopic>(new BdcEntityInfoTopic { EntityId = 20, Description = "alive" }, T1));
        _sampleStore.Add(MakeDisposalSample<BdcEntityInfoTopic>(new BdcEntityInfoTopic { EntityId = 20 }, T1.AddMilliseconds(500)));

        // Query at T2 — the Info descriptor was disposed at T=1.5; should not appear.
        var result = _engine.GetHistoricalState(20, T2);

        // Only the Master descriptor should survive.
        Assert.Equal(EntityState.Alive, result.EntityState);
        Assert.Single(result.Descriptors);
        Assert.All(result.Descriptors.Keys, k => Assert.Contains("Master", k.TopicName));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-049 success condition 3:
    //   TimeTravel_EntityDeadAtTimestamp_ReturnsEmpty
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_EntityDeadAtTimestamp_ReturnsEmpty()
    {
        // Arrange: Master born at T1, disposed at T=1.5.  No resurrection before T2.
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 30 }, T1));
        _sampleStore.Add(MakeDisposalSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 30 }, T1.AddMilliseconds(500)));

        var result = _engine.GetHistoricalState(30, T2);

        Assert.Equal(EntityState.Dead, result.EntityState);
        Assert.Empty(result.Descriptors);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-049 success condition 4:
    //   TimeTravel_MultiInstance_FindsAllPartIds
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_MultiInstance_FindsAllPartIds()
    {
        // Arrange: entity 40 has a Master + two part descriptors (PartId=1 and PartId=2).
        _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 40 }, T1));
        _sampleStore.Add(MakeAliveSample<BdcPartDescriptorTopic>(new BdcPartDescriptorTopic { EntityId = 40, PartId = 1, Value = 100 }, T1));
        _sampleStore.Add(MakeAliveSample<BdcPartDescriptorTopic>(new BdcPartDescriptorTopic { EntityId = 40, PartId = 2, Value = 200 }, T1));

        var result = _engine.GetHistoricalState(40, T2);

        Assert.Equal(EntityState.Alive, result.EntityState);
        // Master(no PartId) + PartDescriptor(PartId=1) + PartDescriptor(PartId=2) = 3 descriptors.
        Assert.Equal(3, result.Descriptors.Count);
        // Both PartIds must be present.
        Assert.Contains(result.Descriptors.Keys, k => k.PartId == 1);
        Assert.Contains(result.Descriptors.Keys, k => k.PartId == 2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BATCH-30 boundary requirement:
    //   5 rapidly changing states → query between event #3 and #4
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_BoundaryBetweenEvent3And4_ReturnsEvent3Payload()
    {
        // Arrange: 5 successive updates, each 100 ms apart.
        var base_   = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var times   = new DateTime[5];
        for (int i = 0; i < 5; i++)
            times[i] = base_.AddMilliseconds(i * 100);

        for (int i = 0; i < 5; i++)
        {
            _sampleStore.Add(MakeAliveSample<BdcEntityMasterTopic>(
                new BdcEntityMasterTopic { EntityId = 50, Name = $"state-{i + 1}" },
                times[i]));
        }

        // Query exactly between event #3 (times[2]) and #4 (times[3]).
        var queryTime = times[2].AddMilliseconds(50);
        var result    = _engine.GetHistoricalState(50, queryTime);

        Assert.Equal(EntityState.Alive, result.EntityState);
        var found = Assert.Single(result.Descriptors).Value;
        // Must be the event #3 payload (index 2 → "state-3").
        Assert.Equal("state-3", ((BdcEntityMasterTopic)found.Payload).Name);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BinarySearch helper unit tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BinarySearch_EmptySamples_ReturnsEmpty()
    {
        var meta = new TopicMetadata(typeof(BdcEntityMasterTopic));
        var entityField = meta.KeyFields[0]; // EntityId

        var result = TimeTravelEngine.FindLatestBeforeTime(
            Array.Empty<SampleData>(), T1, 1, entityField, null);

        Assert.Empty(result);
    }

    [Fact]
    public void BinarySearch_AllSamplesAfterTarget_ReturnsEmpty()
    {
        var meta    = new TopicMetadata(typeof(BdcEntityMasterTopic));
        var samples = new List<SampleData>
        {
            MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 1 }, T3),
        };

        var result = TimeTravelEngine.FindLatestBeforeTime(
            samples, T1, 1, meta.KeyFields[0], null);

        Assert.Empty(result);
    }

    [Fact]
    public void BinarySearch_IgnoresDifferentEntityIds()
    {
        var meta    = new TopicMetadata(typeof(BdcEntityMasterTopic));
        var samples = new List<SampleData>
        {
            MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 99, Name = "other" }, T1),
            MakeAliveSample<BdcEntityMasterTopic>(new BdcEntityMasterTopic { EntityId = 1,  Name = "mine"  }, T2),
        };

        var result = TimeTravelEngine.FindLatestBeforeTime(
            samples, T3, 1, meta.KeyFields[0], null);

        // Only entity 1 should appear.
        var (partId, sample) = Assert.Single(result);
        Assert.Null(partId); // no PartId field
        Assert.Equal("mine", ((BdcEntityMasterTopic)sample.Payload).Name);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IsAliveSample helper tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsAliveSample_DefaultInstanceState_ReturnsTrue()
        => Assert.True(TimeTravelEngine.IsAliveSample(default));

    [Fact]
    public void IsAliveSample_DdsAlive_ReturnsTrue()
        => Assert.True(TimeTravelEngine.IsAliveSample(DdsInstanceState.Alive));

    [Fact]
    public void IsAliveSample_NotAliveDisposed_ReturnsFalse()
        => Assert.False(TimeTravelEngine.IsAliveSample(DdsInstanceState.NotAliveDisposed));

    [Fact]
    public void IsAliveSample_NotAliveNoWriters_ReturnsFalse()
        => Assert.False(TimeTravelEngine.IsAliveSample(DdsInstanceState.NotAliveNoWriters));

    // ──────────────────────────────────────────────────────────────────────────
    // Namespace filter — non-BDC topics are ignored
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TimeTravel_TopicOutsideNamespacePrefix_IsIgnored()
    {
        // OtherNamespaceTopic has "other.NS." namespace prefix.
        _sampleStore.Add(MakeAliveSample<OtherNamespaceTopic>(
            new OtherNamespaceTopic { EntityId = 60, Value = 7 }, T1));

        var result = _engine.GetHistoricalState(60, T2);

        Assert.Equal(EntityState.Dead, result.EntityState);
        Assert.Empty(result.Descriptors);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a live DDS sample with <c>InstanceState = default (0)</c>,
    /// which the engine treats as alive (mirrors InstanceStore.MapInstanceState's
    /// catch-all default).</summary>
    private static SampleData MakeAliveSample<TTopic>(object payload, DateTime timestamp)
        where TTopic : struct
        => new SampleData
        {
            Payload       = payload,
            TopicMetadata = new TopicMetadata(typeof(TTopic)),
            SampleInfo    = default, // InstanceState = 0 → treated as alive
            Timestamp     = timestamp,
        };

    /// <summary>Creates a disposal DDS sample with <c>InstanceState = NotAliveDisposed</c>.</summary>
    private static SampleData MakeDisposalSample<TTopic>(object payload, DateTime timestamp)
        where TTopic : struct
        => new SampleData
        {
            Payload       = payload,
            TopicMetadata = new TopicMetadata(typeof(TTopic)),
            SampleInfo    = new DdsApi.DdsSampleInfo
            {
                InstanceState = DdsInstanceState.NotAliveDisposed
            },
            Timestamp     = timestamp,
        };
}
