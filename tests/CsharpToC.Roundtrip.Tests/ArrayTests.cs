using System;
using System.Threading.Tasks;
using Xunit;
using AtomicTests;

namespace CsharpToC.Roundtrip.Tests
{
    [Collection("Roundtrip Collection")]
    public class ArrayTests : TestBase
    {
        public ArrayTests(RoundtripFixture fixture) : base(fixture) { }

        [Fact]
        public async Task TestArrayInt32()
        {
            await RunRoundtrip<ArrayInt32Topic>(
                "AtomicTests::ArrayInt32Topic", 
                400,
                (s) => { 
                    var msg = new ArrayInt32Topic();
                    msg.Id = s; 
                    msg.Values = new int[5]; // Native uses 5
                    for(int i=0; i<5; i++) msg.Values[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Values.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                        if (msg.Values[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayFloat64()
        {
            await RunRoundtrip<ArrayFloat64Topic>(
                "AtomicTests::ArrayFloat64Topic", 
                410,
                (s) => { 
                    var msg = new ArrayFloat64Topic();
                    msg.Id = s; 
                    msg.Values = new double[5];
                    for(int i=0; i<5; i++) msg.Values[i] = (double)(s + i) * 1.1;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Values.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                         double expected = (double)(s + i) * 1.1;
                         if (Math.Abs(msg.Values[i] - expected) > 0.0001) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayString()
        {
             await RunRoundtrip<ArrayStringTopic>(
                "AtomicTests::ArrayStringTopic", 
                420,
                (s) => { 
                    var msg = new ArrayStringTopic();
                    msg.Id = s; 
                    msg.Names = new string[5];
                    for(int i=0; i<5; i++) msg.Names[i] = $"S_{s}_{i}";
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Names.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                         if (msg.Names[i] != $"S_{s}_{i}") return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayInt32Appendable()
        {
            await RunRoundtrip<ArrayInt32TopicAppendable>(
                "AtomicTests::ArrayInt32TopicAppendable", 
                1400,
                (s) => { 
                    var msg = new ArrayInt32TopicAppendable();
                    msg.Id = s; 
                    msg.Values = new int[5]; // Native uses 5
                    for(int i=0; i<5; i++) msg.Values[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Values.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                        if (msg.Values[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayFloat64Appendable()
        {
            await RunRoundtrip<ArrayFloat64TopicAppendable>(
                "AtomicTests::ArrayFloat64TopicAppendable", 
                1410,
                (s) => { 
                    var msg = new ArrayFloat64TopicAppendable();
                    msg.Id = s; 
                    msg.Values = new double[5];
                    for(int i=0; i<5; i++) msg.Values[i] = (double)(s + i) * 1.1;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Values.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                         double expected = (double)(s + i) * 1.1;
                         if (Math.Abs(msg.Values[i] - expected) > 0.0001) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayStringAppendable()
        {
             await RunRoundtrip<ArrayStringTopicAppendable>(
                "AtomicTests::ArrayStringTopicAppendable", 
                1420,
                (s) => { 
                    var msg = new ArrayStringTopicAppendable();
                    msg.Id = s; 
                    msg.Names = new string[5];
                    for(int i=0; i<5; i++) msg.Names[i] = $"S_{s}_{i}";
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Names.Length != 5) return false;
                    for(int i=0; i<5; i++) {
                         if (msg.Names[i] != $"S_{s}_{i}") return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArray2DInt32()
        {
            await RunRoundtrip<Array2DInt32Topic>(
                "AtomicTests::Array2DInt32Topic",
                500,
                (s) => {
                    var msg = new Array2DInt32Topic();
                    msg.Id = s;
                    msg.Matrix = new int[12];
                    for(int i=0; i<12; i++) msg.Matrix[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Matrix.Length != 12) return false;
                    for(int i=0; i<12; i++) {
                        if (msg.Matrix[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArray3DInt32()
        {
            await RunRoundtrip<Array3DInt32Topic>(
                "AtomicTests::Array3DInt32Topic",
                520,
                (s) => {
                    var msg = new Array3DInt32Topic();
                    msg.Id = s;
                    msg.Cube = new int[24];
                    for(int i=0; i<24; i++) msg.Cube[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Cube.Length != 24) return false;
                    for(int i=0; i<24; i++) {
                        if (msg.Cube[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayStruct()
        {
             await RunRoundtrip<ArrayStructTopic>(
                "AtomicTests::ArrayStructTopic",
                510,
                (s) => {
                    var msg = new ArrayStructTopic();
                    msg.Id = s;
                    msg.Points = new Point2D[3];
                    for (int i=0; i<3; i++) {
                        msg.Points[i].X = s + i;
                        msg.Points[i].Y = s + i + 0.5;
                    }
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Points.Length != 3) return false;
                    for (int i=0; i<3; i++) {
                        if (Math.Abs(msg.Points[i].X - (s+i)) > 0.0001) return false;
                        if (Math.Abs(msg.Points[i].Y - (s+i+0.5)) > 0.0001) return false;
                    }
                    return true;
                }
            );
        }

        // --- Appendable Multi-Dimensional Arrays (Will expose bugs) ---

        [Fact]
        public async Task TestArray2DInt32Appendable()
        {
            await RunRoundtrip<Array2DInt32TopicAppendable>(
                "AtomicTests::Array2DInt32TopicAppendable",
                1500,
                (s) => {
                    var msg = new Array2DInt32TopicAppendable();
                    msg.Id = s;
                    msg.Matrix = new int[12];  // 3x4 matrix flattened
                    for(int i=0; i<12; i++) msg.Matrix[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Matrix.Length != 12) return false;
                    for(int i=0; i<12; i++) {
                        if (msg.Matrix[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArray3DInt32Appendable()
        {
            await RunRoundtrip<Array3DInt32TopicAppendable>(
                "AtomicTests::Array3DInt32TopicAppendable",
                1520,
                (s) => {
                    var msg = new Array3DInt32TopicAppendable();
                    msg.Id = s;
                    msg.Cube = new int[24];  // 2x3x4 cube flattened
                    for(int i=0; i<24; i++) msg.Cube[i] = s + i;
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Cube.Length != 24) return false;
                    for(int i=0; i<24; i++) {
                        if (msg.Cube[i] != s + i) return false;
                    }
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestArrayStructAppendable()
        {
            await RunRoundtrip<ArrayStructTopicAppendable>(
                "AtomicTests::ArrayStructTopicAppendable",
                1510,
                (s) => {
                    var msg = new ArrayStructTopicAppendable();
                    msg.Id = s;
                    msg.Points = new Point2D[3];
                    for (int i=0; i<3; i++) {
                        msg.Points[i].X = s + i;
                        msg.Points[i].Y = s + i + 0.5;
                    }
                    return msg;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.Points.Length != 3) return false;
                    for (int i=0; i<3; i++) {
                        if (Math.Abs(msg.Points[i].X - (s+i)) > 0.0001) return false;
                        if (Math.Abs(msg.Points[i].Y - (s+i+0.5)) > 0.0001) return false;
                    }
                    return true;
                }
            );
        }
    }
}
