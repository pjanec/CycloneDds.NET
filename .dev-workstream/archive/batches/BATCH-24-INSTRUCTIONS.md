# BATCH-24: XCDR1/XCDR2 Dual Encoding Support (FCDC-COMPAT-01)

**Developer Onboarding Document**  
**Version:** 1.0  
**Date:** 2026-01-22  
**Task:** FCDC-COMPAT-01 (XCDR1/XCDR2 Compatibility)  
**Estimated Effort:** 2-3 days  
**Batch Report:** Submit to `.dev-workstream/reports/BATCH-24-REPORT.md`

---

## 1. Welcome & Context

Welcome! This batch implements **dual encoding support** for XCDR1 (Classic CDR, legacy) and XCDR2 (Extended CDR, modern) formats. This enables the C# bindings to interoperate with legacy DDS systems while maintaining XCDR2 as the default.

**Your Mission:**
- Implement stateful encoding context in `CdrWriter`, `CdrReader`, `CdrSizer`
- Update code generators to emit conditional DHEADER logic
- Integrate automatic encoding selection in `DdsWriter`/`DdsReader`
- Ensure backward compatibility with zero performance overhead
- All tests must pass (minimum 8 new tests + all existing)

**Key Design Principle:** **Stateful Context Propagation**  
The encoding decision (XCDR1 vs XCDR2) is made once at the top level and propagates automatically through the entire serialization chain via immutable context fields. This prevents "mixed protocol" bugs.

---

## 2. Workspace Orientation

### 2.1 Project Structure

```
D:\Work\FastCycloneDdsCsharpBindings\
├── Src\
│   ├── CycloneDDS.Core\         # Core serialization primitives
│   │   ├── CdrEncoding.cs       # 🆕 CREATE: Encoding enum
│   │   ├── CdrWriter.cs         # 📝 UPDATE: Add Encoding field & stateful WriteString
│   │   ├── CdrReader.cs         # 📝 UPDATE: Add auto-detection & stateful ReadString
│   │   └── CdrSizer.cs          # 📝 UPDATE: Add Encoding field & stateful size calc
│   └── CycloneDDS.Runtime\      # Runtime DDS layer
│       ├── DdsWriter.cs         # 📝 UPDATE: Select encoding, configure QoS
│       ├── DdsReader.cs         # 📝 UPDATE: Configure QoS for both formats
│       └── Interop\
│           └── DdsApi.cs        # 📝 UPDATE: Add dds_qset_data_representation P/Invoke
├── tools\
│   └── CycloneDDS.CodeGen\      # Code generator
│       ├── SerializerEmitter.cs # 📝 UPDATE: Conditional DHEADER (if Xcdr2)
│       ├── DeserializerEmitter.cs # 📝 UPDATE: Conditional DHEADER skip
│       └── SizerEmitter.cs      # 📝 UPDATE: Conditional DHEADER sizing
├── tests\
│   └── CycloneDDS.Runtime.Tests\
│       └── XcdrCompatibilityTests.cs # 🆕 CREATE: Minimum 8 tests
├── docs\
│   └── XCDR1-XCDR2-COMPATIBILITY-DESIGN.md # ⭐ PRIMARY DESIGN DOC
└── .dev-workstream\
    └── reports\
        └── BATCH-24-REPORT.md   # 📝 SUBMIT: Your completion report
```

### 2.2 Build & Test Commands

```powershell
# Build entire solution
dotnet build D:\Work\FastCycloneDdsCsharpBindings\FastCycloneDdsCsharpBindings.sln

# Run ALL tests
dotnet test D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# Run only compatibility tests
dotnet test --filter "FullyQualifiedName~XcdrCompatibilityTests"

# Rebuild code generator and regenerate types (IMPORTANT!)
dotnet build D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj
# Then regenerate test types - check existing generation scripts
```

---

## 3. Required Reading (CRITICAL!)

### 3.1 Primary Design Document

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\XCDR1-XCDR2-COMPATIBILITY-DESIGN.md`**

This is your **PRIMARY reference**. Read sections 1-7 before writing any code.

**Key Sections:**
- Section 2: XCDR1 vs XCDR2 differences (wire format examples)
- Section 3: **Stateful Encoding Context Architecture** ⭐
- Section 4: Code Generation Updates
- Section 5: Runtime Integration
- Section 6: QoS Integration
- Section 7: Mixed Nesting Example (critical for understanding context propagation)

### 3.2 Supporting Documents

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\xcdr1-xcdr2-compatibility-design-talk.md`**
- Original advisor discussion on compatibility
- Context on QoS handshake mechanism

