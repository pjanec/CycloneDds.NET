using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    /// <summary>
    /// Part 5: Section 13 - Complex Integration Scenarios
    /// Tests real-world simulation topics that combine multiple DDS features.
    /// 
    /// NOTE: These tests are currently commented out pending C# code generation for Section 13 topics.
    /// The native handlers are implemented and the IDL is updated.
    /// To enable: Regenerate C# code and uncomment the test methods below.
    /// </summary>
    public class Part5_ComplexIntegrationTests : SymmetryTestBase
    {
        // --- SCENARIO 1: The "Offset Nightmare" ---
        // Keys at variable offsets after dynamic data
        
        [Fact] 
        public void TestOffsetKeyTopic() => Verify<OffsetKeyTopic>(
            "AtomicTests::OffsetKeyTopic", 
            DeserializeOffsetKeyTopic, 
            SerializeOffsetKeyTopic);
        
        private static OffsetKeyTopic DeserializeOffsetKeyTopic(ref CdrReader reader) => 
            OffsetKeyTopic.Deserialize(ref reader);
        
        private static void SerializeOffsetKeyTopic(OffsetKeyTopic obj, ref CdrWriter writer) => 
            obj.Serialize(ref writer);

        // --- SCENARIO 2: The "Kitchen Sink" (Robotics State) ---
        // @appendable with arrays, sequences of structs, unions, and optional fields
        
        [Fact] 
        public void TestRobotStateTopic() => Verify<RobotStateTopic>(
            "AtomicTests::RobotStateTopic", 
            DeserializeRobotStateTopic, 
            SerializeRobotStateTopic);
        
        private static RobotStateTopic DeserializeRobotStateTopic(ref CdrReader reader) => 
            CsharpToC.Symmetry.Infrastructure.RobotStateTopic_Manual.Deserialize(ref reader);
        
        private static void SerializeRobotStateTopic(RobotStateTopic obj, ref CdrWriter writer) => 
            CsharpToC.Symmetry.Infrastructure.RobotStateTopic_Manual.Serialize(obj, ref writer);

        // --- SCENARIO 3: The "Sparse Mutable" (IoT Telemetry) ---
        // @mutable with sparse IDs and non-sequential keys
        
        [Fact] 
        public void TestIoTDeviceMutableTopic() => Verify<IoTDeviceMutableTopic>(
            "AtomicTests::IoTDeviceMutableTopic", 
            DeserializeIoTDeviceMutableTopic, 
            SerializeIoTDeviceMutableTopic);
        
        private static IoTDeviceMutableTopic DeserializeIoTDeviceMutableTopic(ref CdrReader reader) => 
            IoTDeviceMutableTopic.Deserialize(ref reader);
        
        private static void SerializeIoTDeviceMutableTopic(IoTDeviceMutableTopic obj, ref CdrWriter writer) => 
            obj.Serialize(ref writer);

        // --- SCENARIO 4: The "Alignment Torture Test" ---
        // Mixing 1-byte, 2-byte, 4-byte, and 8-byte types
        
        [Fact] 
        public void TestAlignmentCheckTopic() => Verify<AlignmentCheckTopic>(
            "AtomicTests::AlignmentCheckTopic", 
            DeserializeAlignmentCheckTopic, 
            SerializeAlignmentCheckTopic);
        
        private static AlignmentCheckTopic DeserializeAlignmentCheckTopic(ref CdrReader reader) => 
            AlignmentCheckTopic.Deserialize(ref reader);
        
        private static void SerializeAlignmentCheckTopic(AlignmentCheckTopic obj, ref CdrWriter writer) => 
            obj.Serialize(ref writer);

        // --- Helper ---
        private void Verify<T>(string topicName, DeserializeDelegate<T> d, SerializeDelegate<T> s) 
        {
            VerifySymmetry<T>(topicName, 1420, d, s);
        }
    }
}
