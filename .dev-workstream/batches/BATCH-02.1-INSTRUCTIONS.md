# BATCH-02.1: Fix Incomplete CdrSizer Test (Corrective Batch)

**Batch Number:** BATCH-02.1  
**Parent Batch:** BATCH-02  
**Tasks:** Fix incomplete test from BATCH-02 review  
**Phase:** Stage 1 - Foundation (Corrective)  
**Estimated Effort:** 15-30 minutes  
**Priority:** HIGH (blocks BATCH-02 approval)  
**Dependencies:** BATCH-02

---

## 📋 Onboarding & Workflow

### Developer Instructions

This is a **corrective batch** addressing one incomplete test from BATCH-02 review.

**Your Mission:** Complete the `CdrSizer_Matches_CdrWriter_Output()` test by adding the missing assertion that validates CdrSizer size predictions match actual CdrWriter output.

### Required Reading (IN ORDER)

1. **Review Feedback:** `.dev-workstream/reviews/BATCH-02-REVIEW.md` - **READ ISSUE 1 CAREFULLY**
2. **Previous Report:** `.dev-workstream/reports/BATCH-02-REPORT.md` - Context on what was done
3. **BATCH-02 Instructions:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md` - Original requirements

### Source Code Location

- **File to Modify:** `tests/CycloneDDS.Core.Tests/CdrSizerTests.cs` (line 106-130)
- **Test Project:** `tests/CycloneDDS.Core.Tests/`

### Report Submission

**⚠️ MANDATORY: You MUST submit a completion report when done.**

**Submit your report to EXACTLY this location:**  
`.dev-workstream/reports/BATCH-02.1-REPORT.md`

**Use this template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Report MUST include:**
- Test fix description
- Test execution results (total count, all passing)
- Any issues encountered (even if none)

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Fix

**CRITICAL: You MUST complete the task with passing tests:**

1. **Fix the test** → Add assertion  
2. **Run ALL tests** → `dotnet test tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj`  
3. **Verify ALL tests pass** → Check count matches expected (should be 57 total)  
4. **Write report** → Document what you did

**DO NOT** submit report until:
- ✅ Test has assertion added
- ✅ **ALL tests passing** (run `dotnet test` and verify)
- ✅ Report explains what was fixed

---

## Context

**Review Finding:** BATCH-02 review (`.dev-workstream/reviews/BATCH-02-REVIEW.md`) found one incomplete test.

**Specific Issue:** Test `CdrSizer_Matches_CdrWriter_Output()` (lines 106-130 in `CdrSizerTests.cs`) sets up both `CdrSizer` and `CdrWriter` but **never asserts anything**.

**Why This Matters:** This test is supposed to validate the **critical guarantee** that `CdrSizer` correctly predicts `CdrWriter` output size. This is the foundation of the two-pass XCDR2 architecture. Without this assertion, we don't verify coherency.

---

## 🎯 Batch Objectives

**Primary Goal:** Complete the incomplete test so it actually validates CdrSizer/CdrWriter coherency.

**Success Metric:** Test has assertion, all tests pass.

---

## ✅ Task

### Task 1: Complete CdrSizer_Matches_CdrWriter_Output Test

**File:** `tests/CycloneDDS.Core.Tests/CdrSizerTests.cs` (EDIT existing test)  
**Lines:** 106-130 (current incomplete test)

**Current Code (INCOMPLETE):**
```csharp
[Fact]
public void CdrSizer_Matches_CdrWriter_Output()
{
    var sizer = new CdrSizer(0);
    sizer.WriteInt32(42);
    sizer.WriteString("Test");
    int expectedSize = sizer.GetSizeDelta(0);

    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    cdr.WriteInt32(42);
    cdr.WriteString("Test");
    
    // Comments about Complete() but NO ASSERTION
    // Test ends without verifying anything!
}
```

**Required Fix:**

Add these lines at the end of the test (before closing brace):

```csharp
    cdr.Complete();
    
    // CRITICAL ASSERTION: Verify CdrSizer prediction matches actual output
    Assert.Equal(expectedSize, writer.WrittenCount);
}
```

**What This Validates:**
- `CdrSizer.GetSizeDelta(0)` returns predicted size
- `CdrWriter` actually writes that exact number of bytes
- Proves the two-pass architecture works (size calculation matches actual writing)

**Implementation Steps:**

1. Open `tests/CycloneDDS.Core.Tests/CdrSizerTests.cs`
2. Find line 106 (`public void CdrSizer_Matches_CdrWriter_Output()`)
3. Remove the comment lines (117-129) about checking Complete()
4. Add the two lines shown above before the closing brace (line 130)
5. Save file

**Expected Result:**
```csharp
[Fact]
public void CdrSizer_Matches_CdrWriter_Output()
{
    var sizer = new CdrSizer(0);
    sizer.WriteInt32(42);
    sizer.WriteString("Test");
    int expectedSize = sizer.GetSizeDelta(0);

    var writer = new ArrayBufferWriter<byte>();
    var cdr = new CdrWriter(writer);
    cdr.WriteInt32(42);
    cdr.WriteString("Test");
    cdr.Complete();
    
    Assert.Equal(expectedSize, writer.WrittenCount);
}
```

**Estimated Time:** 5-10 minutes

---

## 🧪 Testing Requirements

**Test Execution:**

Run ALL tests:
```bash
dotnet test tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj
```

**Expected Results:**
- Total Tests: **57** (26 from BATCH-02 + 31 from BATCH-01)
- Passing: **57**
- Failing: **0**

**If test fails:**
- Check that `CdrWriter` has `Complete()` method (it should from BATCH-01)
- Verify `expectedSize` calculation is correct
- Verify both sizer and writer use identical call sequence

---

## 📊 Report Requirements

**⚠️ MANDATORY: Submit report to `.dev-workstream/reports/BATCH-02.1-REPORT.md`**

**Required Sections:**

### 1. Implementation Summary
- What test was fixed (file name, line numbers)
- What assertion was added

### 2. Test Results
- **MUST INCLUDE:** Total test count
- **MUST INCLUDE:** Pass/fail status (should be 57/57)
- **MUST INCLUDE:** Screenshot or copy-paste of `dotnet test` output

### 3. Issues Encountered
- Any problems during fix (even if none, write "No issues encountered")
- How you verified the fix works

### 4. Verification
- Confirm test now validates size match
- Confirm all existing tests still pass

**Keep report brief** - this is a small fix, 1 page maximum.

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ `CdrSizerTests.cs` line 106-130 modified with assertion
- ✅ **ALL 57 tests passing** (verified by running `dotnet test`)
- ✅ No compiler warnings
- ✅ **Report submitted to `.dev-workstream/reports/BATCH-02.1-REPORT.md`**

**GATE:** Cannot proceed without report submission.

---

## ⚠️ Common Pitfalls to Avoid

1. **Forgetting to run tests:** Must verify fix actually works
   - Run: `dotnet test tests/CycloneDDS.Core.Tests/CycloneDDS.Core.Tests.csproj`

2. **Forgetting to write report:** Even small batches require reports
   - Location: `.dev-workstream/reports/BATCH-02.1-REPORT.md`

3. **Not checking existing tests:** Fix might break something
   - Verify all 57 tests pass, not just the fixed one

4. **Wrong assertion:** Make sure comparing size, not something else
   - Should be: `Assert.Equal(expectedSize, writer.WrittenCount)`

---

## 📚 Reference Materials

- **Review:** `.dev-workstream/reviews/BATCH-02-REVIEW.md` - Issue 1 has exact fix
- **Original Instructions:** `.dev-workstream/batches/BATCH-02-INSTRUCTIONS.md`
- **Test Quality Guide:** `.dev-workstream/DEV-LEAD-GUIDE.md` (why tests must assert actual correctness)

---

**Parent Batch:** BATCH-02  
**Blocks:** BATCH-02 final approval  
**Next Batch:** BATCH-03 (Schema Package + Generator Infrastructure) - blocked until this is complete