📖 **`D:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`** (Lines 2078-2213)
- FCDC-COMPAT-01 task definition with detailed requirements

### 3.3 Existing Code to Study

**Study these current implementations:**

1. **`Src/CycloneDDS.Core/CdrWriter.cs`**:
   - Current `WriteString()` implementation (hard-coded XCDR2)
   - Struct layout and field patterns

2. **`tools/CycloneDDS.CodeGen/SerializerEmitter.cs`**:
   - Current DHEADER pattern (unconditional for Appendable)
   - Code generation patterns

3. **`Src/CycloneDDS.Runtime/DdsWriter.cs`**:
   - Encapsulation header writing
   - Serialization setup

---

## 4. Task Breakdown

### **Task 1: Create CdrEncoding Enum** (15 min)

**File:** `Src/CycloneDDS.Core/CdrEncoding.cs` (NEW)

Create a new file with the encoding enum:

```csharp
namespace CycloneDDS.Core
{
    /// <summary>
    /// CDR encoding format selector.
    /// Determines wire format conventions for strings, DHEADERs, and encapsulation.
    /// </summary>
    public enum CdrEncoding : byte
    {
        /// <summary>
        /// Legacy CDR (DDS v1.2 / DCPS 2.1).
        /// - Strings include NUL terminator (Length = ByteCount + 1).
        /// - No DHEADERs (even for Appendable types - degrades to Final behavior).
        /// - Encapsulation header: 0x0000 (BE) or 0x0001 (LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR1 (value = 0).
        /// </summary>
        Xcdr1 = 0,

        /// <summary>
        /// Extended CDR (DDS X-Types 1.3).
        /// - Strings do NOT include NUL terminator (Length = ByteCount exactly).
        /// - DHEADERs used for Appendable/Mutable types (4-byte length prefix).
        /// - Encapsulation header: 0x0008 (D_CDR2 BE) or 0x0009 (D_CDR2 LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR2 (value = 2).
        /// </summary>
        Xcdr2 = 2
    }
}
```

**IMPORTANT:** The enum values (0 and 2) match the native DDS QoS constants exactly.

---

### **Task 2: Update CdrWriter** (30 min)

**File:** `Src/CycloneDDS.Core/CdrWriter.cs`

**Add Encoding Field:**

```csharp
public ref struct CdrWriter
{
    private Span<byte> _span;
    private int _buffered;
    
    /// <summary>
    /// Active encoding format. Immutable after construction.
    /// All serialization decisions (string NUL, DHEADER) are based on this value.
    /// </summary>
    public readonly CdrEncoding Encoding;

    // Existing constructor - UPDATE signature
    public CdrWriter(Span<byte> buffer, CdrEncoding encoding = CdrEncoding.Xcdr2)
    {
        _span = buffer;
        _buffered = 0;
        Encoding = encoding; // NEW
    }

    // ... other fields and methods ...
}
```

**Update WriteString Method:**

Find the current `WriteString` method and replace it:

```csharp
public void WriteString(ReadOnlySpan<char> value)
{
    int utf8Length = System.Text.Encoding.UTF8.GetByteCount(value);
    
    if (Encoding == CdrEncoding.Xcdr2)
    {
        // XCDR2: Length = exact byte count (no NUL)
        WriteInt32(utf8Length);
        EnsureSize(utf8Length);
        int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
        _buffered += written;
    }
    else // Xcdr1
    {
        // XCDR1: Length = byte count + 1 (includes NUL terminator)
        WriteInt32(utf8Length + 1);
        EnsureSize(utf8Length + 1);
        int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
        _buffered += written;
        _span[_buffered] = 0; // Write NUL terminator
        _buffered += 1;
    }
}
```

**CRITICAL:** Leave all other CdrWriter methods unchanged. The encoding only affects strings and DHEADER (handled by generator).

---

### **Task 3: Update CdrReader** (45 min)

**File:** `Src/CycloneDDS.Core/CdrReader.cs`

**Add Encoding Field & Auto-Detection:**

