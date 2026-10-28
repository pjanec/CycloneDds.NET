using System;
using System.Collections.Generic;
using System.Threading;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    // case 1: case 2: long A;  case 3: double B;  default: short C;
    [DdsUnion]
    public partial struct MultiLabelUnion
    {
        [DdsDiscriminator] public int Kind;
        [DdsCase(1, 2)] public int A;
        [DdsCase(3)] public double B;
        [DdsDefaultCase] public short C;
    }

    // sequence<int32, 4>, a 2x3 array, and a multi-label union.
    [DdsTopic("IdlShapeSample")]
    [DdsQos(Reliability = DdsReliability.Reliable, HistoryKind = DdsHistoryKind.KeepAll)]
    [DdsManaged]
    public unsafe partial struct IdlShapeSample
    {
        [DdsKey] public int Id;
        [DdsBound(4)] public List<int> Bounded;
        [ArrayLength(2, 3)] public fixed double Matrix[6];
        public MultiLabelUnion U;
        public int After;
    }

    public unsafe class IdlShapeTests
    {
        private static IdlShapeSample RoundTrip(IdlShapeSample sample)
        {
            string topic = "IdlShape_" + Guid.NewGuid().ToString("N");
            using var participant = new DdsParticipant();
            using var reader = new DdsReader<IdlShapeSample>(participant, topic);
            using var writer = new DdsWriter<IdlShapeSample>(participant, topic);
            Assert.True(writer.WaitForReaderAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            writer.Write(sample);
            for (int i = 0; i < 50; i++)
            {
                using (var loan = reader.Take())
                    foreach (var s in loan)
                        if (s.IsValid) return s.Data;
                Thread.Sleep(100);
            }
            throw new TimeoutException("no sample");
        }

        private static IdlShapeSample Sample(int kind, MultiLabelUnion u)
        {
            var s = new IdlShapeSample { Id = kind, Bounded = new List<int> { 1, 2, 3 }, U = u, After = 99 };
            for (int i = 0; i < 6; i++) s.Matrix[i] = i * 1.5;
            return s;
        }

        [Fact]
        public void GeneratedIdl_HasTheBoundTheDimensionsAndEveryCaseLabel()
        {
            var idl = System.IO.File.ReadAllText(System.IO.Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "obj", "Generated", "IdlShapeTests.idl"));
            Assert.Contains("sequence<int32, 4> Bounded;", idl);
            Assert.Contains("double Matrix[2][3];", idl);
            Assert.Matches(@"case 1:\s*case 2:", idl);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void SecondLabelOfACase_SelectsTheSameArm(int kind)
        {
            var got = RoundTrip(Sample(kind, new MultiLabelUnion { Kind = kind, A = 77 }));
            Assert.Equal(kind, got.U.Kind);
            Assert.Equal(77, got.U.A);
            Assert.Equal(new[] { 1, 2, 3 }, got.Bounded);
            for (int i = 0; i < 6; i++) Assert.Equal(i * 1.5, got.Matrix[i]);
            Assert.Equal(99, got.After);
        }

        [Fact]
        public void OtherArms_RoundTrip()
        {
            Assert.Equal(2.5, RoundTrip(Sample(3, new MultiLabelUnion { Kind = 3, B = 2.5 })).U.B);
            Assert.Equal((short)-4, RoundTrip(Sample(9, new MultiLabelUnion { Kind = 9, C = -4 })).U.C);
        }
    }
}
