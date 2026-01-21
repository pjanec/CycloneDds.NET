using System;

namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Configuration for sender tracking feature.
    /// If this is not provided to DdsParticipant, feature is disabled (zero overhead).
    /// </summary>
    public record SenderIdentityConfig
    {
        /// <summary>
        /// Application domain identifier (user-defined grouping).
        /// </summary>
        public int AppDomainId { get; init; }

        /// <summary>
        /// Application instance identifier (user-defined instance ID).
        /// </summary>
        public int AppInstanceId { get; init; }

        /// <summary>
        /// Optional override for process name (defaults to Process.ProcessName).
        /// </summary>
        public string? ProcessName { get; init; }

        /// <summary>
        /// Optional override for computer name (defaults to Environment.MachineName).
        /// </summary>
        public string? ComputerName { get; init; }

        /// <summary>
        /// If true (default), identity is kept alive until Participant disposal.
        /// If false, identity is disposed when last Writer is disposed.
        /// RECOMMENDATION: Keep true to avoid race conditions.
        /// </summary>
        public bool KeepAliveUntilParticipantDispose { get; init; } = true;
    }
}
