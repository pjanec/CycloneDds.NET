using System;
using System.Collections.Generic;
using System.Linq;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.Bdc.Tests;

/// <summary>
/// Minimal <see cref="ISampleStore"/> stub for unit-testing
/// <see cref="TimeTravelEngine"/> without a live DDS bus.
/// </summary>
internal sealed class StubSampleStore : ISampleStore
{
    private readonly List<SampleData> _samples = new();

    public event Action? Cleared;

    public IReadOnlyList<SampleData> AllSamples => _samples;

    public int TotalCount => _samples.Count;

    public long TotalBytesReceived => 0;

    /// <summary>Appends a sample (alias for <see cref="Append"/> with a friendlier name).</summary>
    public void Add(SampleData sample) => _samples.Add(sample);

    public void Append(SampleData sample) => _samples.Add(sample);

    public SampleData[] GetSamples(int startIndex)
        => _samples.Skip(startIndex).ToArray();

    public ITopicSamples GetTopicSamples(Type topicType)
        => new StubTopicSamples(topicType,
               _samples.Where(s => s.TopicMetadata.TopicType == topicType).ToList());

    public int GetTopicCount(Type topicType)
        => _samples.Count(s => s.TopicMetadata.TopicType == topicType);

    public void Clear()
    {
        _samples.Clear();
        Cleared?.Invoke();
    }

    // ── Inner helper ──────────────────────────────────────────────────────────

    private sealed class StubTopicSamples : ITopicSamples
    {
        private readonly List<SampleData> _sorted;

        public StubTopicSamples(Type topicType, List<SampleData> samples)
        {
            TopicType = topicType;
            // Ensure chronological order so binary search works correctly.
            _sorted = samples.OrderBy(s => s.Timestamp).ToList();
        }

        public Type TopicType { get; }
        public int TotalCount => _sorted.Count;
        public IReadOnlyList<SampleData> Samples => _sorted;
    }
}