```csharp
public ref struct CdrReader
{
    private ReadOnlySpan<byte> _span;
    private int _position;
    
    /// <summary>
    /// Detected encoding format from encapsulation header.
    /// Auto-detected in constructor by inspecting header byte[1].
    /// </summary>
    public readonly CdrEncoding Encoding;

    // UPDATE constructor
    public CdrReader(ReadOnlySpan<byte> buffer, bool isXcdr2) // isXcdr2 param might exist
    {
        _span = buffer;
        _position = 0;
        
        // Auto-detect encoding from header (byte 1 = encoding identifier)
        // XCDR1: 0x00-0x05 (CDR, PL_CDR, etc.)
        // XCDR2: 0x06-0x0B (CDR2, D_CDR2, PL_CDR2)
        if (buffer.Length >= 4)
        {
            byte identifier = buffer[1];
            Encoding = (identifier >= 6) ? CdrEncoding.Xcdr2 : CdrEncoding.Xcdr1;
        }
        else
        {
            // Default to XCDR2 if header not available (shouldn't happen)
            Encoding = isXcdr2 ? CdrEncoding.Xcdr2 : CdrEncoding.Xcdr1;
        }
    }

    // ... other methods ...
}
```

**Update ReadString Method:**

Find the current `ReadString` method and replace it:

```csharp
public string ReadString()
{
    int length = ReadInt32();
    
    if (Encoding == CdrEncoding.Xcdr1)
    {
        // XCDR1: Length includes NUL terminator
        if (length <= 0) return string.Empty;
        
        // Read length-1 bytes (exclude NUL)
        var utf8Bytes = _span.Slice(_position, length - 1);
        _position += length; // Consume NUL too
        return System.Text.Encoding.UTF8.GetString(utf8Bytes);
    }
    else // Xcdr2
    {
        // XCDR2: Length is exact byte count (no NUL)
        if (length <= 0) return string.Empty;
        
        var utf8Bytes = _span.Slice(_position, length);
        _position += length;
        return System.Text.Encoding.UTF8.GetString(utf8Bytes);
    }
}
```

---

### **Task 4: Update CdrSizer** (20 min)

**File:** `Src/CycloneDDS.Core/CdrSizer.cs`

**Add Encoding Field:**

```csharp
public ref struct CdrSizer
{
    private int _cursor;
    
    /// <summary>
    /// Target encoding format for size calculation.
    /// Affects string sizing (NUL terminator) and DHEADER sizing.
    /// </summary>
    public readonly CdrEncoding Encoding;

    public CdrSizer(int initialOffset, CdrEncoding encoding = CdrEncoding.Xcdr2)
    {
        _cursor = initialOffset;
        Encoding = encoding;
    }

    // ... existing methods ...
}
```

**Update WriteString Method:**

```csharp
public void WriteString(ReadOnlySpan<char> value)
{
    _cursor += 4; // Length field (always 4 bytes)
    _cursor += System.Text.Encoding.UTF8.GetByteCount(value);
    
    if (Encoding == CdrEncoding.Xcdr1)
    {
        _cursor += 1; // Add NUL terminator size
    }
}
```

---

### **Task 5: Update SerializerEmitter** (THE BIG ONE - 2-3 hours)

**File:** `tools/CycloneDDS.CodeGen/SerializerEmitter.cs`

**Golden Rule:** DHEADER is written **only if**:
1. Type is `@appendable` (or `@mutable`)
2. **AND** `writer.Encoding == CdrEncoding.Xcdr2`

Find the `EmitSerialize` method and update the DHEADER logic:

**Current Pattern (Simplified):**
```csharp
if (isAppendable)
{
    // Always write DHEADER
    sb.AppendLine("dheaderPos = writer.Position;");
    sb.AppendLine("writer.WriteUInt32(0);");
}
```

