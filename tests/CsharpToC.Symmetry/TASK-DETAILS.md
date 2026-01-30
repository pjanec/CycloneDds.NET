# CsharpToC.Symmetry - Implementation Task Breakdown

**Project:** FastCycloneDDS C# Bindings  
**Version:** 1.0  
**Date:** January 29, 2026  
**Reference:** [DESIGN.md](DESIGN.md) for architecture details

---

## Overview

This document breaks down the CsharpToC.Symmetry test framework implementation into discrete, actionable tasks. Each task has well-defined success conditions and references to the design document.

**Implementation Order:** Tasks should be completed sequentially within each phase. Phases can overlap slightly, but each phase's foundation tasks must complete before dependent tasks in later phases.

---

## Phase 1: Project Infrastructure

### SYM-001: Project Structure Setup

**Description:** Create folder structure and basic project files for the Symmetry test framework.

**References:** [DESIGN.md §2.2 - Directory Structure](DESIGN.md#22-directory-structure)

**Steps:**
1. Create folder: `tests/CsharpToC.Symmetry/`
2. Create subfolders: `Infrastructure/`, `Native/`, `Tests/`, `GoldenData/`
3. Create empty `.gitkeep` files to preserve folder structure
4. Create `.gitignore` entry for `GoldenData/*.txt` (generated files)

**Success Conditions:**
- ✅ Folder structure matches design document
- ✅ Git recognizes folders (via .gitkeep)
- ✅ `GoldenData` folder exists but is empty initially

**Estimated Time:** 5 minutes

---

### SYM-002: Main Project File (CsharpToC.Symmetry.csproj)

**Description:** Create the main test project file with correct dependencies and settings.

**References:** 
- [DESIGN.md §2.1 - System Components](DESIGN.md#21-system-components)
- Design doc section from cumulative doc showing .csproj structure

**Steps:**
1. Create `CsharpToC.Symmetry.csproj` in `tests/CsharpToC.Symmetry/`
2. Set `TargetFramework` to `net8.0`
3. Enable `AllowUnsafeBlocks` (required for `CdrWriter`)
4. Add package references:
   - `Microsoft.NET.Test.Sdk` (17.6.0+)
   - `xunit` (2.4.2+)
   - `xunit.runner.visualstudio` (2.4.5+)
5. Add project references:
   - `../../src/CycloneDDS.Core/CycloneDDS.Core.csproj`
   - `../../tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj` (ReferenceOutputAssembly=false)
6. Import CodeGen targets: `<Import Project="../../tools/CycloneDDS.CodeGen/CycloneDDS.targets" />`
7. Configure GoldenData folder to copy to output directory

**Success Conditions:**
- ✅ Project builds successfully: `dotnet build tests/CsharpToC.Symmetry/CsharpToC.Symmetry.csproj`
- ✅ CodeGen target is imported (check build output for CodeGen messages)
- ✅ No compilation errors related to unsafe code
- ✅ xUnit test framework is available (check project dependencies)

**Estimated Time:** 15 minutes

---

### SYM-003: Self-Test Project (CsharpToC.Symmetry.Tests.csproj)

**Description:** Create a separate test project for testing the Symmetry infrastructure itself (TDD approach).

**References:** [DESIGN.md §2.2 - Directory Structure](DESIGN.md#22-directory-structure)

**Steps:**
1. Create folder: `tests/CsharpToC.Symmetry.Tests/`
2. Create `CsharpToC.Symmetry.Tests.csproj`
3. Add project reference to `CsharpToC.Symmetry.csproj`
4. Add xUnit packages (same versions as SYM-002)
5. Create placeholder test file: `InfrastructureTests.cs` with one passing test

**Success Conditions:**
- ✅ Self-test project builds successfully
- ✅ Can run tests: `dotnet test tests/CsharpToC.Symmetry.Tests/`
- ✅ Placeholder test passes (verifies xUnit is working)

**Estimated Time:** 10 minutes

---

### SYM-004: Copy IDL Definitions

**Description:** Copy the atomic_tests.idl file from CsharpToC.Roundtrip to establish test case definitions.

**References:** [DESIGN.md §9.1 - Relationship to CsharpToC.Roundtrip](DESIGN.md#91-relationship-to-csharptocroundtrip)

**Steps:**
1. Locate `tests/CsharpToC.Roundtrip/atomic_tests.idl`
2. Copy to `tests/CsharpToC.Symmetry/atomic_tests.idl`
3. Add to `.csproj` file with `<None Update="atomic_tests.idl"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`
4. Verify CodeGen target picks up the IDL file

**Success Conditions:**
- ✅ IDL file exists in Symmetry project
- ✅ `dotnet build` triggers code generation (check for Generated/ folder in obj/)
- ✅ Generated C# files appear in `obj/Generated/AtomicTests/`
- ✅ No CodeGen errors in build output

**Estimated Time:** 5 minutes

---

## Phase 2: Core Infrastructure

### SYM-005: HexUtils Implementation

**Description:** Create utility class for converting between byte arrays and human-readable hex strings.

**References:** 
- [DESIGN.md §3.1 - Phase 0: Golden Data Generation](DESIGN.md#31-phase-0-one-time-setup-golden-data-generation)
- Cumulative doc showing HexUtils class structure

**Implementation File:** `Infrastructure/HexUtils.cs`

**Required Methods:**
```csharp
public static string ToHexString(byte[] bytes)
public static byte[] FromHexString(string hex)
```

**Steps:**
1. Create `Infrastructure/HexUtils.cs`
2. Implement `ToHexString`: Convert bytes to space-separated hex (e.g., "00 1A FF")
3. Implement `FromHexString`: Parse hex string back to bytes (handle spaces, dashes, newlines)
4. Handle edge cases: null input, empty string, invalid hex characters

**Success Conditions:**
- ✅ `ToHexString(new byte[] {0x00, 0x1A, 0xFF})` returns `"00 1A FF"`
- ✅ `FromHexString("00 1A FF")` returns `{0x00, 0x1A, 0xFF}`
- ✅ `FromHexString("00-1A-FF")` returns same (handle different separators)
- ✅ Unit tests in `CsharpToC.Symmetry.Tests/InfrastructureTests.cs` pass
- ✅ Round-trip: `FromHexString(ToHexString(bytes))` == `bytes`

**Test Cases to Write:**
```csharp
[Fact] public void ToHexString_EmptyArray_ReturnsEmptyString()
[Fact] public void ToHexString_SingleByte_ReturnsCorrectFormat()
[Fact] public void FromHexString_SpaceSeparated_ParsesCorrectly()
[Fact] public void FromHexString_DashSeparated_ParsesCorrectly()
[Fact] public void FromHexString_InvalidHex_ThrowsException()
[Fact] public void RoundTrip_PreservesBytes()
```

**Estimated Time:** 30 minutes

---

### SYM-006: Native Wrapper (P/Invoke)

**Description:** Create P/Invoke wrapper to call native DLL for golden data generation.

**References:** 
- [DESIGN.md §3.1 - Phase 0: Golden Data Generation](DESIGN.md#31-phase-0-one-time-setup-golden-data-generation)
- Cumulative doc showing NativeMethods class

**Implementation File:** `Native/NativeWrapper.cs`

**Required P/Invoke:**
```csharp
[DllImport("ddsc_test_lib", CallingConvention = CallingConvention.Cdecl)]
public static extern int Native_GeneratePayload(string topicName, int seed, out IntPtr buffer);

[DllImport("ddsc_test_lib", CallingConvention = CallingConvention.Cdecl)]
public static extern void Native_FreeBuffer(IntPtr buffer);
```

**Steps:**
1. Create `Native/NativeWrapper.cs`
2. Define P/Invoke signatures matching native DLL API
3. Create managed wrapper method that handles IntPtr and marshaling:
   ```csharp
   public static byte[] GeneratePayload(string topicName, int seed)
   ```
4. Implement proper memory management (free native buffer after copying)
5. Add error handling for DLL not found, null buffer, negative length

**Success Conditions:**
- ✅ Compiles without P/Invoke signature errors
- ✅ Can call `GeneratePayload("AtomicTests::CharTopic", 1420)` successfully
- ✅ Returns valid byte array (length > 0)
- ✅ No memory leaks (verify with repeated calls)
- ✅ Clear exception when DLL missing

**Note:** Requires `ddsc_test_lib.dll` in output directory. Copy from CsharpToC.Roundtrip or build from native source.

**Estimated Time:** 45 minutes

---

### SYM-007: GoldenDataLoader Implementation

**Description:** Create the core class responsible for loading/generating golden CDR data files.

**References:** 
- [DESIGN.md §3.1 - Phase 0: Golden Data Generation](DESIGN.md#31-phase-0-one-time-setup-golden-data-generation)
- Cumulative doc showing GoldenDataLoader class

**Implementation File:** `Infrastructure/GoldenDataLoader.cs`

**Required Methods:**
```csharp
public static byte[] GetOrGenerate(string topicName, int seed)
private static string GetFileName(string topicName)
private static byte[] LoadFromFile(string fileName)
private static void SaveToFile(string fileName, byte[] bytes)
```

**Algorithm:**
1. Compute file path: `GoldenData/{topicName}.txt` (sanitize name)
2. If file exists: Load, parse hex, return bytes
3. If file missing:
   - Call `NativeWrapper.GeneratePayload(topicName, seed)`
   - Convert bytes to hex string
   - Save to file
   - Return bytes

**Steps:**
1. Create `Infrastructure/GoldenDataLoader.cs`
2. Implement file path logic (replace `:` with `_` in topic name)
3. Implement load logic using `HexUtils.FromHexString`
4. Implement save logic using `HexUtils.ToHexString`
5. Implement generation logic using `NativeWrapper`
6. Add thread-safety (potential concurrent test execution)
7. Add logging/diagnostics for debugging

**Success Conditions:**
- ✅ First call to `GetOrGenerate("AtomicTests::CharTopic", 1420)` generates file
- ✅ File appears in `GoldenData/AtomicTests_CharTopic.txt`
- ✅ Second call to same topic loads from file (fast)
- ✅ File contains valid hex string
- ✅ Round-trip: Load file, parse to bytes, matches original
- ✅ Handles missing GoldenData folder gracefully (creates it)

**Estimated Time:** 45 minutes

---

### SYM-008: DataGenerator Implementation (Seed-Based)

**Description:** Port the deterministic data generation algorithm from native C to C#.

**References:** 
- [DESIGN.md §4.1 - Deterministic Seeding](DESIGN.md#41-deterministic-seeding)
- Original roundtrip design doc explaining seed algorithm

**Implementation File:** `Infrastructure/DataGenerator.cs`

**Required Methods:**
```csharp
public static T Create<T>(int seed) where T : new()
private static object GenerateValue(Type type, int seed, int fieldIndex)
```

**Algorithm (must match C implementation exactly):**
```
Primitives: value = seed + fieldIndex
Floats: value = (seed + fieldIndex) + 0.5
Strings: value = $"Str_{seed}_{fieldIndex}"
Enums: value = (seed + fieldIndex) % enumCount
Collections: length = (seed % 5) + 1
```

**Steps:**
1. Create `Infrastructure/DataGenerator.cs`
2. Implement primitive generation (int, long, float, double, etc.)
3. Implement string generation
4. Implement enum generation
5. Implement array/sequence generation (recursive)
6. Use reflection to populate object fields
7. Add special handling for nested structs

**Success Conditions:**
- ✅ `Create<CharTopic>(1420).Value` matches expected char
- ✅ `Create<ArrayStringTopic>(1420).Strings[0]` == `"Str_1420_0"`
- ✅ Generated object can serialize/deserialize without errors
- ✅ Same seed produces identical object every time
- ✅ Different seeds produce different but valid objects

**Testing Approach:**
- Create unit tests comparing C# generator output with deserialized golden data
- Verify field-by-field equivalence

**Estimated Time:** 90 minutes

---

### SYM-009: SymmetryTestBase Implementation

**Description:** Create the abstract base class containing the core symmetry verification logic.

**References:** 
- [DESIGN.md §3.2 - Phase 1: Fast Iteration Loop](DESIGN.md#32-phase-1-fast-iteration-loop-hot-patch-development)
- [DESIGN.md §6.1 - Multi-Layer Verification](DESIGN.md#61-multi-layer-verification)
- Cumulative doc showing SymmetryTestBase class

**Implementation File:** `Infrastructure/SymmetryTestBase.cs`

**Required Methods:**
```csharp
protected void VerifySymmetry<T>(
    string topicName, 
    int seed, 
    Func<CdrReader, T> deserializer, 
    Action<T, CdrWriter> serializer,
    CdrEncoding? encoding = null)
```

**Algorithm:**
1. Load golden bytes via `GoldenDataLoader.GetOrGenerate(topicName, seed)`
2. Detect encoding from golden bytes if not specified (byte[1] indicates XCDR1 vs XCDR2)
3. Deserialize: `CdrReader reader = new(golden); T obj = deserializer(reader);`
4. (Optional) Validate object using `DataGenerator.Create<T>(seed)`
5. Serialize: `CdrWriter writer = new(buffer, encoding); serializer(obj, writer);`
6. Compare bytes: `Assert.Equal(golden, writer.ToArray())`

**Steps:**
1. Create `Infrastructure/SymmetryTestBase.cs` (abstract class)
2. Implement `VerifySymmetry` method
3. Add encoding detection logic
4. Add detailed error messages (show hex diff on failure)
5. Add optional validation step (can be disabled for performance)
6. Add exception handling with context (which test, which phase failed)

**Success Conditions:**
- ✅ Can call `VerifySymmetry<CharTopic>(...)` from derived test class
- ✅ Detects XCDR1 vs XCDR2 encoding automatically
- ✅ On deserialization failure, shows clear error message
- ✅ On serialization mismatch, shows hex diff (expected vs actual)
- ✅ Execution time < 50ms per test (performance requirement)

**Error Message Format:**
```
Symmetry test failed for AtomicTests::ArrayStringTopic
Phase: Serialization
Expected: 00 01 00 00 42 00 53 74 72 ...
Actual:   00 01 00 00 42 00 00 00 53 74 72 ...
                              ^^^ Extra padding detected
```

**Estimated Time:** 60 minutes

---

## Phase 3: Test Implementation

### SYM-010: Test Case Discovery & Organization

**Description:** Analyze the IDL file and organize test cases into logical groups.

**References:** [DESIGN.md §5.1 - Test Partitioning](DESIGN.md#51-test-partitioning)

**Steps:**
1. Review `atomic_tests.idl` to identify all topics
2. Create test classification spreadsheet/document:
   - Part 1: Primitive types (~30 tests)
   - Part 2: Collection types (~40 tests)
   - Part 3: Complex types (~25 tests)
   - Part 4: XTypes extensions (~15 tests)
3. Assign seed values for each test case (vary to exercise edge cases)
4. Create test file structure: `Tests/Part1_PrimitiveTests.cs`, etc.

**Success Conditions:**
- ✅ All topics from IDL are categorized
- ✅ Test file structure created (empty test methods)
- ✅ Seed values documented in comments
- ✅ No duplicate test names

**Deliverable:** Test matrix document or commented test files showing:
```csharp
// Part1: Primitive Types (30 tests, ~0.5s)
// - CharTopic (Seed: 1420)
// - Int32Topic (Seed: 1420)
// - Int64TopicAppendable (Seed: 65535) -- edge case
```

**Estimated Time:** 45 minutes

---

### SYM-011: Part 1 - Primitive Type Tests

**Description:** Implement tests for basic primitive types (char, int, float, string, etc.).

**References:** [DESIGN.md §5.1 - Test Partitioning](DESIGN.md#51-test-partitioning)

**Implementation File:** `Tests/Part1_PrimitiveTests.cs`

**Test Template:**
```csharp
[Fact]
public void TestCharTopic()
{
    VerifySymmetry<CharTopic>(
        "AtomicTests::CharTopic",
        seed: 1420,
        reader => CharTopic.Deserialize(ref reader),
        (obj, writer) => obj.Serialize(ref writer)
    );
}
```

**Steps:**
1. Create `Tests/Part1_PrimitiveTests.cs` inheriting from `SymmetryTestBase`
2. Implement test methods for:
   - CharTopic, Int8Topic, Int16Topic, Int32Topic, Int64Topic
   - UInt8Topic, UInt16Topic, UInt32Topic, UInt64Topic
   - FloatTopic, DoubleTopic
   - StringTopic, BoundedStringTopic
   - Same types with `_Appendable` variants (XCDR2)
3. Run tests individually to verify
4. Capture any failures for later fixing

**Success Conditions:**
- ✅ All primitive tests compile
- ✅ Tests can be run individually: `dotnet test --filter "TestCharTopic"`
- ✅ Golden data files generated for all primitive topics
- ✅ At least 50% of primitive tests pass (some failures expected initially)

**Expected Issues:**
- Alignment bugs in generated code
- Encoding detection issues
- XCDR2 header mismatches

**Estimated Time:** 90 minutes

---

### SYM-012: Part 2 - Collection Type Tests

**Description:** Implement tests for arrays and sequences.

**References:** [DESIGN.md §5.1 - Test Partitioning](DESIGN.md#51-test-partitioning)

**Implementation File:** `Tests/Part2_CollectionTests.cs`

**Steps:**
1. Create `Tests/Part2_CollectionTests.cs`
2. Implement array tests:
   - ArrayInt32Topic, ArrayStringTopic
   - Fixed-size arrays with different element types
   - Multi-dimensional arrays
3. Implement sequence tests:
   - SequenceInt32Topic, SequenceStringTopic
   - Variable-length sequences
   - Nested sequences
4. Test both XCDR1 (@final) and XCDR2 (@appendable) variants

**Success Conditions:**
- ✅ All collection tests compile
- ✅ Golden data files generated
- ✅ Can run category: `dotnet test --filter "Part2"`
- ✅ At least 40% of tests pass (collection logic is complex)

**Known Challenges:**
- Sequence length encoding (DHEADER in XCDR2)
- Element alignment within arrays
- String termination in sequences

**Estimated Time:** 90 minutes

---

### SYM-013: Part 3 - Complex Type Tests

**Description:** Implement tests for unions, nested structs, optional members, and key fields.

**References:** [DESIGN.md §5.1 - Test Partitioning](DESIGN.md#51-test-partitioning)

**Implementation File:** `Tests/Part3_ComplexTests.cs`

**Steps:**
1. Create `Tests/Part3_ComplexTests.cs`
2. Implement union tests (various discriminator types)
3. Implement nested struct tests (2-3 levels deep)
4. Implement optional member tests (@optional attribute)
5. Implement key field tests (@key attribute)

**Success Conditions:**
- ✅ All complex tests compile
- ✅ Golden data files generated
- ✅ Can run category: `dotnet test --filter "Part3"`
- ✅ At least 30% of tests pass (unions are notoriously tricky)

**Known Challenges:**
- Union discriminator alignment
- Nested struct member header calculation
- Optional member presence flag encoding

**Estimated Time:** 90 minutes

---

### SYM-014: Part 4 - XTypes Extension Tests

**Description:** Implement tests for advanced extensibility features (appendable, mutable).

**References:** [DESIGN.md §5.1 - Test Partitioning](DESIGN.md#51-test-partitioning)

**Implementation File:** `Tests/Part4_XTypesTests.cs`

**Steps:**
1. Create `Tests/Part4_XTypesTests.cs`
2. Implement @appendable tests (with DHEADER)
3. Implement @mutable tests (with EMHEADER + NEXTINT)
4. Implement mixed extensibility scenarios
5. Test inheritance cases if applicable

**Success Conditions:**
- ✅ All XTypes tests compile
- ✅ Golden data files generated
- ✅ Can run category: `dotnet test --filter "Part4"`
- ✅ At least 25% of tests pass (XCDR2 is newest, most complex)

**Known Challenges:**
- DHEADER size calculation
- EMHEADER + NEXTINT proper placement
- Member ID encoding

**Estimated Time:** 90 minutes

---

## Phase 4: Automation & Tooling

### SYM-015: PowerShell Script - rebuild_and_test.ps1

**Description:** Create script for full rebuild + code generation + test execution cycle.

**References:** [DESIGN.md §8.1 - Script: rebuild_and_test.ps1](DESIGN.md#81-script-rebuild_and_testps1)

**Implementation File:** `tests/CsharpToC.Symmetry/rebuild_and_test.ps1`

**Features:**
- Clean previous build artifacts
- Build project (triggers CodeGen)
- Run tests with optional filter
- Color-coded output
- Clear success/failure summary

**Parameters:**
```powershell
param(
    [string]$Filter = "",        # xUnit filter expression
    [switch]$Verbose             # Detailed output
)
```

**Steps:**
1. Create `rebuild_and_test.ps1`
2. Implement clean step: `dotnet clean`
3. Implement build step: `dotnet build -c Debug`
4. Implement test step: `dotnet test --filter` (if filter provided)
5. Add error handling (stop on build failure)
6. Add colored output using `Write-Host -ForegroundColor`
7. Add execution timer

**Success Conditions:**
- ✅ Running `.\rebuild_and_test.ps1` builds and tests successfully
- ✅ `.\rebuild_and_test.ps1 -Filter "Part1"` runs only Part1 tests
- ✅ `.\rebuild_and_test.ps1 -Filter "TestCharTopic"` runs single test
- ✅ Build errors stop execution before tests run
- ✅ Shows clear summary at end (X passed, Y failed, Z skipped)

**Estimated Time:** 45 minutes

---

### SYM-016: PowerShell Script - run_tests_only.ps1

**Description:** Create script for hot-patch mode (no rebuild, just test execution).

**References:** [DESIGN.md §8.2 - Script: run_tests_only.ps1](DESIGN.md#82-script-run_tests_onlyps1)

**Implementation File:** `tests/CsharpToC.Symmetry/run_tests_only.ps1`

**Features:**
- Skip build/CodeGen
- Use existing compiled binaries
- Fast execution (2-5 seconds)
- Same filter support as rebuild script

**Key Difference:** Uses `dotnet test --no-build` flag

**Steps:**
1. Create `run_tests_only.ps1`
2. Implement test execution with `--no-build`
3. Add warning if `bin/` folder missing (need to build first)
4. Add same filtering and output formatting as rebuild script
5. Add execution timer showing speed improvement

**Success Conditions:**
- ✅ `.\run_tests_only.ps1` runs all tests in ~5 seconds
- ✅ `.\run_tests_only.ps1 -Filter "TestCharTopic"` runs in ~2 seconds
- ✅ Shows warning if project not built yet
- ✅ Manual edits to `obj/Generated/*.cs` files are picked up
- ✅ Clearly labeled as "HOT-PATCH MODE" in output

**Estimated Time:** 30 minutes

---

### SYM-017: PowerShell Script - generate_golden_data.ps1

**Description:** Create script to regenerate all golden data files from scratch.

**References:** [DESIGN.md §8.3 - Script: generate_golden_data.ps1](DESIGN.md#83-script-generate_golden_dataps1)

**Implementation File:** `tests/CsharpToC.Symmetry/generate_golden_data.ps1`

**Features:**
- Delete existing golden data files
- Run tests to trigger regeneration
- Validate all files created
- Option to regenerate specific test

**Parameters:**
```powershell
param(
    [string]$Filter = "",        # Regenerate specific test
    [switch]$Force               # Skip confirmation prompt
)
```

**Steps:**
1. Create `generate_golden_data.ps1`
2. Add confirmation prompt (deleting files)
3. Implement deletion logic (remove `GoldenData/*.txt`)
4. Rebuild if needed (ensure native DLL is available)
5. Run tests to trigger generation
6. Validate all expected files exist
7. Show summary of generated files

**Success Conditions:**
- ✅ `.\generate_golden_data.ps1 -Force` deletes and regenerates all files
- ✅ `.\generate_golden_data.ps1 -Filter "Part1" -Force` regenerates Part1 only
- ✅ Shows progress (X of Y files generated)
- ✅ Warns if any files failed to generate
- ✅ Validates file content (non-empty, valid hex)

**Estimated Time:** 45 minutes

---

## Phase 5: Documentation

### SYM-018: Fast Iteration Guide

**Description:** Create comprehensive guide for developers on using the hot-patch workflow.

**References:** 
- [DESIGN.md §7 - Hot-Patch Workflow](DESIGN.md#7-hot-patch-workflow)
- Cumulative doc explaining fast iteration process

**Implementation File:** `tests/CsharpToC.Symmetry/FAST-ITERATION-GUIDE.md`

**Content Structure:**
1. **Quick Start:** 5-minute setup guide
2. **Typical Workflow:** Step-by-step example fixing a failing test
3. **Hot-Patch Technique:** Detailed explanation of editing generated code
4. **Emitter Backport:** How to translate fixes to emitter code
5. **Troubleshooting:** Common issues and solutions
6. **Best Practices:** Do's and don'ts
7. **Performance Tips:** Maximizing iteration speed

**Key Sections:**

#### Quick Start
```markdown
1. Clone repository
2. Run: .\rebuild_and_test.ps1
3. Note failing tests
4. Open obj/Generated/TopicName.Serializer.cs
5. Edit code, save
6. Run: .\run_tests_only.ps1 -Filter "TopicName"
7. Repeat 5-6 until test passes
```

#### Example Walkthrough
Full walkthrough of fixing a real failing test (e.g., ArrayStringAppendable):
- What the error looks like
- How to read the hex diff
- What code to change
- How to verify the fix
- How to update the emitter

**Success Conditions:**
- ✅ New developer can follow guide and fix first failing test in < 30 minutes
- ✅ All scripts referenced are explained
- ✅ Includes screenshots or code snippets
- ✅ Covers both deserialization and serialization bugs
- ✅ Explains when to use which script

**Estimated Time:** 120 minutes

---

### SYM-019: Task Tracker Document

**Description:** Create task tracker document following the project's TASK-TRACKER.md format.

**References:** 
- [DESIGN.md](DESIGN.md) (full document)
- `D:\WORK\FastCycloneDdsCsharpBindings\.dev-workstream\TASK-TRACKER.md` (format example)

**Implementation File:** `tests/CsharpToC.Symmetry/TASK-TRACKER.md`

**Content Structure:**
```markdown
# CsharpToC.Symmetry - Task Tracker

**Status:** [In Progress / Complete]
**Last Updated:** [Date]

## Phase 1: Project Infrastructure ✅/🚧/⏳
- [ ] SYM-001: Project Structure Setup
- [ ] SYM-002: Main Project File
...

## Phase 2: Core Infrastructure
...

## Current Sprint
[What's being worked on now]

## Completed Milestones
[Major achievements]

## Blockers
[Any blocking issues]
```

**Steps:**
1. Create `TASK-TRACKER.md`
2. List all tasks from this document
3. Add status checkboxes
4. Group by phase
5. Add progress indicators (✅ complete, 🚧 in progress, ⏳ not started)
6. Include links to this task details document
7. Add section for current sprint focus
8. Add section for blockers/issues

**Success Conditions:**
- ✅ All tasks from SYM-001 through SYM-019 are listed
- ✅ Format matches existing project task trackers
- ✅ Includes progress visualization (percentage complete)
- ✅ Links to DESIGN.md and TASK-DETAILS.md (this document)
- ✅ Easy to update (checkboxes, clear structure)

**Estimated Time:** 45 minutes

---

### SYM-020: README Documentation

**Description:** Create top-level README for the Symmetry test project.

**Implementation File:** `tests/CsharpToC.Symmetry/README.md`

**Content Structure:**
1. **Project Overview:** What is this project?
2. **Quick Start:** Get running in 5 minutes
3. **Architecture:** High-level diagram and concepts
4. **Running Tests:** Command examples
5. **Developer Workflow:** Link to FAST-ITERATION-GUIDE.md
6. **Documentation Index:** Links to all documents
7. **FAQ:** Common questions

**Success Conditions:**
- ✅ Developer can understand project purpose in 2 minutes of reading
- ✅ Clear links to all documentation
- ✅ Includes usage examples for all scripts
- ✅ Explains relationship to CsharpToC.Roundtrip
- ✅ Professional formatting with badges/shields (optional)

**Estimated Time:** 60 minutes

---

## Phase 6: Validation & Polish

### SYM-021: End-to-End Integration Test

**Description:** Validate the entire workflow from scratch (simulate new developer experience).

**Steps:**
1. Delete all generated artifacts (`bin/`, `obj/`, `GoldenData/`)
2. Run `.\rebuild_and_test.ps1` on clean machine (or in fresh clone)
3. Verify:
   - Project builds successfully
   - Code generation works
   - Native DLL is found
   - Golden data files are created
   - Tests execute and show results
4. Document any setup issues encountered
5. Fix any missing dependencies or unclear error messages

**Success Conditions:**
- ✅ Clean build succeeds without manual intervention
- ✅ All 110 golden data files are generated
- ✅ Test results are accurate (known failures match expectations)
- ✅ Scripts work on both developer machine and CI environment
- ✅ No confusing error messages

**Estimated Time:** 90 minutes

---

### SYM-022: Performance Benchmarking

**Description:** Measure and validate that performance goals are met.

**References:** [DESIGN.md §10.1 - Performance Goals](DESIGN.md#101-performance-goals)

**Metrics to Measure:**
1. Single test execution time (should be < 50ms)
2. Full suite execution time (should be < 10 seconds)
3. Golden data generation time (should be < 60 seconds)
4. Hot-patch iteration cycle time (should be < 5 seconds)

**Steps:**
1. Create performance test script: `measure_performance.ps1`
2. Run benchmarks multiple times and average results
3. Compare against design goals
4. Identify any bottlenecks
5. Document results in performance report

**Deliverable:** `PERFORMANCE-REPORT.md` with:
- Benchmark results
- Comparison to goals
- Performance optimization opportunities
- System specs used for testing

**Success Conditions:**
- ✅ All performance goals from design document are met or exceeded
- ✅ Results documented with charts/tables
- ✅ Comparison to CsharpToC.Roundtrip times shows 99% improvement

**Estimated Time:** 60 minutes

---

### SYM-023: Documentation Review & Polish

**Description:** Final review and polish of all documentation.

**Steps:**
1. Spell check all markdown files
2. Validate all internal links (between documents)
3. Ensure consistent formatting and terminology
4. Add table of contents to longer documents
5. Verify code snippets compile and run
6. Add diagrams where helpful (Mermaid or ASCII art)
7. Peer review by another developer

**Checklist:**
- [ ] All links work
- [ ] No spelling errors
- [ ] Code examples are accurate
- [ ] Consistent terminology across documents
- [ ] Clear section headings
- [ ] Appropriate level of detail for audience

**Success Conditions:**
- ✅ All documentation passes review
- ✅ External reviewer can follow guides without confusion
- ✅ No broken links or missing references

**Estimated Time:** 90 minutes

---

## Summary

### Task Overview

| Phase | Tasks | Estimated Time |
|-------|-------|----------------|
| 1. Project Infrastructure | SYM-001 to SYM-004 | 35 minutes |
| 2. Core Infrastructure | SYM-005 to SYM-009 | 4.5 hours |
| 3. Test Implementation | SYM-010 to SYM-014 | 6.5 hours |
| 4. Automation & Tooling | SYM-015 to SYM-017 | 2 hours |
| 5. Documentation | SYM-018 to SYM-020 | 3.75 hours |
| 6. Validation & Polish | SYM-021 to SYM-023 | 4 hours |
| **Total** | **23 tasks** | **~21 hours** |

### Critical Path

The following tasks are on the critical path and must be completed in order:

1. **SYM-001** → **SYM-002** → **SYM-004** (Project setup)
2. **SYM-005** → **SYM-006** → **SYM-007** (Data loading pipeline)
3. **SYM-008** → **SYM-009** (Test infrastructure)
4. **SYM-010** → **SYM-011** (First working tests)

Once **SYM-011** is complete, the remaining test implementation (SYM-012 to SYM-014) can proceed in parallel with automation (SYM-015 to SYM-017).

### Dependencies Graph

```
SYM-001 (Structure)
    ↓
SYM-002 (Project File) ─┬─→ SYM-004 (IDL) ─→ SYM-010 (Test Discovery)
    ↓                   │
SYM-003 (Self-Test) ────┘
    ↓
SYM-005 (HexUtils)
    ↓
SYM-006 (Native Wrapper)
    ↓
SYM-007 (GoldenDataLoader)
    ↓
SYM-008 (DataGenerator)
    ↓
SYM-009 (SymmetryTestBase)
    ↓
SYM-010 (Test Discovery)
    ↓
SYM-011 (Part1) ──┬─→ SYM-015 (rebuild script)
    ↓             │
SYM-012 (Part2) ──┼─→ SYM-016 (run_tests_only script)
    ↓             │
SYM-013 (Part3) ──┼─→ SYM-017 (generate_golden script)
    ↓             │
SYM-014 (Part4) ──┤
                  ↓
            SYM-018 (Fast Iteration Guide)
                  ↓
            SYM-019 (Task Tracker)
                  ↓
            SYM-020 (README)
                  ↓
            SYM-021 (Integration Test)
                  ↓
            SYM-022 (Performance)
                  ↓
            SYM-023 (Doc Review)
```

---

## Next Steps

After completing all tasks:

1. **Code Review:** Submit for team review
2. **CI Integration:** Add to continuous integration pipeline
3. **Team Training:** Conduct workshop on hot-patch workflow
4. **Metrics Collection:** Track usage and iteration speed improvements
5. **Feedback Loop:** Gather developer feedback and iterate

---

**Document Status:** ✅ Ready for Implementation  
**Next Step:** Begin with SYM-001 or see [TASK-TRACKER.md](TASK-TRACKER.md) for current status
