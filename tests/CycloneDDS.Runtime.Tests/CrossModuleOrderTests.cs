using System;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

// Two namespaces in one source file (so one IDL file), where the alphabetically first one uses the
// other: the IDL must define CrossModuleBbb::Leaf before CrossModuleAaa::Holder uses it.
namespace CycloneDDS.Runtime.Tests.CrossModuleAaa
{
    [DdsTopic("CrossModuleHolder")]
    [DdsQos(Reliability = DdsReliability.Reliable, HistoryKind = DdsHistoryKind.KeepAll)]
    [DdsManaged]
    public partial struct Holder
    {
        [DdsKey] public int Id;
        public CrossModuleBbb.Leaf Leaf;
        public CrossModuleBbb.Leaf[] Leaves;
    }
}

namespace CycloneDDS.Runtime.Tests.CrossModuleBbb
{
    [DdsStruct]
    public partial struct Leaf
    {
        public int A;
        public double B;
    }
}

namespace CycloneDDS.Runtime.Tests
{
    public class CrossModuleOrderTests
    {
        [Fact]
        public void TypeUsingATypeOfAnAlphabeticallyLaterModule_RoundTrips()
        {
            string topic = "XMod_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<CrossModuleAaa.Holder>(participant, topic);
            using var writer = new DdsWriter<CrossModuleAaa.Holder>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            writer.Write(new CrossModuleAaa.Holder
            {
                Id = 1, Leaf = new CrossModuleBbb.Leaf { A = 2, B = 3.5 },
                Leaves = new[] { new CrossModuleBbb.Leaf { A = 4, B = 5.5 } },
            });
            CrossModuleAaa.Holder? got = null;
            for (int i = 0; i < 50 && got == null; i++)
            {
                using (var loan = reader.Take()) foreach (var s in loan) if (s.IsValid) got = s.Data;
                if (got == null) Thread.Sleep(100);
            }
            Assert.NotNull(got);
            Assert.Equal((2, 3.5), (got.Value.Leaf.A, got.Value.Leaf.B));
            Assert.Equal((4, 5.5), (got.Value.Leaves[0].A, got.Value.Leaves[0].B));
        }
    }
}
