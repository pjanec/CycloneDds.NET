namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the built-in wire representation for a custom mapped type.
/// </summary>
public enum DdsWire
{
    /// <summary>
    /// Maps to octet[16], formatted as RFC4122 UUID bytes.
    /// </summary>
    Guid16,

    /// <summary>
    /// Maps to int64, representing UTC ticks since epoch.
    /// </summary>
    Int64TicksUtc,

    /// <summary>
    /// Maps to struct { float x,y,z,w }.
    /// </summary>
    QuaternionF32x4,

    /// <summary>
    /// Maps to octet[32], UTF-8 NUL-padded.
    /// </summary>
    FixedUtf8Bytes32,

    /// <summary>
    /// Maps to octet[64], UTF-8 NUL-padded.
    /// </summary>
    FixedUtf8Bytes64,

    /// <summary>
    /// Maps to octet[128], UTF-8 NUL-padded.
    /// </summary>
    FixedUtf8Bytes128
}
