using System;
using System.Collections.Generic;
using System.Threading;

namespace DdsMonitor.Engine;

/// <summary>
/// Append-only, thread-safe store for DDS samples.
/// All filtering, sorting, and virtualized views are handled by per-window
/// <see cref="SampleView"/> instances.
/// </summary>
public sealed class SampleStore : ISampleStore
{
    private readonly object _sync = new();
    private readonly List<SampleData> _allSamples = new();
    private readonly Dictionary<Type, TopicSamples> _samplesByTopic = new();

    private long _totalBytesReceived;

    /// <inheritdoc />
    public event Action? Cleared;

    /// <inheritdoc />
    public IReadOnlyList<SampleData> AllSamples
    {
        get
        {
            lock (_sync)
            {
                return _allSamples.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public int TotalCount
    {
        get
        {
            lock (_sync)
            {
                return _allSamples.Count;
            }
        }
    }

    /// <inheritdoc />
    public long TotalBytesReceived => Interlocked.Read(ref _totalBytesReceived);

    /// <inheritdoc />
    public SampleData[] GetSamples(int startIndex)
    {
        lock (_sync)
        {
            var count = _allSamples.Count - startIndex;
            if (count <= 0)
            {
                return Array.Empty<SampleData>();
            }

            var result = new SampleData[count];
            _allSamples.CopyTo(startIndex, result, 0, count);
            return result;
        }
    }

    /// <inheritdoc />
    public ITopicSamples GetTopicSamples(Type topicType)
    {
        if (topicType == null)
        {
            throw new ArgumentNullException(nameof(topicType));
        }

        lock (_sync)
        {
            if (!_samplesByTopic.TryGetValue(topicType, out var samples))
            {
                samples = new TopicSamples(topicType);
                _samplesByTopic[topicType] = samples;
            }

            return samples;
        }
    }

    /// <inheritdoc />
    public void Append(SampleData sample)
    {
        if (sample == null)
        {
            throw new ArgumentNullException(nameof(sample));
        }

        Interlocked.Add(ref _totalBytesReceived, sample.SizeBytes);

        lock (_sync)
        {
            _allSamples.Add(sample);

            if (!_samplesByTopic.TryGetValue(sample.TopicMetadata.TopicType, out var topicSamples))
            {
                topicSamples = new TopicSamples(sample.TopicMetadata.TopicType);
                _samplesByTopic[sample.TopicMetadata.TopicType] = topicSamples;
            }

            topicSamples.Add(sample);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_sync)
        {
            _allSamples.Clear();
            // TrimExcess releases the large LOH backing array (8 bytes × N elements).
            // Without this the array stays allocated at full capacity even though all
            // entries have been nulled out by Clear(), keeping the list's segment in the
            // LOH alive until the SampleStore itself is disposed.
            _allSamples.TrimExcess();
            _samplesByTopic.Clear();
        }

        Interlocked.Exchange(ref _totalBytesReceived, 0);
        Cleared?.Invoke();
    }

    private sealed class TopicSamples : ITopicSamples
    {
        private readonly List<SampleData> _samples = new();

        public TopicSamples(Type topicType)
        {
            TopicType = topicType ?? throw new ArgumentNullException(nameof(topicType));
        }

        public Type TopicType { get; }

        public int TotalCount => _samples.Count;

        public IReadOnlyList<SampleData> Samples => _samples;

        public void Add(SampleData sample)
        {
            _samples.Add(sample);
        }
    }
}