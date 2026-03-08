using System;
using CycloneDDS.Schema;

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
public partial class SelfTestSimple
{
    [DdsKey]
    public int Id;

    public string Message;

    public double Value;

    public DateTime Timestamp;
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
}

[DdsStruct]
public partial struct Pose
{
    public Vector3 Position;

    public Vector3 Velocity;
}

[DdsStruct]
public partial struct Vector3
{
    public float X;

    public float Y;

    public float Z;
}

public enum StatusLevel
{
    Ok,
    Warning,
    Error
}
