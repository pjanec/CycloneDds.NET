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
}
