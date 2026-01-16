# BATCH-01: Foundation - CdrWriter and CdrReader

**Batch Number:** BATCH-01  
**Tasks:** FCDC-S001, FCDC-S002, FCDC-S003  
**Phase:** Stage 1 - Foundation (Part 1)  
**Estimated Effort:** 8-10 hours  
**Priority:** CRITICAL  
**Dependencies:** None (First batch)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to the **FastCycloneDDS C# Bindings** project. This batch implements the core CDR (Common Data Representation) serialization primitives - the Writer and Reader that will serve as the foundation for the entire project.

**Critical Context:** We are building **high-performance, zero-allocation C# bindings** for Cyclone DDS using a **serdata-based approach**. Instead of marshalling between C# and native C structs, we serialize directly to XCDR2-compliant byte streams.

**Your Mission:** Implement the byte-level CDR Writer (serialization) and Reader (deserialization) with comprehensive round-trip validation.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - See tasks FCDC-S001, FCDC-S002, FCDC-S003
3. **Design Document:** `docs/SERDATA-DESIGN.md` - Sections 1-6 (Architecture, CDR Core)
4. **Design Context:** `docs/design-talk.md` - Lines 2850-3021 (CDR Writer/Reader implementation)

### Source Code Location

- **New Package:** `Src/CycloneDDS.Core/` (you will create this)
- **Test Project:** `tests/CycloneDDS.Core.Tests/` (you will create this)

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-01-REPORT.md`

**Use template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-01-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **FCDC-S001 (Setup):** Create projects → Build succeeds ✅
2. **FCDC-S002 (CdrWriter):** Implement → Write tests → **ALL tests pass** ✅
3. **FCDC-S003 (CdrReader):** Implement → Write tests → **ALL tests pass** ✅  
4. **Round-Trip Tests:** Write → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous tasks)

**Why:** Each component must be solid before building on top. BATCH-02 will validate everything against Cyclone's native implementation.

---

## Context

This is the **first batch** of the FastCycloneDDS C# Bindings project, implementing a clean-slate serdata-based approach. 

**Architectural Pivot:** We moved away from the old plain-C native struct marshalling (see `old_implem/`) to a high-performance CDR serialization strategy that:
- Eliminates GC spikes for variable-size data (strings, sequences)
- Enables zero-allocation hot paths via ArrayPool
- Provides JIT-optimizable generated serialization code

**Why This Batch Matters:** CdrWriter and CdrReader are the **foundation**. Every serializer we generate will use these primitives. BATCH-02 will prove correctness via Golden Rig validation.

**Related Tasks:**
- [FCDC-S001](../docs/SERDATA-TASK-MASTER.md#fcdc-s001-cycloneddscore-package-setup) - Package setup
- [FCDC-S002](../docs/SERDATA-TASK-MASTER.md#fcdc-s002-cdrwriter-implementation) - CdrWriter (serialization)
- [FCDC-S003](../docs/SERDATA-TASK-MASTER.md#fcdc-s003-cdrreader-implementation) - CdrReader (deserialization)

---

## 🎯 Batch Objectives

**Primary Goal:** Implement XCDR2-compliant CDR Writer and Reader with comprehensive round-trip validation.

**Success Metric:** All round-trip tests pass (Write → Read → assert equal).

---

## ✅ Tasks

### Task 1: CycloneDDS.Core Package Setup (FCDC-S001)

**File:** `Src/CycloneDDS.Core/CycloneDDS.Core.csproj` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s001-cycloneddscore-package-setup)

**Requirements:**
1. Create `Src/CycloneDDS.Core/` directory
2. Create class library project targeting `net8.0`
3. Add package metadata:
   ```xml
   <PropertyGroup>
     <TargetFramework>net8.0</TargetFramework>
     <Nullable>enable</Nullable>
     <LangVersion>12.0</LangVersion>
     <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
     <PackageId>CycloneDDS.Core</PackageId>
     <Version>2.0.0-alpha1</Version>
     <Authors>FastCycloneDDS</Authors>
     <Description>XCDR2 CDR serialization primitives for Cyclone DDS</Description>
   </PropertyGroup>
   ```
4. Create test project: `tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj`
5. Add xUnit, reference to CycloneDDS.Core

**Validation:**
- ✅ `dotnet build Src/CycloneDDS.Core` succeeds
- ✅ `dotnet build tests/CycloneDDS.Core.Tests` succeeds

**Estimated Time:** 30 minutes

---

### Task 2: CdrWriter Implementation (FCDC-S002)

**File:** `Src/CycloneDDS.Core/CdrWriter.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s002-cdrwriter-implementation)

