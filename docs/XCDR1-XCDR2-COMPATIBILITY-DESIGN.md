# XCDR1/XCDR2 Compatibility Design

**Version:** 1.0  
**Date:** 2026-01-21  
**Status:** Approved for Implementation  
**Priority:** MEDIUM  
**Task ID:** FCDC-COMPAT-01

---

## 1. Executive Summary

**Problem:** The C# bindings currently use strict XCDR2 encoding (Appendable CDR2). This prevents interoperability with legacy DDS systems that only support XCDR1 (Classic CDR).

**Solution:** Implement a **stateful encoding context** system that allows runtime selection between XCDR1 and XCDR2 encoding, while maintaining XCDR2 as the default. The encoding decision propagates automatically through the entire serialization chain, preventing "mixed protocol" bugs.

**Impact:**
- ✅ Full backward compatibility with XCDR1-only systems
- ✅ Automatic format detection on read path (zero configuration)
- ✅ Clean architecture with minimal code complexity
- ✅ No performance degradation for XCDR2 hot path

**Estimated Effort:** 2-3 days

---

## 2. Background: XCDR1 vs XCDR2

### 2.1 Key Differences

| Feature | XCDR1 (Classic CDR) | XCDR2 (Extended CDR) |
|---------|---------------------|----------------------|
| **Strings** | NUL-terminated (`Length + 1`) | No NUL (`Length` only) |
| **DHEADER** | Not used (even for appendable types) | Used for appendable/mutable types |
| **Encapsulation Header** | `0x0000/0x0001` (BE/LE) | `0x0008/0x0009` (D_CDR2 BE/LE) |
| **Extensibility** | `@final` only (no schema evolution) | `@final`, `@appendable`, `@mutable` |
| **Alignment** | 4-byte (double/long) | 4-byte (same) |
| **Default in C/C++** | Legacy systems | Modern systems (XTypes) |

### 2.2 Wire Format Examples

**XCDR1 String `"Hello"`:**
```
Length: 0x00000006  (5 bytes + 1 NUL)
Payload: 0x48 0x65 0x6C 0x6C 0x6F 0x00
```

**XCDR2 String `"Hello"`:**
```
Length: 0x00000005  (5 bytes, no NUL)
Payload: 0x48 0x65 0x6C 0x6C 0x6F
```

**XCDR1 Appendable Struct:**
```
[Encapsulation Header: 0x0001 0x0000]
[Field1][Field2][Field3]
(NO DHEADER - degrades to Final behavior)
```

**XCDR2 Appendable Struct:**
```
[Encapsulation Header: 0x0009 0x0000]
[DHEADER: 0x0000000C (body length)]
[Field1][Field2][Field3]
```

---

## 3. Architecture: Stateful Encoding Context

### 3.1 Design Principle: "Context Propagation"

Instead of passing boolean flags (`isXcdr2`) to every method, we embed the encoding decision into the **serialization context** itself (`CdrWriter`, `CdrSizer`). This context flows down the call chain automatically.

**Benefits:**
- Generated code remains clean (no extra parameters)
- Impossible to create mixed-format blobs (context is immutable)
- Minimal changes to existing emitters

### 3.2 Encoding Enum

**File:** `Src/CycloneDDS.Core/CdrEncoding.cs` (NEW)

```csharp
namespace CycloneDDS.Core
{
    /// <summary>
    /// CDR encoding format selector.
    /// </summary>
    public enum CdrEncoding : byte
    {
        /// <summary>
        /// Legacy CDR (DDS v1.2).
        /// - Strings include NUL terminator.
        /// - No DHEADERs (even for Appendable types).
        /// - Encapsulation: 0x0000/0x0001 (BE/LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR1 (0).
        /// </summary>
        Xcdr1 = 0,

        /// <summary>
        /// Extended CDR (DDS X-Types 1.3).
        /// - Strings do NOT include NUL terminator.
        /// - DHEADERs used for Appendable/Mutable types.
        /// - Encapsulation: 0x0008/0x0009 (D_CDR2 BE/LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR2 (2).
        /// </summary>
        Xcdr2 = 2
    }
}
```

