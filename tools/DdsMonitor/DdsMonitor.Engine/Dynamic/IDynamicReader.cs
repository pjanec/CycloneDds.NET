using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides a non-generic DDS reader abstraction.
/// </summary>
public interface IDynamicReader : IDisposable
{
    /// <summary>
    /// Gets the CLR topic type.
    /// </summary>
    Type TopicType { get; }

    /// <summary>
    /// Gets the topic metadata for this reader.
    /// </summary>
    TopicMetadata TopicMetadata { get; }

    /// <summary>
    /// Starts the reader with the given partition.
    /// </summary>
    void Start(string? partition);

    /// <summary>
    /// Stops the reader and releases resources.
    /// </summary>
    void Stop();

    /// <summary>
    /// Raised for each received sample.
    /// </summary>
    event Action<SampleData>? OnSampleReceived;
}
