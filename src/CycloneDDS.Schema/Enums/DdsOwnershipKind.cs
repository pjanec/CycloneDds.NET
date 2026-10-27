namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the ownership QoS policy.
/// </summary>
public enum DdsOwnershipKind
{
    /// <summary>
    /// Every writer may update an instance; readers see the updates of all writers.
    /// </summary>
    Shared = 0,

    /// <summary>
    /// Only one writer (the one with the highest ownership strength that is alive) owns an
    /// instance at a time; readers only see that writer's updates. When the owner unregisters,
    /// loses liveliness or goes away, ownership passes to the next strongest writer.
    /// </summary>
    Exclusive = 1
}