### 3.3 Stateful CdrWriter

**File:** `Src/CycloneDDS.Core/CdrWriter.cs`

```csharp
public ref struct CdrWriter
{
    private Span<byte> _span;
    private int _buffered;
    
    /// <summary>
    /// Active encoding format. Immutable after construction.
    /// </summary>
    public readonly CdrEncoding Encoding;

    public CdrWriter(Span<byte> buffer, CdrEncoding encoding = CdrEncoding.Xcdr2)
    {
        _span = buffer;
        _buffered = 0;
        Encoding = encoding;
    }

    public void WriteString(ReadOnlySpan<char> value)
    {
        int utf8Length = System.Text.Encoding.UTF8.GetByteCount(value);
        
        if (Encoding == CdrEncoding.Xcdr2)
        {
            // XCDR2: Length = ByteCount
            WriteInt32(utf8Length);
            EnsureSize(utf8Length);
            int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
            _buffered += written;
        }
        else // Xcdr1
        {
            // XCDR1: Length = ByteCount + 1 (NUL)
            WriteInt32(utf8Length + 1);
            EnsureSize(utf8Length + 1);
            int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
            _buffered += written;
            _span[_buffered] = 0; // NUL terminator
            _buffered += 1;
        }
    }
    
    // Other methods unchanged...
}
```

### 3.4 Stateful CdrSizer

**File:** `Src/CycloneDDS.Core/CdrSizer.cs`

```csharp
public ref struct CdrSizer
{
    private int _cursor;
    
    public readonly CdrEncoding Encoding;

    public CdrSizer(int initialOffset, CdrEncoding encoding = CdrEncoding.Xcdr2)
    {
        _cursor = initialOffset;
        Encoding = encoding;
    }

    public void WriteString(ReadOnlySpan<char> value)
    {
        _cursor += 4; // Length field
        _cursor += System.Text.Encoding.UTF8.GetByteCount(value);
        
        if (Encoding == CdrEncoding.Xcdr1)
        {
            _cursor += 1; // NUL terminator
        }
    }
    
    // Other methods unchanged...
}
```

### 3.5 Auto-Detecting CdrReader

**File:** `Src/CycloneDDS.Core/CdrReader.cs`

The reader **auto-detects** the format by inspecting the encapsulation header. No configuration needed.

```csharp
public ref struct CdrReader
{
    private ReadOnlySpan<byte> _span;
    private int _position;
    
    public readonly CdrEncoding Encoding;

    public CdrReader(ReadOnlySpan<byte> buffer)
    {
        _span = buffer;
        _position = 0;
        
        // Auto-detect encoding from header (byte 1)
        if (buffer.Length >= 4)
        {
            byte identifier = buffer[1];
            // 0x00-0x05: XCDR1
            // 0x06-0x0B: XCDR2
            Encoding = (identifier >= 6) ? CdrEncoding.Xcdr2 : CdrEncoding.Xcdr1;
        }
        else
        {
            Encoding = CdrEncoding.Xcdr2; // Default
        }
    }

    public string ReadString()
    {
        int length = ReadInt32();
        
        if (Encoding == CdrEncoding.Xcdr1)
        {
            // XCDR1: Length includes NUL terminator
            if (length <= 0) return string.Empty;
            
            var utf8Bytes = _span.Slice(_position, length - 1); // Exclude NUL
            _position += length; // Consume NUL too
            return System.Text.Encoding.UTF8.GetString(utf8Bytes);
        }
        else // Xcdr2
        {
            // XCDR2: Length is exact byte count
            if (length <= 0) return string.Empty;
            
            var utf8Bytes = _span.Slice(_position, length);
            _position += length;
            return System.Text.Encoding.UTF8.GetString(utf8Bytes);
        }
    }
    
    // Other methods unchanged...
}
```

