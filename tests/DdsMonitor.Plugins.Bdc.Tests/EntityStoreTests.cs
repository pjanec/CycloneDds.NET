using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DdsMonitor.Engine;
using DdsMonitor.Plugins.Bdc;

namespace DdsMonitor.Plugins.Bdc.Tests;

/// <summary>
/// Unit tests for MON-BATCH-29:
///   DMON-045 — EntityStore Core (Aggregation Engine)
///   DMON-061 — Regex-based key extraction
///   DMON-062 — Numeric type validation for key fields
///   DMON-060 — Settings hot-reload / re-aggregation
/// </summary>
public sealed class EntityStoreTests : System.IDisposable
{
    private readonly StubInstanceStore _instanceStore;
    private readonly BdcSettings _settings;
    private readonly EntityStore _store;

    public EntityStoreTests()
    {
        _instanceStore = new StubInstanceStore();
        _settings = new BdcSettings
        {
            NamespacePrefix    = "company.BDC",
            EntityIdPattern    = @"(?i)\bEntityId\b",
            PartIdPattern      = @"(?i)\bPartId\b",
            MasterTopicPattern = @"Master$"
        };
        _store = new EntityStore(_instanceStore, _settings);
    }

    public void Dispose() => _store.Dispose();

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-045: Alive / Zombie / Dead state machine
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_NewMasterDescriptor_CreatesAliveEntity()
    {
        // Arrange — fire an Alive event for the Master topic.
        var evt = TestEventFactory.AliveEvent<BdcEntityMasterTopic>(
            TransitionKind.Added,
            keyValues: new object[] { 42 });

        // Act
        _instanceStore.Raise(evt);

        // Assert
        var entities = _store.Entities;
        Assert.True(entities.ContainsKey(42));
        Assert.Equal(EntityState.Alive, entities[42].State);
    }

    [Fact]
    public void EntityStore_NonMasterOnly_CreatesZombieEntity()
    {
        // Arrange — fire an Alive event for a NON-master topic (EntityInfo).
        var evt = TestEventFactory.AliveEvent<BdcEntityInfoTopic>(
            TransitionKind.Added,
            keyValues: new object[] { 7 });

        // Act
        _instanceStore.Raise(evt);

        // Assert — no Master → Zombie
        var entities = _store.Entities;
        Assert.True(entities.ContainsKey(7));
        Assert.Equal(EntityState.Zombie, entities[7].State);
    }

