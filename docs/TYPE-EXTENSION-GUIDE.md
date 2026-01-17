# Type Extension Guide

## Overview

This document explains how to add support for custom types to the code generator.

## Current Type Support

See BATCH-12.1 instructions for full list.

## Adding a New Simple Type (e.g., Guid)

### Step 1: Add to TypeMapper

File: `tools/CycloneDDS.CodeGen/TypeMapper.cs`

```csharp
public static string GetWriterMethod(string typeName)
{
    return typeName switch
    {
        // ... existing types ...
        "Guid" or "System.Guid" => "WriteGuid",  // ADD THIS
        _ => null
    };
}
```

### Step 2: Add CdrWriter Support

File: `src/CycloneDDS.Core/CdrWriter.cs`

```csharp
public void WriteGuid(Guid value)
{
    // Guid is 16 bytes (128 bits)
    // RFC 4122 format: time_low(4) + time_mid(2) + time_hi_version(2) + 
    //                 clock_seq(2) + node(6) = 16 bytes
    
    Span<byte> bytes = stackalloc byte[16];
    value.TryWriteBytes(bytes);
    WriteBytes(bytes);  // Or write as 2 ulongs for alignment
}
```

### Step 3: Add CdrReader Support

File: `src/CycloneDDS.Core/CdrReader.cs`

```csharp
public Guid ReadGuid()
{
    Span<byte> bytes = stackalloc byte[16];
    ReadBytes(bytes);
    return new Guid(bytes);
}
```

### Step 4: Add Tests

File: `tests/CycloneDDS.Core.Tests/CdrWriterTests.cs`

```csharp
[Fact]
public void WriteGuid_RoundTrip()
{
    var guid = Guid.NewGuid();
    var buffer = new ArrayBufferWriter<byte>();
    var writer = new CdrWriter(buffer);
    
    writer.WriteGuid(guid);
    writer.Complete();
    
    var reader = new CdrReader(buffer.WrittenSpan);
    var result = reader.ReadGuid();
    
    Assert.Equal(guid, result);
}
```

## Adding a Complex Type (e.g., Quaternion)

### Step 1: Decide on Wire Format

Quaternion = 4 floats (X, Y, Z, W) = 16 bytes

Map to existing struct:
```csharp
// Quaternion serializes as struct { float X; float Y; float Z; float W; }
```

### Step 2: Add Type Mapper Entry

```csharp
// In SerializerEmitter.cs - EmitFieldWrite:

if (field.TypeName == "Quaternion" || field.TypeName.EndsWith(".Quaternion"))
{
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.X);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.Y);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.Z);");
    sb.AppendLine($"            writer.WriteFloat(value.{field.Name}.W);");
    return;
}
```

### Step 3: Add Deserializer Support

```csharp
// In DeserializerEmitter.cs - EmitFieldRead:

if (field.TypeName == "Quaternion" || field.TypeName.EndsWith(".Quaternion"))
{
    sb.AppendLine($"            result.{field.Name} = new Quaternion(");
    sb.AppendLine($"                reader.ReadFloat(),  // X");
    sb.AppendLine($"                reader.ReadFloat(),  // Y");
    sb.AppendLine($"                reader.ReadFloat(),  // Z");
    sb.AppendLine($"                reader.ReadFloat()   // W");
    sb.AppendLine($"            );");
    return;
}
```

### Step 4: Add Tests

```csharp
[Fact]
public void Quaternion_RoundTrip()
{
    var type = new TypeInfo
    {
        Name = "QuaternionData",
        Namespace = "Math3D",
        Fields = new List<FieldInfo>
        {
            new FieldInfo { Name = "Rotation", TypeName = "System.Numerics.Quaternion" }
        }
    };
    
    // ... generate code, compile, test roundtrip ...
}
```

## Adding Array Support (T[])

Arrays require:
1. Decide: Fixed-size or dynamic-size
2. If dynamic: Serialize length + elements (like List<T>)
3. If fixed: Use attribute `[DdsArray(length)]`

See `BoundedSeq<T>` pattern for reference.

## Future Considerations

- **Custom Serializers:** Attribute-based custom serialization
- **Type Converters:** Auto-convert between wire format and C# type
- **Span<T> Support:** Zero-copy deserialization for value types

---

**For Questions:** Ask in `.dev-workstream/questions/`
