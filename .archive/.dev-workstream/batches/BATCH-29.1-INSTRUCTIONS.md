# BATCH-29.1: Fix Appendable Arrays (Corrective)

**Batch Number:** BATCH-29.1 (Corrective)
**Parent Batch:** BATCH-29
**Estimated Effort:** 2-4 hours
**Priority:** HIGH (Blocking Merge)

---

## 📋 Onboarding & Workflow

### Background
This is a **corrective batch** addressing issues found in BATCH-29 review.
The implementation of Nested Structures and Keys was successful, but the required cleanup of BATCH-28 (Appendable Arrays) was incomplete and failing.

**Original Batch:** `.dev-workstream/batches/BATCH-29-INSTRUCTIONS.md`
**Review with Issues:** `.dev-workstream/reviews/BATCH-29-REVIEW.md`

---

## 🎯 Objectives

1.  **Fix Appendable Array Tests:** Resolve the failures in `TestArrayFloat64Appendable` and `TestArrayStringAppendable`.
2.  **Verify Full Suite:** Ensure all tests (Primitives, Arrays, Nested Structs, Keys) pass simultaneously.

---

## ✅ Tasks

### Task 1: Fix Appendable Array Serialization

**Files to Modify:**
- `tests/CsharpToC.Roundtrip.Tests/Program.cs` (Uncomment tests)
- `src/CycloneDDS/Serialization/SerializerEmitter.cs` (Likely source of issue)
- `src/CycloneDDS/Serialization/CdrWriter.cs` (Possible source)

**Description:**
The tests `TestArrayFloat64Appendable` and `TestArrayStringAppendable` fail when enabled. You must debug the serialization logic for **Arrays within Appendable Types**.

**Investigation Hints:**
- Appendable types use XCDR2.
- Arrays in XCDR2 might have different headers or alignment requirements compared to XCDR1 or Final types.
- Check if the `DHEADER` (length) is being calculated correctly for the appendable struct when it contains an array.
- Check if the Array itself is being emitted with the correct length/header if applicable.

**Requirements:**
1.  Uncomment the tests in `Program.cs`.
2.  Run the tests and analyze the failure (use `CdrDumper` output if needed).
3.  Fix the underlying issue in the serialization code.

---

## 🧪 Testing Requirements

**Success Criteria:**
- [ ] `TestArrayFloat64Appendable` PASSES.
- [ ] `TestArrayStringAppendable` PASSES.
- [ ] All Nested Struct/Key tests (from Batch 29) PASS.
- [ ] All Primitive tests PASS.

**Command:**
```powershell
dotnet run --project tests/CsharpToC.Roundtrip.Tests/CsharpToC.Roundtrip.Tests.csproj
```

---

## 📊 Report Requirements

**Report to:** `.dev-workstream/reports/BATCH-29.1-REPORT.md`

1.  Describe the root cause of the array serialization failure.
2.  Explain the fix applied.
3.  Confirm that **ALL** tests are now uncommented and passing.