**Description:**  
Implement the core `CdrWriter ref struct` that wraps `IBufferWriter<byte>` and provides XCDR2-compliant write operations.

**Design Reference:** [SERDATA-DESIGN.md §6.1](../docs/SERDATA-DESIGN.md#61-cdrwriter)

**Critical Implementation Details:**

1. **Alignment Tracking:** The `_totalWritten` field tracks **absolute stream position**, not just buffer position. This is critical for nested structs (see design-talk.md lines 2857-2873).

2. **Alignment Formula:**
   ```csharp
   int padding = (alignment - ((_totalWritten + _buffered) % alignment)) & (alignment - 1);
   ```
   This handles: 1-byte (no padding), 2-byte, 4-byte, 8-byte alignment per XCDR2 spec.

3. **String Encoding:**
   - Write 4-byte length header (includes NUL terminator in count)
   - Write UTF-8 bytes
   - Write NUL terminator (`\0`)
   - Example: `"Hello"` → `[0x06, 0x00, 0x00, 0x00, 'H', 'e', 'l', 'l', 'o', 0x00]`

**Must Implement:**
```csharp
public ref struct CdrWriter
{
    private IBufferWriter<byte> _output;
    private Span<byte> _span;
    private int _buffered;
    private int _totalWritten;  // CRITICAL: absolute position
    
    public CdrWriter(IBufferWriter<byte> output);
    public int Position => _totalWritten + _buffered;
    
    public void Align(int alignment);
    public void WriteInt32(int value);
    public void WriteUInt32(uint value);
    public void WriteInt64(long value);
    public void WriteUInt64(ulong value);
    public void WriteFloat(float value);
    public void WriteDouble(double value);
    public void WriteByte(byte value);
    public void WriteString(ReadOnlySpan<char> value);
    public void WriteFixedString(ReadOnlySpan<byte> utf8Bytes, int fixedSize);
    public void Complete();  // Flush buffered bytes
}
```

**Tests Required:** (Create `tests/CycloneDDS.Core.Tests/CdrWriterTests.cs`)

**Minimum 15-18 tests covering:**
1. ✅ Primitive alignment (int at offset 0, 1, 2, 3 → correct padding)
2. ✅ Double alignment (8-byte boundary)
3. ✅ String with length header + NUL
4. ✅ Fixed string (32 bytes, UTF-8, NUL-padded)
5. ✅ Buffer flush (write > span capacity, verify _totalWritten correct)
6. ✅ Position tracking across flushes
7. ✅ Alignment after flush
8. ✅ Little-endian encoding (write 0x12345678 → bytes [0x78, 0x56, 0x34, 0x12])
9. ✅ Multiple primitives in sequence (alignment between each)
10. ✅ Empty string (length = 1, just NUL)

**Quality Standard:** Tests must verify **actual byte output**, not just that methods don't throw.

**Estimated Time:** 3-4 hours

---

### Task 3: CdrReader Implementation (FCDC-S003)

**File:** `Src/CycloneDDS.Core/CdrReader.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s003-cdrreader-implementation)

**Description:**  
Implement the core `CdrReader ref struct` for deserialization.

**Design Reference:** [SERDATA-DESIGN.md §6.2](../docs/SERDATA-DESIGN.md#62-cdrreader)

**Must Implement:**
```csharp
public ref struct CdrReader
{
    private ReadOnlySpan<byte> _data;
    private int _position;
    
    public CdrReader(ReadOnlySpan<byte> data);
    public int Position => _position;
    public int Remaining => _data.Length - _position;
    
    public void Align(int alignment);
    public int ReadInt32();
    public uint ReadUInt32();
    public long ReadInt64();
    public ulong ReadUInt64();
    public float ReadFloat();
    public double ReadDouble();
    public byte ReadByte();
    public ReadOnlySpan<byte> ReadStringBytes();  // Returns UTF-8 bytes without NUL
    public ReadOnlySpan<byte> ReadFixedBytes(int count);
    public void Seek(int position);
}
```

**Tests Required:** (Create `tests/CycloneDDS.Core.Tests/CdrReaderTests.cs`)

**Minimum 12-15 tests covering:**
1. ✅ Read primitives with alignment
2. ✅ Read string (length header + UTF-8 bytes, NUL consumed)
3. ✅ Read fixed bytes
4. ✅ Seek (for skipping unknown fields)
5. ✅ Bounds checking (reading past end throws)
6. ✅ Alignment matches CdrWriter
7. ✅ Read after seek
8. ✅ Empty string (length = 1, returns empty span)

**Round-Trip Tests:** (Create `CdrRoundTripTests.cs`)

**Minimum 8-10 tests:**
1. ✅ Write int → Read int → assert equal
2. ✅ Write double → Read double → assert equal
3. ✅ Write string → Read string → assert equal
4. ✅ Write multiple aligned fields → Read all → assert equal
5. ✅ Write with padding → Read → verify position correct
6. ✅ Write struct (int + double) → Read → assert both fields
7. ✅ Write sequence header (uint32 count) → Read → assert count
8. ✅ Write little-endian multi-byte → Read → assert value correct
9. ✅ Write empty string → Read → assert empty
10. ✅ Write complex (byte + int + double) → Read → assert all (alignment traps)

**Estimated Time:** 3-4 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 30-35 tests

**Test Distribution:**
- CdrWriterTests: 15-18 tests
- CdrReaderTests: 12-15 tests
- CdrRoundTripTests: 8-10 tests

**Test Quality Standards:**

**✅ REQUIRED - Tests that verify ACTUAL correctness:**
- Verify byte output (expected hex vs actual hex)
- Verify alignment (serialize at offset 0 vs 1 vs 2 → different padding)
- Verify round-trip (Write → Read → assert equal)
- Verify little-endian encoding

**❌ INSUFFICIENT - Tests that prove nothing:**
- "Can I set a value" tests
- Tests that just check no exception thrown
- Tests that use Assert.Contains on byte arrays without checking order

**All tests must pass before submitting report.**

---

## 📊 Report Requirements

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Focus on Developer Insights:**

**Required Sections:**

1. **Implementation Summary**
   - Tasks completed (FCDC-S001, FCDC-S002, FCDC-S003)
   - Test counts (total, per file)

2. **Issues Encountered**
   - What problems did you run into?
   - How did you solve them?
   - Did alignment formulas work as expected?
   - Did endianness handling work correctly?

3. **Design Decisions Made**
   - Choices YOU made beyond the spec
   - Why you chose specific implementations
   - Alternatives you considered

4. **Weak Points Spotted**
   - What could be improved?
   - Any performance concerns?
   - API ergonomics issues?

5. **Edge Cases Discovered**
   - Scenarios not mentioned in spec
   - Boundary conditions

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S001** Complete: Projects created, build succeeds
- ✅ **FCDC-S002** Complete: CdrWriter implemented, 15-18 tests pass
- ✅ **FCDC-S003** Complete: CdrReader implemented, 12-15 tests pass, round-trip tests pass
- ✅ All 30-35 tests passing
- ✅ No compiler warnings
- ✅ Report submitted to `.dev-workstream/reports/BATCH-01-REPORT.md`

---

## ⚠️ Common Pitfalls to Avoid

1. **Alignment Off-by-One:** Forgetting that alignment padding happens BEFORE the field, not after
   - Wrong: `WriteInt32(value); Align(4);`
   - Right: `Align(4); WriteInt32(value);`

2. **Position Tracking:** Not tracking `_totalWritten` across buffer flushes
   - Consequence: Alignment breaks after first flush

3. **String NUL Terminator:** Forgetting to include NUL in length count or bytes
   - XCDR2 spec: Length includes NUL terminator

4. **Little-Endian:** Writing bytes in wrong order
   - Must use `BinaryPrimitives.WriteInt32LittleEndian`, not manual bit shifting

5. **Test Quality:** Writing tests that check string presence, not actual bytes
   - Must verify hex output, not just "code ran without exception"

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S001, S002, S003
- **Design Doc:** [SERDATA-DESIGN.md](../docs/SERDATA-DESIGN.md) - Sections 1-6
- **Design Context:** [design-talk.md](../docs/design-talk.md) - Lines 2850-3021
- **XCDR2 Spec:** `docs/dds-xtypes-1.3-xcdr2-1-single-file.htm` - Alignment rules, string encoding

---

**Next Batch:** BATCH-02 (CdrSizeCalculator + Golden Rig Validation) - Will validate correctness against Cyclone native