**New Pattern:**
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
        sb.AppendLine("            if (writer.Encoding == CycloneDDS.Core.CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.Align(4);");
        sb.AppendLine("                dheaderPos = writer.Position;");
        sb.AppendLine("                writer.WriteUInt32(0); // Placeholder DHEADER");
        sb.AppendLine("                bodyStart = writer.Position;");
        sb.AppendLine("            }");
    }

    // Generate field serialization (unchanged)
    foreach (var field in type.Fields)
    {
        sb.AppendLine($"            {GetWriterCall(field)};");
    }

    if (isAppendableDef)
    {
        // Patch DHEADER only if we wrote one
        sb.AppendLine("            if (writer.Encoding == CycloneDDS.Core.CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                int bodyLen = writer.Position - bodyStart;");
        sb.AppendLine("                writer.PatchUInt32(dheaderPos, (uint)bodyLen);");
        sb.AppendLine("            }");
    }

    sb.AppendLine("        }");
}
```

**CRITICAL:** Use fully qualified name `CycloneDDS.Core.CdrEncoding.Xcdr2` in generated code.

---

### **Task 6: Update DeserializerEmitter** (1-2 hours)

**File:** `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs`

Apply matching logic for deserialization:

```csharp
private void EmitDeserialize(StringBuilder sb, TypeInfo type)
{
    sb.AppendLine("        public static TView Deserialize(ref CdrReader reader)");
    sb.AppendLine("        {");

    bool isAppendableDef = type.Extensibility == DdsExtensibilityKind.Appendable;

    if (isAppendableDef)
    {
        // Skip DHEADER only if reader detected XCDR2
        sb.AppendLine("            if (reader.Encoding == CycloneDDS.Core.CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.Align(4);");
        sb.AppendLine("                uint dheader = reader.ReadUInt32();");
        sb.AppendLine("                // DHEADER specifies body length (could validate if needed)");
        sb.AppendLine("            }");
    }

    // Deserialize fields (unchanged)
    foreach (var field in type.Fields)
    {
        sb.AppendLine($"            {GetDeserializeCall(field)};");
    }

    sb.AppendLine("            return view;");
    sb.AppendLine("        }");
}
```

---

### **Task 7: Update SizerEmitter** (1 hour)

**File:** `tools/CycloneDDS.CodeGen/SizerEmitter.cs`

The sizer signature might need updating. Find `EmitGetSerializedSize`:

```csharp
private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
{
    // Signature might be: GetSerializedSize(int currentAlignment, bool isXcdr2)
    // Update to pass encoding to sizer
    
    sb.AppendLine("        public int GetSerializedSize(int currentAlignment, bool isXcdr2)");
    sb.AppendLine("        {");
    sb.AppendLine("            var sizer = new CycloneDDS.Core.CdrSizer(currentAlignment, isXcdr2 ? CycloneDDS.Core.CdrEncoding.Xcdr2 : CycloneDDS.Core.CdrEncoding.Xcdr1);");

    bool isAppendableDef = type.Extensibility == DdsExtensibilityKind.Appendable;

    if (isAppendableDef)
    {
        // Conditional DHEADER size
        sb.AppendLine("            if (sizer.Encoding == CycloneDDS.Core.CdrEncoding.Xcdr2)");
        sb.AppendLine("            {");
        sb.AppendLine("                sizer.Align(4);");
        sb.AppendLine("                sizer.WriteUInt32(0); // DHEADER (4 bytes)");
        sb.AppendLine("            }");
    }

    // Generate field sizing calls
    foreach (var field in type.Fields)
    {
        sb.AppendLine($"            {GetSizerCall(field)};");
    }

    sb.AppendLine("            return sizer.Size;");
    sb.AppendLine("        }");
}
```

---

### **Task 8: Update DdsWriter** (1-2 hours)

**File:** `Src/CycloneDDS.Runtime/DdsWriter.cs`

**Add Fields:**
```csharp
private readonly CdrEncoding _encoding;
private readonly byte[] _encapsulationHeader;
```

**Update Constructor:**
```csharp
public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
{
    _participant = participant;

    // 1. Determine encoding from type extensibility
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
    
    try
    {
        // 3. Create topic and writer (existing logic)
        // ...
    }
    finally
    {
        if (actualQos != qos && actualQos != IntPtr.Zero)
        {
            DdsApi.dds_delete_qos(actualQos);
        }
    }
}

private IntPtr ConfigureQos(IntPtr qos, CdrEncoding encoding)
{
    IntPtr actualQos = qos;
    
    if (actualQos == IntPtr.Zero)
    {
        actualQos = DdsApi.dds_create_qos();
    }

    // Set data representation in QoS
    short[] representations = new short[] { (short)encoding };
    DdsApi.dds_qset_data_representation(actualQos, 1, representations);
    
    return actualQos;
}
```

**Update Serialization Method:**

Find where `CdrWriter` is created (likely in `PerformOperation` or `Write`) and update:

```csharp
var cdr = new CdrWriter(span, _encoding); // Pass encoding!

