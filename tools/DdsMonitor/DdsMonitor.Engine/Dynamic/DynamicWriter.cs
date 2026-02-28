using System;
using CycloneDDS.Runtime;

namespace DdsMonitor.Engine;

/// <summary>
/// Generic DDS writer wrapper that accepts boxed payloads.
/// </summary>
public sealed class DynamicWriter<T> : IDynamicWriter
{
    private readonly DdsWriter<T> _writer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicWriter{T}"/> class.
    /// </summary>
    public DynamicWriter(DdsParticipant participant, TopicMetadata topicMetadata, string? partition = null)
    {
        if (participant == null)
        {
            throw new ArgumentNullException(nameof(participant));
        }

        if (topicMetadata == null)
        {
            throw new ArgumentNullException(nameof(topicMetadata));
        }

        _writer = new DdsWriter<T>(participant, topicMetadata.TopicName, partition: partition);
    }

    /// <inheritdoc />
    public Type TopicType => typeof(T);

    /// <inheritdoc />
    public void Write(object payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        _writer.Write((T)payload);
    }

    /// <inheritdoc />
    public void DisposeInstance(object payload)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        _writer.DisposeInstance((T)payload);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }
}
