using System;

namespace DdsMonitor.Engine;

/// <summary>
/// Provides a non-generic DDS writer abstraction.
/// </summary>
public interface IDynamicWriter : IDisposable
{
    /// <summary>
    /// Gets the CLR topic type.
    /// </summary>
    Type TopicType { get; }

    /// <summary>
    /// Writes a payload instance.
    /// </summary>
    void Write(object payload);

    /// <summary>
    /// Disposes a keyed instance.
    /// </summary>
    void DisposeInstance(object payload);
}