// Write encapsulation header (now uses _encapsulationHeader)
cdr.Write(_encapsulationHeader);

// Serialize (encoding propagates automatically)
_serializer!(sample, ref cdr);
```

---

### **Task 9: Update DdsReader** (30 min)

**File:** `Src/CycloneDDS.Runtime/DdsReader.cs`

**Update Constructor to Accept Both Formats:**

```csharp
public DdsReader(DdsParticipant participant, string topicName, IntPtr qos = default)
{
    // ... existing setup ...

    // Configure QoS to accept BOTH XCDR1 and XCDR2
    IntPtr actualQos = ConfigureQos(qos);
    
    try
    {
        // Create reader with configured QoS
        // ...
    }
    finally
    {
        if (actualQos != qos && actualQos != IntPtr.Zero)
        {
            DdsApi.dds_delete_qos(actualQos);
        }
    }
}

private IntPtr ConfigureQos(IntPtr qos)
{
    IntPtr actualQos = qos;
    
    if (actualQos == IntPtr.Zero)
    {
        actualQos = DdsApi.dds_create_qos();
    }

    // Accept BOTH formats (auto-detection will handle it)
    short[] representations = new short[] { 0, 2 }; // XCDR1, XCDR2
    DdsApi.dds_qset_data_representation(actualQos, 2, representations);
    
    return actualQos;
}
```

**NOTE:** The reader doesn't need to know the encoding ahead of time - `CdrReader` auto-detects it from the encapsulation header.

---

### **Task 10: Add P/Invoke** (10 min)

**File:** `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

Add the QoS configuration function:

```csharp
/// <summary>
/// Set data representation QoS policy.
/// Specifies which CDR encoding formats this entity supports.
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

---

## 5. Testing Requirements (CRITICAL!)

### 5.1 Create Test File

**File:** `tests/CycloneDDS.Runtime.Tests/XcdrCompatibilityTests.cs`

### 5.2 Minimum Required Tests (8 total)

**Unit Tests (6):**

1. **Xcdr1String_Roundtrip**
   - Create struct: `[DdsExtensibility(Final)] struct TestMessage { string Name; }`
   - Serialize with `Name = "Hello"`
   - Inspect buffer bytes: Verify NUL terminator present (`0x00` at expected offset)
   - Deserialize, verify `Name == "Hello"`

2. **Xcdr2String_Roundtrip**
   - Create struct: `[DdsExtensibility(Appendable)] struct TestMessage { string Name; }`
   - Serialize with `Name = "World"`
   - Inspect buffer: Verify NO NUL after string bytes
   - Deserialize, verify `Name == "World"`

3. **Xcdr1Appendable_DegradeToFinal**
   - Create `@appendable` struct
   - Manually force XCDR1 encoding (create writer with Final type wrapper)
   - Verify NO DHEADER in serialized buffer
   - Verify strings have NUL terminators
   - Deserialize successfully

4. **MixedNesting_OuterFinal_InnerAppendable**
   ```csharp
   [DdsExtensibility(Final)]
   struct OuterStruct { InnerStruct Inner; }
   
   [DdsExtensibility(Appendable)]
   struct InnerStruct { string Data; }
   ```
   - Serialize `OuterStruct`
   - Verify header is `0x0001` (XCDR1)
   - Verify NO DHEADER anywhere in buffer
   - Verify `Data` string has NUL terminator
   - Deserialize successfully

5. **AutoDetection_Xcdr1Message_CorrectRead**
   - Manually create buffer with XCDR1 header (`0x00 0x01 0x00 0x00`)
   - Add XCDR1 string ("Test" + NUL)
   - Create `CdrReader` from buffer
   - Verify `reader.Encoding == CdrEncoding.Xcdr1`
   - Read string, verify correct

6. **AutoDetection_Xcdr2Message_CorrectRead**
   - Manually create buffer with XCDR2 header (`0x00 0x09 0x00 0x00`)
   - Add DHEADER, add XCDR2 string ("Test", no NUL)
   - Create `CdrReader` from buffer
   - Verify `reader.Encoding == CdrEncoding.Xcdr2`
   - Read with DHEADER skip, verify string correct

**Integration Tests (2):**

7. **QosHandshake_FinalType_AdvertisesXcdr1**
   - Create `DdsWriter<FinalStruct>`
   - If possible, query QoS to verify `DDS_DATA_REPRESENTATION_XCDR1` is set
   - At minimum: Ensure writer creation succeeds without errors

8. **QosHandshake_AppendableType_AdvertisesXcdr2**
   - Create `DdsWriter<AppendableStruct>`
   - If possible, query QoS to verify `DDS_DATA_REPRESENTATION_XCDR2` is set
   - At minimum: Ensure writer creation succeeds without errors

### 5.3 Test Quality Standards

**Critical:** Tests MUST verify actual wire format bytes, not just successful round-trip!

**Example Byte Verification:**
```csharp
// Serialize
var writer = new DdsWriter<FinalMessage>(participant, "Topic");
writer.Write(new FinalMessage { Name = "Hi" });

