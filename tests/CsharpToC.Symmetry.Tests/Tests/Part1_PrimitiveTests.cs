using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    public class Part1_PrimitiveTests : SymmetryTestBase
    {
        // --- Primitives ---

        [Fact] public void TestBooleanTopic() => Verify<BooleanTopic>("AtomicTests::BooleanTopic", DeserializeBooleanTopic, SerializeBooleanTopic);
        private static BooleanTopic DeserializeBooleanTopic(ref CdrReader reader) => BooleanTopic.Deserialize(ref reader);
        private static void SerializeBooleanTopic(BooleanTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestCharTopic() => Verify<CharTopic>("AtomicTests::CharTopic", DeserializeCharTopic, SerializeCharTopic);
        private static CharTopic DeserializeCharTopic(ref CdrReader reader) => CharTopic.Deserialize(ref reader);
        private static void SerializeCharTopic(CharTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOctetTopic() => Verify<OctetTopic>("AtomicTests::OctetTopic", DeserializeOctetTopic, SerializeOctetTopic);
        private static OctetTopic DeserializeOctetTopic(ref CdrReader reader) => OctetTopic.Deserialize(ref reader);
        private static void SerializeOctetTopic(OctetTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt16Topic() => Verify<Int16Topic>("AtomicTests::Int16Topic", DeserializeInt16Topic, SerializeInt16Topic);
        private static Int16Topic DeserializeInt16Topic(ref CdrReader reader) => Int16Topic.Deserialize(ref reader);
        private static void SerializeInt16Topic(Int16Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt16Topic() => Verify<UInt16Topic>("AtomicTests::UInt16Topic", DeserializeUInt16Topic, SerializeUInt16Topic);
        private static UInt16Topic DeserializeUInt16Topic(ref CdrReader reader) => UInt16Topic.Deserialize(ref reader);
        private static void SerializeUInt16Topic(UInt16Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt32Topic() => Verify<Int32Topic>("AtomicTests::Int32Topic", DeserializeInt32Topic, SerializeInt32Topic);
        private static Int32Topic DeserializeInt32Topic(ref CdrReader reader) => Int32Topic.Deserialize(ref reader);
        private static void SerializeInt32Topic(Int32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt32Topic() => Verify<UInt32Topic>("AtomicTests::UInt32Topic", DeserializeUInt32Topic, SerializeUInt32Topic);
        private static UInt32Topic DeserializeUInt32Topic(ref CdrReader reader) => UInt32Topic.Deserialize(ref reader);
        private static void SerializeUInt32Topic(UInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt64Topic() => Verify<Int64Topic>("AtomicTests::Int64Topic", DeserializeInt64Topic, SerializeInt64Topic);
        private static Int64Topic DeserializeInt64Topic(ref CdrReader reader) => Int64Topic.Deserialize(ref reader);
        private static void SerializeInt64Topic(Int64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt64Topic() => Verify<UInt64Topic>("AtomicTests::UInt64Topic", DeserializeUInt64Topic, SerializeUInt64Topic);
        private static UInt64Topic DeserializeUInt64Topic(ref CdrReader reader) => UInt64Topic.Deserialize(ref reader);
        private static void SerializeUInt64Topic(UInt64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFloat32Topic() => Verify<Float32Topic>("AtomicTests::Float32Topic", DeserializeFloat32Topic, SerializeFloat32Topic);
        private static Float32Topic DeserializeFloat32Topic(ref CdrReader reader) => Float32Topic.Deserialize(ref reader);
        private static void SerializeFloat32Topic(Float32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFloat64Topic() => Verify<Float64Topic>("AtomicTests::Float64Topic", DeserializeFloat64Topic, SerializeFloat64Topic);
        private static Float64Topic DeserializeFloat64Topic(ref CdrReader reader) => Float64Topic.Deserialize(ref reader);
        private static void SerializeFloat64Topic(Float64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Strings ---

        [Fact] public void TestStringUnboundedTopic() => Verify<StringUnboundedTopic>("AtomicTests::StringUnboundedTopic", DeserializeStringUnboundedTopic, SerializeStringUnboundedTopic);
        private static StringUnboundedTopic DeserializeStringUnboundedTopic(ref CdrReader reader) => StringUnboundedTopic.Deserialize(ref reader);
        private static void SerializeStringUnboundedTopic(StringUnboundedTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestStringBounded32Topic() => Verify<StringBounded32Topic>("AtomicTests::StringBounded32Topic", DeserializeStringBounded32Topic, SerializeStringBounded32Topic);
        private static StringBounded32Topic DeserializeStringBounded32Topic(ref CdrReader reader) => StringBounded32Topic.Deserialize(ref reader);
        private static void SerializeStringBounded32Topic(StringBounded32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestStringBounded256Topic() => Verify<StringBounded256Topic>("AtomicTests::StringBounded256Topic", DeserializeStringBounded256Topic, SerializeStringBounded256Topic);
        private static StringBounded256Topic DeserializeStringBounded256Topic(ref CdrReader reader) => StringBounded256Topic.Deserialize(ref reader);
        private static void SerializeStringBounded256Topic(StringBounded256Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestLongStringTopic() => Verify<LongStringTopic>("AtomicTests::LongStringTopic", DeserializeLongStringTopic, SerializeLongStringTopic);
        private static LongStringTopic DeserializeLongStringTopic(ref CdrReader reader) => LongStringTopic.Deserialize(ref reader);
        private static void SerializeLongStringTopic(LongStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUnboundedStringTopic() => Verify<UnboundedStringTopic>("AtomicTests::UnboundedStringTopic", DeserializeUnboundedStringTopic, SerializeUnboundedStringTopic);
        private static UnboundedStringTopic DeserializeUnboundedStringTopic(ref CdrReader reader) => UnboundedStringTopic.Deserialize(ref reader);
        private static void SerializeUnboundedStringTopic(UnboundedStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Enums ---

        [Fact] public void TestEnumTopic() => Verify<EnumTopic>("AtomicTests::EnumTopic", DeserializeEnumTopic, SerializeEnumTopic);
        private static EnumTopic DeserializeEnumTopic(ref CdrReader reader) => EnumTopic.Deserialize(ref reader);
        private static void SerializeEnumTopic(EnumTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestColorEnumTopic() => Verify<ColorEnumTopic>("AtomicTests::ColorEnumTopic", DeserializeColorEnumTopic, SerializeColorEnumTopic);
        private static ColorEnumTopic DeserializeColorEnumTopic(ref CdrReader reader) => ColorEnumTopic.Deserialize(ref reader);
        private static void SerializeColorEnumTopic(ColorEnumTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Helper ---
        private void Verify<T>(string topicName, DeserializeDelegate<T> d, SerializeDelegate<T> s) 
        {
            VerifySymmetry<T>(topicName, 1420, d, s);
        }
    }
}