---

## 4. Code Generation Updates

### 4.1 SerializerEmitter (DHEADER Conditional Logic)

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`

**Golden Rule:** DHEADER is written **only if**:
1. Type is `@appendable` (or `@mutable`)
2. **AND** `writer.Encoding == Xcdr2`

This handles the "Final struct containing Appendable struct" case automatically.

```csharp
private void EmitSerialize(StringBuilder sb, TypeInfo type)
{
    sb.AppendLine("        public void Serialize(ref CdrWriter writer)");
    sb.AppendLine("        {");

    bool isAppendableDef = type.Extensibility == DdsExtensibilityKind.Appendable;

    if (isAppendableDef)
    {
        // DYNAMIC CHECK: Only write DHEADER if writer is in XCDR2 mode
        sb.AppendLine("            int dheaderPos = 0;");
        sb.AppendLine("            int bodyStart = 0;");
        sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.Align(4);");
        sb.AppendLine("                dheaderPos = writer.Position;");
        sb.AppendLine("                writer.WriteUInt32(0); // Placeholder");
        sb.AppendLine("                bodyStart = writer.Position;");
        sb.AppendLine("            }");
    }

    foreach (var field in type.Fields)
    {
        // Generate field serialization
        // writer.WriteString() checks writer.Encoding internally
        // Nested.Serialize(ref writer) passes writer (and its Encoding) down
        sb.AppendLine($"            {GetWriterCall(field)};");
    }

    if (isAppendableDef)
    {
        // Patch DHEADER only if we wrote one
        sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                int bodyLen = writer.Position - bodyStart;");
        sb.AppendLine("                writer.PatchUInt32(dheaderPos, (uint)bodyLen);");
        sb.AppendLine("            }");
    }

    sb.AppendLine("        }");
}
```

### 4.2 SizerEmitter (Matching Logic)

**File:** `tools/CycloneDDS.CodeGen/SizerEmitter.cs`

Apply identical logic to `GetSerializedSize`:

```csharp
private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
{
    sb.AppendLine("        public int GetSerializedSize(int currentAlignment, bool isXcdr2)");
    sb.AppendLine("        {");
    sb.AppendLine("            var sizer = new CdrSizer(currentAlignment, isXcdr2 ? CdrEncoding.Xcdr2 : CdrEncoding.Xcdr1);");

    bool isAppendableDef = type.Extensibility == DdsExtensibilityKind.Appendable;

    if (isAppendableDef)
    {
        sb.AppendLine("            if (sizer.Encoding == CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                sizer.Align(4);");
        sb.AppendLine("                sizer.WriteUInt32(0); // DHEADER");
        sb.AppendLine("            }");
    }

    foreach (var field in type.Fields)
    {
        sb.AppendLine($"            {GetSizerCall(field)};");
    }

    sb.AppendLine("            return sizer.Size;");
    sb.AppendLine("        }");
}
```

### 4.3 DeserializerEmitter (Auto-Detection)

**File:** `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`

The deserializer uses `reader.Encoding` (auto-detected from header):

```csharp
private void EmitDeserialize(StringBuilder sb, TypeInfo type)
{
    sb.AppendLine("        public static TView Deserialize(ref CdrReader reader)");
    sb.AppendLine("        {");

    bool isAppendableDef = type.Extensibility == DdsExtensibilityKind.Appendable;

    if (isAppendableDef)
    {
        // Skip DHEADER only if reader detected XCDR2
        sb.AppendLine("            if (reader.Encoding == CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.Align(4);");
        sb.AppendLine("                uint dheader = reader.ReadUInt32();");
        sb.AppendLine("                // Could validate dheader if needed");
        sb.AppendLine("            }");
    }

    // Deserialize fields
    // reader.ReadString() checks reader.Encoding internally

    sb.AppendLine("            return new TView { ... };");
    sb.AppendLine("        }");
}
```

---

## 5. Runtime Integration

### 5.1 DdsWriter (Encoding Selection)

**File:** `Src/CycloneDDS.Runtime/DdsWriter.cs`

The writer determines encoding at construction based on type extensibility:

```csharp
public sealed class DdsWriter<T> : IDisposable
{
    private readonly CdrEncoding _encoding;
    private readonly byte[] _encapsulationHeader;

