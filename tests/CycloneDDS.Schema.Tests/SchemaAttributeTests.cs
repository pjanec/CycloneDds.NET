using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests
{
    public class SchemaAttributeTests
    {
        [DdsTopic("TestStruct")]
        struct TestStructTopic { }

        [DdsTopic("TestClass")]
        class TestClassTopic { }

        struct TestKeyStruct
        {
            [DdsKey]
            public int Id;
        }

        [DdsUnion]
        struct TestUnion { }

        [DdsManaged]
        class TestManagedClass { }

        struct TestManagedField
        {
            [DdsManaged]
            public string Name;
        }

        [Fact]
        public void DdsTopic_CanBeAppliedToStruct()
        {
            var attr = typeof(TestStructTopic).GetCustomAttribute<DdsTopicAttribute>();
            Assert.NotNull(attr);
            Assert.Equal("TestStruct", attr.TopicName);
        }

        [Fact]
        public void DdsTopic_CanBeAppliedToClass()
        {
            var attr = typeof(TestClassTopic).GetCustomAttribute<DdsTopicAttribute>();
            Assert.NotNull(attr);
            Assert.Equal("TestClass", attr.TopicName);
        }

        [Fact]
        public void DdsKey_CanBeAppliedToField()
        {
            var field = typeof(TestKeyStruct).GetField("Id");
            var attr = field.GetCustomAttribute<DdsKeyAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void DdsUnion_CanBeAppliedToStruct()
        {
            var attr = typeof(TestUnion).GetCustomAttribute<DdsUnionAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void DdsManaged_CanBeAppliedToClassAndField()
        {
            var classAttr = typeof(TestManagedClass).GetCustomAttribute<DdsManagedAttribute>();
            Assert.NotNull(classAttr);

            var field = typeof(TestManagedField).GetField("Name");
            var fieldAttr = field.GetCustomAttribute<DdsManagedAttribute>();
            Assert.NotNull(fieldAttr);
        }

        [Fact]
        public void FixedString32_HasCorrectSize()
        {
            Assert.Equal(32, Marshal.SizeOf<FixedString32>());
        }

        [Fact]
        public void FixedString64_HasCorrectSize()
        {
            Assert.Equal(64, Marshal.SizeOf<FixedString64>());
        }

        [Fact]
        public void BoundedSeq_EnforcesMaxLength()
        {
            var seq = new BoundedSeq<int>(3);
            seq.Add(1);
            seq.Add(2);
            seq.Add(3);

            Assert.Throws<InvalidOperationException>(() => seq.Add(4));
            Assert.Equal(3, seq.Count);
        }

        [Fact]
        public void DdsQos_StoresSettings()
        {
            var attr = new DdsQosAttribute
            {
                Reliability = DdsReliability.Reliable,
                Durability = DdsDurability.TransientLocal
            };

            Assert.Equal(DdsReliability.Reliable, attr.Reliability);
            Assert.Equal(DdsDurability.TransientLocal, attr.Durability);
        }

        [Fact]
        public void DdsTopic_Constructor_ThrowsOnNullOrWhitespace()
        {
            // ME1-T03: the constructor no longer throws – null/whitespace topic names are
            // valid and signal "derive from type FullName" at runtime.
            // Verify that the old behaviour (throwing) is NOT present.
            var noArg  = new DdsTopicAttribute();          // no throw expected
            var nullArg = new DdsTopicAttribute(null);     // no throw expected

            Assert.Null(noArg.TopicName);
            Assert.Null(nullArg.TopicName);
        }

        [Fact]
        public void DdsTopic_NoArgConstructor_IsValid()
        {
            // ME1-T03 success condition 1: new DdsTopicAttribute() is valid, TopicName is null.
            var attr = new DdsTopicAttribute();
            Assert.Null(attr.TopicName);
        }

        [Fact]
        public void DdsTopic_ExplicitName_IsPreserved()
        {
            // ME1-T03 success condition 3: explicit name is kept unchanged.
            var attr = new DdsTopicAttribute("ExplicitName");
            Assert.Equal("ExplicitName", attr.TopicName);
        }
    }
}
