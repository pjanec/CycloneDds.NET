using Xunit;
using CsharpToC.Symmetry.Infrastructure;
using AtomicTests;
using CycloneDDS.Core;

namespace CsharpToC.Symmetry.Tests
{
    /// <summary>
    /// Part 1: Primitive Types
    /// </summary>
    public class Part1_PrimitiveTests : SymmetryTestBase
    {
        [Fact]
        public void TestCharTopic()
        {
            VerifySymmetry<AtomicTests.CharTopic>(
                "AtomicTests::CharTopic",
                seed: 1420,
                deserializer: DeserializeCharTopic,
                serializer: SerializeCharTopic
            );
        }

        private static AtomicTests.CharTopic DeserializeCharTopic(ref CdrReader reader)
        {
            return AtomicTests.CharTopic.Deserialize(ref reader);
        }

        private static void SerializeCharTopic(AtomicTests.CharTopic obj, ref CdrWriter writer)
        {
            obj.Serialize(ref writer);
        }

        [Fact]
        public void TestInt32Topic()
        {
            VerifySymmetry<AtomicTests.Int32Topic>(
                "AtomicTests::Int32Topic",
                seed: 1420,
                deserializer: DeserializeInt32Topic,
                serializer: SerializeInt32Topic
            );
        }

        private static AtomicTests.Int32Topic DeserializeInt32Topic(ref CdrReader reader)
        {
            return AtomicTests.Int32Topic.Deserialize(ref reader);
        }

        private static void SerializeInt32Topic(AtomicTests.Int32Topic obj, ref CdrWriter writer)
        {
            obj.Serialize(ref writer);
        }
    }
}