    public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
    {
        // 1. Determine encoding from type attribute
        var extensibility = DdsTypeSupport.GetExtensibility<T>();
        
        if (extensibility == DdsExtensibilityKind.Final)
        {
            _encoding = CdrEncoding.Xcdr1;
            _encapsulationHeader = BitConverter.IsLittleEndian 
                ? new byte[] { 0x00, 0x01, 0x00, 0x00 } // XCDR1_LE
                : new byte[] { 0x00, 0x00, 0x00, 0x00 }; // XCDR1_BE
        }
        else
        {
            _encoding = CdrEncoding.Xcdr2;
            _encapsulationHeader = BitConverter.IsLittleEndian 
                ? new byte[] { 0x00, 0x09, 0x00, 0x00 } // D_CDR2_LE
                : new byte[] { 0x00, 0x08, 0x00, 0x00 }; // D_CDR2_BE
        }

        // 2. Configure QoS for data representation
        IntPtr actualQos = ConfigureQos(qos, _encoding);
        
        // 3. Create writer with QoS
        // ... (existing creation logic)
    }

    private IntPtr ConfigureQos(IntPtr qos, CdrEncoding encoding)
    {
        IntPtr actualQos = qos;
        bool ownQos = false;

        if (actualQos == IntPtr.Zero)
        {
            actualQos = DdsApi.dds_create_qos();
            ownQos = true;
        }

        try
        {
            // Set data representation in QoS
            short[] representations = new short[] { (short)encoding };
            DdsApi.dds_qset_data_representation(actualQos, 1, representations);
            
            return actualQos;
        }
        finally
        {
            if (ownQos && actualQos != qos)
            {
                // QoS was cloned, original caller still owns theirs
            }
        }
    }

    private void PerformOperation(in T sample, DdsOperationDelegate operation)
    {
        // ... size calculation ...
        
        byte[] buffer = Arena.Rent(totalSize);
        try
        {
            var span = buffer.AsSpan(0, totalSize);
            var cdr = new CdrWriter(span, _encoding); // Pass encoding here!

            // Write encapsulation header
            cdr.Write(_encapsulationHeader);

            // Serialize (encoding propagates automatically)
            _serializer!(sample, ref cdr);

            // ... send to DDS ...
        }
        finally
        {
            Arena.Return(buffer);
        }
    }
}
```

### 5.2 DdsReader (Auto-Detection)

**File:** `Src/CycloneDDS.Runtime/DdsReader.cs`

The reader auto-detects encoding from incoming data:

```csharp
public sealed class DdsReader<T, TView> : IDisposable where TView : struct
{
    public DdsReader(DdsParticipant participant, string topicName, IntPtr qos = default)
    {
        // Configure QoS to accept BOTH XCDR1 and XCDR2
        IntPtr actualQos = ConfigureQos(qos);
        
        // ... create reader ...
    }

    private IntPtr ConfigureQos(IntPtr qos)
    {
        IntPtr actualQos = qos;
        bool ownQos = false;

        if (actualQos == IntPtr.Zero)
        {
            actualQos = DdsApi.dds_create_qos();
            ownQos = true;
        }

        try
        {
            // Accept BOTH formats
            short[] representations = new short[] { 0, 2 }; // XCDR1, XCDR2
            DdsApi.dds_qset_data_representation(actualQos, 2, representations);
            
            return actualQos;
        }
        finally
        {
            // ...
        }
    }

    // ViewScope indexer auto-detects format
    // CdrReader constructor inspects header[1] and sets Encoding field
    // Deserializer checks reader.Encoding automatically
}
```

---

## 6. QoS Integration

### 6.1 New P/Invoke

**File:** `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

