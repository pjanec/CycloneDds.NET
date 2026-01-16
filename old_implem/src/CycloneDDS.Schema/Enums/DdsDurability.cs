namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the durability QoS policy.
/// </summary>
public enum DdsDurability
{
    /// <summary>
    /// The Service does not need to keep any samples of data-instances on behalf of any DataReader that is unknown by the DataWriter at the time the instance is written.
    /// </summary>
    Volatile = 0,

    /// <summary>
    /// The Service will attempt to keep some samples so that they can be delivered to any potential late joining DataReader.
    /// </summary>
    TransientLocal = 1,

    /// <summary>
    /// The Service will attempt to keep some samples utilizing transient storage.
    /// </summary>
    Transient = 2,

    /// <summary>
    /// Data is kept on permanent storage, so that they can outlive the system session.
    /// </summary>
    Persistent = 3
}
