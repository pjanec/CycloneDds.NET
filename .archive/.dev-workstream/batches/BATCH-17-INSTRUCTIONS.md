# BATCH-17: Advanced IDL Generation Control

**Batch Number:** BATCH-17  
**Tasks:** FCDC-S025 (Advanced IDL Generation Control)  
**Phase:** Stage 2 - Code Generation Enhancements  
**Estimated Effort:** 5-7 days  
**Priority:** 🔴 CRITICAL (Essential for real-world DDS systems)  
**Dependencies:** BATCH-16 complete (nested struct support)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to BATCH-17! This is a **complex, multi-phase refactor** of the code generator's IDL emission system.

**Goal:** Transform from "one type = one IDL file" to registry-based generation with smart grouping, module overrides, and cross-assembly dependency resolution.

**Why Critical:** Real DDS systems need:
- Multiple types in one IDL file (e.g., `CommonTypes.idl`)
- Legacy interop (override C# namespace → IDL module mapping)
- Cross-assembly references with automatic `#include` handling
- IDL files that travel with compiled DLLs

### Required Reading (IN ORDER)

**🚨 READ THESE BEFORE STARTING - This is complex architecture:**

1. **Workflow Guide:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   - Batch system, report requirements

2. **Task Definition:** `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   - Lines 1088-1200: FCDC-S025 task overview

3. **Design Document:** `d:\Work\FastCycloneDdsCsharpBindings\docs\ADVANCED-IDL-GENERATION-DESIGN.md` ← **CRITICAL**  
   - **READ ENTIRE FILE (1002 lines)**
   - Section 1: Problem statement and solution overview
   - Section 2: New attributes (DdsIdlFile, DdsIdlModule, DdsIdlMapping)
   - Section 3: Three-phase generation architecture
   - Section 4: Detailed implementation with code patterns
   - Section 5-6: Usage examples and build integration
   - Section 7: Error messages and edge cases
   - Section 8: Testing requirements
   
4. **Previous Review:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-16-REVIEW.md`  
   - Understand test quality expectations

### Repository Structure

```
d:\Work\FastCycloneDdsCsharpBindings\
├── src\
│   └── CycloneDDS.Schema\
│       └── Attributes\
│           └── TypeLevel\
│               ├── DdsIdlFileAttribute.cs      # ← NEW FILE (Task 1)
│               ├── DdsIdlModuleAttribute.cs    # ← NEW FILE (Task 2)
│               └── DdsIdlMappingAttribute.cs   # ← NEW FILE (Task 3)
│
├── tools\
│   └── CycloneDDS.CodeGen\
│       ├── IdlTypeDefinition.cs     # ← NEW FILE (Task 4)
│       ├── GlobalTypeRegistry.cs    # ← NEW FILE (Task 5)
│       ├── SchemaDiscovery.cs       # ← MODIFY (Task 6)
│       ├── CodeGenerator.cs         # ← MAJOR REFACTOR (Task 7)
│       ├── IdlEmitter.cs            # ← MAJOR REFACTOR (Task 8)
│       └── CycloneDDS.targets       # ← MODIFY (Task 9)
│
├── tests\
│   └── CycloneDDS.CodeGen.Tests\
│       ├── IdlGenerationTests.cs       # ← NEW FILE (Task 10)
│       └── CrossAssemblyTests.cs       # ← NEW FILE (Task 11)
│
└── docs\
    └── ADVANCED-IDL-GENERATION-DESIGN.md  # ← YOUR REFERENCE
```

### Critical Tool Locations

**IDL Compiler:**
- **Location:** `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe`
- **Usage:** Runtime path calculation (BATCH-15.3 pattern)

**Build Order:**
```bash
# 1. Schema (attributes)
dotnet build d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj

# 2. Code Generator
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

# 3. Tests
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj

# 4. Run all tests
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj
```

### Report Submission

**When done, submit your report to:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-17-REPORT.md`

**Use template:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

**If you have questions before starting, create:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-17-QUESTIONS.md`

---

## Context

### Why This Batch Matters

**Current Limitation:**
- One C# type → one IDL file (e.g., `Point3D.cs` → `Point3D.idl`)
- C# namespace directly becomes IDL module (no override)
- No cross-assembly dependency tracking
- No automatic `#include` generation

**Real-World Problem:**
```csharp
// Assembly A (Corp.Common.dll)
namespace Corp.Common.Geometry {
    [DdsStruct] public partial struct Point3D { ... }
}

// Assembly B (Robot.Control.dll)
using Corp.Common.Geometry;

[DdsTopic("Path")]
public partial struct RobotPath {
    public Point3D StartLocation;  // From Assembly A
}
```

**Today:** B's IDL has no `#include` for A → idlc fails  
**After S025:** B's IDL automatically has `#include "Geometry.idl"`

### Related Task

- [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control) - Advanced IDL Generation Control

---

## 🎯 Batch Objectives

**You will accomplish:**

1. ✅ Add 3 new attributes to CycloneDDS.Schema
2. ✅ Create GlobalTypeRegistry for type-to-IDL mapping
3. ✅ Refactor IdlEmitter to support file grouping and dependencies
4. ✅ Implement cross-assembly metadata resolution
5. ✅ Add MSBuild targets for IDL file copying
6. ✅ Write 17 tests (12 unit + 5 integration)

**Success:** Users can group types, override modules, and use cross-assembly types seamlessly.

---

## ✅ Tasks

### 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Tasks 1-3:** Attributes → Build → Verify compilation ✅
2. **Tasks 4-5:** Registry classes → Unit tests → **ALL tests pass** ✅  
3. **Task 6:** Discovery integration → Tests → **ALL tests pass** ✅
4. **Tasks 7-8:** Code generator refactor → Integration tests → **ALL tests pass** ✅
5. **Task 9:** Build integration → Manual verification ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

**Why:** Each component must be solid before building on top. Prevents cascading failures.

---

### Task 1: Add DdsIdlFileAttribute (FCDC-S025 Part 1)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\Attributes\TypeLevel\DdsIdlFileAttribute.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Create attribute for controlling IDL file grouping.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 2.1** (lines 41-77)
- Implement exactly as specified
- Include XML documentation
- Validation: filename cannot contain extension, path separators, or be empty

**Tests Required:**
- ✅ Attribute compiles and can be applied to structs

---

### Task 2: Add DdsIdlModuleAttribute (FCDC-S025 Part 2)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\Attributes\TypeLevel\DdsIdlModuleAttribute.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Create attribute for overriding IDL module hierarchy.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 2.2** (lines 79-115)
- Use `::` as separator (IDL syntax, not C# `.` syntax)
- Include XML documentation with legacy interop example

**Tests Required:**
- ✅ Attribute compiles and accepts module paths with `::`

---

### Task 3: Add DdsIdlMappingAttribute (FCDC-S025 Part 3)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\Attributes\TypeLevel\DdsIdlMappingAttribute.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Internal attribute for cross-assembly metadata (auto-generated by code generator).

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 2.3** (lines 117-151)
- `AttributeUsage`: Assembly-level, AllowMultiple = true
- Stores: TypeFullName, IdlFileName, IdlModule

**Tests Required:**
- ✅ Attribute compiles and can be applied to assemblies

---

### Task 4: Create IdlTypeDefinition Class (FCDC-S025 Part 4)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\IdlTypeDefinition.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Data class representing a type's IDL mapping.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 3.2** (lines 179-186)
- Properties: CSharpFullName, TargetIdlFile, TargetModule, TypeInfo, IsExternal, SourceFile

**Tests Required:**
- ✅ Class instantiates correctly
- ✅ Properties are settable

---

### Task 5: Create GlobalTypeRegistry Class (FCDC-S025 Part 5)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\GlobalTypeRegistry.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Central registry for all discovered types (local and external).

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 3.2** (lines 189-225)
- Methods: RegisterLocal(), RegisterExternal(), TryGetDefinition()
- Property: LocalTypes (non-external only)
- **Edge Case Handling:** IDL name collision detection (Section 7, lines 819-905)

**Critical Features:**
1. Track types by C# full name
2. Detect IDL identity collisions (`file::module::typename`)
3. Clear error messages on collision

**Tests Required:**
- ✅ `Registry_LocalType_StoresCorrectMapping`
- ✅ `Registry_ExternalType_ResolvedViaMetadata`
- ✅ `Registry_IdlCollision_DetectedAndReported`

---

### Task 6: Update SchemaDiscovery (FCDC-S025 Part 6)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SchemaDiscovery.cs` **(MODIFY)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Extract IDL file and module from attributes during type discovery.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 4.1** (lines 234-268)
- Add `GetIdlFileName(TypeInfo, sourceFileName)` helper
- Add `GetIdlModule(TypeInfo)` helper
- Default IDL file: C# source filename without extension
- Default IDL module: C# namespace with `.` → `::`

**Validation:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 4.2** (lines 272-338)
- `ValidateIdlFileName()`: no extension, no paths, no invalid chars
- `ValidateIdlModule()`: no C# syntax (`.`), valid IDL identifiers

**Tests Required:**
- ✅ `Discovery_DefaultIdlFile_UsesSourceFileName`
- ✅ `Discovery_CustomIdlFile_UsesAttribute`
- ✅ `Discovery_DefaultModule_UsesNamespace`
- ✅ `Discovery_CustomModule_UsesAttribute`
- ✅ `Validation_IdlFile_WithExtension_ThrowsError`
- ✅ `Validation_IdlModule_WithDots_ThrowsError`

---

### Task 7: Refactor CodeGenerator (FCDC-S025 Part 7)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CodeGenerator.cs` **(MAJOR REFACTOR)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Implement three-phase generation pipeline.

**Architecture:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 3.1** (lines 158-174)

**Phase 1: Discovery & Mapping**
- Scan all types, extract IDL file/module
- Build GlobalTypeRegistry
- Register local types

**Phase 2: Dependency Resolution**
- For each type, examine field types
- Resolve external dependencies via `[DdsIdlMapping]`
- See **Section 4.3** (lines 342-396) for Roslyn-based resolution

**Phase 3: Emission**
- Generate assembly metadata (`CycloneDDS.IdlMap.g.cs`)
- See **Section 4.5** (lines 496-514)
- Call refactored IdlEmitter

**Tests Required:**
- Integration tests in Task 11

---

### Task 8: Refactor IdlEmitter (FCDC-S025 Part 8)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\IdlEmitter.cs` **(MAJOR REFACTOR)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Transform from one-type-per-file to registry-based emission with grouping and dependencies.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 4.4** (lines 399-491)

**Key Changes:**
1. `EmitIdlFiles(GlobalTypeRegistry, outputDir)` - new main method
2. Group types by TargetIdlFile
3. Generate `#include` directives for dependencies (`GetFileDependencies`)
4. Group by module and emit hierarchies (`EmitModuleHierarchy`)
5. Nested module syntax (open/close correctly)

**Critical Requirements:**
- `#include` directives at top, sorted
- Dependencies exclude self-references
- Module hierarchy properly nested
- Types within modules sorted by name

**Tests Required:**
- ✅ `EmitIdl_MultipleTypes_SameFile_Grouped`
- ✅ `EmitIdl_Dependencies_IncludesFirst`
- ✅ `EmitIdl_MultipleModules_NestedCorrectly`

---

### Task 9: Update MSBuild Targets (FCDC-S025 Part 9)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CycloneDDS.targets` **(MODIFY)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Add targets to copy IDL files from referenced assemblies and include in build output.

**Requirements:**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 6.1** (lines 677-698)

**Targets to Add:**
1. `CopyReferencedIdlFiles` (BeforeTargets: CycloneDdsCodeGen)
   - Copy `*.idl` from `ReferenceCopyLocalPaths`
2. `IncludeIdlFilesInOutput` (AfterTargets: CycloneDdsCodeGen)
   - Include generated `*.idl` with `CopyToOutputDirectory="PreserveNewest"`

**Validation:**
- Manual test: Build two projects (A defines types, B references A)
- Verify: B's output folder contains A.idl

---

### Task 10: Unit Tests (FCDC-S025 Part 10)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\IdlGenerationTests.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
Unit tests for validation, registry, and emission logic.

**Tests Required (Minimum 12):**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 8.1** (lines 911-932)

**Validation Tests (4):**
1. `ValidateIdlFile_WithExtension_ThrowsError`
2. `ValidateIdlFile_WithPath_ThrowsError`
3. `ValidateIdlModule_WithDots_ThrowsError`
4. `ValidateIdlModule_InvalidIdentifier_ThrowsError`

**Registry Tests (2):**
5. `Registry_LocalType_StoresCorrectMapping`
6. `Registry_ExternalType_ResolvedViaMetadata`

**Dependency Tests (3):**
7. `Dependencies_SameFile_NoInclude`
8. `Dependencies_DifferentFile_AddsInclude`
9. `Dependencies_External_AddsInclude`

**Emission Tests (3):**
10. `EmitIdl_MultipleModules_NestedCorrectly`
11. `EmitIdl_Dependencies_IncludesFirst`
12. `EmitMetadata_AllTypes_Recorded`

**Test Quality:**
- ✅ Verify actual generated IDL content (not just "contains X")
- ✅ Check module nesting depth
- ✅ Verify `#include` order
- ✅ Check assembly metadata attributes

---

### Task 11: Integration Tests (FCDC-S025 Part 11)

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CrossAssemblyTests.cs` **(NEW FILE)**

**Task Definition:** [FCDC-S025](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control)

**Description:**  
End-to-end tests proving the full pipeline works.

**Tests Required (Minimum 6):**
- See **ADVANCED-IDL-GENERATION-DESIGN.md Section 8.2** (lines 934-960)

13. `TwoAssemblies_BReferencesA_IncludeGenerated`
    - Assembly A defines Point
    - Assembly B uses Point
    - Verify: B's IDL has `#include "A.idl"`

14. `CustomFile_MultipleTypes_SingleIdl`
    - Three types with `[DdsIdlFile("Common")]`
    - Verify: One `Common.idl` with all three types

15. `CustomModule_LegacyInterop_CorrectHierarchy`
    - C# namespace: `MyApp.Internal`
    - Attribute: `[DdsIdlModule("Legacy::Sys")]`
    - Verify: IDL uses `module Legacy { module Sys { } }`

16. `CrossAssembly_Transitive_AllIncluded`
    - A defines Base, B uses Base, C uses B
    - Verify: C's output has both A.idl and B.idl
    - **Critical Edge Case:** See Section 7.5 (lines 736-772)

17. `CircularDependency_Detected_ClearError`
    - Type A uses Type B (file A.idl)
    - Type B uses Type A (file B.idl)
    - Verify: Clear error about circular dependency

18. `IdlNameCollision_Detected_ClearError`
    - Two C# types → same `file::module::name`
    - Verify: Error message with guidance to use `[DdsIdlModule]`
    - **Critical Edge Case:** See Section 7.5 (lines 819-905)

---

## 🧪 Testing Requirements

### Minimum Test Counts

**Unit Tests:** 12 minimum  
**Integration Tests:** 6 minimum  
**Total:** 18 tests minimum

### Test Quality Standards

**❌ BAD TEST (String Presence):**
```csharp
Assert.Contains("#include", idlContent);  // Too vague!
```

**✅ GOOD TEST (Actual Structure):**
```csharp
var lines = idlContent.Split('\n');
Assert.Equal("#include \"MathDefs.idl\"", lines[2].Trim());
Assert.Contains("module Robot {", idlContent);
Assert.Contains("module Control {", idlContent);
// Verify actual nesting and content
```

**Tests MUST verify:**
- ✅ Generated IDL structure correct (modules, includes)
- ✅ Assembly metadata attributes emitted
- ✅ Error messages actionable
- ✅ Cross-assembly resolution works

---

## 📊 Report Requirements

### Focus: Developer Insights

**✅ ANSWER THESE:**

**Q1:** What was the most challenging part of the refactor? How did you approach it?

**Q2:** Did you encounter any issues with Roslyn's metadata resolution? How did you solve them?

**Q3:** What design decisions did you make beyond the spec? (e.g., error message wording, edge case handling)

**Q4:** Did you discover any edge cases not mentioned in the design document?

**Q5:** How did you handle the transition from old IdlEmitter to new? Any backwards compatibility concerns?

**Q6:** Are there performance concerns with the three-phase pipeline for large codebases?

**Q7:** Did the MSBuild targets work on first try? Any build integration issues?

### Report Must Include

1. **Completion Status:** Which tasks completed, test counts
2. **Code Changes:** Files modified/created
3. **Test Results:** Pass/fail counts, any skipped tests
4. **Refactor Approach:** How you structured the changes
5. **Issues Encountered:** Problems and solutions
6. **Design Decisions:** Choices you made beyond spec
7. **Build Integration:** Manual verification of MSBuild targets

---

## 🎯 Success Criteria

This batch is DONE when:

- [x] **Attributes:** DdsIdlFile, DdsIdlModule, DdsIdlMapping added
- [x] **Registry:** GlobalTypeRegistry with collision detection
- [x] **Discovery:** Extracts IDL metadata from attributes
- [x] **Generator:** Three-phase pipeline (discovery, resolution, emission)
- [x] **Emitter:** Groups types, generates includes, nested modules
- [x] **Metadata:** Assembly attributes generated
- [x] **Build:** MSBuild targets copy IDL files
- [x] **Tests:** Minimum 18 tests passing (12 unit + 6 integration)
- [x] **Quality:** Clear error messages, edge cases handled
- [x] **Report:** Submitted to `.dev-workstream/reports/BATCH-17-REPORT.md`

---

## ⚠️ Common Pitfalls to Avoid

### Pitfall 1: Forgetting Cross-Assembly Metadata

**Problem:** Assembly B uses types from A, but A wasn't rebuilt with new generator  
**Solution:** Clear error message: "Type 'A.Point' has no [DdsIdlMapping]. Rebuild A with CycloneDDS generator."

### Pitfall 2: Module Nesting Order

**Problem:** Opening modules but not closing in reverse order  
**Solution:** Use stack or counter to track depth (see design Section 4.4)

### Pitfall 3: Self-References in Dependencies

**Problem:** `Common.idl` includes `#include "Common.idl"`  
**Solution:** Filter out `dep.TargetIdlFile != type.TargetIdlFile`

### Pitfall 4: Generic Type Extraction

**Problem:** Field type `BoundedSeq<Point3D>` → need to extract `Point3D`  
**Solution:** Strip generic wrapper before registry lookup

### Pitfall 5: Validation Error Messages

**❌ Bad:** "Invalid IDL file name"  
**✅ Good:** "[DdsIdlFile(\"Types.idl\")] on 'MyStruct' contains extension. Use \"Types\" without .idl extension."

### Pitfall 6: Transitive Dependencies

**Problem:** App → LibB → LibA, but LibA.idl not copied to App  
**Solution:** `ReferenceCopyLocalPaths` handles transitivity automatically (see design Section 7.5)

---

## 📚 Reference Materials

**Task Definition:**
- [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s025-advanced-idl-generation-control) - Lines 1088-1200

**Design Document (PRIMARY REFERENCE):**
- [ADVANCED-IDL-GENERATION-DESIGN.md](../docs/ADVANCED-IDL-GENERATION-DESIGN.md) - **ENTIRE FILE**
  - Section 1: Problem & solution
  - Section 2: Attributes
  - Section 3: Architecture
  - Section 4: Implementation patterns
  - Section 5: Usage examples
  - Section 6: Build integration
  - Section 7: Error messages & edge cases
  - Section 8: Testing

**Previous Reviews:**
- [BATCH-16-REVIEW.md](../reviews/BATCH-16-REVIEW.md) - Test quality standards
- [BATCH-15.3-REVIEW.md](../reviews/BATCH-15.3-REVIEW.md) - Path calculation patterns

**Workflow Guide:**
- [README.md](../README.md) - Batch system, report template

**Code Examples:**
- Existing tests in `tests/CycloneDDS.CodeGen.Tests/`
- Current `IdlEmitter.cs` (will be refactored)
- Current `SchemaDiscovery.cs` (will be extended)

---

## 🔄 Workflow Reminder

1. ✅ Read **ADVANCED-IDL-GENERATION-DESIGN.md** in full (do NOT skip)
2. ✅ Build projects to verify environment
3. ✅ Run existing tests to establish baseline (should be ~101 passing)
4. ✅ Implement Tasks 1-3 (attributes) → build → verify
5. ✅ Implement Tasks 4-5 (registry) → write unit tests → ALL PASS
6. ✅ Implement Task 6 (discovery) → write tests → ALL PASS
7. ✅ Implement Tasks 7-8 (refactor) → write integration tests → ALL PASS
8. ✅ Implement Task 9 (MSBuild) → manual verification
9. ✅ Run ALL tests (existing + new) → ensure nothing broken
10. ✅ Submit report to `.dev-workstream/reports/BATCH-17-REPORT.md`

**DO NOT** skip ahead - this is complex architecture requiring solid foundations.

**DO NOT** submit report until ALL tests pass (existing + new).

---

## 🚨 Complexity Warning

**This is the most complex batch so far:**
- Major refactor of code generator core
- Cross-assembly dependency resolution via Roslyn
- MSBuild integration
- Multiple edge cases to handle

**Estimated actual time:** 5-7 days (not hours)

**Strategy:**
- Break work into small increments
- Test each component in isolation before integration
- Use design document as your guide (don't improvise architecture)
- Ask questions early if blocked

**You are implementing a production-grade IDL generation system. Take your time and build it right.**

---

**Good luck! This batch unlocks real-world DDS interoperability. The design document has all the answers - use it!** 🚀
