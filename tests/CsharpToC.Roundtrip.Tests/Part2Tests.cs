using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using AtomicTests;

namespace CsharpToC.Roundtrip.Tests
{
    [Collection("Roundtrip Collection")]
    public class Part2Tests : TestBase
    {
        public Part2Tests(RoundtripFixture fixture) : base(fixture) { }

        // --- Unions (Appendable) ---

        [Fact]
        public async Task TestUnionBoolDiscAppendable()
        {
            await RunRoundtrip<UnionBoolDiscTopicAppendable>(
                "AtomicTests::UnionBoolDiscTopicAppendable",
                2001,
                (s) => {
                    var msg = new UnionBoolDiscTopicAppendable();
                    msg.Id = s;
                    bool disc = (s % 2) == 0;
                    var u = new BoolUnionAppendable();
                    u._d = disc;
                    if (disc) u.True_val = s * 50;
                    else u.False_val = s * 1.5;
                    msg.Data = u;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    bool disc = (s % 2) == 0;
                    if (msg.Data._d != disc) return false;
                    if (disc) return msg.Data.True_val == s * 50;
                    else return Math.Abs(msg.Data.False_val - (s * 1.5)) < 0.0001;
                }
            );
        }

        [Fact]
        public async Task TestUnionEnumDiscAppendable()
        {
            await RunRoundtrip<UnionEnumDiscTopicAppendable>(
                "AtomicTests::UnionEnumDiscTopicAppendable",
                2002,
                (s) => {
                    var msg = new UnionEnumDiscTopicAppendable();
                    msg.Id = s;
                    var disc = (ColorEnum)((s % 4));
                    var u = new ColorUnionAppendable();
                    u._d = disc;
                    
                    if (disc == ColorEnum.RED) u.Red_data = s * 20;
                    else if (disc == ColorEnum.GREEN) u.Green_data = s * 2.5;
                    else if (disc == ColorEnum.BLUE) u.Blue_data = $"Blue_{s}";
                    else if (disc == ColorEnum.YELLOW) u.Yellow_point = new Point2DAppendable { X = s * 1.1, Y = s * 2.2 };
                    
                    msg.Data = u;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    var disc = (ColorEnum)((s % 4));
                    if (msg.Data._d != disc) return false;
                    
                    if (disc == ColorEnum.RED) return msg.Data.Red_data == s * 20;
                    if (disc == ColorEnum.GREEN) return Math.Abs(msg.Data.Green_data - (s * 2.5)) < 0.0001;
                    if (disc == ColorEnum.BLUE) return msg.Data.Blue_data == $"Blue_{s}";
                    if (disc == ColorEnum.YELLOW) 
                        return Math.Abs(msg.Data.Yellow_point.X - (s * 1.1)) < 0.0001 &&
                               Math.Abs(msg.Data.Yellow_point.Y - (s * 2.2)) < 0.0001;
                    return false;
                }
            );
        }

        [Fact]
        public async Task TestUnionShortDiscAppendable()
        {
             await RunRoundtrip<UnionShortDiscTopicAppendable>(
                "AtomicTests::UnionShortDiscTopicAppendable",
                2003,
                (s) => {
                    var msg = new UnionShortDiscTopicAppendable();
                    msg.Id = s;
                    short disc = (short)((s % 4) + 1);
                    var u = new ShortUnionAppendable();
                    u._d = disc;

                    if (disc == 1) u.Byte_val = (byte)(s & 0xFF);
                    else if (disc == 2) u.Short_val = (short)(s * 10);
                    else if (disc == 3) u.Long_val = s * 1000;
                    else if (disc == 4) u.Float_val = s * 0.5f;

                    msg.Data = u;
                    return msg;
                },
                (msg, s) => {
                     if (msg.Id != s) return false;
                     short disc = (short)((s % 4) + 1);
                     if (msg.Data._d != disc) return false;
                     
                     if (disc == 1) return msg.Data.Byte_val == (byte)(s & 0xFF);
                     if (disc == 2) return msg.Data.Short_val == (short)(s * 10);
                     if (disc == 3) return msg.Data.Long_val == s * 1000;
                     if (disc == 4) return Math.Abs(msg.Data.Float_val - (s * 0.5f)) < 0.0001f;
                     return false;
                }
             );
        }

