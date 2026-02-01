using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Runtime.Interop;

using DdsGuid = CycloneDDS.Runtime.Interop.DdsGuid;

namespace CycloneDDS.Runtime.Tracking
{
    /// <summary>
    /// Central registry that correlates DDS publication handles to application identities.
    /// Singleton per DdsParticipant.
    /// </summary>
    public sealed class SenderRegistry : IDisposable
    {
        // Identity cache: GUID -> Identity (populated from Identity Topic)
        private readonly ConcurrentDictionary<DdsGuid, SenderIdentity> _guidToIdentity = new();

        // Fast lookup: PublicationHandle -> Identity (O(1))
        private readonly ConcurrentDictionary<long, SenderIdentity> _handleToIdentity = new();

        // Store Handle -> GUID mapping for lazy resolution
        private readonly ConcurrentDictionary<long, DdsGuid> _handleToGuid = new();

        // Background reader for identity topic
        private readonly DdsReader<SenderIdentity> _identityReader;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _monitorTask;
        private readonly DdsParticipant _participant;

        internal SenderRegistry(DdsParticipant participant)
        {
            _participant = participant;
            
            // QoS: Reliable + TransientLocal
            IntPtr qos = DdsApi.dds_create_qos();
            DdsApi.dds_qset_durability(qos, DdsApi.DDS_DURABILITY_TRANSIENT_LOCAL);
            DdsApi.dds_qset_reliability(qos, DdsApi.DDS_RELIABILITY_RELIABLE, 100_000_000);

            // Subscribe to identity announcements
            _identityReader = new DdsReader<SenderIdentity>(
                participant, "__FcdcSenderIdentity", qos);
            
            DdsApi.dds_delete_qos(qos);

            // Start async monitoring
            _monitorTask = MonitorIdentitiesAsync();
        }

        /// <summary>
        /// Background task: Updates _guidToIdentity as remote participants announce themselves.
        /// </summary>
        private async Task MonitorIdentitiesAsync()
        {
            try
            {
                while (!_cancellation.Token.IsCancellationRequested)
                {
                    if (await _identityReader.WaitDataAsync(_cancellation.Token))
                    {
                        ProcessIncomingIdentities();
                    }
                }
            }
            catch (OperationCanceledException) 
            { 
                // Expected on dispose 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SenderRegistry] Monitor task failed: {ex}");
            }
        }

        private void ProcessIncomingIdentities()
        {
            using var scope = _identityReader.Take(100);
            for (int i = 0; i < scope.Count; i++)
            {
                var identity = scope[i];
                _guidToIdentity[identity.ParticipantGuid] = identity;
                
                // Also retry resolving any pending handles for this participant
                // (In a real implementation we might want to optimize this)
            }
        }

        /// <summary>
        /// Called when DdsReader detects a new remote writer.
        /// Maps PublicationHandle -> ParticipantGuid -> Identity.
        /// </summary>
        public void RegisterRemoteWriter(long publicationHandle, DdsGuid writerGuid)
        {
            // Save handle -> guid mapping for lazy resolution
            _handleToGuid[publicationHandle] = writerGuid;

            // Extract participant GUID from writer GUID
            var participantGuid = ExtractParticipantGuid(writerGuid);

            if (_guidToIdentity.TryGetValue(participantGuid, out var identity))
            {
                _handleToIdentity[publicationHandle] = identity;
            }
            // If identity not found yet, it may arrive later (race condition)
            // Lazy resolution will handle this in TryGetIdentity
        }

        /// <summary>
        /// Fast O(1) lookup for UI/processing code.
        /// </summary>
        public bool TryGetIdentity(long publicationHandle, out SenderIdentity identity)
        {
            if (_handleToIdentity.TryGetValue(publicationHandle, out identity))
                return true;

            // Lazy fallback: Identity topic might have arrived after data connection
            return TryResolveLazy(publicationHandle, out identity);
        }

        private bool TryResolveLazy(long publicationHandle, out SenderIdentity identity)
        {
            // Check if we have the GUID
            if (_handleToGuid.TryGetValue(publicationHandle, out var writerGuid))
            {
                var participantGuid = ExtractParticipantGuid(writerGuid);
                if (_guidToIdentity.TryGetValue(participantGuid, out identity))
                {
                    // Cache it for next time
                    _handleToIdentity[publicationHandle] = identity;
                    return true;
                }
            }
            
            identity = default;
            return false;
        }

        private DdsGuid ExtractParticipantGuid(DdsGuid writerGuid)
        {
            // DDS GUID structure: [Prefix: 12 bytes][EntityId: 4 bytes]
            // Participant EntityId = 0x000001C1.
            // On Little Endian, 'Low' contains bytes 8-15.
            // EntityId is bytes 12-15.
            // So EntityId corresponds to the most significant 32 bits of Low.
            
            // Mask out the EntityId (top 32 bits of Low)
            long prefixMask = 0x00000000FFFFFFFF;
            
            // EntityId for Participant: 0x000001c1. In LE byte stream: 00 00 01 C1
            // C1 is at MSB (v[15]).
            // Value: 0xC101000000000000 (unchecked)
            
            long participantEntityId = unchecked((long)0xC101000000000000UL); // 00 00 01 C1 in upper bytes
            
            DdsGuid participantGuid = writerGuid;
            participantGuid.Low = (participantGuid.Low & prefixMask) | participantEntityId;
            
            return participantGuid;
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _identityReader?.Dispose();
            _cancellation.Dispose();
            
            // Wait for monitor task to complete (with timeout)
            try 
            { 
                _monitorTask?.Wait(TimeSpan.FromSeconds(1)); 
            }
            catch { /* Best effort */ }
        }
    }
}
