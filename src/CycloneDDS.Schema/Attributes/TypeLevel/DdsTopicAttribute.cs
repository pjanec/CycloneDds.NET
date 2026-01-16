using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a class or struct as a DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsTopicAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the DDS topic.
    /// </summary>
    public string TopicName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsTopicAttribute"/> class.
    /// </summary>
    /// <param name="topicName">The name of the topic. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when topicName is null or whitespace.</exception>
    public DdsTopicAttribute(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("Topic name cannot be null or whitespace.", nameof(topicName));
        }
        TopicName = topicName;
    }
}
