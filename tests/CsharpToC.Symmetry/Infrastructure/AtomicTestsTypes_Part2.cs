using System;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace AtomicTests
{
    // ========================================================================
    // PART 2: APPENDABLE DUPLICATES AND EDGE CASES
    // ========================================================================

    // --- Sequences ---

    [DdsTopic("BoundedSequenceInt32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct BoundedSequenceInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [MaxLength(10)]
        [DdsManaged]
        public List<int> Values;
    }

    [DdsTopic("SequenceInt64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceInt64TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<long> Values;
    }

    [DdsTopic("SequenceFloat32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceFloat32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<float> Values;
    }

    [DdsTopic("SequenceFloat64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceFloat64TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<double> Values;
    }

    [DdsTopic("SequenceBooleanTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceBooleanTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<bool> Values;
    }

    [DdsTopic("SequenceOctetTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceOctetTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<byte> Bytes;
    }

    [DdsTopic("SequenceStringTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceStringTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<string> Values;
    }

    [DdsTopic("SequenceStructTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct SequenceStructTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<Point2D> Points;
    }

    // --- Nested ---

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Point2DAppendable
    {
        public double X;
        public double Y;
    }

    [DdsTopic("NestedStructTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct NestedStructTopicAppendable
    {
        [DdsKey]
        public int Id;
        public Point2DAppendable Point;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Point3DAppendable
    {
        public double X;
        public double Y;
        public double Z;
    }

    [DdsTopic("Nested3DTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Nested3DTopicAppendable
    {
        [DdsKey]
        public int Id;
        public Point3DAppendable Point;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct BoxAppendable
    {
        public Point2DAppendable TopLeft;
        public Point2DAppendable BottomRight;
    }

    [DdsTopic("DoublyNestedTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct DoublyNestedTopicAppendable
    {
        [DdsKey]
        public int Id;
        public BoxAppendable Box;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ContainerAppendable
    {
        public int Count;
        public Point3DAppendable Center;
        public double Radius;
    }

    [DdsTopic("ComplexNestedTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ComplexNestedTopicAppendable
    {
        [DdsKey]
        public int Id;
        public ContainerAppendable Container;
    }

    // --- Unions ---
    // SimpleUnionAppendable etc are already in AtomicTestsTypes.cs

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct BoolUnionAppendable
    {
        [DdsDiscriminator]
        public bool _d;

        [DdsCase(true)]
        public int True_val;

        [DdsCase(false)]
        public double False_val;
    }

    [DdsTopic("UnionBoolDiscTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionBoolDiscTopicAppendable
    {
        [DdsKey]
        public int Id;
        public BoolUnionAppendable Data;
    }

    [DdsUnion] 
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ColorUnionAppendable
    {
        [DdsDiscriminator]
        public ColorEnum _d;

        [DdsCase((int)ColorEnum.RED)]
        public int Red_data;

        [DdsCase((int)ColorEnum.GREEN)]
        public double Green_data;

        [DdsCase((int)ColorEnum.BLUE)]
        [MaxLength(32)]
        [DdsManaged]
        public string Blue_data;
        
        [DdsCase((int)ColorEnum.YELLOW)]
        public Point2DAppendable Yellow_point;
    }

    [DdsTopic("UnionEnumDiscTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionEnumDiscTopicAppendable
    {
        [DdsKey]
        public int Id;
        public ColorUnionAppendable Data;
    }

    [DdsUnion] 
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ShortUnionAppendable
    {
        [DdsDiscriminator]
        public short _d;

        [DdsCase((short)1)]
        public byte Byte_val;

        [DdsCase((short)2)]
        public short Short_val;

        [DdsCase((short)3)]
        public int Long_val;

        [DdsCase((short)4)]
        public float Float_val;
    }

    [DdsTopic("UnionShortDiscTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionShortDiscTopicAppendable
    {
        [DdsKey]
        public int Id;
        public ShortUnionAppendable Data;
    }

    // --- Optionals ---

    [DdsTopic("OptionalInt32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OptionalInt32TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public int Opt_value;
    }

    [DdsTopic("OptionalFloat64TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OptionalFloat64TopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public double Opt_value;
    }

    [DdsTopic("OptionalStringTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OptionalStringTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        [MaxLength(64)]
        [DdsManaged]
        public string Opt_string;
    }

    [DdsTopic("OptionalStructTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OptionalStructTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public Point2DAppendable Opt_point;
    }

    [DdsTopic("OptionalEnumTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct OptionalEnumTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsOptional]
        public SimpleEnum Opt_enum;
    }

    [DdsTopic("MultiOptionalTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct MultiOptionalTopicAppendable
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

    // --- Composite Keys ---

    [DdsTopic("TwoKeyInt32TopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct TwoKeyInt32TopicAppendable
    {
        [DdsKey]
        public int Key1;
        [DdsKey]
        public int Key2;
        public double Value;
    }

    [DdsTopic("TwoKeyStringTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct TwoKeyStringTopicAppendable
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

    [DdsTopic("ThreeKeyTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct ThreeKeyTopicAppendable
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

    [DdsTopic("FourKeyTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct FourKeyTopicAppendable
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

    // --- Nested Keys ---

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct LocationAppendable
    {
        [DdsKey]
        public int Building;
        [DdsKey]
        public short Floor;
    }

    [DdsTopic("NestedKeyTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct NestedKeyTopicAppendable
    {
        [DdsKey]
        public LocationAppendable Loc;
        public double Temperature;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct CoordinatesAppendable
    {
        [DdsKey]
        public double Latitude;
        [DdsKey]
        public double Longitude;
    }

    [DdsTopic("NestedKeyGeoTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct NestedKeyGeoTopicAppendable
    {
        [DdsKey]
        public CoordinatesAppendable Coords;
        [MaxLength(128)]
        [DdsManaged]
        public string Location_name;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct TripleKeyAppendable
    {
        [DdsKey]
        public int Id1;
        [DdsKey]
        public int Id2;
        [DdsKey]
        public int Id3;
    }

    [DdsTopic("NestedTripleKeyTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct NestedTripleKeyTopicAppendable
    {
        [DdsKey]
        public TripleKeyAppendable Keys;
        [MaxLength(64)]
        [DdsManaged]
        public string Data;
    }

    // --- Edge Cases ---

    [DdsTopic("EmptySequenceTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct EmptySequenceTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public List<int> Empty_seq;
    }

    [DdsTopic("UnboundedStringTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnboundedStringTopicAppendable
    {
        [DdsKey]
        public int Id;
        [DdsManaged]
        public string Unbounded;
    }

    [DdsTopic("AllPrimitivesAtomicTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct AllPrimitivesAtomicTopicAppendable
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
        public long Llong_val;
        public ulong Ullong_val;
        public float Float_val;
        public double Double_val;
    }

    // --- NEW Edge Cases (Final + Appendable) ---

    [DdsTopic("MaxSizeStringTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct MaxSizeStringTopic
    {
        [DdsKey]
        public int Id;
        [MaxLength(8192)]
        [DdsManaged]
        public string Max_string;
    }

    [DdsTopic("MaxSizeStringTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct MaxSizeStringTopicAppendable
    {
        [DdsKey]
        public int Id;
        [MaxLength(8192)]
        [DdsManaged]
        public string Max_string;
    }

    [DdsTopic("MaxLengthSequenceTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct MaxLengthSequenceTopic
    {
        [DdsKey]
        public int Id;
        [MaxLength(10000)]
        [DdsManaged]
        public List<int> Max_seq;
    }

    [DdsTopic("MaxLengthSequenceTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct MaxLengthSequenceTopicAppendable
    {
        [DdsKey]
        public int Id;
        [MaxLength(10000)]
        [DdsManaged]
        public List<int> Max_seq;
    }
    
    // Deep Nested Structs

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Level5 { public int Value5; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Level4 { public int Value4; public Level5 Nested5; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Level3 { public int Value3; public Level4 Nested4; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Level2 { public int Value2; public Level3 Nested3; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct Level1 { public int Value1; public Level2 Nested2; }

    [DdsTopic("DeepNestedStructTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct DeepNestedStructTopic
    {
        [DdsKey]
        public int Id;
        public Level1 Nested1;
    }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Level5Appendable { public int Value5; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Level4Appendable { public int Value4; public Level5Appendable Nested5; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Level3Appendable { public int Value3; public Level4Appendable Nested4; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Level2Appendable { public int Value2; public Level3Appendable Nested3; }

    [DdsStruct]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct Level1Appendable { public int Value1; public Level2Appendable Nested2; }

    [DdsTopic("DeepNestedStructTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct DeepNestedStructTopicAppendable
    {
        [DdsKey]
        public int Id;
        public Level1Appendable Nested1;
    }

    // Union With Optional

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionWithOptional
    {
        [DdsDiscriminator] public int _d;

        [DdsCase(1)] public int Int_val;
        [DdsCase(2)] [MaxLength(64)] [DdsManaged] public string Opt_str_val; 
        [DdsCase(3)] public double Double_val;
    }

    [DdsTopic("UnionWithOptionalTopic")]
    [DdsExtensibility(DdsExtensibilityKind.Final)]
    public partial struct UnionWithOptionalTopic
    {
        [DdsKey]
        public int Id;
        public UnionWithOptional Data;
    }

    [DdsUnion]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionWithOptionalAppendable
    {
        [DdsDiscriminator] public int _d;

        [DdsCase(1)] public int Int_val;
        [DdsCase(2)] [MaxLength(64)] [DdsManaged] public string Opt_str_val;
        [DdsCase(3)] public double Double_val;
    }

    [DdsTopic("UnionWithOptionalTopicAppendable")]
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    public partial struct UnionWithOptionalTopicAppendable
    {
        [DdsKey]
        public int Id;
        public UnionWithOptionalAppendable Data;
    }
}
