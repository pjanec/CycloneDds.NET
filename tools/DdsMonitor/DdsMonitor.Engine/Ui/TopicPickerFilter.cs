using System;
using System.Collections.Generic;
using System.Linq;

namespace DdsMonitor.Engine;

/// <summary>
/// Filters topics for incremental search pickers.
/// </summary>
public static class TopicPickerFilter
{
    /// <summary>
    /// Filters topics by short name or namespace using a case-insensitive contains match.
    /// </summary>
    public static IReadOnlyList<TopicMetadata> FilterTopics(IEnumerable<TopicMetadata> topics, string query)
    {
        if (topics == null)
        {
            throw new ArgumentNullException(nameof(topics));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return topics.ToList();
        }

        var trimmed = query.Trim();
        return topics.Where(topic => Matches(topic, trimmed)).ToList();
    }

    /// <summary>
    /// Returns true when the topic short name or namespace matches the query.
    /// </summary>
    public static bool Matches(TopicMetadata topic, string query)
    {
        if (topic == null)
        {
            throw new ArgumentNullException(nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return topic.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               topic.Namespace.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
