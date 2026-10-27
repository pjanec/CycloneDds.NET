using System;
using System.Runtime.InteropServices;
using System.Threading;
using CycloneDDS.Runtime.Interop;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    public class WaitForAcksTests
    {
        [Fact]
        public void WaitForAcks_ReliableMatchedReader_ReturnsTrue()
        {
            string topic = "WaitForAcks_" + Guid.NewGuid().ToString("N");
            using var pWriter = new DdsParticipant();
            using var pReader = new DdsParticipant();
            using var writer = new DdsWriter<KeyedTestMessage>(pWriter, topic);
            using var reader = CreateReliableReader(pReader, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

            for (int i = 0; i < 10; i++)
                writer.Write(new KeyedTestMessage { Id = i, Value = i, Message = "m" });
            writer.DisposeInstance(new KeyedTestMessage { Id = 0 });

            Assert.True(writer.WaitForAcks(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// Acknowledgements from a remote reader need a network round trip, so a zero-timeout
        /// wait right after a burst of writes times out; given time, the same data is acknowledged.
        /// </summary>
        [Fact]
        public void WaitForAcks_RemoteReaderAckPending_ReturnsFalseThenTrue()
        {
            // In-process readers on the same domain are served by Cyclone's local fast path and
            // never hold acks back, so put the reader on a second domain instance (id 1) whose
            // wire-level domain is 0: it then is a genuinely remote reader.
            const uint RemoteDomainId = 1;
            int domain = dds_create_domain(RemoteDomainId,
                "<CycloneDDS><Domain id=\"any\"><Discovery><ExternalDomainId>0</ExternalDomainId></Discovery></Domain></CycloneDDS>");
            Assert.True(domain > 0, $"dds_create_domain failed: {domain}");
            try
            {
                string topic = "WaitForAcksRemote_" + Guid.NewGuid().ToString("N");
                using var pWriter = new DdsParticipant();
                using var pReader = new DdsParticipant(RemoteDomainId);
                using var reader = CreateReliableReader(pReader, topic);
                using var writer = new DdsWriter<KeyedTestMessage>(pWriter, topic);
                Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult(), "remote reader never matched");

                string payload = new string('x', 1000);
                for (int i = 0; i < 200; i++)
                    writer.Write(new KeyedTestMessage { Id = i, Value = i, Message = payload });

                Assert.False(writer.WaitForAcks(TimeSpan.Zero));
                Assert.True(writer.WaitForAcks(TimeSpan.FromSeconds(10)));
            }
            finally
            {
                DdsApi.dds_delete(new DdsApi.DdsEntity { Handle = domain });
            }
        }

        // KeyedTestMessage has no [DdsQos], and the DDS default for readers is best-effort,
        // which never acknowledges anything.
        private static DdsReader<KeyedTestMessage> CreateReliableReader(DdsParticipant participant, string topic)
        {
            IntPtr qos = DdsApi.dds_create_qos();
            try
            {
                DdsApi.dds_qset_reliability(qos, DdsApi.DDS_RELIABILITY_RELIABLE, 100_000_000);
                return new DdsReader<KeyedTestMessage>(participant, topic, qos);
            }
            finally
            {
                DdsApi.dds_delete_qos(qos);
            }
        }

        [DllImport("ddsc")]
        private static extern int dds_create_domain(uint domainId, [MarshalAs(UnmanagedType.LPStr)] string config);

        [Fact]
        public void WaitForAcks_NoReaders_ReturnsTrue()
        {
            using var participant = new DdsParticipant();
            using var writer = new DdsWriter<KeyedTestMessage>(participant, "WaitForAcks_" + Guid.NewGuid().ToString("N"));
            writer.Write(new KeyedTestMessage { Id = 1, Value = 1, Message = "m" });

            Assert.True(writer.WaitForAcks(TimeSpan.Zero));
        }

        [Fact]
        public void WaitForAcks_AfterDispose_Throws()
        {
            using var participant = new DdsParticipant();
            var writer = new DdsWriter<KeyedTestMessage>(participant, "WaitForAcks_" + Guid.NewGuid().ToString("N"));
            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.WaitForAcks(TimeSpan.FromSeconds(1)));
        }
    }
}
