using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    public class Part3_ComplexTests : SymmetryTestBase
    {
        // --- Nested Structures ---

        [Fact] public void TestNestedStructTopic() => Verify<NestedStructTopic>("AtomicTests::NestedStructTopic", DeserializeNestedStructTopic, SerializeNestedStructTopic);
        private static NestedStructTopic DeserializeNestedStructTopic(ref CdrReader reader) => NestedStructTopic.Deserialize(ref reader);
        private static void SerializeNestedStructTopic(NestedStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestNested3DTopic() => Verify<Nested3DTopic>("AtomicTests::Nested3DTopic", DeserializeNested3DTopic, SerializeNested3DTopic);
        private static Nested3DTopic DeserializeNested3DTopic(ref CdrReader reader) => Nested3DTopic.Deserialize(ref reader);
        private static void SerializeNested3DTopic(Nested3DTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestDoublyNestedTopic() => Verify<DoublyNestedTopic>("AtomicTests::DoublyNestedTopic", DeserializeDoublyNestedTopic, SerializeDoublyNestedTopic);
        private static DoublyNestedTopic DeserializeDoublyNestedTopic(ref CdrReader reader) => DoublyNestedTopic.Deserialize(ref reader);
        private static void SerializeDoublyNestedTopic(DoublyNestedTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestComplexNestedTopic() => Verify<ComplexNestedTopic>("AtomicTests::ComplexNestedTopic", DeserializeComplexNestedTopic, SerializeComplexNestedTopic);
        private static ComplexNestedTopic DeserializeComplexNestedTopic(ref CdrReader reader) => ComplexNestedTopic.Deserialize(ref reader);
        private static void SerializeComplexNestedTopic(ComplexNestedTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Unions ---

        [Fact] public void TestUnionLongDiscTopic() => Verify<UnionLongDiscTopic>("AtomicTests::UnionLongDiscTopic", DeserializeUnionLongDiscTopic, SerializeUnionLongDiscTopic);
        private static UnionLongDiscTopic DeserializeUnionLongDiscTopic(ref CdrReader reader) => UnionLongDiscTopic.Deserialize(ref reader);
        private static void SerializeUnionLongDiscTopic(UnionLongDiscTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUnionBoolDiscTopic() => Verify<UnionBoolDiscTopic>("AtomicTests::UnionBoolDiscTopic", DeserializeUnionBoolDiscTopic, SerializeUnionBoolDiscTopic);
        private static UnionBoolDiscTopic DeserializeUnionBoolDiscTopic(ref CdrReader reader) => UnionBoolDiscTopic.Deserialize(ref reader);
        private static void SerializeUnionBoolDiscTopic(UnionBoolDiscTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUnionEnumDiscTopic() => Verify<UnionEnumDiscTopic>("AtomicTests::UnionEnumDiscTopic", DeserializeUnionEnumDiscTopic, SerializeUnionEnumDiscTopic);
        private static UnionEnumDiscTopic DeserializeUnionEnumDiscTopic(ref CdrReader reader) => UnionEnumDiscTopic.Deserialize(ref reader);
        private static void SerializeUnionEnumDiscTopic(UnionEnumDiscTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestUnionShortDiscTopic() => Verify<UnionShortDiscTopic>("AtomicTests::UnionShortDiscTopic", DeserializeUnionShortDiscTopic, SerializeUnionShortDiscTopic);
        private static UnionShortDiscTopic DeserializeUnionShortDiscTopic(ref CdrReader reader) => UnionShortDiscTopic.Deserialize(ref reader);
        private static void SerializeUnionShortDiscTopic(UnionShortDiscTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Optional Fields ---

        [Fact] public void TestOptionalInt32Topic() => Verify<OptionalInt32Topic>("AtomicTests::OptionalInt32Topic", DeserializeOptionalInt32Topic, SerializeOptionalInt32Topic);
        private static OptionalInt32Topic DeserializeOptionalInt32Topic(ref CdrReader reader) => OptionalInt32Topic.Deserialize(ref reader);
        private static void SerializeOptionalInt32Topic(OptionalInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOptionalFloat64Topic() => Verify<OptionalFloat64Topic>("AtomicTests::OptionalFloat64Topic", DeserializeOptionalFloat64Topic, SerializeOptionalFloat64Topic);
        private static OptionalFloat64Topic DeserializeOptionalFloat64Topic(ref CdrReader reader) => OptionalFloat64Topic.Deserialize(ref reader);
        private static void SerializeOptionalFloat64Topic(OptionalFloat64Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOptionalStringTopic() => Verify<OptionalStringTopic>("AtomicTests::OptionalStringTopic", DeserializeOptionalStringTopic, SerializeOptionalStringTopic);
        private static OptionalStringTopic DeserializeOptionalStringTopic(ref CdrReader reader) => OptionalStringTopic.Deserialize(ref reader);
        private static void SerializeOptionalStringTopic(OptionalStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOptionalStructTopic() => Verify<OptionalStructTopic>("AtomicTests::OptionalStructTopic", DeserializeOptionalStructTopic, SerializeOptionalStructTopic);
        private static OptionalStructTopic DeserializeOptionalStructTopic(ref CdrReader reader) => OptionalStructTopic.Deserialize(ref reader);
        private static void SerializeOptionalStructTopic(OptionalStructTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestOptionalEnumTopic() => Verify<OptionalEnumTopic>("AtomicTests::OptionalEnumTopic", DeserializeOptionalEnumTopic, SerializeOptionalEnumTopic);
        private static OptionalEnumTopic DeserializeOptionalEnumTopic(ref CdrReader reader) => CsharpToC.Symmetry.Infrastructure.OptionalEnumTopic_Manual.Deserialize(ref reader);
        private static void SerializeOptionalEnumTopic(OptionalEnumTopic obj, ref CdrWriter writer) => CsharpToC.Symmetry.Infrastructure.OptionalEnumTopic_Manual.Serialize(obj, ref writer);

        [Fact] public void TestMultiOptionalTopic() => Verify<MultiOptionalTopic>("AtomicTests::MultiOptionalTopic", DeserializeMultiOptionalTopic, SerializeMultiOptionalTopic);
        private static MultiOptionalTopic DeserializeMultiOptionalTopic(ref CdrReader reader) => MultiOptionalTopic.Deserialize(ref reader);
        private static void SerializeMultiOptionalTopic(MultiOptionalTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Keys ---

        [Fact] public void TestTwoKeyInt32Topic() => Verify<TwoKeyInt32Topic>("AtomicTests::TwoKeyInt32Topic", DeserializeTwoKeyInt32Topic, SerializeTwoKeyInt32Topic);
        private static TwoKeyInt32Topic DeserializeTwoKeyInt32Topic(ref CdrReader reader) => TwoKeyInt32Topic.Deserialize(ref reader);
        private static void SerializeTwoKeyInt32Topic(TwoKeyInt32Topic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestTwoKeyStringTopic() => Verify<TwoKeyStringTopic>("AtomicTests::TwoKeyStringTopic", DeserializeTwoKeyStringTopic, SerializeTwoKeyStringTopic);
        private static TwoKeyStringTopic DeserializeTwoKeyStringTopic(ref CdrReader reader) => TwoKeyStringTopic.Deserialize(ref reader);
        private static void SerializeTwoKeyStringTopic(TwoKeyStringTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestThreeKeyTopic() => Verify<ThreeKeyTopic>("AtomicTests::ThreeKeyTopic", DeserializeThreeKeyTopic, SerializeThreeKeyTopic);
        private static ThreeKeyTopic DeserializeThreeKeyTopic(ref CdrReader reader) => ThreeKeyTopic.Deserialize(ref reader);
        private static void SerializeThreeKeyTopic(ThreeKeyTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestFourKeyTopic() => Verify<FourKeyTopic>("AtomicTests::FourKeyTopic", DeserializeFourKeyTopic, SerializeFourKeyTopic);
        private static FourKeyTopic DeserializeFourKeyTopic(ref CdrReader reader) => FourKeyTopic.Deserialize(ref reader);
        private static void SerializeFourKeyTopic(FourKeyTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestNestedKeyTopic() => Verify<NestedKeyTopic>("AtomicTests::NestedKeyTopic", DeserializeNestedKeyTopic, SerializeNestedKeyTopic);
        private static NestedKeyTopic DeserializeNestedKeyTopic(ref CdrReader reader) => NestedKeyTopic.Deserialize(ref reader);
        private static void SerializeNestedKeyTopic(NestedKeyTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestNestedKeyGeoTopic() => Verify<NestedKeyGeoTopic>("AtomicTests::NestedKeyGeoTopic", DeserializeNestedKeyGeoTopic, SerializeNestedKeyGeoTopic);
        private static NestedKeyGeoTopic DeserializeNestedKeyGeoTopic(ref CdrReader reader) => NestedKeyGeoTopic.Deserialize(ref reader);
        private static void SerializeNestedKeyGeoTopic(NestedKeyGeoTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        [Fact] public void TestNestedTripleKeyTopic() => Verify<NestedTripleKeyTopic>("AtomicTests::NestedTripleKeyTopic", DeserializeNestedTripleKeyTopic, SerializeNestedTripleKeyTopic);
        private static NestedTripleKeyTopic DeserializeNestedTripleKeyTopic(ref CdrReader reader) => NestedTripleKeyTopic.Deserialize(ref reader);
        private static void SerializeNestedTripleKeyTopic(NestedTripleKeyTopic obj, ref CdrWriter writer) => obj.Serialize(ref writer);

        // --- Helper ---
        private void Verify<T>(string topicName, DeserializeDelegate<T> d, SerializeDelegate<T> s) 
        {
            VerifySymmetry<T>(topicName, 1420, d, s);
        }
    }
}