// Get the internal buffer (you may need to expose this for testing)
byte[] buffer = GetLastSerializedBuffer();

// Verify XCDR1 header
Assert.Equal(0x00, buffer[0]);
Assert.Equal(0x01, buffer[1]); // LE identifier
Assert.Equal(0x00, buffer[2]);
Assert.Equal(0x00, buffer[3]);

// Verify string length includes NUL
int lengthOffset = 4; // After header
int length = BitConverter.ToInt532(buffer, lengthOffset);
Assert.Equal(3, length); // "Hi" = 2 bytes + 1 NUL

// Verify NUL terminator present
int nullOffset = lengthOffset + 4 + 2; // After length + "Hi"
Assert.Equal(0x00, buffer[nullOffset]);
```

---

## 6. Common Pitfalls & Tips

### 6.1 CdrReader Constructor Overloads

**ISSUE:** Existing code might have multiple `CdrReader` constructors.

**SOLUTION:** Update ALL constructors to perform auto-detection. The `isXcdr2` parameter can become a fallback if header is missing.

### 6.2 Fully Qualified Names in Generated Code

**ISSUE:** Generated code might not have `using CycloneDDS.Core;`.

**SOLUTION:** Always use fully qualified names in emitters:
- `CycloneDDS.Core.CdrEncoding.Xcdr2`
- `CycloneDDS.Core.CdrSizer`

### 6.3 Regenerating Test Types

**CRITICAL:** After updating emitters, you MUST rebuild the code generator AND regenerate all test types!

```powershell
# 1. Rebuild code generator
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

# 2. Regenerate types (check for existing scripts or manual process)
# You may need to run CodeGen tool on test IDL files
```

### 6.4 Existing Tests Breaking

**ISSUE:** Existing tests might expect XCDR2-only behavior.

**SOLUTION:** Existing `@appendable` types should still use XCDR2 (no change). Only `@final` types now use XCDR1.

---

## 7. Definition of Done

### 7.1 Code Quality

- ✅ All 8 new tests pass
- ✅ All 92 existing tests still pass
- ✅ No compiler warnings
- ✅ Code regenerated successfully

### 7.2 Functional Requirements

- ✅ `@final` types serialize as XCDR1 (verified byte-by-byte)
- ✅ `@appendable` types serialize as XCDR2 (verified byte-by-byte)
- ✅ Reader auto-detects both formats correctly
- ✅ Mixed nesting works (context propagates)
- ✅ QoS configured correctly (discovery handshake)

### 7.3 Performance

- ✅ Zero overhead for XCDR2 path (default, no extra checks)
- ✅ Auto-detection is O(1) (single byte check)

### 7.4 Documentation

- ✅ XML comments on new `CdrEncoding` enum
- ✅ Report submitted to `.dev-workstream/reports/BATCH-24-REPORT.md`

---

## 8. Report Template

**File:** `.dev-workstream/reports/BATCH-24-REPORT.md`

```markdown
# BATCH-24 Report: XCDR1/XCDR2 Dual Encoding Support (FCDC-COMPAT-01)

**Developer:** [Your Name]  
**Date:** [Completion Date]  
**Status:** ✅ COMPLETE / ⏳ IN PROGRESS / ❌ BLOCKED

---

## Summary

[Brief overview - did you complete the stateful encoding context implementation?]

---

## Implementation Notes

### Core Primitives (Task 1-4)

