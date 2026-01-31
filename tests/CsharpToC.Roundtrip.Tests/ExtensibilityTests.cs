using System;
using System.Threading.Tasks;
using Xunit;
using AtomicTests;

namespace CsharpToC.Roundtrip.Tests
{
    /// <summary>
    /// Tests for DDS extensibility features (@final, @appendable, @mutable)
    /// These tests expose bugs in the C# serializer/deserializer that were
    /// not caught by the other test suites.
    /// </summary>
    [Collection("Roundtrip Collection")]
    public class ExtensibilityTests : TestBase
    {
        public ExtensibilityTests(RoundtripFixture fixture) : base(fixture) { }

        // --- Mutable Extensibility Tests (Will likely fail - EMHEADER bugs) ---

        // [Fact]
        // public async Task TestMutableInt32Topic()
        // {
        //     await RunRoundtrip<MutableInt32Topic>(
        //         "AtomicTests::MutableInt32Topic",
        //         3100,
        //         (s) => new MutableInt32Topic { Id = s, Value = s * 100 },
        //         (msg, s) => msg.Id == s && msg.Value == s * 100
        //     );
        // }

        // [Fact]
        // public async Task TestMutableStructTopic()
        // {
        //     await RunRoundtrip<MutableStructTopic>(
        //         "AtomicTests::MutableStructTopic",
        //         3200,
        //         (s) => new MutableStructTopic 
        //         { 
        //             Id = s, 
        //             Point = new Point2D { X = s * 1.5, Y = s * 2.5 }
        //         },
        //         (msg, s) => msg.Id == s && 
        //                    Math.Abs(msg.Point.X - s * 1.5) < 0.0001 && 
        //                    Math.Abs(msg.Point.Y - s * 2.5) < 0.0001
        //     );
        // }

        // --- Final vs Appendable Struct Tests ---

        [Fact]
        public async Task TestAppendableStructTopic()
        {
            await RunRoundtrip<AppendableStructTopic>(
                "AtomicTests::AppendableStructTopic",
                3300,
                (s) => new AppendableStructTopic 
                { 
                    Id = s, 
                    Point = new Point2D { X = s * 1.1, Y = s * 2.2 }
                },
                (msg, s) => msg.Id == s && 
                           Math.Abs(msg.Point.X - s * 1.1) < 0.0001 && 
                           Math.Abs(msg.Point.Y - s * 2.2) < 0.0001
            );
        }

        // --- All Primitives Test (Final version - Will likely fail) ---

        [Fact]
        public async Task TestAllPrimitivesAtomicTopic()
        {
            await RunRoundtrip<AllPrimitivesAtomicTopic>(
                "AtomicTests::AllPrimitivesAtomicTopic",
                3400,
                (s) => new AllPrimitivesAtomicTopic
                {
                    Id = s,
                    Bool_val = true,
                    Char_val = (byte)'X',
                    Octet_val = (byte)(s & 0xFF),
                    Short_val = (short)(s * 10),
                    Ushort_val = (ushort)(s * 20),
                    Long_val = s * 100,
                    Ulong_val = (uint)(s * 200),
                    Llong_val = s * 1000,
                    Ullong_val = (ulong)s * 2000,
                    Float_val = s * 1.5f,
                    Double_val = s * 2.5
                },
                (msg, s) => {
                    return msg.Id == s &&
                           msg.Bool_val == true &&
                           msg.Char_val == (byte)'X' &&
                           msg.Octet_val == (byte)(s & 0xFF) &&
                           msg.Short_val == (short)(s * 10) &&
                           msg.Ushort_val == (ushort)(s * 20) &&
                           msg.Long_val == s * 100 &&
                           msg.Ulong_val == (uint)(s * 200) &&
                           msg.Llong_val == (long)s * 1000 &&
                           msg.Ullong_val == (ulong)s * 2000 &&
                           Math.Abs(msg.Float_val - s * 1.5f) < 0.001f &&
                           Math.Abs(msg.Double_val - s * 2.5) < 0.001;
                }
            );
        }

        [Fact]
        public async Task TestArrayStructTopicAppendable()
        {
            await RunRoundtrip<ArrayStructTopicAppendable>(
                "AtomicTests::ArrayStructTopicAppendable",
                3600,
                (s) => new ArrayStructTopicAppendable
                {
                    Id = s,
                    Points = new Point2D[]
                    {
                        new Point2D { X = s * 1.0, Y = s * 2.0 },
                        new Point2D { X = s * 3.0, Y = s * 4.0 },
                        new Point2D { X = s * 5.0, Y = s * 6.0 }
                    }
                },
                (msg, s) => msg.Id == s
            );
        }

        // --- Optional Fields Test (Will likely fail) ---

        [Fact]
        public async Task TestOptionalEnumTopic()
        {
            await RunRoundtrip<OptionalEnumTopic>(
                "AtomicTests::OptionalEnumTopic",
                3500,
                (s) => new OptionalEnumTopic 
                { 
                    Id = s, 
                    Opt_enum = SimpleEnum.SECOND 
                },
                (msg, s) => msg.Id == s && msg.Opt_enum == SimpleEnum.SECOND
            );
        }
    }
}
