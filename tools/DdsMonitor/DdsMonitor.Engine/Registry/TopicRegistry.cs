using System;
using System.Collections.Generic;

namespace DdsMonitor.Engine;

/// <summary>
/// Default in-memory topic registry.
/// </summary>
public sealed class TopicRegistry : ITopicRegistry
{
    private readonly object _sync = new();
    private readonly List<TopicMetadata> _topics = new();
    private readonly Dictionary<Type, TopicMetadata> _topicsByType = new();
    private readonly Dictionary<string, TopicMetadata> _topicsByName = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<TopicMetadata> AllTopics
    {
        get
        {
            lock (_sync)
            {
                return _topics.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public TopicMetadata? GetByType(Type topicType)
    {
        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        lock (_sync)
        {
            _topicsByType.TryGetValue(topicType, out var metadata);
            return metadata;
        }
    }

    /// <inheritdoc />
    public TopicMetadata? GetByName(string topicName)
    {
        if (topicName == null)
        {
            throw new ArgumentNullException(nameof(topicName));
        }

        lock (_sync)
        {
            _topicsByName.TryGetValue(topicName, out var metadata);
            return metadata;
        }
    }

    /// <inheritdoc />
    public void Register(TopicMetadata meta)
    {
        if (meta == null)
        {
            throw new ArgumentNullException(nameof(meta));
        }

        lock (_sync)
        {
            if (_topicsByType.ContainsKey(meta.TopicType) || _topicsByName.ContainsKey(meta.TopicName))
            {
                return;
            }

            _topics.Add(meta);
            _topicsByType[meta.TopicType] = meta;
            _topicsByName[meta.TopicName] = meta;
        }
    }
}
