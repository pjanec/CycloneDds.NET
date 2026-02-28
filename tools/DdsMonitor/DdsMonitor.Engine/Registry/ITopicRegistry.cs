using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides access to discovered DDS topics and their metadata.
/// </summary>
public interface ITopicRegistry
{
    /// <summary>
    /// Gets all registered topics.
    /// </summary>
    IReadOnlyList<TopicMetadata> AllTopics { get; }

    /// <summary>
    /// Gets metadata by CLR topic type.
    /// </summary>
    TopicMetadata? GetByType(Type topicType);

    /// <summary>
    /// Gets metadata by DDS topic name.
    /// </summary>
    TopicMetadata? GetByName(string topicName);

    /// <summary>
    /// Registers metadata for a discovered topic.
    /// </summary>
    void Register(TopicMetadata meta);
}
