using System;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests;

public class AttributesTests
{
    [Fact]
    public void DdsTopicAttribute_RejectsNullOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new DdsTopicAttribute(null!));
        Assert.Throws<ArgumentException>(() => new DdsTopicAttribute(""));
        Assert.Throws<ArgumentException>(() => new DdsTopicAttribute("   "));
    }

    [Fact]
    public void DdsTopicAttribute_AcceptsValidName()
    {
        var attr = new DdsTopicAttribute("MyTopic");
        Assert.Equal("MyTopic", attr.TopicName);
    }

    [Fact]
    public void DdsQosAttribute_HasDefaults()
    {
        var attr = new DdsQosAttribute();
        Assert.Equal(DdsReliability.Reliable, attr.Reliability);
        Assert.Equal(DdsDurability.Volatile, attr.Durability); // Instructions said Volatile default?
        // Let's check my impl. Yes, Volatile.
        Assert.Equal(DdsHistoryKind.KeepLast, attr.HistoryKind);
        Assert.Equal(1, attr.HistoryDepth);
    }

    [Fact]
    public void DdsBoundAttribute_RejectsZeroOrNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DdsBoundAttribute(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DdsBoundAttribute(-1));
    }

    [Fact]
    public void DdsBoundAttribute_AcceptsPositive()
    {
        var attr = new DdsBoundAttribute(10);
        Assert.Equal(10, attr.Max);
    }

    [Fact]
    public void DdsTypeNameAttribute_RejectsNullOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new DdsTypeNameAttribute(null!));
        Assert.Throws<ArgumentException>(() => new DdsTypeNameAttribute(""));
    }

    [Fact]
    public void DdsTypeNameAttribute_AcceptsValidName()
    {
        var attr = new DdsTypeNameAttribute("module::struct");
        Assert.Equal("module::struct", attr.Name);
    }

    [Fact]
    public void DdsIdAttribute_StoresId()
    {
        var attr = new DdsIdAttribute(42);
        Assert.Equal(42, attr.Id);
    }

    [Fact]
    public void DdsCaseAttribute_StoresValue()
    {
        var attr = new DdsCaseAttribute(123);
        Assert.Equal(123, attr.Value);
    }

    [Fact]
    public void DdsCaseAttribute_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DdsCaseAttribute(null!));
    }

    // Reflection Tests
    [DdsTopic("TestTopic")]
    [DdsQos(Reliability = DdsReliability.BestEffort)]
    [DdsUnion]
    private class TestClass
    {
        [DdsKey]
        [DdsBound(5)]
        [DdsId(1)]
        [DdsOptional]
        public string? Field1;

        [DdsDiscriminator]
        public int Kind;

        [DdsCase(1)]
        public int Case1;

        [DdsDefaultCase]
        public int Default;
    }

    [Fact]
    public void CanRetrieveAttributes_FromClass()
    {
        var type = typeof(TestClass);
        var topic = (DdsTopicAttribute)Attribute.GetCustomAttribute(type, typeof(DdsTopicAttribute))!;
        Assert.NotNull(topic);
        Assert.Equal("TestTopic", topic.TopicName);

        var qos = (DdsQosAttribute)Attribute.GetCustomAttribute(type, typeof(DdsQosAttribute))!;
        Assert.NotNull(qos);
        Assert.Equal(DdsReliability.BestEffort, qos.Reliability); // Changed in attribute
        Assert.Equal(DdsDurability.Volatile, qos.Durability); // Default

        Assert.NotNull(Attribute.GetCustomAttribute(type, typeof(DdsUnionAttribute)));
    }

    [Fact]
    public void CanRetrieveAttributes_FromField()
    {
        var field = typeof(TestClass).GetField("Field1")!;
        
        Assert.NotNull(Attribute.GetCustomAttribute(field, typeof(DdsKeyAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(field, typeof(DdsOptionalAttribute)));
        
        var bound = (DdsBoundAttribute)Attribute.GetCustomAttribute(field, typeof(DdsBoundAttribute))!;
        Assert.Equal(5, bound.Max);

        var id = (DdsIdAttribute)Attribute.GetCustomAttribute(field, typeof(DdsIdAttribute))!;
        Assert.Equal(1, id.Id);
    }

    [Fact]
    public void CanRetrieveAttributes_UnionSpecific()
    {
        var kind = typeof(TestClass).GetField("Kind")!;
        Assert.NotNull(Attribute.GetCustomAttribute(kind, typeof(DdsDiscriminatorAttribute)));

        var case1 = typeof(TestClass).GetField("Case1")!;
        var caseAttr = (DdsCaseAttribute)Attribute.GetCustomAttribute(case1, typeof(DdsCaseAttribute))!;
        Assert.Equal(1, caseAttr.Value);

        var def = typeof(TestClass).GetField("Default")!;
        Assert.NotNull(Attribute.GetCustomAttribute(def, typeof(DdsDefaultCaseAttribute)));
    }

    [Fact]
    public void TypesMarkedSealed()
    {
        Assert.True(typeof(DdsTopicAttribute).IsSealed);
        Assert.True(typeof(DdsQosAttribute).IsSealed);
        Assert.True(typeof(DdsBoundAttribute).IsSealed);
    }

    [Fact]
    public void MultipleAttributesNotAllowed()
    {
        // Check Usage attribute
        var attr = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(DdsTopicAttribute), typeof(AttributeUsageAttribute))!;
        Assert.False(attr.AllowMultiple);
    }
}
