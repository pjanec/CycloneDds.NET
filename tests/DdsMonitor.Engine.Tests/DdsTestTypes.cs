using CycloneDDS.Schema;
using System.Runtime.CompilerServices;

namespace DdsMonitor.Engine.Tests;

[DdsTopic("Sample")]
public partial struct SampleTopic
{
    public int Id;
}

[DdsTopic("MockTopic")]
public partial struct MockTopic
{
    public int Id;
}

[DdsTopic("DynamicReaderTopic")]
public partial struct DynamicReaderMessage
{
    public int Id;
    public int Value;
}

[DdsTopic("DynamicWriterTopic")]
public partial struct DynamicWriterMessage
{
    public int Id;
    public int Value;
}

[DdsTopic("DynamicWriterKeyed")]
public partial struct DynamicWriterKeyedMessage
{
    [DdsKey]
    public int Id;

    public int Value;
}

[DdsTopic("Simple")]
public partial struct SimpleType
{
    public int Count;
}

[DdsTopic("InstanceKeyed")]
public partial struct InstanceKeyedMessage
{
    [DdsKey]
    public int Id;

    public int Value;
}

[DdsTopic("InstanceComposite")]
public partial struct InstanceCompositeKeyMessage
{
    [DdsKey]
    public int EntityId;

    [DdsKey]
    public int PartId;

    public int Value;
}

[DdsTopic("RobotTopic")]
public partial struct RobotTopic
{
    public int Id;
}

[DdsTopic("OtherTopic")]
public partial struct OtherTopic
{
    public int Id;
}

public enum SampleStatus
{
    Unknown = 0,
    Active = 1,
    Inactive = 2
}

[DdsTopic("StatusTopic")]
public partial struct StatusTopic
{
    public int Id;
    public SampleStatus Status;
}

[DdsTopic("StringTopic")]
[DdsManaged]
public partial struct StringTopic
{
    public int Id;
    public string Message;
}

// ─── Array / fixed-buffer test types ────────────────────────────────────────

/// <summary>Topic type with a plain managed int[] field (DDS sequence).</summary>
[DdsTopic("IntArrayTopic")]
public partial struct IntArrayTopic
{
    public int Id;
    public int[] Values;
}

/// <summary>Topic type with a <c>List&lt;float&gt;</c> field (DDS sequence).</summary>
[DdsTopic("FloatListTopic")]
[DdsManaged]
public partial struct FloatListTopic
{
    public int Id;
    public System.Collections.Generic.List<float> Samples;
}

/// <summary>Topic type with a C# fixed-size byte buffer.</summary>
[DdsTopic("FixedByteBufferTopic")]
public unsafe partial struct FixedByteBufferTopic
{
    public int Id;
    public unsafe fixed byte Payload[8];
}

/// <summary>Topic type with a C# fixed-size int buffer.</summary>
[DdsTopic("FixedIntBufferTopic")]
public unsafe partial struct FixedIntBufferTopic
{
    public int Id;
    public unsafe fixed int Readings[4];
}

/// <summary>Topic type with a nested struct that contains a fixed buffer.</summary>
[DdsTopic("NestedFixedBufferTopic")]
public unsafe partial struct NestedFixedBufferTopic
{
    public int Id;
    public NestedSensorData Sensor;
}

[DdsStruct]
public unsafe partial struct NestedSensorData
{
    public short Channel;
    public unsafe fixed byte Data[4];
}

/// <summary>Topic with both a dynamic array and a fixed buffer.</summary>
[DdsTopic("MixedArrayTopic")]
public unsafe partial struct MixedArrayTopic
{
    public int Id;
    public int[] DynamicValues;
    public unsafe fixed float FixedFloats[3];
}

// ─── ME1-T02: [InlineArray] test types ──────────────────────────────────────

/// <summary>
/// InlineArray struct holding 8 floats.  Used to verify metadata and JSON for [InlineArray].
/// </summary>
[InlineArray(8)]
public struct FloatBuf8
{
    public float _elem;
}

/// <summary>
/// InlineArray struct holding 4 ints.  Used to verify JSON serialization produces [1,2,3,4].
/// </summary>
[InlineArray(4)]
public struct IntBuf4
{
    public int _elem;
}

/// <summary>Topic with a [InlineArray(8)] float field.</summary>
[DdsTopic("InlineArrayFloatTopic")]
public partial struct InlineArrayFloatTopic
{
    public int Id;
    public FloatBuf8 Data;
}

/// <summary>Topic with a [InlineArray(4)] int field.</summary>
[DdsTopic("InlineArrayIntTopic")]
public partial struct InlineArrayIntTopic
{
    public int Id;
    public IntBuf4 Values;
}

// ─── ME1-T03: Default topic name test types ───────────────────────────────────

/// <summary>
/// Topic with no explicit name – TopicName should fall back to
/// "DdsMonitor_Engine_Tests_DefaultNameTopic".
/// </summary>
[DdsTopic]
public partial struct DefaultNameTopic
{
    public int Value;
}

/// <summary>
/// Topic with an explicit name – TopicName should be "ExplicitNameTopic".
/// </summary>
[DdsTopic("ExplicitNameTopic")]
public partial struct ExplicitNamedTopic
{
    public int Value;
}
