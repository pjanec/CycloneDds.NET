# BATCH-01: Foundation - Schema Package Complete

**Batch Number:** BATCH-01  
**Tasks:** FCDC-001, FCDC-002, FCDC-003, FCDC-004  
**Phase:** Phase 1 - Foundation & Schema Package  
**Estimated Effort:** 10-12 hours  
**Priority:** CRITICAL (Foundation for entire project)  
**Dependencies:** None (greenfield)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This is the **first batch** of the FastCycloneDDS C# Bindings project. You are establishing the foundation—the Schema package that users will reference to define their DDS topic types using C# attributes.

This batch creates **CycloneDDS.Schema** NuGet package containing:
- Attribute classes for schema definition
- Wrapper types for bounded/specialized data
- Global type map registry system
- QoS and error type enumerations

**CRITICAL:** This is greenfield—no existing code yet. You'll be setting up the initial project structure and coding standards.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches *(create this if missing)*
2. **Task Definitions:** `docs/FCDC-TASK-MASTER.md` - See FCDC-001 through FCDC-004
3. **Detailed Task Files:** 
   - `tasks/FCDC-001.md` - Schema Attribute Definitions (very detailed)
   - `tasks/FCDC-002.md` - *(you may need to create this following FCDC-001 pattern)*
   - `tasks/FCDC-003.md` - *(you may need to create this following FCDC-001 pattern)*
   - `tasks/FCDC-004.md` - *(you may need to create this following FCDC-001 pattern)*
4. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` - Especially §4 (Schema DSL Design)
5. **Implementation Plan:** `docs/FCDC-IMPLEMENTATION-PLAN-SUMMARY.md` - Critical notes on design decisions

### Source Code Location

**You will create:**
- **Project:** `src/CycloneDDS.Schema/CycloneDDS.Schema.csproj`
- **Test Project:** `tests/CycloneDDS.Schema.Tests/CycloneDDS.Schema.Tests.csproj`

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-01-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-01-QUESTIONS.md`

---

## Context

This is the **foundation layer** of the project. Everything else depends on these attribute classes and wrapper types being correct and well-designed.

