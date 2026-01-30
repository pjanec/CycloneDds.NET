# BATCH-15.2: Fix idlc.exe Path - Use Source Location

**Batch Number:** BATCH-15.2  
**Parent:** BATCH-15.1  
**Stage:** 4 - Performance Foundation (Cleanup)  
**Priority:** 🟡 LOW (Code Quality)  
**Estimated Effort:** 15-20 minutes  
**Assigned:** [TBD]  
**Due Date:** [TBD]

---

## 🎯 Objective

Update test code to use the existing `idlc.exe` location instead of copying the file.

**What you're fixing:**
- Remove duplicated `idlc.exe` from `cyclone-bin\Release`
- Update test code to point to `cyclone-compiled\bin\idlc.exe`
- Use source of truth instead of file duplication

**Why this matters:**
- Avoid maintaining duplicate files
- Use existing compiled tools location
- Cleaner project structure

---

## 📋 Context

In BATCH-15.1, you chose **Option A** (copy file) which works but creates duplication.

**Current State:**
- `idlc.exe` source: `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe` ✅
- `idlc.exe` copy: `d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\idlc.exe` ❌ (unnecessary duplicate)

**Better Approach:** Use **Option B** - Update test code to point to source location.

---

## 🛠️ Implementation Steps

### Step 1: Update Test Code

**File:** `tests\CycloneDDS.CodeGen.Tests\ErrorHandlingTests.cs`

**Line 122 - Update hardcoded path:**

```csharp
// OLD (points to copy):
runner.IdlcPathOverride = @"d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\idlc.exe";

// NEW (points to source):
runner.IdlcPathOverride = @"d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe";
```

**Check for other references:**
```powershell
cd d:\Work\FastCycloneDdsCsharpBindings
Select-String "cyclone-bin.*idlc" -Path tests\*.cs -Recurse
```

Update any other hardcoded paths you find.

---

### Step 2: Delete Duplicate File

**Remove the copied file:**
```powershell
# Delete the duplicate
Remove-Item "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release\idlc.exe" -Force

# Optionally remove empty directory
Remove-Item "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\Release" -Force
Remove-Item "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin" -Force
```

---

### Step 3: Verify Tests Still Pass

**Run all CodeGen tests:**
```powershell
dotnet test tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj
```

**Expected Results:**
```
Test summary: total: 95; failed: 0; succeeded: 95; skipped: 0
```

All tests should still pass, now using the source `idlc.exe` location.

---

## 📊 Deliverables Checklist

- [ ] Updated `ErrorHandlingTests.cs` line 122
- [ ] Checked for other hardcoded paths (if any, update them)
- [ ] Deleted duplicate `cyclone-bin\Release\idlc.exe`
- [ ] Deleted empty `cyclone-bin` directory
- [ ] Verified all 95 tests still PASS

---

## 📝 Report Requirements

**Create:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-15.2-REPORT.md`

```markdown
# BATCH-15.2 Report: idlc.exe Path Cleanup

**Status:** COMPLETE

## Changes Made

1. **Updated Test Code:**
   - File: tests\CycloneDDS.CodeGen.Tests\ErrorHandlingTests.cs
   - Line 122: Updated path to cyclone-compiled\bin\idlc.exe
   - [List any other files updated]

2. **Removed Duplicates:**
   - Deleted: cyclone-bin\Release\idlc.exe
   - Deleted: cyclone-bin directory

3. **Test Verification:**
   - All 95 tests: PASS ✅
   - Using source idlc.exe location

## Outcome

Tests now use the single source of truth for idlc.exe.
No file duplication. Cleaner project structure.
```

---

## 🎯 Success Criteria

1. ✅ Test code points to `cyclone-compiled\bin\idlc.exe`
2. ✅ No duplicate `idlc.exe` in `cyclone-bin`
3. ✅ All 95 tests still PASS
4. ✅ Single source of truth maintained

---

## ⏱️ Time Estimate

**Update test code:** 5 minutes  
**Delete duplicate:** 2 minutes  
**Run tests:** 5 minutes  
**Report:** 5 minutes  

**Total:** 15-20 minutes

---

## 🎉 Expected Outcome

**After this batch:**
- ✅ Single `idlc.exe` location (cyclone-compiled\bin)
- ✅ Test code uses source location
- ✅ No file duplication
- ✅ All tests passing

**This maintains code quality and simplicity!**

---

**Batch Version:** 1.0  
**Last Updated:** 2026-01-18  
**Prepared by:** Development Lead
