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

    // ── ME1-T07: Participant stamping ────────────────────────────────────────

    /// <summary>Gets the DDS domain identifier of the participant that received this sample.</summary>
    public uint DomainId { get; init; }

    /// <summary>Gets the partition name the participant was listening on when the sample arrived.</summary>
    public string PartitionName { get; init; } = string.Empty;

    /// <summary>Gets the zero-based index of the participant in <see cref="IDdsBridge.Participants"/> that received this sample.</summary>
    public int ParticipantIndex { get; init; }
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