```csharp
/// <summary>
/// Set data representation QoS policy.
/// </summary>
/// <param name="qos">QoS handle</param>
/// <param name="n">Number of representations in array</param>
/// <param name="values">Array of representation IDs (0=XCDR1, 2=XCDR2)</param>
[DllImport(DLL_NAME)]
public static extern void dds_qset_data_representation(
    IntPtr qos,
    uint n,
    [In] short[] values);
```

### 6.2 Discovery Handshake

When C# Writer (XCDR2) and C Reader discover each other:
1. C# Writer advertises: "I only speak XCDR2" (QoS: representations={2})
2. C Reader advertises: "I speak XCDR1" (QoS: representations={0})
3. **Discovery Result:** No match (incompatible representations)
4. **Outcome:** They don't connect (correct behavior, prevents garbage data)

When C# Writer (XCDR1 - Final type) and C Reader discover each other:
1. C# Writer advertises: "I speak XCDR1" (QoS: representations={0})
2. C Reader advertises: "I speak XCDR1" (QoS: representations={0})
3. **Discovery Result:** Match!
4. **Outcome:** They connect and communicate successfully

---

## 7. Mixed Nesting Example

### 7.1 The Challenge

```csharp
[DdsExtensibility(DdsExtensibilityKind.Final)]
public partial struct OuterStruct
{
    public InnerStruct Inner;
}

[DdsExtensibility(DdsExtensibilityKind.Appendable)]
public partial struct InnerStruct
{
    public string Name;
}
```

**Question:** What format does `InnerStruct` use when serialized inside `OuterStruct`?

**Answer:** XCDR1 (Final), because the **top-level encoding context** (determined by `OuterStruct`) is XCDR1.

### 7.2 Execution Flow

1. `DdsWriter<OuterStruct>` sees `OuterStruct.Extensibility = Final`
2. Sets `_encoding = Xcdr1`
3. Creates `CdrWriter(buffer, Xcdr1)`
4. `OuterStruct.Serialize(ref writer)` executes
5. Calls `Inner.Serialize(ref writer)`
6. `InnerStruct.Serialize` checks `writer.Encoding` → `Xcdr1`
7. Skips DHEADER (because `Encoding != Xcdr2`)
8. `writer.WriteString(Name)` writes with NUL terminator (because `Encoding == Xcdr1`)
9. **Result:** Entire blob is valid XCDR1

**No code changes needed** - the context propagates automatically!

---

## 8. Testing Strategy

### 8.1 Unit Tests (Minimum 6)

1. **Xcdr1String_Roundtrip**
   - Serialize `@final` struct with string in XCDR1
   - Verify NUL terminator present in buffer
   - Deserialize and verify string matches

2. **Xcdr2String_Roundtrip**
   - Serialize `@appendable` struct with string in XCDR2
   - Verify NO NUL terminator in buffer
   - Deserialize and verify string matches

3. **Xcdr1Appendable_DegradeToFinal**
   - Serialize `@appendable` struct in XCDR1 mode (forced by parent)
   - Verify NO DHEADER in buffer
   - Deserialize and verify data correct

4. **MixedNesting_OuterFinal_InnerAppendable**
   - `OuterStruct(@final)` contains `InnerStruct(@appendable)`
   - Serialize `OuterStruct`
   - Verify entire blob is XCDR1 (no DHEADER, strings have NUL)

5. **AutoDetection_Xcdr1Message_CorrectRead**
   - Create XCDR1 blob manually (header `0x0001`)
   - Pass to `CdrReader`
   - Verify `reader.Encoding == Xcdr1`
   - Deserialize string, verify NUL handled correctly

6. **AutoDetection_Xcdr2Message_CorrectRead**
   - Create XCDR2 blob manually (header `0x0009`)
   - Pass to `CdrReader`
   - Verify `reader.Encoding == Xcdr2`
   - Deserialize string, verify no NUL expected

