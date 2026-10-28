using System;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    // An sbyte is IDL int8 (C int8_t). The idlc JSON plugin used to size int8_t as 4 bytes, so every
    // member after it had the wrong offset in the serializer ops.
    [DdsTopic("SByteLayoutSample")]
    [DdsManaged]
    public partial struct SByteLayoutSample
    {
        [DdsKey] public int Id;
        public sbyte Small;
        public sbyte Small2;
        public short Mid;
        public string Text;
        public long Big;
    }

    public class SByteLayoutTests
    {
        [Fact]
        public void MembersAfterAnSByte_RoundTrip()
        {
            string topic = "SByteLayout_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<SByteLayoutSample>(participant, topic);
            using var writer = new DdsWriter<SByteLayoutSample>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            writer.Write(new SByteLayoutSample { Id = 1, Small = -3, Small2 = 7, Mid = -1234, Text = "after", Big = 1L << 40 });

            for (int i = 0; i < 50; i++)
            {
                using (var loan = reader.Take())
                    foreach (var s in loan)
                        if (s.IsValid)
                        {
                            var d = s.Data;
                            Assert.Equal(((sbyte)-3, (sbyte)7, (short)-1234, "after", 1L << 40), (d.Small, d.Small2, d.Mid, d.Text, d.Big));
                            return;
                        }
                Thread.Sleep(100);
            }
            throw new TimeoutException("no sample");
        }
    }
}
