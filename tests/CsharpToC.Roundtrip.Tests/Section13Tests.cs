using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using AtomicTests;

namespace CsharpToC.Roundtrip.Tests
{
    [Collection("Roundtrip Collection")]
    public class Section13Tests : TestBase
    {
        public Section13Tests(RoundtripFixture fixture) : base(fixture) { }

        [Fact]
        public async Task TestEmptySequenceTopicAppendable()
        {
            await RunRoundtrip<EmptySequenceTopicAppendable>(
                "AtomicTests::EmptySequenceTopicAppendable",
                4000,
                (seed) => {
                    var obj = new EmptySequenceTopicAppendable();
                    obj.Id = seed;
                    // C generator sets length 0
                    obj.Empty_seq = new List<int>();
                    return obj;
                },
                (msg, s) => {
                    // C generator sends empty sequence
                    if (msg.Id != s) return false;
                    if (msg.Empty_seq.Count != 0) return false;
                    return true;
                }
            );
        }

        [Fact]
        public async Task TestUnboundedStringTopicAppendable()
        {
            await RunRoundtrip<UnboundedStringTopicAppendable>(
                "AtomicTests::UnboundedStringTopicAppendable",
                4100,
                (seed) => new UnboundedStringTopicAppendable
                {
                    Id = seed,
                    // C generator sets "S"
                    Unbounded = "S"
                },
                (msg, s) => msg.Id == s && msg.Unbounded == "S"
            );
        }

        [Fact]
        public async Task TestAllPrimitivesAtomicTopicAppendable()
        {
            await RunRoundtrip<AllPrimitivesAtomicTopicAppendable>(
                "AtomicTests::AllPrimitivesAtomicTopicAppendable",
                4200,
                (seed) => new AllPrimitivesAtomicTopicAppendable
                {
                    Id = seed
                    // C generator sets values. We send 0s/defaults to trigger it.
                },
                (msg, seed) =>
                {
                    // Match atomic_tests_native.c generation logic
                    bool bool_val = (seed % 2) == 0;
                    byte char_val = (byte)('A' + (seed % 26));
                    byte octet_val = (byte)(seed & 0xFF); 
                    short short_val = (short)(seed * 2);
                    ushort ushort_val = (ushort)(seed * 3);
                    int long_val = seed * 4;
                    uint ulong_val = (uint)(seed * 5);
                    long llong_val = (long)(seed * 6);
                    ulong ullong_val = (ulong)(seed * 7);
                    float float_val = (float)(seed * 8.0);
                    double double_val = (double)(seed * 9.0);

                    return msg.Id == seed &&
                           msg.Bool_val == bool_val &&
                           msg.Char_val == char_val &&
                           msg.Octet_val == octet_val &&
                           msg.Short_val == short_val &&
                           msg.Ushort_val == ushort_val &&
                           msg.Long_val == long_val &&
                           msg.Ulong_val == ulong_val &&
                           msg.Llong_val == llong_val &&
                           msg.Ullong_val == ullong_val &&
                           Math.Abs(msg.Float_val - float_val) < 0.0001f &&
                           Math.Abs(msg.Double_val - double_val) < 0.0001;
                }
            );
        }

        [Fact]
        public async Task TestOffsetKeyTopic()
        {
            await RunRoundtrip<OffsetKeyTopic>(
                "AtomicTests::OffsetKeyTopic",
                4300,
                (seed) => {
                    // C generator logic:
                    var obj = new OffsetKeyTopic();
                    obj.GroupName = $"Group_{seed % 100}";
                    obj.SensorId = seed;
                    
                    int calSize = 3 + (seed % 5);
                    obj.CalibrationData = new float[calSize];
                    for (int i = 0; i < calSize; i++) {
                        obj.CalibrationData[i] = (float)(seed + i) * 0.1f;
                    }

                    obj.InstanceSubId = (short)(seed % 1000);
                    
                    obj.FinalPos = new Point3D { 
                        X = (double)seed * 1.1,
                        Y = (double)seed * 2.2,
                        Z = (double)seed * 3.3
                    };
                    return obj;
                },
                (msg, seed) => {
                    if (msg.SensorId != seed) return false;
                    if (msg.GroupName != $"Group_{seed % 100}") return false;
                    if (msg.InstanceSubId != (short)(seed % 1000)) return false;
                    if (Math.Abs(msg.FinalPos.X - ((double)seed * 1.1)) > 0.0001) return false;
                    
                    int calSize = 3 + (seed % 5);
                    if (msg.CalibrationData.Length != calSize) return false;
                     for (int i = 0; i < calSize; i++) {
                        if (Math.Abs(msg.CalibrationData[i] - ((float)(seed + i) * 0.1f)) > 0.0001f) return false;
                    }
                    return true;
                }
            );
        }
        