**Files Created:**
- `Src/CycloneDDS.Core/CdrEncoding.cs`

**Files Modified:**
- `Src/CycloneDDS.Core/CdrWriter.cs` (added Encoding field, stateful WriteString)
- `Src/CycloneDDS.Core/CdrReader.cs` (auto-detection, stateful ReadString)
- `Src/CycloneDDS.Core/CdrSizer.cs` (added Encoding field)

**Key Decisions:**
- [Explain any design choices]

### Code Generation (Task 5-7)

**Files Modified:**
- `tools/CycloneDDS.CodeGen/SerializerEmitter.cs` (conditional DHEADER)
- `tools/CycloneDDS.CodeGen/DeserializerEmitter.cs` (conditional DHEADER skip)
- `tools/CycloneDDS.CodeGen/SizerEmitter.cs` (conditional sizing)

**Challenges:**
- [Any code generation issues?]
- [Did you regenerate all types successfully?]

### Runtime Integration (Task 8-10)

**Files Modified:**
- `Src/CycloneDDS.Runtime/DdsWriter.cs`
- `Src/CycloneDDS.Runtime/DdsReader.cs`
- `Src/CycloneDDS.Runtime/Interop/DdsApi.cs`

**QoS Integration:**
- [Confirm dds_qset_data_representation working]

---

## Test Results

**Test File:** `tests/CycloneDDS.Runtime.Tests/XcdrCompatibilityTests.cs`

**Test Summary:**
- Total New Tests: [count]
- Passing: [count]
- Failing: [count]

**Detailed Results:**
1. Xcdr1String_Roundtrip: ✅ PASS (NUL verified at offset XX)
2. Xcdr2String_Roundtrip: ✅ PASS (no NUL, DHEADER verified)
3. Xcdr1Appendable_DegradeToFinal: ✅ PASS
4. MixedNesting_OuterFinal_InnerAppendable: ✅ PASS
5. AutoDetection_Xcdr1Message_CorrectRead: ✅ PASS
6. AutoDetection_Xcdr2Message_CorrectRead: ✅ PASS
7. QosHandshake_FinalType_AdvertisesXcdr1: ✅ PASS
8. QosHandshake_AppendableType_AdvertisesXcdr2: ✅ PASS

**Existing Tests:**
- All 92 existing tests: ✅ PASS / ❌ [count] FAIL

**Full Test Output:**
```
[Paste dotnet test output]
```

---

## Wire Format Verification

### XCDR1 Final Struct
**Sample:** `FinalMessage { Name = "Hi" }`

**Buffer Hex:**
```
00 01 00 00   # Header (XCDR1_LE)
03 00 00 00   # Length = 3 (2 + NUL)
48 69 00      # "Hi\0"
```

**Verification:** ✅ NUL terminator present

### XCDR2 Appendable Struct
**Sample:** `AppendableMessage { Name = "Hi" }`

**Buffer Hex:**
```
00 09 00 00      # Header (D_CDR2_LE)
0C 00 00 00      # DHEADER = 12 bytes
02 00 00 00      # String length = 2 (no NUL)
48 69            # "Hi" (no NUL!)
```

**Verification:** ✅ DHEADER present, no NUL

---

## Known Issues / Deferred Work

[Any issues or features not implemented?]

---

## Performance Impact

- Encoding selection overhead: **Zero** (compile-time decision at writer creation)
- Auto-detection overhead: **O(1)** (single byte read)
- XCDR2 hot path: **Unchanged** (no extra checks)

---

## Questions for Review

[Questions for dev lead?]
```

---

## 9. Success Checklist

Before submitting:

- [ ] All 8 new tests passing
- [ ] All 92 existing tests passing
- [ ] `CdrEncoding` enum created
- [ ] `CdrWriter`, `CdrReader`, `CdrSizer` updated with Encoding field
- [ ] Emitters updated with conditional DHEADER logic
- [ ] `DdsWriter`/`DdsReader` configure QoS correctly
- [ ] Code regenerated successfully
- [ ] Wire format verified byte-by-byte (manual inspection)
- [ ] Report submitted with full test output

---

**Good luck! Remember: The stateful encoding context is the key innovation. Once you set it at the top level, everything else propagates automatically.**

**Questions? Check the design doc Section 7 for the "Mixed Nesting" example - it shows exactly how context propagation prevents bugs!**
