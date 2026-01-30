# BATCH-15.3: Fix Absolute Paths - Use Relative Paths

**Batch Number:** BATCH-15.3  
**Parent:** BATCH-15.2  
**Stage:** 4 - Performance Foundation (Portability Fix)  
**Priority:** 🔴 **HIGH** (Blocks portability)  
**Estimated Effort:** 20-30 minutes  
**Assigned:** [TBD]  
**Due Date:** [TBD]

---

## 🎯 Objective

Replace absolute `idlc.exe` paths with relative paths to enable cross-platform/cross-machine compatibility.

**What you're fixing:**
- Absolute path: `@"d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe"` ❌
- Relative path: Calculated from test directory ✅

**Why this is CRITICAL:**
- Tests won't work on different machines
- Won't work for other developers
- Won't work in CI/CD
- Hard-coded drive letter (D:) prevents portability

---

## 📋 Context

BATCH-15.2 fixed file duplication but **retained absolute paths**.

**Current Problem:**
```csharp
runner.IdlcPathOverride = @"d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe";
```

This breaks on:
- Different drive letters
- Different user paths
- CI/CD systems
- Other developers' machines

---

## 🛠️ Implementation Steps

### Step 1: Understand Project Structure

**Repository layout:**
```
d:\Work\FastCycloneDdsCsharpBindings\       ← Root
├── cyclone-compiled\
│   └── bin\
│       └── idlc.exe                        ← Target
├── tests\
│   └── CycloneDDS.CodeGen.Tests\
│       └── ErrorHandlingTests.cs           ← Current file
```

**Relative path from test to idlc.exe:**
```
tests/CycloneDDS.CodeGen.Tests → ../../cyclone-compiled/bin/idlc.exe
```

---

### Step 2: Calculate Relative Path at Runtime

**File:** `tests\CycloneDDS.CodeGen.Tests\ErrorHandlingTests.cs`

**Find the line with absolute path** (around line 122):
```csharp
runner.IdlcPathOverride = @"d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe";
```

**Replace with relative path calculation:**
```csharp
// Get test assembly directory
var testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
// Navigate: tests/CycloneDDS.CodeGen.Tests/bin/Debug/net8.0 → repo root
var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
// Build path to idlc.exe
var idlcPath = Path.Combine(repoRoot, "cyclone-compiled", "bin", "idlc.exe");
runner.IdlcPathOverride = idlcPath;
```

**Add required using:**
```csharp
using System.Reflection;  // For Assembly.GetExecutingAssembly()
```

---

### Step 3: Alternative (Simpler) Approach

If you know the test runs from `bin/Debug/net8.0`, you can use:

```csharp
// Relative from test bin directory
var testDir = AppContext.BaseDirectory;  // or AppDomain.CurrentDomain.BaseDirectory
var idlcPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "cyclone-compiled", "bin", "idlc.exe"));
runner.IdlcPathOverride = idlcPath;
```

**Even simpler - check if IdlcRunner has auto-discovery:**

Check if `IdlcRunner` class can auto-discover `idlc.exe`. If it already searches relative paths, you might not need `IdlcPathOverride` at all for normal cases.

---

### Step 4: Verify No Other Absolute Paths

**Search for all hardcoded paths:**
```powershell
cd d:\Work\FastCycloneDdsCsharpBindings
Select-String '@"d:\\' -Path tests\*.cs -Recurse
Select-String '@"c:\\' -Path tests\*.cs -Recurse
Select-String ":\\\\Work" -Path tests\*.cs -Recurse
```

Fix any other absolute paths you find.

---

### Step 5: Test on Multiple Scenarios

**Test 1: Normal run**
```powershell
dotnet test tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj
```

**Test 2: Simulate different directory structure**
```powershell
# Copy repo to a test location
xcopy /E /I d:\Work\FastCycloneDdsCsharpBindings c:\Temp\TestRepo
cd c:\Temp\TestRepo
dotnet test tests\CycloneDDS.CodeGen.Tests\CycloneDDS.CodeGen.Tests.csproj
```

Both should pass with relative paths.

---

## 📊 Deliverables Checklist

- [ ] Updated `ErrorHandlingTests.cs` to use relative path
- [ ] Added any required `using` statements
- [ ] Checked for other absolute paths in test files
- [ ] Verified tests pass (95/95)
- [ ] Documented the path calculation logic

---

## 📝 Report Requirements

**Create:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-15.3-REPORT.md`

```markdown
# BATCH-15.3 Report: Relative Path Fix

**Status:** COMPLETE

## Changes Made

1. **Updated ErrorHandlingTests.cs:**
   - Removed absolute path: `d:\Work\...`
   - Added relative path calculation
   - [Paste the exact code you used]

2. **Required Usings:**
   - [List any using statements added]

3. **Other Files Updated:**
   - [List any other files with absolute paths that were fixed]

4. **Test Verification:**
   - All 95 tests: PASS ✅
   - Works with relative paths

## Path Calculation Logic

[Explain how the relative path is calculated]

## Portability Verification

- [x] No hardcoded drive letters
- [x] No absolute paths
- [x] Tests work from any repository location
```

---

## 🎯 Success Criteria

1. ✅ **No absolute paths** in test code
2. ✅ **Relative path** calculated at runtime
3. ✅ **All 95 tests PASS**
4. ✅ **Works from different directories**
5. ✅ **Portable across machines**

---

## 🆘 Common Issues

**Issue 1: Path not found**
- Check the number of `..` navigations
- Print `repoRoot` to verify it's correct
- Ensure `idlc.exe` actually exists at calculated path

**Issue 2: Tests still fail**
- Check if `IdlcRunner` has other absolute path assumptions
- Verify working directory is correct

**Issue 3: Different build configurations**
- Path might change between Debug/Release
- Use `AppContext.BaseDirectory` which is always correct

---

## ⏱️ Time Estimate

**Update path calculation:** 10 minutes  
**Find/fix other absolute paths:** 10 minutes  
**Testing:** 5 minutes  
**Report:** 5 minutes  

**Total:** 20-30 minutes

---

## 🎉 Expected Outcome

**After this batch:**
- ✅ Tests work on any machine
- ✅ No hardcoded paths
- ✅ CI/CD compatible
- ✅ Other developers can run tests

**Example path calculation:**
```csharp
// From: D:\Work\Fast...\tests\...\bin\Debug\net8.0
// To:   D:\Work\Fast...\cyclone-compiled\bin\idlc.exe
// Via: testDir → ../.5x → repoRoot → cyclone-compiled/bin/idlc.exe
```

**This makes tests truly portable!**

---

**Batch Version:** 1.0  
**Last Updated:** 2026-01-18  
**Prepared by:** Development Lead

**CRITICAL:** This MUST be fixed before committing BATCH-15.2!
