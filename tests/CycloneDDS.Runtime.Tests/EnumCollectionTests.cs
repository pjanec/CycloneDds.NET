using System;
using System.Collections.Generic;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    public enum EnumCollColor { Red, Green, Blue = 7 }

    // Enum elements of a sequence or array: in native memory each is a 4-byte int (idlc's enum layout).
    [DdsTopic("EnumCollectionSample")]
    [DdsQos(Reliability = DdsReliability.Reliable, HistoryKind = DdsHistoryKind.KeepAll)]
    [DdsManaged]
    public partial struct EnumCollectionSample
    {
        [DdsKey] public int Id;
        public List<EnumCollColor> List;
        public EnumCollColor[] Seq;
        public EnumCollColor Single;
    }

    public class EnumCollectionTests
    {
        [Fact]
        public void ListAndArrayOfEnum_RoundTrip()
        {
            string topic = "EnumColl_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<EnumCollectionSample>(participant, topic);
            using var writer = new DdsWriter<EnumCollectionSample>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());

            writer.Write(new EnumCollectionSample
            {
                Id = 1,
                List = new List<EnumCollColor> { EnumCollColor.Blue, EnumCollColor.Red, EnumCollColor.Green },
                Seq = new[] { EnumCollColor.Green, EnumCollColor.Blue },
                Single = EnumCollColor.Blue,
            });

            EnumCollectionSample? got = null;
            for (int i = 0; i < 50 && got == null; i++)
            {
                using (var loan = reader.Take())
                    foreach (var s in loan)
                        if (s.IsValid) got = s.Data;
                if (got == null) Thread.Sleep(100);
            }

            Assert.NotNull(got);
            var v = got.Value;
            Assert.Equal(new[] { EnumCollColor.Blue, EnumCollColor.Red, EnumCollColor.Green }, v.List);
            Assert.Equal(new[] { EnumCollColor.Green, EnumCollColor.Blue }, v.Seq);
            Assert.Equal(EnumCollColor.Blue, v.Single);
        }
    }
}