    [Fact]
    public void EntityStore_DisposeMaster_TransitionsToZombie()
    {
        // Arrange — add both Master and Info descriptors.
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(TransitionKind.Added, new object[] { 10 }));
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityInfoTopic>(TransitionKind.Added, new object[] { 10 }));

        Assert.Equal(EntityState.Alive, _store.Entities[10].State);

        // Act — dispose the Master.
        _instanceStore.Raise(TestEventFactory.RemovedEvent<BdcEntityMasterTopic>(new object[] { 10 }));

        // Assert — still has Info → Zombie.
        Assert.Equal(EntityState.Zombie, _store.Entities[10].State);
    }

    [Fact]
    public void EntityStore_DisposeAllDescriptors_TransitionsToDead()
    {
        // Arrange — add both descriptors.
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(TransitionKind.Added, new object[] { 5 }));
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityInfoTopic>(TransitionKind.Added, new object[] { 5 }));

        // Act — dispose both.
        _instanceStore.Raise(TestEventFactory.RemovedEvent<BdcEntityMasterTopic>(new object[] { 5 }));
        _instanceStore.Raise(TestEventFactory.RemovedEvent<BdcEntityInfoTopic>(new object[] { 5 }));

        // Assert
        Assert.Equal(EntityState.Dead, _store.Entities[5].State);
        Assert.Empty(_store.Entities[5].Descriptors);
    }

    [Fact]
    public void EntityStore_MultiInstanceDescriptor_TracksPartIdsSeparately()
    {
        // Arrange — two samples for the same EntityId but different PartIds.
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcPartDescriptorTopic>(
            TransitionKind.Added, keyValues: new object[] { 99, 1 }));
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcPartDescriptorTopic>(
            TransitionKind.Added, keyValues: new object[] { 99, 2 }));

        // Also add the Master so we can confirm the full descriptor count.
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(
            TransitionKind.Added, keyValues: new object[] { 99 }));

        // Assert — Master(no partId) + PartDescriptor(partId=1) + PartDescriptor(partId=2) = 3 descriptors.
        var entity = _store.Entities[99];
        Assert.Equal(3, entity.Descriptors.Count);
        Assert.Equal(EntityState.Alive, entity.State);
    }

    [Fact]
    public void EntityStore_Journal_RecordsTransitions()
    {
        // Exercise: Dead → Zombie → Alive → Zombie → Dead
        int id = 77;

        // Dead → Zombie (first non-master descriptor)
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityInfoTopic>(TransitionKind.Added, new object[] { id }));
        // Zombie → Alive (Master added)
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(TransitionKind.Added, new object[] { id }));
        // Alive → Zombie (Master removed)
        _instanceStore.Raise(TestEventFactory.RemovedEvent<BdcEntityMasterTopic>(new object[] { id }));
        // Zombie → Dead (non-master removed)
        _instanceStore.Raise(TestEventFactory.RemovedEvent<BdcEntityInfoTopic>(new object[] { id }));

        var journal = _store.Entities[id].Journal;

        // We expect exactly 4 transitions: to Zombie, to Alive, to Zombie, and to Dead.
        Assert.Equal(4, journal.Count);
        Assert.Equal(EntityState.Zombie, journal[0].NewState);
        Assert.Equal(EntityState.Alive,  journal[1].NewState);
        Assert.Equal(EntityState.Zombie, journal[2].NewState);
        Assert.Equal(EntityState.Dead,   journal[3].NewState);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-061: Generic regex extraction from StructuredName
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_RegexExtractsCorrectKeyFields_EntityId()
    {
        // Verify that the EntityId field index is found via the regex, not by position.
        var meta = new TopicMetadata(typeof(BdcEntityMasterTopic));
        var regex = new Regex(@"(?i)\bEntityId\b", RegexOptions.Compiled);

        bool found = EntityStore.TryFindKeyField(meta, regex, out int idx);

        Assert.True(found);
        Assert.Equal("EntityId", meta.KeyFields[idx].StructuredName);
    }

    [Fact]
    public void EntityStore_RegexExtractsCorrectKeyFields_PartId()
    {
        // Verify that the PartId field is correctly located in a multi-key topic.
        var meta = new TopicMetadata(typeof(BdcPartDescriptorTopic));
        var entityIdRegex = new Regex(@"(?i)\bEntityId\b", RegexOptions.Compiled);
        var partIdRegex   = new Regex(@"(?i)\bPartId\b",   RegexOptions.Compiled);

        bool foundEntityId = EntityStore.TryFindKeyField(meta, entityIdRegex, out int entityIdx);
        bool foundPartId   = EntityStore.TryFindKeyField(meta, partIdRegex,   out int partIdx, skipIndex: entityIdx);

        Assert.True(foundEntityId);
        Assert.True(foundPartId);
        Assert.Equal("EntityId", meta.KeyFields[entityIdx].StructuredName);
        Assert.Equal("PartId",   meta.KeyFields[partIdx].StructuredName);
        Assert.NotEqual(entityIdx, partIdx);
    }

    [Fact]
    public void EntityStore_TopicWithNoEntityIdField_IsIgnored()
    {
        // BdcNoEntityIdTopic has a key field "SomeOtherId" that won't match the EntityId regex.
        var evt = TestEventFactory.AliveEvent<BdcNoEntityIdTopic>(
            TransitionKind.Added, keyValues: new object[] { 1 });

        _instanceStore.Raise(evt);

        // EntityStore must NOT create any entity for this topic.
        Assert.Empty(_store.Entities);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-062: Integer type validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_InvalidNumericKeyType_TopicIsRejected()
    {
        // BdcInvalidKeyTopic has a double EntityId, which must cause the topic to be
        // rejected even though the field name matches the EntityId regex.
        var evt = TestEventFactory.AliveEvent<BdcInvalidKeyTopic>(
            TransitionKind.Added, keyValues: new object[] { 3.14 });

        _instanceStore.Raise(evt);

        // The event must be silently dropped — no entity created.
        Assert.Empty(_store.Entities);
    }

    [Theory]
    [InlineData(typeof(sbyte))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(ushort))]
    [InlineData(typeof(int))]
    [InlineData(typeof(uint))]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    public void EntityStore_AllValidIntegerTypes_AreAccepted(System.Type type)
    {
        Assert.True(EntityStore.IsValidIntegerType(type));
    }

    [Theory]
    [InlineData(typeof(double))]
    [InlineData(typeof(float))]
    [InlineData(typeof(string))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(decimal))]
    public void EntityStore_NonIntegerTypes_AreRejected(System.Type type)
    {
        Assert.False(EntityStore.IsValidIntegerType(type));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-060: Namespace filter
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_TopicOutsideNamespacePrefix_IsIgnored()
    {
        // OtherNamespaceTopic is in "other.NS." which does NOT start with "company.BDC".
        var evt = TestEventFactory.AliveEvent<OtherNamespaceTopic>(
            TransitionKind.Added, keyValues: new object[] { 100 });

        _instanceStore.Raise(evt);

        Assert.Empty(_store.Entities);
    }

    [Fact]
    public void EntityStore_EmptyNamespacePrefix_AcceptsAllTopics()
    {
        // With an empty prefix, even "other.NS." topics should be aggregated if they
        // have a valid EntityId field.
        _settings.NamespacePrefix = string.Empty;

        var evt = TestEventFactory.AliveEvent<OtherNamespaceTopic>(
            TransitionKind.Added, keyValues: new object[] { 55 });
        _instanceStore.Raise(evt);

        Assert.True(_store.Entities.ContainsKey(55));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DMON-060: Settings hot-reload resets EntityStore
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_ChangingRegex_ResetsAggregation()
    {
        // Seed an entity.
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(
            TransitionKind.Added, keyValues: new object[] { 1 }));
        Assert.NotEmpty(_store.Entities);

        // Act — change EntityIdPattern to an unmatchable value.
        _settings.EntityIdPattern = @"(?!x)x"; // matches nothing

        // Assert — all entities cleared, Changed was raised (indirectly via empty dict).
        Assert.Empty(_store.Entities);
    }

    [Fact]
    public void EntityStore_ChangingSettings_RaisesChangedEvent()
    {
        var changeCount = 0;
        _store.Changed += () => changeCount++;

        _settings.EntityIdPattern = @"\bEntityId\b";

        Assert.True(changeCount > 0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IInstanceStore.Cleared handling
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EntityStore_OnInstanceStoreClear_ResetsAllEntities()
    {
        _instanceStore.Raise(TestEventFactory.AliveEvent<BdcEntityMasterTopic>(
            TransitionKind.Added, keyValues: new object[] { 20 }));
        Assert.NotEmpty(_store.Entities);

        _instanceStore.FireCleared();

        Assert.Empty(_store.Entities);
    }
}
