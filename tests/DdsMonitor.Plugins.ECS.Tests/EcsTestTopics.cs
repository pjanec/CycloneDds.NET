using CycloneDDS.Schema;
#pragma warning disable CS0649  // Test topic fields are filled via reflection / TopicMetadata accessors

namespace DdsMonitor.Plugins.ECS.Tests;

// ── ECS-style topics used by EntityStore unit tests ──────────────────────────
// These are non-partial structs; no DDS code generation is required because
// TopicMetadata is built via pure reflection on [DdsTopic] / [DdsKey] attributes.

/// <summary>Simulates a ECS Master descriptor topic (entity lifecycle token).</summary>
[DdsTopic("company.ECS.EntityMaster")]
internal struct EcsEntityMasterTopic
{
    [DdsKey]
    public int EntityId;
    public string Name;
}

/// <summary>Simulates a ECS non-master descriptor (entity data).</summary>
[DdsTopic("company.ECS.EntityInfo")]
internal struct EcsEntityInfoTopic
{
    [DdsKey]
    public int EntityId;
    public string Description;
}

/// <summary>Simulates a ECS topic that supports both EntityId and PartId keys.</summary>
[DdsTopic("company.ECS.PartDescriptor")]
internal struct EcsPartDescriptorTopic
{
    [DdsKey]
    public int EntityId;

    [DdsKey]
    public int PartId;

    public int Value;
}

/// <summary>A topic whose key field has an invalid (double) type for DMON-062 testing.</summary>
[DdsTopic("company.ECS.InvalidKeyTopic")]
internal struct EcsInvalidKeyTopic
{
    [DdsKey]
    public double EntityId;   // double is NOT a valid integer key type → must be rejected

    public int Value;
}

/// <summary>A topic from a completely different namespace — should be ignored when prefix filter is active.</summary>
[DdsTopic("other.NS.SomeTopic")]
internal struct OtherNamespaceTopic
{
    [DdsKey]
    public int EntityId;

    public int Value;
}

/// <summary>A non-ECS topic with no EntityId-matching field.</summary>
[DdsTopic("company.ECS.NoEntityIdTopic")]
internal struct EcsNoEntityIdTopic
{
    [DdsKey]
    public int SomeOtherId;   // "SomeOtherId" won't match the default EntityId regex

    public int Value;
}
