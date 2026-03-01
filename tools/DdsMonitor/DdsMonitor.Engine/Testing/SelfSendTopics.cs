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
public sealed class SelfTestSimple
{
    [DdsKey]
    public int Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime Timestamp { get; set; }
}

[DdsTopic("SelfTest.Pose")]
public sealed class SelfTestPose
{
    [DdsKey]
    public int Id { get; set; }

    public Pose Pose { get; set; }

    public float[] Samples { get; set; } = Array.Empty<float>();

    public StatusLevel Level { get; set; }
}

public struct Pose
{
    public Vector3 Position { get; set; }

    public Vector3 Velocity { get; set; }
}

public struct Vector3
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }
}

public enum StatusLevel
{
    Ok,
    Warning,
    Error
}
