namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the reliability QoS policy.
/// </summary>
public enum DdsReliability
{
    /// <summary>
    /// Indicates that it is acceptable to not retry propagation of any samples.
    /// </summary>
    BestEffort = 0,

    /// <summary>
    /// Specifies that the service will attempt to deliver all samples in its history.
    /// </summary>
    Reliable = 1
}