### 8.2 Integration Tests (Minimum 2)

7. **Interop_CsharpXcdr1_ToCppXcdr1**
   - C# Writer with `@final` type (XCDR1)
   - C++ Reader (legacy, XCDR1-only)
   - Verify discovery matches
   - Verify data received correctly

8. **Interop_CppXcdr1_ToCsharpReader**
   - C++ Writer (legacy, XCDR1)
   - C# Reader (auto-detect enabled)
   - C# Reader should auto-detect XCDR1 and parse correctly

---

## 9. Implementation Plan

### Phase 1: Core Primitives (4 hours)
- ✅ Create `CdrEncoding` enum
- ✅ Update `CdrWriter` with `Encoding` field and stateful `WriteString`
- ✅ Update `CdrReader` with auto-detection and stateful `ReadString`
- ✅ Update `CdrSizer` with `Encoding` field

### Phase 2: Code Generation (4 hours)
- ✅ Update `SerializerEmitter` for conditional DHEADER
- ✅ Update `DeserializerEmitter` for conditional DHEADER skip
- ✅ Update `SizerEmitter` for conditional DHEADER sizing
- ✅ Regenerate all test types

### Phase 3: Runtime Integration (3 hours)
- ✅ Update `DdsWriter` to select encoding based on extensibility
- ✅ Add `dds_qset_data_representation` P/Invoke
- ✅ Configure QoS in writer/reader constructors
- ✅ Update encapsulation header writing

### Phase 4: Testing (5 hours)
- ✅ Implement 6 unit tests
- ✅ Implement 2 integration tests (may require C++ test harness)
- ✅ Verify all existing tests still pass

**Total Effort:** 16 hours (~2 days)

---

## 10. Success Criteria

### Functional
- ✅ C# Writer with `@final` type creates XCDR1-compliant blobs
- ✅ C# Writer with `@appendable` type creates XCDR2-compliant blobs
- ✅ C# Reader auto-detects and parses both XCDR1 and XCDR2
- ✅ Mixed nesting handled correctly (encoding context propagates)
- ✅ QoS handshake prevents incompatible matches

### Performance
- ✅ Zero overhead for XCDR2 hot path (default)
- ✅ Auto-detection is O(1) (peek header[1])

### Quality
- ✅ All 8 new tests pass
- ✅ All existing tests pass
- ✅ No breaking changes to public API
- ✅ Clean generated code (no manual overrides needed)

---

## 11. Migration Guide for Users

### For New Projects
No action needed. XCDR2 is the default for all `@appendable` types.

### For Legacy Interop Projects
If you need to communicate with XCDR1-only systems:

**Option A: Mark types as `@final`**
```csharp
[DdsExtensibility(DdsExtensibilityKind.Final)]
public partial struct LegacyMessage
{
    public string Data;
}
// Automatically uses XCDR1
```

**Option B: Configure QoS manually**
```csharp
var qos = DdsApi.dds_create_qos();
short[] reps = new short[] { 0 }; // XCDR1 only
DdsApi.dds_qset_data_representation(qos, 1, reps);
var writer = new DdsWriter<MyType>(participant, "Topic", qos);
DdsApi.dds_delete_qos(qos);
```

### For Mixed Environments
Use `@appendable` for modern systems, `@final` for legacy. The QoS handshake ensures only compatible systems match.

---

## 12. Open Questions / Future Work

1. **XCDR1 Extended Keys:** XCDR1 has limited key size (16 bytes). Do we need to detect and warn?
2. **Mutable Types:** This design focuses on Final/Appendable. Full `@mutable` support requires PL_CDR2 logic.
3. **Performance Metrics:** Benchmark XCDR1 vs XCDR2 throughput to quantify overhead.

---

**Document Status:** Approved for Implementation  
**Next Step:** Create BATCH-24-INSTRUCTIONS.md and add to SERDATA-TASK-MASTER.md
