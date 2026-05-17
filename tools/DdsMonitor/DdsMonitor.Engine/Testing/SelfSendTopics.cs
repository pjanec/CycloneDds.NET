using CycloneDDS.Schema;
using System;
using System.Runtime.CompilerServices;

namespace DdsMonitor.Engine;

public static class SelfSendTopics
{
    public static readonly Type[] TopicTypes =
    {
        typeof(SelfTestSimple),
        typeof(SelfTestPose)
    };

    public static void Register(ITopicRegistry registry)
    {
        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        foreach (var topicType in TopicTypes)
        {
            registry.Register(new TopicMetadata(topicType));
        }
    }
}

[DdsTopic("SelfTest.Simple")]
[DdsManaged]
public partial struct SelfTestSimple
{
    [DdsKey]
    public int Id;

    public string Message;

    public double Value;

    public DateTime Timestamp;

    public unsafe fixed byte Data[16];
}

[DdsTopic("SelfTest.Pose")]
[DdsManaged]
public partial class SelfTestPose
{
    [DdsKey]
    public int Id;

    public Pose Pose;

    public System.Collections.Generic.List<float> Samples;
    public StatusLevel Level;

    public TestingUnion UnionValue;


    public Enum16 enum16;

}

[DdsStruct]
[DdsTypeFormat("[pos:{Position}, vel:{Velocity}]")]
public partial struct Pose
{
    public Vector3 Position;

    public Vector3 Velocity;
}

[DdsStruct]
[DdsTypeFormat("[{X}, {Y}, {Z}]")]
public partial struct Vector3
{
    public float X;

    public float Y;

    public float Z;
}

[DdsUnion]
public partial struct TestingUnion
{
    [DdsDiscriminator]
    public StatusLevel level;

	[DdsCase(StatusLevel.Ok)]
	public FixedString32 OkMessage;
    
    [DdsCase(StatusLevel.Error)]
	public FloatBuf8 EightFloatsInline;

    [DdsDefaultCase]
	[DdsManaged]
	public string DefaultMessage;
}

public enum StatusLevel : byte
{
    Ok,
    Warning,
    Error
}

public enum Enum16 : ushort
{
    Enum16_A,
    Enum16_B,
    Enum16_C
}

[InlineArray(8)]
public struct FloatBuf8
{
    public float _elem;
}
