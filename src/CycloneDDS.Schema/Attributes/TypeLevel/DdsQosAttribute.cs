using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the Quality of Service (QoS) settings for a DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsQosAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reliability QoS policy.
    /// </summary>
    public DdsReliability Reliability { get; set; } = DdsReliability.Reliable;

    /// <summary>
    /// Gets or sets the durability QoS policy.
    /// </summary>
    public DdsDurability Durability { get; set; } = DdsDurability.Volatile;

    /// <summary>
    /// Gets or sets the history kind QoS policy.
    /// </summary>
    public DdsHistoryKind HistoryKind { get; set; } = DdsHistoryKind.KeepLast;

    /// <summary>
    /// Gets or sets the history depth. Only used when HistoryKind is KeepLast.
    /// </summary>
    public int HistoryDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the writer data lifecycle QoS policy (writers only).
    /// When <c>true</c> (the DDS default), unregistering an instance also disposes it, so
    /// readers see <c>NotAliveDisposed</c>. Set to <c>false</c> to hand an instance over to
    /// another writer: unregistering then only releases this writer's ownership, and readers
    /// see <c>NotAliveNoWriters</c> once no writer is left.
    /// </summary>
    public bool AutodisposeUnregisteredInstances { get; set; } = true;

    /// <summary>
    /// Gets or sets the ownership QoS policy. Readers and writers must agree on it, which a
    /// type-level setting guarantees. The per-writer strength is set with
    /// <c>DdsWriterOptions.OwnershipStrength</c>.
    /// </summary>
    public DdsOwnershipKind Ownership { get; set; } = DdsOwnershipKind.Shared;

    /// <summary>
    /// Gets or sets the liveliness QoS policy kind.
    /// </summary>
    public DdsLivelinessKind LivelinessKind { get; set; } = DdsLivelinessKind.Automatic;

    /// <summary>
    /// Gets or sets the liveliness lease duration in milliseconds; a negative value (the
    /// default) means infinite. A writer that is not asserted alive within its lease is
    /// considered gone: readers see <c>NotAliveNoWriters</c> (or the next owner takes over).
    /// A writer's lease must not exceed the reader's for them to match.
    /// </summary>
    public int LivelinessLeaseDurationMs { get; set; } = -1;

    /// <summary>
    /// Gets or sets the deadline period in milliseconds: the maximum time between updates of
    /// each instance. A negative value (the default) means infinite. A writer's period must
    /// not exceed the reader's for them to match.
    /// </summary>
    public int DeadlineMs { get; set; } = -1;
}
