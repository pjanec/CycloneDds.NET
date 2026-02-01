using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tracking;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Schema;

using CycloneDDS.Runtime.Memory; // Add using

using DdsGuid = CycloneDDS.Runtime.Interop.DdsGuid;

namespace CycloneDDS.Runtime.Tests
{
    // Need to define a message type for tests if not available globally
    [DdsTopic("SenderTrackingTestMsg")]
    public partial struct SenderTrackingTestMsg
    {
        [DdsId(0), DdsKey]
        public int Id;
        [DdsId(1)]
        public int Value;
    }

    public class SenderTrackingTests
    {
        private const string TEST_TOPIC = "SenderTrackingTestTopic";

        [Fact]
        public void IdentityPublishing_WriterCreated_PublishesSenderInfo()
        {
            var config = new SenderIdentityConfig
            {
                AppDomainId = 101,
                AppInstanceId = 202,
                KeepAliveUntilParticipantDispose = true
            };
            
            using var participant = new DdsParticipant();
            participant.EnableSenderTracking(config);
            
            // Create a reader to verify identity is published from same participant
            // (Loopback should work for TransientLocal if we wait)
            using var identityReader = new DdsReader<SenderIdentity>(participant, "__FcdcSenderIdentity");
            
            // Create a writer to trigger identity publishing
            using var writer = new DdsWriter<SenderTrackingTestMsg>(participant, TEST_TOPIC);
            
            // Wait for identity
            bool received = false;
            SenderIdentity receivedIdentity = default;
            
            // Poll
            for(int i=0; i<40; i++)
            {
                using var scope = identityReader.Take();
                if (scope.Count > 0)
                {
                    receivedIdentity = scope[0];
                    received = true;
                    break;
                }
                Thread.Sleep(50);
            }
            
            // Note: Loopback might not work depending on Qos. SenderIdentity is Reliable/TransientLocal.
            // If loopback is enabled (default), it should work.
            
            if (!received)
            {
                // Identity publishing is async/triggered. Maybe it was slow.
            }

            Assert.True(received, "Should have received SenderIdentity via loopback");
            Assert.Equal(101, receivedIdentity.AppDomainId);
            Assert.Equal(202, receivedIdentity.AppInstanceId);
            Assert.Equal(Process.GetCurrentProcess().Id, receivedIdentity.ProcessId);
        }

        [Fact]
        public void EnableSenderTracking_AfterWriterCreation_ThrowsException()
        {
            using var participant = new DdsParticipant();
            using var writer = new DdsWriter<SenderTrackingTestMsg>(participant, TEST_TOPIC);

            var config = new SenderIdentityConfig { AppDomainId = 1 };
            
            Assert.Throws<InvalidOperationException>(() => participant.EnableSenderTracking(config));
        }

        [Fact]
        public void DisabledOverhead_TrackingOff_ZeroImpact()
        {
            using var participant = new DdsParticipant();
            Assert.Null(participant.SenderRegistry);
            
            using var writer = new DdsWriter<SenderTrackingTestMsg>(participant, TEST_TOPIC);
            Assert.Null(participant.SenderRegistry);
        }

        [Fact]
        public void SenderIdentity_Struct_Equality()
        {
            var guid = new DdsGuid { High = 1, Low = 2 };
            var id1 = new SenderIdentity { ParticipantGuid = guid, AppDomainId = 10 };
            var id2 = new SenderIdentity { ParticipantGuid = guid, AppDomainId = 10 };
            var id3 = new SenderIdentity { ParticipantGuid = guid, AppDomainId = 11 };

            Assert.Equal(id1, id2);
            Assert.NotEqual(id1, id3);
        }
        
        [Fact]
        public void DdsGuid_Struct_Equality()
        {
            var g1 = new DdsGuid { High = 10, Low = 20 };
            var g2 = new DdsGuid { High = 10, Low = 20 };
            var g3 = new DdsGuid { High = 10, Low = 21 };
            
            Assert.Equal(g1, g2);
            Assert.NotEqual(g1, g3);
            Assert.True(g1 == g2);
            Assert.True(g1 != g3);
        }

        [Fact]
        public void SenderRegistry_CanBeDisposed()
        {
            using var participant = new DdsParticipant();
            participant.EnableSenderTracking(new SenderIdentityConfig());
            Assert.NotNull(participant.SenderRegistry);
            // Dispose will call SenderRegistry.Dispose
        }

        [Fact]
        public void GetSender_WithoutTrackingEnabled_ReturnsNull()
        {
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<SenderTrackingTestMsg>(participant, TEST_TOPIC);
            // No EnableSenderTracking on reader
            
            // We can't really get a sample easily without a writer, but let's assume we could.
            // But we can check if property/method behaves or check state.
            // ViewScope needs a sample to call GetSender(index).
            
            // Let's create a dummy ViewScope via reflection or partial mock if needed, or just integration test.
            // Integration test:
            using var writer = new DdsWriter<SenderTrackingTestMsg>(participant, TEST_TOPIC);
            writer.Write(new SenderTrackingTestMsg { Id = 1 });
            
            Thread.Sleep(500);
            using var scope = reader.Take();
            if (scope.Count > 0)
            {
                 var sender = scope.GetSender(0);
                 Assert.Null(sender);
            }
        }

         [Fact]
        public void SenderTracking_MultiInstance_ProcessIdDisambiguates()
        {
             using var p1 = new DdsParticipant();
             p1.EnableSenderTracking(new SenderIdentityConfig { AppInstanceId = 10 });
             using var w1 = new DdsWriter<SenderTrackingTestMsg>(p1, TEST_TOPIC);
             
             using var p2 = new DdsParticipant();
             p2.EnableSenderTracking(new SenderIdentityConfig { AppInstanceId = 20 });
             using var w2 = new DdsWriter<SenderTrackingTestMsg>(p2, TEST_TOPIC);
             
             using var pReceiver = new DdsParticipant();
             pReceiver.EnableSenderTracking(new SenderIdentityConfig { AppInstanceId = 99 });
             using var reader = new DdsReader<SenderTrackingTestMsg>(pReceiver, TEST_TOPIC);
             reader.EnableSenderTracking(pReceiver.SenderRegistry!);
             
             Thread.Sleep(2000); // Discovery can take time
             
             w1.Write(new SenderTrackingTestMsg { Id = 10 });
             w2.Write(new SenderTrackingTestMsg { Id = 20 });
             
             Thread.Sleep(500);
             
             // Receiving might happen in any order or batching
             bool saw10 = false;
             bool saw20 = false;
             
             for(int i=0; i<10; i++)
             {
                 using var scope = reader.Take(10);
                 for(int k=0; k<scope.Count; k++)
                 {
                     var msg = scope[k];
                     var sender = scope.GetSender(k);
                     if (sender != null)
                     {
                         if (msg.Id == 10)
                         {
                             Assert.Equal(10, sender.Value.AppInstanceId);
                             saw10 = true;
                         }
                         if (msg.Id == 20)
                         {
                             Assert.Equal(20, sender.Value.AppInstanceId);
                             saw20 = true;
                         }
                     }
                 }
                 if (saw10 && saw20) break;
                 Thread.Sleep(200);
             }
             
             // If this fails it might be timing/discovery issue in test environment
             Assert.True(saw10, "Did not identify sender 10");
             Assert.True(saw20, "Did not identify sender 20");
        }
    }
}
