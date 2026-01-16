using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Schema.Metadata;

namespace CycloneDDS.CodeGen.Runtime;

public static class MetadataRegistry
{
    private static readonly List<TopicMetadata> _topics = new();

    public static void Register(TopicMetadata metadata)
    {
        lock (_topics)
        {
            _topics.Add(metadata);
        }
    }

    public static IEnumerable<TopicMetadata> GetAllTopics()
    {
        lock (_topics)
        {
            return _topics.ToList();
        }
    }
}
