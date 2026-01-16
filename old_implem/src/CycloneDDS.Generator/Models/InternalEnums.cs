namespace CycloneDDS.Generator.Models;

internal enum DdsDurability
{
    Volatile = 0,
    TransientLocal = 1,
    Transient = 2,
    Persistent = 3
}

internal enum DdsHistoryKind
{
    KeepLast = 0,
    KeepAll = 1
}

internal enum DdsReliability
{
    BestEffort = 0,
    Reliable = 1
}

internal enum DdsWire
{
    Guid16,
    Int64TicksUtc,
    QuaternionF32x4,
    FixedUtf8Bytes32,
    FixedUtf8Bytes64,
    FixedUtf8Bytes128
}
