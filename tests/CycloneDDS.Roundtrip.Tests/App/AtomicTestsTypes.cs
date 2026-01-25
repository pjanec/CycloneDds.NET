using System;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace AtomicTests
{
    [DdsTopic("BooleanTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct BooleanTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public bool Value { get; set; }
    }

    [DdsTopic("Int32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public int Value { get; set; }
    }

    [DdsTopic("CharTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct CharTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public byte Value { get; set; }
    }

    [DdsTopic("OctetTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OctetTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public byte Value { get; set; }
    }
    
    [DdsTopic("Int16Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int16Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public short Value { get; set; }
    }

    [DdsTopic("UInt16Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt16Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public ushort Value { get; set; }
    }

    [DdsTopic("UInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public uint Value { get; set; }
    }

    [DdsTopic("Int64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int64Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public long Value { get; set; }
    }

    [DdsTopic("UInt64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt64Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public ulong Value { get; set; }
    }

    [DdsTopic("Float32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Float32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public float Value { get; set; }
    }

    [DdsTopic("Float64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Float64Topic
    {
        [DdsKey]
        public int Id { get; set; }
        public double Value { get; set; }
    }

    [DdsTopic("StringBounded32Topic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringBounded32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        
        [MaxLength(32)]
        public string Value { get; set; }
    }

    [DdsTopic("StringUnboundedTopic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringUnboundedTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public string Value { get; set; }
    }

    [DdsTopic("StringBounded256Topic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringBounded256Topic
    {
        [DdsKey]
        public int Id { get; set; }
        
        [MaxLength(256)]
        public string Value { get; set; }
    }

    public enum SimpleEnum
    {
        FIRST, 
        SECOND, 
        THIRD 
    }

    [DdsTopic("EnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct EnumTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public SimpleEnum Value { get; set; }
    }

    public enum ColorEnum
    {
        RED, 
        GREEN, 
        BLUE, 
        YELLOW, 
        MAGENTA, 
        CYAN 
    }

    [DdsTopic("ColorEnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ColorEnumTopic
    {
        [DdsKey]
        public int Id { get; set; }
        public ColorEnum Color { get; set; }
    }

    [DdsTopic("ArrayInt32Topic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayInt32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(5)]
        public int[] Values;
    }
    
    [DdsTopic("ArrayFloat64Topic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayFloat64Topic
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(5)]
        public double[] Values;
    }

    [DdsTopic("ArrayStringTopic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayStringTopic
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(3)]
        [MaxLength(16)]
        public string[] Names;
    }

    [DdsTopic("SequenceInt32Topic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceInt32Topic
    {
        [DdsKey]
        public int Id { get; set; }
        
        // List is fine for managed
        public List<int> Values { get; set; }
    }

    // Union definition
    [DdsUnion]
    [DdsStruct] // Hack
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SimpleUnion
    {
        [DdsDiscriminator]
        public int _d { get; set; }

        [DdsCase(1)]
        public int Int_value { get; set; }

        [DdsCase(2)]
        public double Double_value { get; set; }

        [DdsCase(3)]
        public string String_value { get; set; }
    }

    [DdsTopic("UnionLongDiscTopic")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionLongDiscTopic
    {
        [DdsKey]
        public int Id { get; set; }
        
        public SimpleUnion Data { get; set; }
    }

    // ========================================================================
    // ROUNDTRIP SPECIFIC APPENDABLE DUPLICATES
    // ========================================================================

    [DdsTopic("BooleanTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct BooleanTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public bool Value { get; set; }
    }

    [DdsTopic("Int32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Int32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public int Value { get; set; }
    }

    [DdsTopic("CharTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct CharTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public byte Value { get; set; }
    }

    [DdsTopic("OctetTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OctetTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public byte Value { get; set; }
    }
    
    [DdsTopic("Int16TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Int16TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public short Value { get; set; }
    }

    [DdsTopic("UInt16TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UInt16TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public ushort Value { get; set; }
    }

    [DdsTopic("UInt32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UInt32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public uint Value { get; set; }
    }

    [DdsTopic("Int64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Int64TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public long Value { get; set; }
    }

    [DdsTopic("UInt64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UInt64TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public ulong Value { get; set; }
    }

    [DdsTopic("Float32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Float32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public float Value { get; set; }
    }

    [DdsTopic("Float64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Float64TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public double Value { get; set; }
    }

    [DdsTopic("StringBounded32TopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct StringBounded32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        [MaxLength(32)]
        public string Value { get; set; }
    }

    [DdsTopic("StringUnboundedTopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct StringUnboundedTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public string Value { get; set; }
    }

    [DdsTopic("StringBounded256TopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct StringBounded256TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        [MaxLength(256)]
        public string Value { get; set; }
    }

    [DdsTopic("EnumTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct EnumTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public SimpleEnum Value { get; set; }
    }

    [DdsTopic("ColorEnumTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ColorEnumTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        public ColorEnum Color { get; set; }
    }

    [DdsTopic("ArrayInt32TopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ArrayInt32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(5)]
        public int[] Values;
    }

    [DdsTopic("ArrayFloat64TopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ArrayFloat64TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(5)]
        public double[] Values;
    }

    [DdsTopic("ArrayStringTopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ArrayStringTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        [ArrayLength(3)]
        [MaxLength(16)]
        public string[] Names;
    }

    [DdsTopic("SequenceInt32TopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceInt32TopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        public List<int> Values { get; set; }
    }

    [DdsUnion]
    [DdsStruct]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SimpleUnionAppendable
    {
        [DdsDiscriminator]
        public int _d { get; set; }

        [DdsCase(1)]
        public int Int_value { get; set; }

        [DdsCase(2)]
        public double Double_value { get; set; }

        [DdsCase(3)]
        public string String_value { get; set; }
    }

    [DdsTopic("UnionLongDiscTopicAppendable")]
    [DdsManaged]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionLongDiscTopicAppendable
    {
        [DdsKey]
        public int Id { get; set; }
        
        public SimpleUnionAppendable Data { get; set; }
    }
}
