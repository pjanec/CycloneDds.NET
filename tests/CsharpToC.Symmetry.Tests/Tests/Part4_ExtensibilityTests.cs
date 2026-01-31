using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    public class Part4_ExtensibilityTests : SymmetryTestBase
    {
        // --- Appendable Primitives ---

        [Fact] public void TestBooleanTopicAppendable() => Verify<BooleanTopicAppendable>("AtomicTests::BooleanTopicAppendable", DeserializeBooleanTopicAppendable, SerializeBooleanTopicAppendable);
        private static BooleanTopicAppendable DeserializeBooleanTopicAppendable(ref CdrReader reader) => BooleanTopicAppendable.Deserialize(ref reader);
        private static void SerializeBooleanTopicAppendable(BooleanTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt32TopicAppendable() => Verify<Int32TopicAppendable>("AtomicTests::Int32TopicAppendable", DeserializeInt32TopicAppendable, SerializeInt32TopicAppendable);
        private static Int32TopicAppendable DeserializeInt32TopicAppendable(ref CdrReader reader) => Int32TopicAppendable.Deserialize(ref reader);
        private static void SerializeInt32TopicAppendable(Int32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestCharTopicAppendable() => Verify<CharTopicAppendable>("AtomicTests::CharTopicAppendable", DeserializeCharTopicAppendable, SerializeCharTopicAppendable);
        private static CharTopicAppendable DeserializeCharTopicAppendable(ref CdrReader reader) => CharTopicAppendable.Deserialize(ref reader);
        private static void SerializeCharTopicAppendable(CharTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOctetTopicAppendable() => Verify<OctetTopicAppendable>("AtomicTests::OctetTopicAppendable", DeserializeOctetTopicAppendable, SerializeOctetTopicAppendable);
        private static OctetTopicAppendable DeserializeOctetTopicAppendable(ref CdrReader reader) => OctetTopicAppendable.Deserialize(ref reader);
        private static void SerializeOctetTopicAppendable(OctetTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt16TopicAppendable() => Verify<Int16TopicAppendable>("AtomicTests::Int16TopicAppendable", DeserializeInt16TopicAppendable, SerializeInt16TopicAppendable);
        private static Int16TopicAppendable DeserializeInt16TopicAppendable(ref CdrReader reader) => Int16TopicAppendable.Deserialize(ref reader);
        private static void SerializeInt16TopicAppendable(Int16TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt16TopicAppendable() => Verify<UInt16TopicAppendable>("AtomicTests::UInt16TopicAppendable", DeserializeUInt16TopicAppendable, SerializeUInt16TopicAppendable);
        private static UInt16TopicAppendable DeserializeUInt16TopicAppendable(ref CdrReader reader) => UInt16TopicAppendable.Deserialize(ref reader);
        private static void SerializeUInt16TopicAppendable(UInt16TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt32TopicAppendable() => Verify<UInt32TopicAppendable>("AtomicTests::UInt32TopicAppendable", DeserializeUInt32TopicAppendable, SerializeUInt32TopicAppendable);
        private static UInt32TopicAppendable DeserializeUInt32TopicAppendable(ref CdrReader reader) => UInt32TopicAppendable.Deserialize(ref reader);
        private static void SerializeUInt32TopicAppendable(UInt32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestInt64TopicAppendable() => Verify<Int64TopicAppendable>("AtomicTests::Int64TopicAppendable", DeserializeInt64TopicAppendable, SerializeInt64TopicAppendable);
        private static Int64TopicAppendable DeserializeInt64TopicAppendable(ref CdrReader reader) => Int64TopicAppendable.Deserialize(ref reader);
        private static void SerializeInt64TopicAppendable(Int64TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUInt64TopicAppendable() => Verify<UInt64TopicAppendable>("AtomicTests::UInt64TopicAppendable", DeserializeUInt64TopicAppendable, SerializeUInt64TopicAppendable);
        private static UInt64TopicAppendable DeserializeUInt64TopicAppendable(ref CdrReader reader) => UInt64TopicAppendable.Deserialize(ref reader);
        private static void SerializeUInt64TopicAppendable(UInt64TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFloat32TopicAppendable() => Verify<Float32TopicAppendable>("AtomicTests::Float32TopicAppendable", DeserializeFloat32TopicAppendable, SerializeFloat32TopicAppendable);
        private static Float32TopicAppendable DeserializeFloat32TopicAppendable(ref CdrReader reader) => Float32TopicAppendable.Deserialize(ref reader);
        private static void SerializeFloat32TopicAppendable(Float32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFloat64TopicAppendable() => Verify<Float64TopicAppendable>("AtomicTests::Float64TopicAppendable", DeserializeFloat64TopicAppendable, SerializeFloat64TopicAppendable);
        private static Float64TopicAppendable DeserializeFloat64TopicAppendable(ref CdrReader reader) => Float64TopicAppendable.Deserialize(ref reader);
        private static void SerializeFloat64TopicAppendable(Float64TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Appendable Strings & Enums ---

        [Fact] public void TestStringUnboundedTopicAppendable() => Verify<StringUnboundedTopicAppendable>("AtomicTests::StringUnboundedTopicAppendable", DeserializeStringUnboundedTopicAppendable, SerializeStringUnboundedTopicAppendable);
        private static StringUnboundedTopicAppendable DeserializeStringUnboundedTopicAppendable(ref CdrReader reader) => StringUnboundedTopicAppendable.Deserialize(ref reader);
        private static void SerializeStringUnboundedTopicAppendable(StringUnboundedTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestStringBounded32TopicAppendable() => Verify<StringBounded32TopicAppendable>("AtomicTests::StringBounded32TopicAppendable", DeserializeStringBounded32TopicAppendable, SerializeStringBounded32TopicAppendable);
        private static StringBounded32TopicAppendable DeserializeStringBounded32TopicAppendable(ref CdrReader reader) => StringBounded32TopicAppendable.Deserialize(ref reader);
        private static void SerializeStringBounded32TopicAppendable(StringBounded32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestStringBounded256TopicAppendable() => Verify<StringBounded256TopicAppendable>("AtomicTests::StringBounded256TopicAppendable", DeserializeStringBounded256TopicAppendable, SerializeStringBounded256TopicAppendable);
        private static StringBounded256TopicAppendable DeserializeStringBounded256TopicAppendable(ref CdrReader reader) => StringBounded256TopicAppendable.Deserialize(ref reader);
        private static void SerializeStringBounded256TopicAppendable(StringBounded256TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestEnumTopicAppendable() => Verify<EnumTopicAppendable>("AtomicTests::EnumTopicAppendable", DeserializeEnumTopicAppendable, SerializeEnumTopicAppendable);
        private static EnumTopicAppendable DeserializeEnumTopicAppendable(ref CdrReader reader) => EnumTopicAppendable.Deserialize(ref reader);
        private static void SerializeEnumTopicAppendable(EnumTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestColorEnumTopicAppendable() => Verify<ColorEnumTopicAppendable>("AtomicTests::ColorEnumTopicAppendable", DeserializeColorEnumTopicAppendable, SerializeColorEnumTopicAppendable);
        private static ColorEnumTopicAppendable DeserializeColorEnumTopicAppendable(ref CdrReader reader) => ColorEnumTopicAppendable.Deserialize(ref reader);
        private static void SerializeColorEnumTopicAppendable(ColorEnumTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Appendable Collections ---

        [Fact] public void TestSequenceInt32TopicAppendable() => Verify<SequenceInt32TopicAppendable>("AtomicTests::SequenceInt32TopicAppendable", DeserializeSequenceInt32TopicAppendable, SerializeSequenceInt32TopicAppendable);
        private static SequenceInt32TopicAppendable DeserializeSequenceInt32TopicAppendable(ref CdrReader reader) => SequenceInt32TopicAppendable.Deserialize(ref reader);
        private static void SerializeSequenceInt32TopicAppendable(SequenceInt32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayInt32TopicAppendable() => Verify<ArrayInt32TopicAppendable>("AtomicTests::ArrayInt32TopicAppendable", DeserializeArrayInt32TopicAppendable, SerializeArrayInt32TopicAppendable);
        private static ArrayInt32TopicAppendable DeserializeArrayInt32TopicAppendable(ref CdrReader reader) => ArrayInt32TopicAppendable.Deserialize(ref reader);
        private static void SerializeArrayInt32TopicAppendable(ArrayInt32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayFloat64TopicAppendable() => Verify<ArrayFloat64TopicAppendable>("AtomicTests::ArrayFloat64TopicAppendable", DeserializeArrayFloat64TopicAppendable, SerializeArrayFloat64TopicAppendable);
        private static ArrayFloat64TopicAppendable DeserializeArrayFloat64TopicAppendable(ref CdrReader reader) => CsharpToC.Symmetry.Infrastructure.ArrayFloat64TopicAppendable_Manual.Deserialize(ref reader);
        private static void SerializeArrayFloat64TopicAppendable(ArrayFloat64TopicAppendable obj, ref CdrWriter writer) => CsharpToC.Symmetry.Infrastructure.ArrayFloat64TopicAppendable_Manual.Serialize(obj, ref writer);

        [Fact] public void TestArrayStringTopicAppendable() => Verify<ArrayStringTopicAppendable>("AtomicTests::ArrayStringTopicAppendable", DeserializeArrayStringTopicAppendable, SerializeArrayStringTopicAppendable);
        private static ArrayStringTopicAppendable DeserializeArrayStringTopicAppendable(ref CdrReader reader) => ArrayStringTopicAppendable.Deserialize(ref reader);
        private static void SerializeArrayStringTopicAppendable(ArrayStringTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArray2DInt32TopicAppendable() => Verify<Array2DInt32TopicAppendable>("AtomicTests::Array2DInt32TopicAppendable", DeserializeArray2DInt32TopicAppendable, SerializeArray2DInt32TopicAppendable);
        private static Array2DInt32TopicAppendable DeserializeArray2DInt32TopicAppendable(ref CdrReader reader) => Array2DInt32TopicAppendable.Deserialize(ref reader);
        private static void SerializeArray2DInt32TopicAppendable(Array2DInt32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArray3DInt32TopicAppendable() => Verify<Array3DInt32TopicAppendable>("AtomicTests::Array3DInt32TopicAppendable", DeserializeArray3DInt32TopicAppendable, SerializeArray3DInt32TopicAppendable);
        private static Array3DInt32TopicAppendable DeserializeArray3DInt32TopicAppendable(ref CdrReader reader) => Array3DInt32TopicAppendable.Deserialize(ref reader);
        private static void SerializeArray3DInt32TopicAppendable(Array3DInt32TopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayStructTopicAppendable() => Verify<ArrayStructTopicAppendable>("AtomicTests::ArrayStructTopicAppendable", DeserializeArrayStructTopicAppendable, SerializeArrayStructTopicAppendable);
        private static ArrayStructTopicAppendable DeserializeArrayStructTopicAppendable(ref CdrReader reader) => ArrayStructTopicAppendable.Deserialize(ref reader);
        private static void SerializeArrayStructTopicAppendable(ArrayStructTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceUnionAppendableTopic() => Verify<SequenceUnionAppendableTopic>("AtomicTests::SequenceUnionAppendableTopic", DeserializeSequenceUnionAppendableTopic, SerializeSequenceUnionAppendableTopic);
        private static SequenceUnionAppendableTopic DeserializeSequenceUnionAppendableTopic(ref CdrReader reader) => SequenceUnionAppendableTopic.Deserialize(ref reader);
        private static void SerializeSequenceUnionAppendableTopic(SequenceUnionAppendableTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceEnumAppendableTopic() => Verify<SequenceEnumAppendableTopic>("AtomicTests::SequenceEnumAppendableTopic", DeserializeSequenceEnumAppendableTopic, SerializeSequenceEnumAppendableTopic);
        private static SequenceEnumAppendableTopic DeserializeSequenceEnumAppendableTopic(ref CdrReader reader) => SequenceEnumAppendableTopic.Deserialize(ref reader);
        private static void SerializeSequenceEnumAppendableTopic(SequenceEnumAppendableTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUnionLongDiscTopicAppendable() => Verify<UnionLongDiscTopicAppendable>("AtomicTests::UnionLongDiscTopicAppendable", DeserializeUnionLongDiscTopicAppendable, SerializeUnionLongDiscTopicAppendable);
        private static UnionLongDiscTopicAppendable DeserializeUnionLongDiscTopicAppendable(ref CdrReader reader) => UnionLongDiscTopicAppendable.Deserialize(ref reader);
        private static void SerializeUnionLongDiscTopicAppendable(UnionLongDiscTopicAppendable obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestAppendableStructTopic() => Verify<AppendableStructTopic>("AtomicTests::AppendableStructTopic", DeserializeAppendableStructTopic, SerializeAppendableStructTopic);
        private static AppendableStructTopic DeserializeAppendableStructTopic(ref CdrReader reader) => AppendableStructTopic.Deserialize(ref reader);
        private static void SerializeAppendableStructTopic(AppendableStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Extensibility ---

        [Fact] public void TestFinalInt32Topic() => Verify<FinalInt32Topic>("AtomicTests::FinalInt32Topic", DeserializeFinalInt32Topic, SerializeFinalInt32Topic);
        private static FinalInt32Topic DeserializeFinalInt32Topic(ref CdrReader reader) => FinalInt32Topic.Deserialize(ref reader);
        private static void SerializeFinalInt32Topic(FinalInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFinalStructTopic() => Verify<FinalStructTopic>("AtomicTests::FinalStructTopic", DeserializeFinalStructTopic, SerializeFinalStructTopic);
        private static FinalStructTopic DeserializeFinalStructTopic(ref CdrReader reader) => FinalStructTopic.Deserialize(ref reader);
        private static void SerializeFinalStructTopic(FinalStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // [Fact] public void TestMutableInt32Topic() => Verify<MutableInt32Topic>("AtomicTests::MutableInt32Topic", DeserializeMutableInt32Topic, SerializeMutableInt32Topic);
        // private static MutableInt32Topic DeserializeMutableInt32Topic(ref CdrReader reader) => MutableInt32Topic.Deserialize(ref reader);
        // private static void SerializeMutableInt32Topic(MutableInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // [Fact] public void TestMutableStructTopic() => Verify<MutableStructTopic>("AtomicTests::MutableStructTopic", DeserializeMutableStructTopic, SerializeMutableStructTopic);
        // private static MutableStructTopic DeserializeMutableStructTopic(ref CdrReader reader) => MutableStructTopic.Deserialize(ref reader);
        // private static void SerializeMutableStructTopic(MutableStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Catch All ---

        [Fact] public void TestAllPrimitivesAtomicTopic() => Verify<AllPrimitivesAtomicTopic>("AtomicTests::AllPrimitivesAtomicTopic", DeserializeAllPrimitivesAtomicTopic, SerializeAllPrimitivesAtomicTopic);
        private static AllPrimitivesAtomicTopic DeserializeAllPrimitivesAtomicTopic(ref CdrReader reader) => CsharpToC.Symmetry.Infrastructure.AllPrimitivesAtomicTopic_Manual.Deserialize(ref reader);
        private static void SerializeAllPrimitivesAtomicTopic(AllPrimitivesAtomicTopic obj, ref CdrWriter writer) => CsharpToC.Symmetry.Infrastructure.AllPrimitivesAtomicTopic_Manual.Serialize(obj, ref writer);

        // --- Helper ---
        private void Verify<T>(string topicName, DeserializeDelegate<T> d, SerializeDelegate<T> s) 
        {
            VerifySymmetry<T>(topicName, 1420, d, s);
        }
    }
}
