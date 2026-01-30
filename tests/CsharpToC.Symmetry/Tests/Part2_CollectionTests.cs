using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    public class Part2_CollectionTests : SymmetryTestBase
    {
        // --- Sequences ---

        [Fact] public void TestSequenceInt32Topic() => Verify<SequenceInt32Topic>("AtomicTests::SequenceInt32Topic", DeserializeSequenceInt32Topic, SerializeSequenceInt32Topic);
        private static SequenceInt32Topic DeserializeSequenceInt32Topic(ref CdrReader reader) => SequenceInt32Topic.Deserialize(ref reader);
        private static void SerializeSequenceInt32Topic(SequenceInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceInt64Topic() => Verify<SequenceInt64Topic>("AtomicTests::SequenceInt64Topic", DeserializeSequenceInt64Topic, SerializeSequenceInt64Topic);
        private static SequenceInt64Topic DeserializeSequenceInt64Topic(ref CdrReader reader) => SequenceInt64Topic.Deserialize(ref reader);
        private static void SerializeSequenceInt64Topic(SequenceInt64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceFloat32Topic() => Verify<SequenceFloat32Topic>("AtomicTests::SequenceFloat32Topic", DeserializeSequenceFloat32Topic, SerializeSequenceFloat32Topic);
        private static SequenceFloat32Topic DeserializeSequenceFloat32Topic(ref CdrReader reader) => SequenceFloat32Topic.Deserialize(ref reader);
        private static void SerializeSequenceFloat32Topic(SequenceFloat32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceFloat64Topic() => Verify<SequenceFloat64Topic>("AtomicTests::SequenceFloat64Topic", DeserializeSequenceFloat64Topic, SerializeSequenceFloat64Topic);
        private static SequenceFloat64Topic DeserializeSequenceFloat64Topic(ref CdrReader reader) => SequenceFloat64Topic.Deserialize(ref reader);
        private static void SerializeSequenceFloat64Topic(SequenceFloat64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceBooleanTopic() => Verify<SequenceBooleanTopic>("AtomicTests::SequenceBooleanTopic", DeserializeSequenceBooleanTopic, SerializeSequenceBooleanTopic);
        private static SequenceBooleanTopic DeserializeSequenceBooleanTopic(ref CdrReader reader) => SequenceBooleanTopic.Deserialize(ref reader);
        private static void SerializeSequenceBooleanTopic(SequenceBooleanTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceOctetTopic() => Verify<SequenceOctetTopic>("AtomicTests::SequenceOctetTopic", DeserializeSequenceOctetTopic, SerializeSequenceOctetTopic);
        private static SequenceOctetTopic DeserializeSequenceOctetTopic(ref CdrReader reader) => SequenceOctetTopic.Deserialize(ref reader);
        private static void SerializeSequenceOctetTopic(SequenceOctetTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceStringTopic() => Verify<SequenceStringTopic>("AtomicTests::SequenceStringTopic", DeserializeSequenceStringTopic, SerializeSequenceStringTopic);
        private static SequenceStringTopic DeserializeSequenceStringTopic(ref CdrReader reader) => SequenceStringTopic.Deserialize(ref reader);
        private static void SerializeSequenceStringTopic(SequenceStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceEnumTopic() => Verify<SequenceEnumTopic>("AtomicTests::SequenceEnumTopic", DeserializeSequenceEnumTopic, SerializeSequenceEnumTopic);
        private static SequenceEnumTopic DeserializeSequenceEnumTopic(ref CdrReader reader) => SequenceEnumTopic.Deserialize(ref reader);
        private static void SerializeSequenceEnumTopic(SequenceEnumTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceStructTopic() => Verify<SequenceStructTopic>("AtomicTests::SequenceStructTopic", DeserializeSequenceStructTopic, SerializeSequenceStructTopic);
        private static SequenceStructTopic DeserializeSequenceStructTopic(ref CdrReader reader) => SequenceStructTopic.Deserialize(ref reader);
        private static void SerializeSequenceStructTopic(SequenceStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestSequenceUnionTopic() => Verify<SequenceUnionTopic>("AtomicTests::SequenceUnionTopic", DeserializeSequenceUnionTopic, SerializeSequenceUnionTopic);
        private static SequenceUnionTopic DeserializeSequenceUnionTopic(ref CdrReader reader) => SequenceUnionTopic.Deserialize(ref reader);
        private static void SerializeSequenceUnionTopic(SequenceUnionTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Bounded / Edge Case Sequences ---

        [Fact] public void TestBoundedSequenceInt32Topic() => Verify<BoundedSequenceInt32Topic>("AtomicTests::BoundedSequenceInt32Topic", DeserializeBoundedSequenceInt32Topic, SerializeBoundedSequenceInt32Topic);
        private static BoundedSequenceInt32Topic DeserializeBoundedSequenceInt32Topic(ref CdrReader reader) => BoundedSequenceInt32Topic.Deserialize(ref reader);
        private static void SerializeBoundedSequenceInt32Topic(BoundedSequenceInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestEmptySequenceTopic() => Verify<EmptySequenceTopic>("AtomicTests::EmptySequenceTopic", DeserializeEmptySequenceTopic, SerializeEmptySequenceTopic);
        private static EmptySequenceTopic DeserializeEmptySequenceTopic(ref CdrReader reader) => EmptySequenceTopic.Deserialize(ref reader);
        private static void SerializeEmptySequenceTopic(EmptySequenceTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestLargeSequenceTopic() => Verify<LargeSequenceTopic>("AtomicTests::LargeSequenceTopic", DeserializeLargeSequenceTopic, SerializeLargeSequenceTopic);
        private static LargeSequenceTopic DeserializeLargeSequenceTopic(ref CdrReader reader) => LargeSequenceTopic.Deserialize(ref reader);
        private static void SerializeLargeSequenceTopic(LargeSequenceTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Arrays ---

        [Fact] public void TestArrayInt32Topic() => Verify<ArrayInt32Topic>("AtomicTests::ArrayInt32Topic", DeserializeArrayInt32Topic, SerializeArrayInt32Topic);
        private static ArrayInt32Topic DeserializeArrayInt32Topic(ref CdrReader reader) => ArrayInt32Topic.Deserialize(ref reader);
        private static void SerializeArrayInt32Topic(ArrayInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayFloat64Topic() => Verify<ArrayFloat64Topic>("AtomicTests::ArrayFloat64Topic", DeserializeArrayFloat64Topic, SerializeArrayFloat64Topic);
        private static ArrayFloat64Topic DeserializeArrayFloat64Topic(ref CdrReader reader) => ArrayFloat64Topic.Deserialize(ref reader);
        private static void SerializeArrayFloat64Topic(ArrayFloat64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayStringTopic() => Verify<ArrayStringTopic>("AtomicTests::ArrayStringTopic", DeserializeArrayStringTopic, SerializeArrayStringTopic);
        private static ArrayStringTopic DeserializeArrayStringTopic(ref CdrReader reader) => ArrayStringTopic.Deserialize(ref reader);
        private static void SerializeArrayStringTopic(ArrayStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArray2DInt32Topic() => Verify<Array2DInt32Topic>("AtomicTests::Array2DInt32Topic", DeserializeArray2DInt32Topic, SerializeArray2DInt32Topic);
        private static Array2DInt32Topic DeserializeArray2DInt32Topic(ref CdrReader reader) => Array2DInt32Topic.Deserialize(ref reader);
        private static void SerializeArray2DInt32Topic(Array2DInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArray3DInt32Topic() => Verify<Array3DInt32Topic>("AtomicTests::Array3DInt32Topic", DeserializeArray3DInt32Topic, SerializeArray3DInt32Topic);
        private static Array3DInt32Topic DeserializeArray3DInt32Topic(ref CdrReader reader) => Array3DInt32Topic.Deserialize(ref reader);
        private static void SerializeArray3DInt32Topic(Array3DInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestArrayStructTopic() => Verify<ArrayStructTopic>("AtomicTests::ArrayStructTopic", DeserializeArrayStructTopic, SerializeArrayStructTopic);
        private static ArrayStructTopic DeserializeArrayStructTopic(ref CdrReader reader) => ArrayStructTopic.Deserialize(ref reader);
        private static void SerializeArrayStructTopic(ArrayStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Helper ---
        private void Verify<T>(string topicName, DeserializeDelegate<T> d, SerializeDelegate<T> s) 
        {
            VerifySymmetry<T>(topicName, 1420, d, s);
        }
    }
}
