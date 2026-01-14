namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the history code QoS policy.
/// </summary>
public enum DdsHistoryKind
{
    /// <summary>
    /// Keep only the last N samples, defined by HistoryDepth.
    /// </summary>
    KeepLast = 0,

    /// <summary>
    /// Keep all samples until the ResourceLimits are hit.
    /// </summary>
    KeepAll = 1
}
