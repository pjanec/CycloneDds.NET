using System;

namespace DdsMonitor.Engine;

/// <summary>
/// No-op reader used when a topic cannot be subscribed.
/// </summary>
internal sealed class NullDynamicReader : IDynamicReader
{
    public NullDynamicReader(TopicMetadata topicMetadata, string? errorMessage)
    {
        TopicMetadata = topicMetadata ?? throw new ArgumentNullException(nameof(topicMetadata));
        TopicType = topicMetadata.TopicType;
        ErrorMessage = errorMessage;
    }

    public Type TopicType { get; }

    public TopicMetadata TopicMetadata { get; }

    public string? ErrorMessage { get; }

    public event Action<SampleData>? OnSampleReceived
    {
        add { }
        remove { }
    }

    public void Start(string? partition)
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
