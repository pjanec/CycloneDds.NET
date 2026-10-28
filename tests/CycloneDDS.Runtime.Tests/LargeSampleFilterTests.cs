using System;
using System.Threading;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    // A sample of 64 KiB: a call passing it by value followed by more stack arguments is rejected by the JIT
    // (that broke ddsmonitor). The reader API must not make such a call - the filter takes the sample as its only argument.
    [DdsTopic("LargeFilterSample")]
    public unsafe partial struct LargeFilterSample
    {
        [DdsKey] public int Id;
        public fixed byte Payload[65536];
    }

    public class LargeSampleFilterTests
    {
        [Fact]
        public void Filter_OnA64KbSample_SelectsMatchingSamples()
        {
            string topic = "LargeFilter_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            var qos = DdsApi.dds_create_qos();
            DdsApi.dds_qset_reliability(qos, DdsApi.DDS_RELIABILITY_RELIABLE, 100_000_000);
            DdsApi.dds_qset_history(qos, DdsApi.DDS_HISTORY_KEEP_ALL, 0);
            using var reader = new DdsReader<LargeFilterSample>(participant, topic, qos);
            using var writer = new DdsWriter<LargeFilterSample>(participant, topic, qos);
            DdsApi.dds_delete_qos(qos);

            for (int i = 1; i <= 4; i++)
                writer.Write(new LargeFilterSample { Id = i });
            Thread.Sleep(500);

            reader.SetFilter(s => s.Id % 2 == 0);

            int count = 0;
            using var loan = reader.Take();
            foreach (var sample in loan)
            {
                Assert.Equal(0, sample.Data.Id % 2);
                count++;
            }
            Assert.Equal(2, count);
        }
    }
}
