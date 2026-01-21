using System;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Application-level identity_broadcast by each participant.
    /// Used to correlate DDS publication handles to user metadata.
    /// </summary>
    [DdsTopic("__FcdcSenderIdentity")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SenderIdentity
    {
        // ... fields ...

        /// <summary>
        /// Native DDS Participant GUID (16 bytes) - used as correlation key.
        /// </summary>
        [DdsKey, DdsId(0)]
        public DdsGuid ParticipantGuid;

        /// <summary>
        /// User-defined application domain identifier.
        /// </summary>
        [DdsId(1)]
        public int AppDomainId;

        /// <summary>
        /// User-defined application instance identifier.
        /// </summary>
        [DdsId(2)]
        public int AppInstanceId;

        /// <summary>
        /// Machine name where process is running.
        /// </summary>
        [DdsManaged, DdsId(3)]
        public string ComputerName;

        /// <summary>
        /// Process executable name.
        /// </summary>
        [DdsManaged, DdsId(4)]
        public string ProcessName;

        /// <summary>
        /// OS Process ID (disambiguates multiple instances of same exe).
        /// </summary>
        [DdsId(5)]
        public int ProcessId;
    }
}