**Related Tasks:**
- [FCDC-001](../tasks/FCDC-001.md) - Schema Attribute Definitions
- [FCDC-002 Summary](../docs/FCDC-TASK-MASTER.md#fcdc-002-schema-wrapper-types) - Schema Wrapper Types
- [FCDC-003 Summary](../docs/FCDC-TASK-MASTER.md#fcdc-003-global-type-map-registry) - Global Type Map Registry
- [FCDC-004 Summary](../docs/FCDC-TASK-MASTER.md#fcdc-004-qos-and-error-type-definitions) - QoS and Error Type Definitions

**Why This Batch Matters:**
- Users will see these APIs first—sets tone for entire library
- Attributes drive the source generator (Phase 2)
- Wrapper types enable zero-allocation patterns
- Quality here determines user experience throughout

---

## 🎯 Batch Objectives

By completing this batch, you will:

1. ✅ Establish project structure for CycloneDDS.Schema (.NET 8, nullable enabled, strong naming)
2. ✅ Implement all attribute classes following design spec (minimal attributes, smart inference)
3. ✅ Implement wrapper types (FixedString32/64/128, BoundedSeq<T,N>, etc.)
4. ✅ Implement global type map registry (assembly-level [DdsTypeMap] attribute)
5. ✅ Implement QoS enumerations and error types
6. ✅ Create comprehensive test suite (100% coverage on public APIs)
7. ✅ Set up NuGet package metadata (ready to pack)

---

## ✅ Tasks

### Task 1: Schema Attribute Definitions (FCDC-001)

**Files to Create:**
- `src/CycloneDDS.Schema/CycloneDDS.Schema.csproj` (NEW)
- `src/CycloneDDS.Schema/Attributes/TypeLevel/*.cs` (NEW - 4 files)
- `src/CycloneDDS.Schema/Attributes/FieldLevel/*.cs` (NEW - 4 files)
- `src/CycloneDDS.Schema/Attributes/UnionSpecific/*.cs` (NEW - 3 files)

**Task Definition:** See [tasks/FCDC-001.md](../tasks/FCDC-001.md) for complete implementation details.

**Requirements:**

1. **Type-Level Attributes:**
   - `DdsTopicAttribute(string topicName)` - Required, validates non-null/whitespace
   - `DdsQosAttribute` - Reliability, Durability, HistoryKind, HistoryDepth properties
   - `DdsUnionAttribute` - Marker attribute
   - `DdsTypeNameAttribute(string idlTypeName)` - Optional IDL name override

2. **Field-Level Attributes:**
   - `DdsKeyAttribute` - Marker for key fields
   - `DdsBoundAttribute(int max)` - Max bound for strings/sequences (validate > 0)
   - `DdsIdAttribute(int id)` - Explicit member ID
   - `DdsOptionalAttribute` - Marker for optional fields (nullable refs)

3. **Union-Specific Attributes:**
   - `DdsDiscriminatorAttribute` - Marker for discriminator field
   - `DdsCaseAttribute(object value)` - Discriminator value for union arm
   - `DdsDefaultCaseAttribute` - Marker for default case

**Design Reference:** [Detailed Design §4.4](../docs/FCDC-DETAILED-DESIGN.md#44-required-attributes)

**Tests Required (Minimum 15 tests):**
- ✅ Attribute construction with valid parameters
- ✅ Attribute construction with invalid parameters (ArgumentException)
- ✅ Attribute usage on types and fields (reflection-based tests)
- ✅ AllowMultiple = false enforcement tests
- ✅ Attribute retrieval correctness

**Code Quality Standards:**
- XML documentation on all public types (required for IntelliSense)
- Sealed classes (attributes should not be inherited)
- Validation in constructors (fail fast on bad parameters)
- Follow naming conventions: `Dds*Attribute`

---

### Task 2: Schema Wrapper Types (FCDC-002)

**Files to Create:**
- `src/CycloneDDS.Schema/WrapperTypes/FixedString32.cs` (NEW)
- `src/CycloneDDS.Schema/WrapperTypes/FixedString64.cs` (NEW)
- `src/CycloneDDS.Schema/WrapperTypes/FixedString128.cs` (NEW)
- `src/CycloneDDS.Schema/WrapperTypes/BoundedSeq.cs` (NEW - generic)

**Requirements:**

1. **FixedStringN Types:**
   - Readonly struct with UTF-8 byte storage (not exposed)
   - `TryFrom(string value, out FixedStringN result)` - Returns false if too long
   - `AsUtf8Span()` → ReadOnlySpan<byte>
   - `ToStringAllocated()` → string (explicit allocation)
   - `Length` property (actual UTF-8 byte length, not capacity)
   - UTF-8 validation on construction

2. **BoundedSeq<T, N> Type:**
   - Generic struct where N is capacity (compile-time constant?)
   - Backing storage (array or inline buffer, TBD your design choice—document rationale)
   - `Add(T item)`, `Clear()`, `Count`, `Capacity`
   - `AsSpan()` → Span<T>
   - Bounds checking

**Design Reference:** [Detailed Design §4.2 Type Mapping](../docs/FCDC-DETAILED-DESIGN.md#42-type-mapping-rules), [§8.2 Fixed Buffers](../docs/FCDC-DETAILED-DESIGN.md#82-fixed-buffers-for-bounded-data)

**Critical Design Decision:**
UTF-8 validation strategy: **Reject invalid UTF-8 in debug builds, optionally skip in release** (Design Talk §2193-2201).

**Tests Required (Minimum 20 tests):**
- ✅ FixedString32: Valid UTF-8 strings within bounds
- ✅ FixedString32: Strings exceeding bounds (rejected via TryFrom)
- ✅ FixedString32: Invalid UTF-8 sequences (rejected)
- ✅ FixedString32: Multi-byte character boundary truncation handling
- ✅ FixedString32: Empty string, null string handling
- ✅ BoundedSeq<T,N>: Add within capacity
- ✅ BoundedSeq<T,N>: Add exceeding capacity (exception or failure)
- ✅ BoundedSeq<T,N>: Clear and reuse
- ✅ BoundedSeq<T,N>: AsSpan() correctness

---

### Task 3: Global Type Map Registry (FCDC-003)

**Files to Create:**
- `src/CycloneDDS.Schema/TypeMap/DdsTypeMapAttribute.cs` (NEW - assembly-level)
- `src/CycloneDDS.Schema/TypeMap/DdsWire.cs` (NEW - enum of built-in wire kinds)

**Requirements:**

1. **DdsTypeMapAttribute:**
   - `[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]`
   - Constructor: `DdsTypeMapAttribute(Type sourceType, DdsWire wireKind)`
   - Properties: `Type SourceType { get; }`, `DdsWire WireKind { get; }`
   - Validation: sourceType not null, wireKind defined

2. **DdsWire Enum:**
   - `Guid16` → octet[16]
   - `Int64TicksUtc` → int64
   - `QuaternionF32x4` → struct { float x,y,z,w }
   - `FixedUtf8Bytes32` → octet[32]
   - `FixedUtf8Bytes64` → octet[64]
   - `FixedUtf8Bytes128` → octet[128]

**Design Reference:** [Detailed Design §4.3](../docs/FCDC-DETAILED-DESIGN.md#43-global-type-map-registry), [Design Talk §2050-2161](../docs/fcdc-design-talk.md)

**Tests Required (Minimum 5 tests):**
- ✅ Apply [DdsTypeMap] to assembly
- ✅ Retrieve assembly attributes via reflection
- ✅ Multiple type mappings on same assembly
- ✅ DdsWire enum has all expected values

---

### Task 4: QoS and Error Type Definitions (FCDC-004)

**Files to Create:**
- `src/CycloneDDS.Schema/Enums/DdsReliability.cs` (NEW)
- `src/CycloneDDS.Schema/Enums/DdsDurability.cs` (NEW)
- `src/CycloneDDS.Schema/Enums/DdsHistoryKind.cs` (NEW)
- `src/CycloneDDS.Schema/ErrorTypes/DdsException.cs` (NEW)
- `src/CycloneDDS.Schema/ErrorTypes/DdsReturnCode.cs` (NEW)
- `src/CycloneDDS.Schema/ErrorTypes/DdsSampleInfo.cs` (NEW - may defer struct details to Phase 3)

**Requirements:**

1. **QoS Enums:**
   - DdsReliability: BestEffort = 0, Reliable = 1
   - DdsDurability: Volatile = 0, TransientLocal = 1, Transient = 2, Persistent = 3
   - DdsHistoryKind: KeepLast = 0, KeepAll = 1
   - XML documentation on each value

2. **DdsException:**
   - Inherits Exception
   - `DdsReturnCode ErrorCode { get; }`
   - Constructor: `DdsException(DdsReturnCode code, string message)`
   - Message format: "DDS Error {code}: {message}"

3. **DdsReturnCode Enum:**
   - Ok = 0, Error = -1, Unsupported = -2, BadParameter = -3, etc.
   - Match Cyclone DDS return codes (see Design Doc §11.2)

4. **DdsSampleInfo Struct (Stub):**
   - Define struct with StructLayout(LayoutKind.Sequential)
   - Placeholder fields (can defer full implementation to Phase 3)
   - XML doc: "Stub - will be completed in FCDC-015"

**Design Reference:** [Detailed Design §11.2 Error Handling](../docs/FCDC-DETAILED-DESIGN.md#112-error-handling)

**Tests Required (Minimum 10 tests):**
- ✅ Enum values are correct
- ✅ DdsException construction and properties
- ✅ DdsException message format
- ✅ DdsReturnCode enum completeness

---

## 🧪 Testing Requirements

### Minimum Test Counts
- **FCDC-001 Tests:** 15 tests minimum
- **FCDC-002 Tests:** 20 tests minimum
- **FCDC-003 Tests:** 5 tests minimum
- **FCDC-004 Tests:** 10 tests minimum
- **Total Target:** 50+ tests

### Test Quality Standards

**✅ ACCEPTABLE:**
- Tests that verify behavior under different conditions
- Tests that validate error handling (ArgumentException, etc.)
- Tests that verify edge cases (empty strings, max bounds, etc.)
- Integration tests showing attributes can be applied and retrieved

**❌ NOT ACCEPTABLE:**
- Tests that only verify "can I create this object" (constructor tests with no assertions)
- Tests that don't actually assert anything meaningful
- Tests with vague names like "TestWorks()"

**Example Good Test:**
```csharp
[Fact]
public void DdsTopicAttribute_ThrowsArgumentException_WhenTopicNameIsNull()
{
    var ex = Assert.Throws<ArgumentException>(() => new DdsTopicAttribute(null));
    Assert.Contains("topicName", ex.Message);
}
```

**Example Bad Test:**
```csharp
[Fact]
public void AttributeExists()
{
    var attr = new DdsTopicAttribute("test");
    Assert.NotNull(attr); // Tests nothing useful
}
```

### Code Coverage
- **Target:** 100% coverage on all public APIs
- Use coverage tools (coverlet or dotCover)
- Include coverage report in your submission

---

## 📊 Report Requirements

**CRITICAL:** You must submit a detailed report documenting your work. This is NOT optional.

*Create:* `.dev-workstream/reports/BATCH-01-REPORT.md`

### Required Sections

1. **Executive Summary**
   - 1-2 sentences on what was accomplished

2. **Implementation Summary**
   - For each task (FCDC-001 through FCDC-004):
     - Files created
     - Key design decisions made
     - Any deviations from spec (with rationale)

3. **Test Results**
   - Total test count
   - All tests passing ✅
   - Code coverage percentage
   - Brief description of test categories

4. **Developer Insights**

   **Q1:** What issues did you encounter during implementation? How did you resolve them?

   **Q2:** What design decisions did you make beyond the instructions? What alternatives did you consider and why did you choose your approach?

   **Q3:** Are there any weak points in the current design or implementation? What would you improve if you could refactor?

   **Q4:** What edge cases did you discover that weren't mentioned in the spec? How did you handle them?

   **Q5:** Are there any performance concerns or optimization opportunities you noticed? (e.g., FixedString validation overhead)

   **Q6:** What guidance would you give to the next developer working on the source generator (Phase 2) regarding how to use these attributes effectively?

5. **Build Instructions**
   - Commands to build the project
   - Commands to run tests
   - Any setup required

6. **Code Quality Checklist**
   - [ ] All code compiles without warnings
   - [ ] XML documentation on all public types
   - [ ] Null reference annotations correct (nullable enabled)
   - [ ] Tests cover edge cases
   - [ ] No TODO comments left in code
   - [ ] NuGet package metadata complete

---

## 🎯 Success Criteria

This batch is DONE when:

- [ ] **FCDC-001 Complete:** All 11 attribute classes implemented with XML docs and tests
- [ ] **FCDC-002 Complete:** FixedString32/64/128 + BoundedSeq<T,N> implemented with UTF-8 validation and tests
- [ ] **FCDC-003 Complete:** [DdsTypeMap] attribute + DdsWire enum implemented and tested
- [ ] **FCDC-004 Complete:** QoS enums + DdsException + DdsReturnCode implemented and tested
- [ ] **All tests passing:** Minimum 50+ tests, all green
- [ ] **Code coverage:** 100% on public APIs
- [ ] **No compiler warnings:** Clean build
- [ ] **NuGet ready:** Package can be built with `dotnet pack`
- [ ] **Report submitted:** `.dev-workstream/reports/BATCH-01-REPORT.md` with all sections complete

---

## ⚠️ Common Pitfalls to Avoid

### 1. UTF-8 Validation Performance
**Pitfall:** Validating UTF-8 on every access kills performance.  
**Solution:** Validate once on construction (TryFrom), trust internal state thereafter. See Design Talk §2193-2201.

### 2. BoundedSeq<T, N> Implementation
**Pitfall:** Generic type parameters can't be used as array sizes in C#.  
**Challenge:** You'll need to decide on implementation strategy—consider:
- Runtime-sized backing array (simple, but bounds not compile-time enforced)
- Separate types per bound (BoundedSeq8<T>, BoundedSeq16<T>—verbose but compile-time safe)
- Other creative solutions?

**Action Required:** Document your design choice and rationale in report Q2.

### 3. Attribute AttributeUsage
**Pitfall:** Forgetting to set `AllowMultiple = false` allows duplicate attributes.  
**Solution:** Explicitly set for all type-level attributes except [DdsTypeMap] (which is AllowMultiple = true).

### 4. Null Reference Annotations
**Pitfall:** Nullable context is enabled, but not properly annotating nullable parameters.  
**Solution:** Use `string?` where null is allowed, `string` where required. Validate in constructors.

### 5. Test Quality
**Pitfall:** Writing many shallow tests to hit test count targets.  
**Solution:** Focus on meaningful tests that verify actual behavior and edge cases.

---

## ⚠️ Quality Standards

### ❗ TEST QUALITY EXPECTATIONS
- **NOT ACCEPTABLE:** Tests that only verify "can I set this value" or "object exists"
- **REQUIRED:** Tests that verify actual behavior, validation logic, and edge cases
- **REQUIRED:** Tests must assert meaningful conditions, not just "not null"

### ❗ REPORT QUALITY EXPECTATIONS
- **REQUIRED:** Answer ALL questions in the Developer Insights section with detailed responses
- **REQUIRED:** Document EVERY design decision you made beyond the spec
- **REQUIRED:** Share insights on code quality and improvement opportunities
- **REQUIRED:** Note edge cases discovered during implementation
- **NOT ACCEPTABLE:** One-line answers to questions
- **NOT ACCEPTABLE:** "No issues encountered" (if true, explain why implementation was smooth)

### ❗ CODE QUALITY EXPECTATIONS
- **REQUIRED:** XML documentation on every public type and member
- **REQUIRED:** Zero compiler warnings
- **REQUIRED:** Consistent naming conventions throughout
- **REQUIRED:** Proper null reference annotations (nullable context enabled)

---

## 📚 Reference Materials

- **Task Master:** [docs/FCDC-TASK-MASTER.md](../docs/FCDC-TASK-MASTER.md) - All tasks overview
- **Detailed Task:** [tasks/FCDC-001.md](../tasks/FCDC-001.md) - Most detailed guidance
- **Design Doc:** [docs/FCDC-DETAILED-DESIGN.md](../docs/FCDC-DETAILED-DESIGN.md) - §4 Schema DSL Design, §8 Type System
- **Design Talk:** [docs/fcdc-design-talk.md](../docs/fcdc-design-talk.md) - Lines 2193-2201 (UTF-8 validation), 2050-2161 (type map)
- **Implementation Plan:** [docs/FCDC-IMPLEMENTATION-PLAN-SUMMARY.md](../docs/FCDC-IMPLEMENTATION-PLAN-SUMMARY.md) - Critical notes

---

## 🔄 Next Steps After Completion

After this batch is approved:
1. I will review your code and tests (1-2 hour review)
2. I will generate a git commit message for you to use
3. You will commit using the provided message
4. BATCH-02 will begin Phase 2 (Roslyn Source Generator Infrastructure)

**This batch is critical—take your time to do it right. The entire project builds on this foundation.**

Good luck! 🚀
