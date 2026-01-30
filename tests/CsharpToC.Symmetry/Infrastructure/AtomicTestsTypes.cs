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
        public int Id;
        public bool Value;
    }

    [DdsTopic("CharTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct CharTopic
    {
        [DdsKey]
        public int Id;
        public byte Value;
    }

    [DdsTopic("OctetTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OctetTopic
    {
        [DdsKey]
        public int Id;
        public byte Value;
    }

    [DdsTopic("Int16Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int16Topic
    {
        [DdsKey]
        public int Id;
        public short Value;
    }

    [DdsTopic("UInt16Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt16Topic
    {
        [DdsKey]
        public int Id;
        public ushort Value;
    }

    [DdsTopic("Int32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int32Topic
    {
        [DdsKey]
        public int Id;
        public int Value;
    }

    [DdsTopic("UInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt32Topic
    {
        [DdsKey]
        public int Id;
        public uint Value;
    }

    [DdsTopic("Int64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Int64Topic
    {
        [DdsKey]
        public int Id;
        public long Value;
    }

    [DdsTopic("UInt64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UInt64Topic
    {
        [DdsKey]
        public int Id;
        public ulong Value;
    }

    [DdsTopic("Float32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Float32Topic
    {
        [DdsKey]
        public int Id;
        public float Value;
    }

    [DdsTopic("Float64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Float64Topic
    {
        [DdsKey]
        public int Id;
        public double Value;
    }

    [DdsTopic("StringUnboundedTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringUnboundedTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public string Value;
    }

    [DdsTopic("StringBounded32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringBounded32Topic
    {
        [DdsKey]
        public int Id;
        [MaxLength(32)]
        [DdsManaged]
        public string Value;
    }

    [DdsTopic("StringBounded256Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct StringBounded256Topic
    {
        [DdsKey]
        public int Id;
        [MaxLength(256)]
        [DdsManaged]
        public string Value;
    }

    public enum SimpleEnum
    {
        FIRST,
        SECOND,
        THIRD,
    }

    [DdsTopic("EnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct EnumTopic
    {
        [DdsKey]
        public int Id;
        public SimpleEnum Value;
    }

    public enum ColorEnum
    {
        RED,
        GREEN,
        BLUE,
        YELLOW,
        MAGENTA,
        CYAN,
    }

    [DdsTopic("ColorEnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ColorEnumTopic
    {
        [DdsKey]
        public int Id;
        public ColorEnum Color;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Point2D
    {
        public double X;
        public double Y;
    }

    [DdsTopic("NestedStructTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct NestedStructTopic
    {
        [DdsKey]
        public int Id;
        public Point2D Point;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Point3D
    {
        public double X;
        public double Y;
        public double Z;
    }

    [DdsTopic("Nested3DTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Nested3DTopic
    {
        [DdsKey]
        public int Id;
        public Point3D Point;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Box
    {
        public Point2D TopLeft;
        public Point2D BottomRight;
    }

    [DdsTopic("DoublyNestedTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct DoublyNestedTopic
    {
        [DdsKey]
        public int Id;
        public Box Box;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Container
    {
        public int Count;
        public Point3D Center;
        public double Radius;
    }

    [DdsTopic("ComplexNestedTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ComplexNestedTopic
    {
        [DdsKey]
        public int Id;
        public Container Container;
    }

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SimpleUnion
    {
        [DdsDiscriminator]
        public int _d;
        [DdsCase(1)]
        public int Int_value;
        [DdsCase(2)]
        public double Double_value;
        [DdsCase(3)]
        [DdsManaged]
        public string String_value;
    }

    [DdsTopic("UnionLongDiscTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionLongDiscTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public SimpleUnion Data;
    }

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct BoolUnion
    {
        [DdsDiscriminator]
        public bool _d;
        [DdsCase(true)]
        public int True_val;
        [DdsCase(false)]
        public double False_val;
    }

    [DdsTopic("UnionBoolDiscTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionBoolDiscTopic
    {
        [DdsKey]
        public int Id;
        public BoolUnion Data;
    }

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ColorUnion
    {
        [DdsDiscriminator]
        public ColorEnum _d;
        [DdsCase(ColorEnum.RED)]
        public int Red_data;
        [DdsCase(ColorEnum.GREEN)]
        public double Green_data;
        [DdsCase(ColorEnum.BLUE)]
        [DdsManaged]
        public string Blue_data;
        [DdsCase(ColorEnum.YELLOW)]
        public Point2D Yellow_point;
    }

    [DdsTopic("UnionEnumDiscTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionEnumDiscTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public ColorUnion Data;
    }

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ShortUnion
    {
        [DdsDiscriminator]
        public short _d;
        [DdsCase(1)]
        public byte Byte_val;
        [DdsCase(2)]
        public short Short_val;
        [DdsCase(3)]
        public int Long_val;
        [DdsCase(4)]
        public float Float_val;
    }

    [DdsTopic("UnionShortDiscTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionShortDiscTopic
    {
        [DdsKey]
        public int Id;
        public ShortUnion Data;
    }

    [DdsTopic("OptionalInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OptionalInt32Topic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public int Opt_value;
    }

    [DdsTopic("OptionalFloat64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OptionalFloat64Topic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public double Opt_value;
    }

    [DdsTopic("OptionalStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OptionalStringTopic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        [MaxLength(64)]
        [DdsManaged]
        public string Opt_string;
    }

    [DdsTopic("OptionalStructTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OptionalStructTopic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public Point2D Opt_point;
    }

    [DdsTopic("OptionalEnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct OptionalEnumTopic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public SimpleEnum Opt_enum;
    }

    [DdsTopic("MultiOptionalTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct MultiOptionalTopic
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public int Opt_int;
        [DdsOptional]
        public double Opt_double;
        [DdsOptional]
        [MaxLength(32)]
        [DdsManaged]
        public string Opt_string;
    }

    [DdsTopic("SequenceInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceInt32Topic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<int> Values;
    }

    [DdsTopic("BoundedSequenceInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct BoundedSequenceInt32Topic
    {
        [DdsKey]
        public int Id;
        [MaxLength(10)]
        [DdsManaged]
        public List<int> Values;
    }

    [DdsTopic("SequenceInt64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceInt64Topic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<long> Values;
    }

    [DdsTopic("SequenceFloat32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceFloat32Topic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<float> Values;
    }

    [DdsTopic("SequenceFloat64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceFloat64Topic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<double> Values;
    }

    [DdsTopic("SequenceBooleanTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceBooleanTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<bool> Values;
    }

    [DdsTopic("SequenceOctetTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceOctetTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<byte> Bytes;
    }

    [DdsTopic("SequenceStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceStringTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<string> Values;
    }

    [DdsTopic("SequenceEnumTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceEnumTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<SimpleEnum> Values;
    }

    [DdsTopic("SequenceStructTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceStructTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<Point2D> Points;
    }

    [DdsTopic("SequenceUnionTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct SequenceUnionTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<SimpleUnion> Unions;
    }

    [DdsTopic("ArrayInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayInt32Topic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(5)]
        [DdsManaged]
        public int[] Values;
    }

    [DdsTopic("ArrayFloat64Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayFloat64Topic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(5)]
        [DdsManaged]
        public double[] Values;
    }

    [DdsTopic("ArrayStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayStringTopic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(5)]
        [DdsManaged]
        [MaxLength(16)]
        public string[] Names;
    }

    [DdsTopic("Array2DInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Array2DInt32Topic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(12)]
        [DdsManaged]
        public int[] Matrix;
    }

    [DdsTopic("Array3DInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Array3DInt32Topic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(24)]
        [DdsManaged]
        public int[] Cube;
    }

    [DdsTopic("ArrayStructTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ArrayStructTopic
    {
        [DdsKey]
        public int Id;
        [ArrayLength(3)]
        [DdsManaged]
        public Point2D[] Points;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("AppendableInt32Topic")]
    public partial struct AppendableInt32Topic
    {
        [DdsKey]
        public int Id;
        public int Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("AppendableStructTopic")]
    public partial struct AppendableStructTopic
    {
        [DdsKey]
        public int Id;
        public Point2D Point;
    }

    [DdsExtensibility(DdsExtensibilityKind.Final)]
    [DdsTopic("FinalInt32Topic")]
    public partial struct FinalInt32Topic
    {
        [DdsKey]
        public int Id;
        public int Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Final)]
    [DdsTopic("FinalStructTopic")]
    public partial struct FinalStructTopic
    {
        [DdsKey]
        public int Id;
        public Point2D Point;
    }

    [DdsExtensibility(DdsExtensibilityKind.Mutable)]
    [DdsTopic("MutableInt32Topic")]
    public partial struct MutableInt32Topic
    {
        [DdsKey]
        public int Id;
        [DdsId(100)]
        public int Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Mutable)]
    [DdsTopic("MutableStructTopic")]
    public partial struct MutableStructTopic
    {
        [DdsKey]
        public int Id;
        [DdsId(200)]
        public Point2D Point;
    }

    [DdsTopic("TwoKeyInt32Topic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct TwoKeyInt32Topic
    {
        [DdsKey]
        public int Key1;
        [DdsKey]
        public int Key2;
        public double Value;
    }

    [DdsTopic("TwoKeyStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct TwoKeyStringTopic
    {
        [DdsKey]
        [MaxLength(32)]
        [DdsManaged]
        public string Key1;
        [DdsKey]
        [MaxLength(32)]
        [DdsManaged]
        public string Key2;
        public double Value;
    }

    [DdsTopic("ThreeKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct ThreeKeyTopic
    {
        [DdsKey]
        public int Key1;
        [DdsKey]
        [MaxLength(32)]
        [DdsManaged]
        public string Key2;
        [DdsKey]
        public short Key3;
        public double Value;
    }

    [DdsTopic("FourKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct FourKeyTopic
    {
        [DdsKey]
        public int Key1;
        [DdsKey]
        public int Key2;
        [DdsKey]
        public int Key3;
        [DdsKey]
        public int Key4;
        [MaxLength(64)]
        [DdsManaged]
        public string Description;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Location
    {
        [DdsKey]
        public int Building;
        [DdsKey]
        public short Floor;
    }

    [DdsTopic("NestedKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct NestedKeyTopic
    {
        [DdsKey]
        public Location Loc;
        public double Temperature;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Coordinates
    {
        [DdsKey]
        public double Latitude;
        [DdsKey]
        public double Longitude;
    }

    [DdsTopic("NestedKeyGeoTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct NestedKeyGeoTopic
    {
        [DdsKey]
        public Coordinates Coords;
        [MaxLength(128)]
        [DdsManaged]
        public string Location_name;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct TripleKey
    {
        [DdsKey]
        public int Id1;
        [DdsKey]
        public int Id2;
        [DdsKey]
        public int Id3;
    }

    [DdsTopic("NestedTripleKeyTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct NestedTripleKeyTopic
    {
        [DdsKey]
        public TripleKey Keys;
        [MaxLength(64)]
        [DdsManaged]
        public string Data;
    }

    [DdsTopic("EmptySequenceTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct EmptySequenceTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<int> Empty_seq;
    }

    [DdsTopic("LargeSequenceTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct LargeSequenceTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<int> Large_seq;
    }

    [DdsTopic("LongStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct LongStringTopic
    {
        [DdsKey]
        public int Id;
        [MaxLength(4096)]
        [DdsManaged]
        public string Long_string;
    }

    [DdsTopic("UnboundedStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnboundedStringTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public string Unbounded;
    }

    [DdsTopic("AllPrimitivesAtomicTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct AllPrimitivesAtomicTopic
    {
        [DdsKey]
        public int Id;
        public bool Bool_val;
        public byte Char_val;
        public byte Octet_val;
        public short Short_val;
        public ushort Ushort_val;
        public int Long_val;
        public uint Ulong_val;
        public int Llong_val;
        public ulong Ullong_val;
        public float Float_val;
        public double Double_val;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("BooleanTopicAppendable")]
    public partial struct BooleanTopicAppendable
    {
        [DdsKey]
        public int Id;
        public bool Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Int32TopicAppendable")]
    public partial struct Int32TopicAppendable
    {
        [DdsKey]
        public int Id;
        public int Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("StringBounded32TopicAppendable")]
    public partial struct StringBounded32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [MaxLength(32)]
        [DdsManaged]
        public string Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("SequenceInt32TopicAppendable")]
    public partial struct SequenceInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<int> Values;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsUnion]
    public partial struct SimpleUnionAppendable
    {
        [DdsDiscriminator]
        public int _d;
        [DdsCase(1)]
        public int Int_value;
        [DdsCase(2)]
        public double Double_value;
        [DdsCase(3)]
        [DdsManaged]
        public string String_value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("UnionLongDiscTopicAppendable")]
    public partial struct UnionLongDiscTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public SimpleUnionAppendable Data;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("CharTopicAppendable")]
    public partial struct CharTopicAppendable
    {
        [DdsKey]
        public int Id;
        public byte Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("OctetTopicAppendable")]
    public partial struct OctetTopicAppendable
    {
        [DdsKey]
        public int Id;
        public byte Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Int16TopicAppendable")]
    public partial struct Int16TopicAppendable
    {
        [DdsKey]
        public int Id;
        public short Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("UInt16TopicAppendable")]
    public partial struct UInt16TopicAppendable
    {
        [DdsKey]
        public int Id;
        public ushort Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("UInt32TopicAppendable")]
    public partial struct UInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        public uint Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Int64TopicAppendable")]
    public partial struct Int64TopicAppendable
    {
        [DdsKey]
        public int Id;
        public long Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("UInt64TopicAppendable")]
    public partial struct UInt64TopicAppendable
    {
        [DdsKey]
        public int Id;
        public ulong Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Float32TopicAppendable")]
    public partial struct Float32TopicAppendable
    {
        [DdsKey]
        public int Id;
        public float Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Float64TopicAppendable")]
    public partial struct Float64TopicAppendable
    {
        [DdsKey]
        public int Id;
        public double Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("StringUnboundedTopicAppendable")]
    public partial struct StringUnboundedTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public string Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("StringBounded256TopicAppendable")]
    public partial struct StringBounded256TopicAppendable
    {
        [DdsKey]
        public int Id;
        [MaxLength(256)]
        [DdsManaged]
        public string Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("EnumTopicAppendable")]
    public partial struct EnumTopicAppendable
    {
        [DdsKey]
        public int Id;
        public SimpleEnum Value;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("ColorEnumTopicAppendable")]
    public partial struct ColorEnumTopicAppendable
    {
        [DdsKey]
        public int Id;
        public ColorEnum Color;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("ArrayInt32TopicAppendable")]
    public partial struct ArrayInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [ArrayLength(5)]
        [DdsManaged]
        public int[] Values;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("ArrayFloat64TopicAppendable")]
    public partial struct ArrayFloat64TopicAppendable
    {
        [DdsKey]
        public int Id;
        public int DummyPad;
        [ArrayLength(5)]
        [DdsManaged]
        public double[] Values;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("ArrayStringTopicAppendable")]
    public partial struct ArrayStringTopicAppendable
    {
        [DdsKey]
        public int Id;
        [ArrayLength(5)]
        [DdsManaged]
        [MaxLength(16)]
        public string[] Names;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Array2DInt32TopicAppendable")]
    public partial struct Array2DInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [ArrayLength(12)]
        [DdsManaged]
        public int[] Matrix;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Array3DInt32TopicAppendable")]
    public partial struct Array3DInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [ArrayLength(24)]
        [DdsManaged]
        public int[] Cube;
    }

    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("ArrayStructTopicAppendable")]
    public partial struct ArrayStructTopicAppendable
    {
        [DdsKey]
        public int Id;
        [ArrayLength(3)]
        [DdsManaged]
        public Point2D[] Points;
    }

    [DdsTopic("SequenceUnionAppendableTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceUnionAppendableTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<SimpleUnionAppendable> Unions;
    }

    [DdsTopic("SequenceEnumAppendableTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceEnumAppendableTopic
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<ColorEnum> Colors;
    }

}