        [Fact]
        public async Task TestRobotStateTopic()
        {
            await RunRoundtrip<RobotStateTopic>(
                "AtomicTests::RobotStateTopic",
                4400,
                (seed) => {
                    var obj = new RobotStateTopic();
                    obj.RobotId = $"ROBOT_{seed:D4}";
                    obj.TimestampNs = (ulong)seed * 1000000UL;
                    
                    obj.OperationalMode = (SimpleEnum)(seed % 3);
                    
                    // Fixed array length 9 (flattened from 3x3)
                    obj.TransformMatrix = new double[9]; 
                    for(int i=0; i<3; i++) {
                        for(int j=0; j<3; j++) {
                           obj.TransformMatrix[i*3+j] = (double)(seed + i * 10 + j);
                        }
                    }
                    
                    int pathLen = 2 + (seed % 4);
                    obj.CurrentPath = new Point2D[pathLen];
                    for(uint i=0; i<pathLen; i++) {
                        obj.CurrentPath[i] = new Point2D { 
                            X = (double)(seed + i) * 10.0, 
                            Y = (double)(seed + i) * 20.0 
                        };
                    }
                    
                    obj.CurrentAction = new SimpleUnion();
                    int disc = 1 + (seed % 3);
                    obj.CurrentAction._d = disc; 
                    
                    switch (disc) {
                        case 1:
                            obj.CurrentAction.Int_value = (seed * 100);
                            break;
                        case 2:
                            obj.CurrentAction.Double_value = (double)seed * 3.14;
                            break;
                        case 3:
                            obj.CurrentAction.String_value = $"Action_{seed}";
                            break;
                    }

                    // Optional CargoHold
                    if (seed % 2 == 0) {
                        obj.CargoHold = new Container {
                            Count = seed,
                            Center = new Point3D {
                                X = (double)seed * 10.0,
                                Y = (double)seed * 20.0,
                                Z = (double)seed * 30.0
                            },
                            Radius = (double)seed * 5.0
                        };
                    } else {
                        obj.CargoHold = null;
                    }

                    // Optional BatteryVoltage
                    if (seed % 3 == 0) {
                        obj.BatteryVoltage = 12.5 + (double)(seed % 100) * 0.01;
                    } else {
                        obj.BatteryVoltage = null;
                    }
                    return obj;
                },
                (msg, s) => {
                    if (msg.RobotId != $"ROBOT_{s:D4}") return false;
                    if (msg.TimestampNs != (ulong)s * 1000000UL) return false;
                    return true; // Simplified validation
                }
            );
        }

        [Fact]
        public async Task TestAlignmentCheckTopic()
        {
            await RunRoundtrip<AlignmentCheckTopic>(
                "AtomicTests::AlignmentCheckTopic",
                4500,
                (seed) => {
                    var obj = new AlignmentCheckTopic();
                    obj.Id = seed;
                    obj.B1 = (byte)(seed % 256);
                    obj.D1 = (double)seed * 1.23456789;
                    obj.S1 = (short)(seed % 30000);
                    obj.C1 = (byte)('A' + (seed % 26));
                    obj.L1 = seed * 1000;
                    
                    int blobSize = 5 + (seed % 10);
                    obj.Blob = new byte[blobSize];
                    for(int i=0; i<blobSize; i++) obj.Blob[i] = (byte)((seed + i) % 256);
                    
                    obj.CheckValue = (ulong)seed * 123456789UL;
                    return obj;
                },
                (msg, s) => {
                    if (msg.Id != s) return false;
                    if (msg.B1 != (byte)(s % 256)) return false;
                    if (Math.Abs(msg.D1 - ((double)s * 1.23456789)) > 0.000001) return false;
                    if (msg.S1 != (short)(s % 30000)) return false;
                    if (msg.C1 != (byte)('A' + (s % 26))) return false;
                    if (msg.L1 != s * 1000) return false;
                    if (msg.CheckValue != (ulong)s * 123456789UL) return false;
                    
                    int blobSize = 5 + (s % 10);
                    if (msg.Blob.Length != blobSize) return false;
                    for(int i=0; i<blobSize; i++) if (msg.Blob[i] != (byte)((s + i) % 256)) return false;
                    
                    return true;
                }
            );
        }
    }
}
