using CycloneDDS.Schema;

namespace DdsMonitor.Avalonia.StandardPlugin;

/// <summary>
/// A synthetic DDS topic type used to prove the data pipeline end-to-end.
/// </summary>
[DdsTopic]
public struct HeartbeatSample
{
    [DdsKey]
    public int Id;

    public long Timestamp;

    public int Sequence;
}