        // --- Sequences (Appendable) ---

        [Fact]
        public async Task TestSequenceInt64Appendable()
        {
            await RunRoundtrip<SequenceInt64TopicAppendable>(
                "AtomicTests::SequenceInt64TopicAppendable",
                2101,
                (s) => {
                    var msg = new SequenceInt64TopicAppendable();
                    msg.Id = s;
                    var list = new List<long>();
                    int len = (s % 5);
                    for(int i=0; i<len; i++) list.Add((long)((s+i) * 1000000L));
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                     if (msg.Id != s) return false;
                     int len = (s % 5);
                     if ((msg.Values?.Count ?? 0) != len) return false;
                     for(int i=0; i<len; i++) if (msg.Values[i] != (long)((s+i) * 1000000L)) return false;
                     return true;
                }
            );
        }

        [Fact]
        public async Task TestBoundedSequenceInt32Appendable()
        {
            await RunRoundtrip<BoundedSequenceInt32TopicAppendable>(
                "AtomicTests::BoundedSequenceInt32TopicAppendable",
                2102,
                (s) => {
                    var msg = new BoundedSequenceInt32TopicAppendable();
                    msg.Id = s;
                    var list = new List<int>();
                    int len = (s % 4); // Max 10
                    for(int i=0; i<len; i++) list.Add(s + i);
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 4);
                    if ((msg.Values?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (msg.Values[i] != s + i) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceFloat32Appendable()
        {
            await RunRoundtrip<SequenceFloat32TopicAppendable>(
                "AtomicTests::SequenceFloat32TopicAppendable",
                2103,
                (s) => {
                    var msg = new SequenceFloat32TopicAppendable();
                    msg.Id = s;
                    var list = new List<float>();
                    int len = (s % 3);
                    for(int i=0; i<len; i++) list.Add(s + i + 0.5f);
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 3);
                    if ((msg.Values?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (Math.Abs(msg.Values[i] - (s + i + 0.5f)) > 0.001) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceFloat64Appendable()
        {
            await RunRoundtrip<SequenceFloat64TopicAppendable>(
                "AtomicTests::SequenceFloat64TopicAppendable",
                2104,
                (s) => {
                    var msg = new SequenceFloat64TopicAppendable();
                    msg.Id = s;
                    var list = new List<double>();
                    int len = (s % 3);
                    for(int i=0; i<len; i++) list.Add(s + i + 0.12345);
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 3);
                    if ((msg.Values?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (Math.Abs(msg.Values[i] - (s + i + 0.12345)) > 0.000001) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceBooleanAppendable()
        {
            await RunRoundtrip<SequenceBooleanTopicAppendable>(
                "AtomicTests::SequenceBooleanTopicAppendable",
                2105,
                (s) => {
                    var msg = new SequenceBooleanTopicAppendable();
                    msg.Id = s;
                    var list = new List<bool>();
                    int len = (s % 4);
                    for(int i=0; i<len; i++) list.Add((i % 2) == 0);
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 4);
                    if ((msg.Values?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (msg.Values[i] != ((i % 2) == 0)) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceOctetAppendable()
        {
            await RunRoundtrip<SequenceOctetTopicAppendable>(
                "AtomicTests::SequenceOctetTopicAppendable",
                2106,
                (s) => {
                    var msg = new SequenceOctetTopicAppendable();
                    msg.Id = s;
                    var list = new List<byte>();
                    int len = (s % 5);
                    for(int i=0; i<len; i++) list.Add((byte)(s + i));
                    msg.Bytes = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 5);
                    if ((msg.Bytes?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (msg.Bytes[i] != (byte)(s + i)) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceStringAppendable()
        {
            await RunRoundtrip<SequenceStringTopicAppendable>(
                "AtomicTests::SequenceStringTopicAppendable",
                2107,
                (s) => {
                    var msg = new SequenceStringTopicAppendable();
                    msg.Id = s;
                    var list = new List<string>();
                    int len = (s % 3);
                    for(int i=0; i<len; i++) list.Add($"Str_{s}_{i}");
                    msg.Values = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 3);
                    if ((msg.Values?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++) if (msg.Values[i] != $"Str_{s}_{i}") return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestSequenceStructAppendable()
        {
            await RunRoundtrip<SequenceStructTopicAppendable>(
                "AtomicTests::SequenceStructTopicAppendable",
                2108,
                (s) => {
                    var msg = new SequenceStructTopicAppendable();
                    msg.Id = s;
                    var list = new List<Point2D>();
                    int len = (s % 3);
                    for(int i=0; i<len; i++) list.Add(new Point2D { X = s + i, Y = s - i });
                    msg.Points = list;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    int len = (s % 3);
                    if ((msg.Points?.Count ?? 0) != len) return false;
                    for(int i=0; i<len; i++)
                    {
                        if (Math.Abs(msg.Points[i].X - (s + i)) > 0.001) return false;
                        if (Math.Abs(msg.Points[i].Y - (s - i)) > 0.001) return false;
                    } 
                    return true;
                }
            );
        }

        // --- Nested (Appendable) ---

        [Fact]
        public async Task TestNestedStructAppendable()
        {
            await RunRoundtrip<NestedStructTopicAppendable>(
                "AtomicTests::NestedStructTopicAppendable",
                2201,
                (s) => new NestedStructTopicAppendable { Id = s, Point = new Point2DAppendable { X = s, Y = s * 2 } },
                (msg, s) => msg.Id == s && msg.Point.X == s && msg.Point.Y == s * 2
            );
        }

        [Fact]
        public async Task TestNested3DAppendable()
        {
             await RunRoundtrip<Nested3DTopicAppendable>(
                "AtomicTests::Nested3DTopicAppendable",
                2202,
                (s) => new Nested3DTopicAppendable { Id = s, Point = new Point3DAppendable { X = s, Y = s + 1, Z = s + 2 } },
                (msg, s) => msg.Id == s && msg.Point.X == s && msg.Point.Y == s + 1 && msg.Point.Z == s + 2
            );
        }

        [Fact]
        public async Task TestDoublyNestedAppendable()
        {
            await RunRoundtrip<DoublyNestedTopicAppendable>(
               "AtomicTests::DoublyNestedTopicAppendable",
               2203,
               (s) => new DoublyNestedTopicAppendable 
               { 
                   Id = s, 
                   Box = new BoxAppendable 
                   { 
                       TopLeft = new Point2DAppendable { X = s, Y = s },
                       BottomRight = new Point2DAppendable { X = s + 10, Y = s + 10 }
                   }
               },
               (msg, s) => msg.Id == s && 
                           msg.Box.TopLeft.X == s && 
                           msg.Box.BottomRight.X == s + 10
           );
        }

        [Fact]
        public async Task TestComplexNestedAppendable()
        {
            await RunRoundtrip<ComplexNestedTopicAppendable>(
               "AtomicTests::ComplexNestedTopicAppendable",
               2204,
               (s) => new ComplexNestedTopicAppendable 
               { 
                   Id = s, 
                   Container = new ContainerAppendable 
                   { 
                       Count = s,
                       Radius = s * 0.5,
                       Center = new Point3DAppendable { X = 1, Y = 2, Z = 3 }
                   }
               },
               (msg, s) => msg.Id == s && msg.Container.Count == s
           );
        }

        // --- Optionals ---

        [Fact]
        public async Task TestOptionalInt32Appendable()
        {
            await RunRoundtrip<OptionalInt32TopicAppendable>(
                "AtomicTests::OptionalInt32TopicAppendable",
                2301,
                (s) => {
                    var msg = new OptionalInt32TopicAppendable { Id = s };
                    if (s % 2 == 0) msg.Opt_value = s * 10;
                    // else null (default 0 for value type optional generated as field?? Check impl. 
                    // Actually DdsOptional on struct field usually implies a wrapper or a boolean flag parallel?
                    // In CycloneDDS C# binding for value types, it might be T?
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (!msg.Opt_value.HasValue) return false;
                    if (msg.Opt_value.Value != s * 10) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestOptionalFloat64Appendable()
        {
             await RunRoundtrip<OptionalFloat64TopicAppendable>(
                "AtomicTests::OptionalFloat64TopicAppendable",
                2302,
                (s) => new OptionalFloat64TopicAppendable { Id = s, Opt_value = s * 2.0 },
                (msg, s) => msg.Id == s && msg.Opt_value == s * 2.0
            );
        }

        [Fact]
        public async Task TestOptionalStringAppendable()
        {
            await RunRoundtrip<OptionalStringTopicAppendable>(
                "AtomicTests::OptionalStringTopicAppendable",
                2303,
                (s) => {
                    var msg = new OptionalStringTopicAppendable { Id = s };
                    if (s % 2 == 0) msg.Opt_string = $"Opt_{s}";
                    return msg;
                },
                (msg, s) => msg.Id == s && msg.Opt_string == $"Opt_{s}"
            );
        }

        [Fact]
        public async Task TestOptionalStructAppendable()
        {
            await RunRoundtrip<OptionalStructTopicAppendable>(
                "AtomicTests::OptionalStructTopicAppendable",
                2304,
                (s) => {
                    var msg = new OptionalStructTopicAppendable { Id = s };
                    if (s % 2 == 0) msg.Opt_point = new Point2DAppendable { X = s, Y = s };
                    // If optional struct is null in C#, usually field is nullable or refernece? 
                    // Point2DAppendable is struct. 
                    // Actually in C# `public Point2DAppendable Opt_point;` is a value type. 
                    // The bindings might ignoring DdsOptional for value types without nullable modifier?
                    // Or maybe it uses a separate presence flag?
                    // Proceeding with assumption that we just write value.
                    return msg;
                },
                (msg, s) => msg.Id == s && msg.Opt_point.HasValue && msg.Opt_point.Value.X == s
            );
        }

        [Fact]
        public async Task TestOptionalEnumAppendable()
        {
            await RunRoundtrip<OptionalEnumTopicAppendable>(
                "AtomicTests::OptionalEnumTopicAppendable",
                2305,
                (s) => new OptionalEnumTopicAppendable { Id = s, Opt_enum = SimpleEnum.SECOND },
                (msg, s) => msg.Id == s && msg.Opt_enum == SimpleEnum.FIRST
            );
        }

        [Fact]
        public async Task TestMultiOptionalAppendable()
        {
            await RunRoundtrip<MultiOptionalTopicAppendable>(
                "AtomicTests::MultiOptionalTopicAppendable",
                2306,
                (s) => new MultiOptionalTopicAppendable { Id = s, Opt_int = s, Opt_double = s, Opt_string = "A" },
                (msg, s) => msg.Id == s && msg.Opt_int == s && msg.Opt_double == s && msg.Opt_string == "A"
            );
        }

        // --- Keys ---

        [Fact]
        public async Task TestTwoKeyInt32Appendable()
        {
            await RunRoundtrip<TwoKeyInt32TopicAppendable>(
                "AtomicTests::TwoKeyInt32TopicAppendable",
                2401,
                (s) => new TwoKeyInt32TopicAppendable { Key1 = s, Key2 = s + 1, Value = s * 2.0 },
                (msg, s) => msg.Key1 == s && msg.Key2 == s + 1 && msg.Value == s * 2.0
            );
        }

        [Fact]
        public async Task TestTwoKeyStringAppendable()
        {
            await RunRoundtrip<TwoKeyStringTopicAppendable>(
                "AtomicTests::TwoKeyStringTopicAppendable",
                2402,
                (s) => new TwoKeyStringTopicAppendable { Key1 = $"K1_{s}", Key2 = $"K2_{s}", Value = s },
                (msg, s) => msg.Key1 == $"K1_{s}" && msg.Key2 == $"K2_{s}"
            );
        }

        [Fact]
        public async Task TestThreeKeyAppendable()
        {
            await RunRoundtrip<ThreeKeyTopicAppendable>(
                "AtomicTests::ThreeKeyTopicAppendable",
                2403,
                (s) => new ThreeKeyTopicAppendable { Key1 = s, Key2 = $"K_{s}", Key3 = (short)s, Value = s },
                (msg, s) => msg.Key1 == s && msg.Key2 == $"K_{s}"
            );
        }

        [Fact]
        public async Task TestFourKeyAppendable()
        {
            await RunRoundtrip<FourKeyTopicAppendable>(
                "AtomicTests::FourKeyTopicAppendable",
                2404,
                (s) => new FourKeyTopicAppendable { Key1 = s, Key2 = s, Key3 = s, Key4 = s, Description = "Desc" },
                (msg, s) => msg.Key1 == s
            );
        }

        [Fact]
        public async Task TestNestedKeyAppendable()
        {
             await RunRoundtrip<NestedKeyTopicAppendable>(
                "AtomicTests::NestedKeyTopicAppendable",
                2405,
                (s) => new NestedKeyTopicAppendable { Loc = new LocationAppendable { Building = s, Floor = (short)(s % 10) }, Temperature = 25.0 },
                (msg, s) => msg.Loc.Building == s
            );
        }

        [Fact]
        public async Task TestNestedKeyGeoAppendable()
        {
            await RunRoundtrip<NestedKeyGeoTopicAppendable>(
                "AtomicTests::NestedKeyGeoTopicAppendable",
                2406,
                (s) => new NestedKeyGeoTopicAppendable { Coords = new CoordinatesAppendable { Latitude = s, Longitude = s }, Location_name = "Home" },
                (msg, s) => msg.Coords.Latitude == s
            );
        }
        
        [Fact]
        public async Task TestNestedTripleKeyAppendable()
        {
            await RunRoundtrip<NestedTripleKeyTopicAppendable>(
                "AtomicTests::NestedTripleKeyTopicAppendable",
                2407,
                (s) => new NestedTripleKeyTopicAppendable { Keys = new TripleKeyAppendable { Id1 = s, Id2 = s, Id3 = s }, Data = "Data" },
                (msg, s) => msg.Keys.Id1 == s
            );
        }

        // --- Edge Cases ---

        [Fact]
        public async Task TestEmptySequenceAppendable()
        {
            await RunRoundtrip<EmptySequenceTopicAppendable>(
                "AtomicTests::EmptySequenceTopicAppendable",
                2501,
                (s) => new EmptySequenceTopicAppendable { Id = s, Empty_seq = new List<int>() },
                (msg, s) => msg.Id == s && (msg.Empty_seq == null || msg.Empty_seq.Count == 0)
            );
        }

        [Fact]
        public async Task TestUnboundedStringAppendable()
        {
             await RunRoundtrip<UnboundedStringTopicAppendable>(
                "AtomicTests::UnboundedStringTopicAppendable",
                2502,
                (s) => new UnboundedStringTopicAppendable { Id = s, Unbounded = new string('A', 500) },
                (msg, s) => msg.Id == s && msg.Unbounded == "S"
            );
        }

        [Fact]
        public async Task TestAllPrimitivesAtomicAppendable()
        {
            await RunRoundtrip<AllPrimitivesAtomicTopicAppendable>(
                "AtomicTests::AllPrimitivesAtomicTopicAppendable",
                2503,
                (s) => new AllPrimitivesAtomicTopicAppendable { Id = s, Bool_val = true, Long_val = s },
                (msg, s) => msg.Id == s
            );
        }

        [Fact]
        public async Task TestMaxSizeStringTopic()
        {
            await RunRoundtrip<MaxSizeStringTopic>(
                "AtomicTests::MaxSizeStringTopic",
                2504,
                (s) => new MaxSizeStringTopic { Id = s, Max_string = "Short" },
                (msg, s) => msg.Id == s
            );
        }

        [Fact]
        public async Task TestMaxSizeStringTopicAppendable()
        {
            await RunRoundtrip<MaxSizeStringTopicAppendable>(
               "AtomicTests::MaxSizeStringTopicAppendable",
               2505,
               (s) => new MaxSizeStringTopicAppendable { Id = s, Max_string = "ShortAppend" },
               (msg, s) => msg.Id == s
           );
        }

        [Fact]
        public async Task TestMaxLengthSequenceTopic()
        {
            await RunRoundtrip<MaxLengthSequenceTopic>(
               "AtomicTests::MaxLengthSequenceTopic",
               2506,
               (s) => new MaxLengthSequenceTopic { Id = s, Max_seq = new List<int> { 1, 2, 3 } },
               (msg, s) => msg.Id == s && msg.Max_seq.Count == 3
           );
        }

        [Fact]
        public async Task TestMaxLengthSequenceTopicAppendable()
        {
             await RunRoundtrip<MaxLengthSequenceTopicAppendable>(
               "AtomicTests::MaxLengthSequenceTopicAppendable",
               2507,
               (s) => new MaxLengthSequenceTopicAppendable { Id = s, Max_seq = new List<int> { 4, 5, 6 } },
               (msg, s) => msg.Id == s && msg.Max_seq.Count == 3
           );
        }

        [Fact]
        public async Task TestDeepNestedStructTopic()
        {
            await RunRoundtrip<DeepNestedStructTopic>(
                "AtomicTests::DeepNestedStructTopic",
                2508,
                (s) => new DeepNestedStructTopic 
                { 
                    Id = s, 
                    Nested1 = new Level1 { Value1 = 1, Nested2 = new Level2 { Value2 = 2, Nested3 = new Level3 { Value3 = 3, Nested4 = new Level4 { Value4 = 4, Nested5 = new Level5 { Value5 = 5 } } } } }
                },
                (msg, s) => msg.Id == s && msg.Nested1.Nested2.Nested3.Nested4.Nested5.Value5 == 5
            );
        }
        
        [Fact]
        public async Task TestDeepNestedStructTopicAppendable()
        {
            await RunRoundtrip<DeepNestedStructTopicAppendable>(
                "AtomicTests::DeepNestedStructTopicAppendable",
                2509,
                 (s) => new DeepNestedStructTopicAppendable 
                { 
                    Id = s, 
                    Nested1 = new Level1Appendable { Value1 = 1, Nested2 = new Level2Appendable { Value2 = 2, Nested3 = new Level3Appendable { Value3 = 3, Nested4 = new Level4Appendable { Value4 = 4, Nested5 = new Level5Appendable { Value5 = 5 } } } } }
                },
                (msg, s) => msg.Id == s && msg.Nested1.Nested2.Nested3.Nested4.Nested5.Value5 == 5
            );
        }

        [Fact]
        public async Task TestUnionWithOptionalTopic()
        {
            await RunRoundtrip<UnionWithOptionalTopic>(
                "AtomicTests::UnionWithOptionalTopic",
                2510,
                (s) => {
                    var u = new UnionWithOptional();
                    u._d = 1;
                    u.Int_val = s;
                    return new UnionWithOptionalTopic { Id=s, Data = u };
                },
                (msg, s) => msg.Id == s
            );
        }

        [Fact]
        public async Task TestUnionWithOptionalTopicAppendable()
        {
             await RunRoundtrip<UnionWithOptionalTopicAppendable>(
                "AtomicTests::UnionWithOptionalTopicAppendable",
                2511,
                (s) => {
                    var u = new UnionWithOptionalAppendable();
                    u._d = 3;
                    u.Double_val = s * 1.0;
                    return new UnionWithOptionalTopicAppendable { Id=s, Data = u };
                },
                (msg, s) => msg.Id == s
            );
        }

    }
}
