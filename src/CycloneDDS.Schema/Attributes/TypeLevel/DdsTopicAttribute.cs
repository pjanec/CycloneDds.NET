using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a class or struct as a DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsTopicAttribute : Attribute
{
    /// <summary>
    /// Gets the explicit name of the DDS topic, or <c>null</c> when the name should be
    /// derived from the type's fully-qualified name (dots replaced by underscores).
    /// </summary>
    public string? TopicName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsTopicAttribute"/> class with an
    /// optional explicit topic name.
    /// </summary>
    /// <param name="topicName">
    /// The name of the topic. When <c>null</c> or omitted the topic name is computed at
    /// runtime from the type's fully-qualified name with dots replaced by underscores.
    /// </param>
    public DdsTopicAttribute(string? topicName = null)
    {
        TopicName = topicName;
    }
}
