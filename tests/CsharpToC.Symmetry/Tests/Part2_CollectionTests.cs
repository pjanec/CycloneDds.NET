using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    /// <summary>
    /// Placeholder test class for Part 2: Collection Types (Arrays, Sequences)
    /// TODO: Implement actual test cases once IDL is processed and types are generated
    /// </summary>
    public class Part2_CollectionTests : SymmetryTestBase
    {
        [Fact]
        public void TestArrayInt32Topic()
        {
            VerifySymmetry<AtomicTests.ArrayInt32Topic>(
                "AtomicTests::ArrayInt32Topic",
                seed: 1420,
                deserializer: DeserializeArrayInt32Topic,
                serializer: SerializeArrayInt32Topic
            );
        }

        private static AtomicTests.ArrayInt32Topic DeserializeArrayInt32Topic(ref CdrReader reader)
        {
            return AtomicTests.ArrayInt32Topic.Deserialize(ref reader);
        }

        private static void SerializeArrayInt32Topic(AtomicTests.ArrayInt32Topic obj, ref CdrWriter writer)
        {
            obj.Serialize(ref writer);
        }

        [Fact]
        public void TestSequenceStringTopic()
        {
            VerifySymmetry<AtomicTests.SequenceStringTopic>(
                "AtomicTests::SequenceStringTopic",
                seed: 1420,
                deserializer: DeserializeSequenceStringTopic,
                serializer: SerializeSequenceStringTopic
            );
        }

        private static AtomicTests.SequenceStringTopic DeserializeSequenceStringTopic(ref CdrReader reader)
        {
            return AtomicTests.SequenceStringTopic.Deserialize(ref reader);
        }

        private static void SerializeSequenceStringTopic(AtomicTests.SequenceStringTopic obj, ref CdrWriter writer)
        {
            obj.Serialize(ref writer);
        }

        [Fact]
        public void TestInt32TopicAppendable()
        {
            VerifySymmetry<AtomicTests.Int32TopicAppendable>(
                "AtomicTests::Int32TopicAppendable",
                seed: 1420,
                deserializer: DeserializeInt32TopicAppendable,
                serializer: SerializeInt32TopicAppendable
            );
        }

        private static AtomicTests.Int32TopicAppendable DeserializeInt32TopicAppendable(ref CdrReader reader)
        {
            return AtomicTests.Int32TopicAppendable.Deserialize(ref reader);
        }

        private static void SerializeInt32TopicAppendable(AtomicTests.Int32TopicAppendable obj, ref CdrWriter writer)
        {
            obj.Serialize(ref writer);
        }


    }
}
