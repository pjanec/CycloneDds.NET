using System;
using System.Linq;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.KeyedMessages;

namespace CycloneDDS.Runtime.Tests
{
    /// <summary>
    /// Unit tests for DdsTypeSupport reflection helpers, including the cross-assembly-context
    /// fallback path introduced by ME2-T28.
    /// </summary>
    public class DdsTypeSupportTests
    {
        // ─── GetKeyDescriptors happy paths ───────────────────────────────────────

        [Fact]
        public void GetKeyDescriptors_KeyedType_ReturnsDescriptors()
        {
            var keys = DdsTypeSupport.GetKeyDescriptors<SingleKeyMessage>();

            Assert.NotNull(keys);
            Assert.Single(keys);
            Assert.Equal("DeviceId", keys[0].Name);
        }

        [Fact]
        public void GetKeyDescriptors_CompositeKeyType_ReturnsAllKeys()
        {
            var keys = DdsTypeSupport.GetKeyDescriptors<CompositeKeyMessage>();

            Assert.NotNull(keys);
            Assert.True(keys.Length >= 2, "CompositeKeyMessage should have at least 2 keys");
        }

        [Fact]
        public void GetKeyDescriptors_NonKeyedType_ReturnsNull()
        {
            // TestMessage has no [DdsKey] fields; generated GetKeyDescriptors() => null
            var keys = DdsTypeSupport.GetKeyDescriptors<TestMessage>();

            Assert.Null(keys);
        }

        [Fact]
        public void GetKeyDescriptors_MissingMethod_ThrowsInvalidOperationException()
        {
            // int has no generated DDS methods at all
            Assert.Throws<InvalidOperationException>(() => DdsTypeSupport.GetKeyDescriptors<int>());
        }

        [Fact]
        public void GetKeyDescriptors_IsCached_ReturnsSameArrayReference()
        {
            // The second call must be served from the ConcurrentDictionary cache, not a fresh allocation.
            var first  = DdsTypeSupport.GetKeyDescriptors<SingleKeyMessage>();
            var second = DdsTypeSupport.GetKeyDescriptors<SingleKeyMessage>();

            Assert.Same(first, second);
        }

        // ─── ConvertExternalKeyDescriptors (fallback slow path) ──────────────────

        /// <summary>
        /// Mimics a DdsKeyDescriptor struct that arrived from a foreign assembly load context:
        /// same field names and types, but a different CLR type identity.
        /// </summary>
        private struct ForeignDdsKeyDescriptor
        {
            public string Name;
            public uint   Offset;
            public uint   Index;
        }

        [Fact]
        public void ConvertExternalKeyDescriptors_Null_ReturnsNull()
        {
            var result = DdsTypeSupport.ConvertExternalKeyDescriptors(null);

            Assert.Null(result);
        }

        [Fact]
        public void ConvertExternalKeyDescriptors_EmptyArray_ReturnsEmptyArray()
        {
            var empty = Array.Empty<ForeignDdsKeyDescriptor>();

            var result = DdsTypeSupport.ConvertExternalKeyDescriptors(empty);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ConvertExternalKeyDescriptors_SingleElement_MapsFieldsCorrectly()
        {
            var foreign = new ForeignDdsKeyDescriptor[]
            {
                new ForeignDdsKeyDescriptor { Name = "SensorId", Offset = 8, Index = 0 }
            };

            var result = DdsTypeSupport.ConvertExternalKeyDescriptors(foreign);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("SensorId", result[0].Name);
            Assert.Equal(8u,         result[0].Offset);
            Assert.Equal(0u,         result[0].Index);
        }

        [Fact]
        public void ConvertExternalKeyDescriptors_MultipleElements_PreservesOrder()
        {
            var foreign = new ForeignDdsKeyDescriptor[]
            {
                new ForeignDdsKeyDescriptor { Name = "TopicId",   Offset = 4,  Index = 0 },
                new ForeignDdsKeyDescriptor { Name = "PartitionId", Offset = 8, Index = 1 },
                new ForeignDdsKeyDescriptor { Name = "SeqNum",    Offset = 12, Index = 2 }
            };

            var result = DdsTypeSupport.ConvertExternalKeyDescriptors(foreign);

            Assert.Equal(3,           result.Length);
            Assert.Equal("TopicId",   result[0].Name);
            Assert.Equal(4u,          result[0].Offset);
            Assert.Equal(0u,          result[0].Index);
            Assert.Equal("PartitionId", result[1].Name);
            Assert.Equal(8u,          result[1].Offset);
            Assert.Equal(1u,          result[1].Index);
            Assert.Equal("SeqNum",    result[2].Name);
            Assert.Equal(12u,         result[2].Offset);
            Assert.Equal(2u,          result[2].Index);
        }

        [Fact]
        public void ConvertExternalKeyDescriptors_MissingOffsetField_DefaultsToZero()
        {
            // Partial struct — only Name and Index, no Offset (e.g., old codegen variant)
            var partial = new PartialForeignDescriptor[]
            {
                new PartialForeignDescriptor { Name = "Key", Index = 0 }
            };

            var result = DdsTypeSupport.ConvertExternalKeyDescriptors(partial);

            Assert.NotNull(result);
            Assert.Equal("Key", result[0].Name);
            Assert.Equal(0u,    result[0].Offset);   // defaulted
            Assert.Equal(0u,    result[0].Index);
        }

        private struct PartialForeignDescriptor
        {
            public string Name;
            public uint   Index;
            // Offset intentionally absent
        }

        // ─── GetDescriptorOps passthrough sanity ─────────────────────────────────

        [Fact]
        public void GetDescriptorOps_ValidType_ReturnsNonEmpty()
        {
            var ops = DdsTypeSupport.GetDescriptorOps<SingleKeyMessage>();

            Assert.NotNull(ops);
            Assert.NotEmpty(ops);
        }

        [Fact]
        public void GetDescriptorOps_InvalidType_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => DdsTypeSupport.GetDescriptorOps<string>());
        }
    }
}
