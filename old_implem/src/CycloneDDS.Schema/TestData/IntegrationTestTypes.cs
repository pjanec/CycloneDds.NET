using System;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.TestData;

// Simple primitive type
[DdsTopic("SimpleMessage")]
public partial class SimpleMessage
{
    [DdsKey] public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Value { get; set; }
}

// All basic types
[DdsTopic("AllPrimitivesMessage")]
public partial class AllPrimitivesMessage
{
    [DdsKey] public int Id { get; set; }
    public bool BoolField { get; set; }
    public byte ByteField { get; set; }
    public short Int16Field { get; set; }
    public int Int32Field { get; set; }
    public long Int64Field { get; set; }
    public float FloatField { get; set; }
    public double DoubleField { get; set; }
    public string StringField { get; set; } = "";
}

// Fixed array
[DdsTopic("ArrayMessage")]
public partial class ArrayMessage
{
    [DdsKey] public int Id { get; set; }
    
    [ArrayLength(5)]
    public int[] FixedIntArray { get; set; } = new int[5];
    
    [ArrayLength(3)]
    public double[] FixedDoubleArray { get; set; } = new double[3];
}

// Bounded sequence
[DdsTopic("SequenceMessage")]
public partial class SequenceMessage
{
    [DdsKey] public int Id { get; set; }
    
    [MaxLength(100)]
    public int[] BoundedIntSeq { get; set; } = Array.Empty<int>();
    
    [MaxLength(50)]
    public string[] BoundedStringSeq { get; set; } = Array.Empty<string>();
}

// Nested struct
[DdsTopic("NestedMessage")]
public partial class NestedMessage
{
    [DdsKey] public int Id { get; set; }
    public SimpleMessage Inner { get; set; } = new();
    public string Description { get; set; } = "";
}

// Array of structs
[DdsTopic("StructArrayMessage")]
public partial class StructArrayMessage
{
    [DdsKey] public int Id { get; set; }
    
    [ArrayLength(3)]
    public SimpleMessage[] MessageArray { get; set; } = new SimpleMessage[3];
}

// Complex - kitchen sink
[DdsTopic("ComplexMessage")]
public partial class ComplexMessage
{
    [DdsKey] public int Id { get; set; }
    public string Name { get; set; } = "";
    
    public SimpleMessage NestedStruct { get; set; } = new();
    
    [ArrayLength(3)]
    public SimpleMessage[] StructArray { get; set; } = new SimpleMessage[3];
    
    [MaxLength(10)]
    public string[] StringSeq { get; set; } = Array.Empty<string>();
    
    [ArrayLength(5)]
    public int[] IntArray { get; set; } = new int[5];
}

// Keyed topic (multiple instances)
[DdsTopic("SensorData")]
public partial class SensorData
{
    [DdsKey] public int SensorId { get; set; }
    [DdsKey] public int DeviceId { get; set; }  // Composite key
    
    public long Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double Humidity { get; set; }
}

// Empty message (edge case)
[DdsTopic("EmptyMessage")]
public partial class EmptyMessage
{
    // No fields - tests edge case
    // Adding dummy field if IDL requires at least one struct member (standard DDS usually requires this)
    public byte Dummy { get; set; }
}
