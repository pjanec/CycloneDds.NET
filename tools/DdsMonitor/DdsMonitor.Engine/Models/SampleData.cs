using System;
using CycloneDDS.Runtime.Interop;

namespace DdsMonitor.Engine;

/// <summary>
/// Represents a single DDS sample with metadata used by the monitor.
/// </summary>
public record SampleData
{
    public long Ordinal { get; init; }

    public object Payload { get; init; } = default!;

    public TopicMetadata TopicMetadata { get; init; } = default!;

    public DdsApi.DdsSampleInfo SampleInfo { get; init; }

    public SenderIdentity? Sender { get; init; }

    public DateTime Timestamp { get; init; }

    public int SizeBytes { get; init; }
}

/// <summary>
/// Identifies the sender of a DDS sample.
/// </summary>
public record SenderIdentity
{
    public uint ProcessId { get; init; }

    public string? MachineName { get; init; }

    public string? IpAddress { get; init; }
}
