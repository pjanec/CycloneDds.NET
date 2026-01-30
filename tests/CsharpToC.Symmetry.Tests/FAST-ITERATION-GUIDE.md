# CsharpToC.Symmetry.Tests - Fast Iteration Guide

**Project:** FastCycloneDDS C# Bindings  
**Version:** 1.0  
**Date:** January 29, 2026  
**Audience:** Developers fixing serialization bugs

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Understanding the Problem](#understanding-the-problem)
3. [The Hot-Patch Workflow](#the-hot-patch-workflow)
4. [Step-by-Step Example](#step-by-step-example)
5. [Reading CDR Analysis Documents](#reading-cdr-analysis-documents)
6. [Common Bug Patterns](#common-bug-patterns)
7. [Backporting to Emitter](#backporting-to-emitter)
8. [Troubleshooting](#troubleshooting)
9. [Best Practices](#best-practices)
10. [Advanced Techniques](#advanced-techniques)

---

## Quick Start

**Goal:** Fix a failing serialization test in < 30 minutes

### Setup (5 minutes)

```powershell
# 1. Clone and navigate to project
cd D:\WORK\FastCycloneDdsCsharpBindings\tests\CsharpToC.Symmetry

# 2. Initial build (generates code and golden data)
.\rebuild_and_test.ps1

# 3. Note which tests fail
# Output will show: "Failed: TestArrayStringAppendable"
```

### Iterate (2-5 seconds per cycle)

```powershell
# 1. Edit the generated file directly
code obj\Generated\AtomicTests\ArrayStringTopicAppendable.Serializer.cs

# 2. Save changes

# 3. Re-run ONLY that test (no rebuild!)
.\run_tests_only.ps1 -Filter "TestArrayStringAppendable"

# 4. Repeat until test passes
```

### Finalize (10 minutes)

```powershell
# 1. Identify the fix pattern
# 2. Update SerializerEmitter.cs
# 3. Regenerate all code
.\rebuild_and_test.ps1

# 4. Verify no regressions
# All previously passing tests should still pass
```

**Total Time:** ~15-30 minutes (vs. 1-2 hours with traditional workflow)

---

## Understanding the Problem

### Why Traditional Testing is Slow

```
Traditional Workflow (60+ seconds per iteration):
┌─────────────────────────────────────────────┐
│ 1. Edit SerializerEmitter.cs                │  Developer action
├─────────────────────────────────────────────┤
│ 2. Run CodeGen (generates 110 .cs files)    │  ~30 seconds
├─────────────────────────────────────────────┤
│ 3. Rebuild solution (compile all files)     │  ~20 seconds
├─────────────────────────────────────────────┤
│ 4. Run tests (with native DLL & DDS)        │  ~10 seconds
├─────────────────────────────────────────────┤
│ 5. See result                                │  Developer sees output
└─────────────────────────────────────────────┘
Total: ~60 seconds
```

**Problem:** You're regenerating and testing **all 110 topics** when you only care about **one failing test**.

### The Symmetry Approach

```
Hot-Patch Workflow (2-5 seconds per iteration):
┌─────────────────────────────────────────────┐
│ 1. Edit SPECIFIC generated .cs file         │  Developer action
├─────────────────────────────────────────────┤
│ 2. Run test with --no-build flag            │  ~2 seconds
├─────────────────────────────────────────────┤
│ 3. See result                                │  Developer sees output
└─────────────────────────────────────────────┘
Total: ~2 seconds
```

**Key Insight:** We bypass CodeGen and rebuild by editing the already-compiled code directly. The C# compiler picks up the changes incrementally.

---

## The Hot-Patch Workflow

### Concept

When you run `dotnet build`, the generated C# files are compiled into DLLs in the `bin/` and `obj/` directories. These DLLs contain the serialization logic.

**The Trick:** You can modify the source `.cs` files in `obj/Generated/`, and the next `dotnet test --no-build` will use your modified version (via incremental compilation mechanisms).

### The Magic Flag: `--no-build`

```powershell
dotnet test --no-build --filter "TestArrayStringAppendable"
```

**What this does:**
- ✅ Runs tests using existing DLL
- ✅ Picks up modified source files from `obj/Generated/`
- ❌ Does NOT run CodeGen
- ❌ Does NOT recompile everything

**Result:** Test runs in ~2 seconds instead of ~60 seconds

### Safety Rails

**Q: Won't I accidentally commit broken generated code?**  
**A:** No! The `obj/` folder is in `.gitignore`. Your manual edits are never committed.

**Q: How do I "undo" my manual changes?**  
**A:** Just run `.\rebuild_and_test.ps1`. It regenerates everything from scratch.

**Q: What if I mess up badly?**  
**A:** Delete `obj/` and `bin/` folders, then rebuild. Fresh start.

---

## Step-by-Step Example

### Scenario: Fixing "TestArrayStringAppendable"

#### Step 1: Identify the Failure

```powershell
PS> .\rebuild_and_test.ps1 -Filter "ArrayString"

Running tests...
[FAIL] TestArrayStringAppendable
  Symmetry test failed for AtomicTests::ArrayStringTopicAppendable
  Phase: Serialization
  Expected: 00 01 00 00 03 00 00 00 53 74 72 5F 31 34 32 30 5F 30 00
  Actual:   00 01 00 00 03 00 53 74 72 5F 31 34 32 30 5F 30 00
                            ^^^^^^^ Missing padding!
```

**Analysis:** The actual bytes are missing `00 00` padding before the string. This is a classic XCDR2 alignment bug.

#### Step 2: Locate the Generated File

```powershell
# Open in VS Code
code obj\Generated\AtomicTests\ArrayStringTopicAppendable.Serializer.cs
```

**Tip:** Use Ctrl+P in VS Code and type "ArrayStringTopic" to find the file quickly.

#### Step 3: Review the CDR Analysis

Open the CDR analysis document for this topic (provided separately or in `docs/`):

```
Topic: ArrayStringTopicAppendable (@appendable)
Encoding: XCDR2
Structure:
  Offset 0-3:   DHEADER (encapsulation + options)
  Offset 4-7:   Array length (uint32) = 3
  Offset 8:     Align to 4 bytes  <-- This is missing!
  Offset 8-N:   String elements...
```

**Key Finding:** We need to align to 4 bytes after the array length but before the first string element.

#### Step 4: Examine the Generated Code

```csharp
public void Serialize(ref CdrWriter writer)
{
    // DHEADER
    writer.WriteUInt32(0); // placeholder
    
    // Array length
    writer.WriteUInt32((uint)Strings.Length);
    
    // Array elements
    foreach (var str in Strings)
    {
        writer.WriteString(str);
    }
}
```

**Problem:** Missing `writer.Align(4);` before the loop!

#### Step 5: Apply the Fix

Edit the file:

```csharp
public void Serialize(ref CdrWriter writer)
{
    // DHEADER
    writer.WriteUInt32(0); // placeholder
    
    // Array length
    writer.WriteUInt32((uint)Strings.Length);
    
    // Align before array elements (XCDR2 rule for variable-size collections)
    writer.Align(4);  // <-- ADDED THIS LINE
    
    // Array elements
    foreach (var str in Strings)
    {
        writer.WriteString(str);
    }
}
```

Save the file (Ctrl+S).

#### Step 6: Re-run the Test

```powershell
PS> .\run_tests_only.ps1 -Filter "TestArrayStringAppendable"

Running tests (hot-patch mode)...
[PASS] TestArrayStringAppendable (14ms)

Tests: 1 passed, 0 failed
Time: 2.1 seconds
```

✅ **Success!** The test now passes.

#### Step 7: Backport to Emitter

Now that you know the fix, update the code generator:

**File:** `tools\CycloneDDS.CodeGen\SerializerEmitter.cs`

Find the method that generates array serialization:

```csharp
private void EmitArraySerialization(ArrayType array, CdrEncoding encoding)
{
    // ... existing code ...
    
    // Write array length for XCDR2
    if (encoding == CdrEncoding.Xcdr2)
    {
        _writer.WriteLine("writer.WriteUInt32((uint)array.Length);");
        
        // NEW: Add alignment before elements for variable-size types
        if (array.ElementType.IsVariableSize)
        {
            _writer.WriteLine("writer.Align(4);");
        }
    }
    
    // ... rest of code ...
}
```

#### Step 8: Validate No Regressions

```powershell
PS> .\rebuild_and_test.ps1

Rebuilding entire solution...
Running all tests...

Part 1: 30 passed, 0 failed
Part 2: 38 passed, 2 failed  <-- Some tests still failing (expected)
Part 3: 20 passed, 5 failed
Part 4: 10 passed, 5 failed

Total: 98 passed, 12 failed
```

**Analysis:** 
- ✅ TestArrayStringAppendable now passes
- ✅ All previously passing tests still pass (no regressions)
- ⏳ 12 tests still failing (work on those next)

---

## Reading CDR Analysis Documents

Each test case has a corresponding CDR analysis that explains the expected byte structure.

### Example: CharTopic

```
Topic: AtomicTests::CharTopic
Extensibility: @final (XCDR1)
Seed: 1420

Expected CDR Stream (19 bytes):
Offset | Hex        | Interpretation
-------|------------|----------------------------------
0-1    | 00 01      | Encapsulation (Big Endian)
2-3    | 00 00      | Options (unused)
4      | 42         | char value ('B')
5-7    | 00 00 00   | Padding to align next field
8-15   | 00 00 00 00| int64 default value (0)
        | 00 00 00 00
16-18  | 00 00 00   | Padding (struct end)
```

### How to Use This

1. **Compare with test output:** When test fails, match the "Expected" vs "Actual" hex
2. **Identify offset:** Note at which byte offset the difference starts
3. **Check interpretation:** See what field or padding is at that offset
4. **Fix code:** Adjust generated code to match expected structure

### Common Patterns

| Pattern | Meaning | Fix |
|---------|---------|-----|
| Extra `00 00 00` | Too much padding | Remove or reduce `Align()` call |
| Missing `00 00` | Too little padding | Add `Align()` call |
| Wrong byte count | Wrong data type | Check `Write` method (e.g., `WriteInt32` vs `WriteInt64`) |
| Inverted bytes | Wrong endianness | Rare, usually not an issue in modern systems |

---

## Common Bug Patterns

### 1. Missing Alignment Before Variable-Size Collection

**Symptom:** Missing padding before sequences/arrays of strings

**Fix:**
```csharp
writer.WriteUInt32((uint)collection.Length);
writer.Align(4);  // Add this
foreach (var item in collection) { ... }
```

**Emitter Pattern:**
```csharp
if (encoding == CdrEncoding.Xcdr2 && isVariableSize)
{
    EmitLine("writer.Align(4);");
}
```

### 2. Wrong DHEADER Size Calculation

**Symptom:** First 4 bytes are incorrect in @appendable structs

**Fix:**
```csharp
// Wrong
writer.WriteUInt32(0);

// Right
int headerPos = writer.Position;
writer.WriteUInt32(0); // placeholder
// ... serialize members ...
int endPos = writer.Position;
writer.WriteUInt32At(headerPos, (uint)(endPos - headerPos - 4));
```

### 3. Missing EMHEADER in @mutable

**Symptom:** @mutable structs missing member headers

**Fix:**
```csharp
// Before each member in @mutable
uint memberId = GetMemberId(memberName);
uint memberSize = CalculateSize(memberValue);
writer.WriteUInt32((memberId << 16) | (memberSize & 0xFFFF));
```

### 4. Incorrect Union Discriminator Alignment

**Symptom:** Union values misaligned after discriminator

**Fix:**
```csharp
writer.WriteInt32(discriminator);
writer.Align(GetAlignment(selectedCase));  // Add alignment
writer.Write(selectedCaseValue);
```

### 5. String Length Encoding Error

**Symptom:** Strings are cut off or have extra garbage

**Fix:**
```csharp
// Ensure length includes null terminator
writer.WriteUInt32((uint)(str.Length + 1));
writer.WriteString(str);
```

---

## Backporting to Emitter

### General Process

1. **Identify the change:** What specific line(s) did you modify?
2. **Find the pattern:** Is this change needed for all arrays? All XCDR2? Specific conditions?
3. **Locate emitter code:** Find the method in `SerializerEmitter.cs` or `DeserializerEmitter.cs`
4. **Add conditional logic:** Implement the fix with proper conditions
5. **Test with regeneration:** Run `.\rebuild_and_test.ps1` to verify

### Example: Adding Alignment Logic

**Manual Fix:**
```csharp
// In obj/Generated/Topic.Serializer.cs
writer.Align(4);
```

**Emitter Update:**
```csharp
// In SerializerEmitter.cs
private void EmitAlignment(int alignment, CdrEncoding encoding)
{
    if (encoding == CdrEncoding.Xcdr2)
    {
        _writer.WriteLine($"writer.Align({alignment});");
    }
}
```

**Usage in emitter:**
```csharp
private void EmitSequenceSerialization(SequenceType seq)
{
    EmitLine("writer.WriteUInt32((uint)sequence.Count);");
    
    if (seq.ElementType.IsVariableSize)
    {
        EmitAlignment(4, _encoding);  // Use helper
    }
    
    // ... emit loop ...
}
```

### Validation Checklist

After updating emitter:

- [ ] Regenerate all code: `dotnet build`
- [ ] The specific test still passes
- [ ] Run full suite: `.\rebuild_and_test.ps1`
- [ ] No new failures (regressions)
- [ ] Review generated code for 2-3 similar topics (spot check)

---

## Troubleshooting

### Issue: "Test still fails after my fix"

**Possible Causes:**
1. **Wrong file edited:** Check you edited the correct topic's file
2. **Not saved:** Ensure you saved the file (Ctrl+S)
3. **Cache issue:** Try deleting `bin/` and `obj/`, then rebuild

**Debug Steps:**
```powershell
# 1. Verify your edit is present
cat obj\Generated\AtomicTests\TopicName.Serializer.cs | Select-String "Align"

# 2. Try full rebuild
.\rebuild_and_test.ps1 -Filter "TopicName"

# 3. Add debug output to generated code
Console.WriteLine($"Position before: {writer.Position}");
writer.Align(4);
Console.WriteLine($"Position after: {writer.Position}");
```

### Issue: "Multiple tests fail after regeneration"

**Possible Causes:**
1. **Emitter logic too broad:** Your fix applies to cases where it shouldn't
2. **Missing condition:** Need to check encoding, extensibility, or type

**Debug Steps:**
```csharp
// Add condition to emitter
if (encoding == CdrEncoding.Xcdr2 && 
    isAppendable &&                   // Add more specific conditions
    elementType.IsVariableSize)
{
    EmitLine("writer.Align(4);");
}
```

### Issue: "Test passes but byte count is wrong"

**Possible Causes:**
1. **Alignment added but size calculator not updated:** `CdrSizer` needs matching logic
2. **DHEADER not updated:** Size calculation includes headers

**Fix:**
Update both `SerializerEmitter.cs` AND `CdrSizeCalculator.cs`:

```csharp
// In CdrSizeCalculator.cs
if (isVariableSize)
{
    size += 4; // alignment padding
}
```

### Issue: "Golden data seems wrong"

**Possible Causes:**
1. **Native DLL out of date:** Regenerate golden data
2. **Wrong seed used:** Check test code for seed value
3. **Topic name mismatch:** Ensure exact match including `::`

**Fix:**
```powershell
# Regenerate golden data for specific test
.\generate_golden_data.ps1 -Filter "TopicName" -Force

# Or regenerate all
.\generate_golden_data.ps1 -Force
```

---

## Best Practices

### DO ✅

1. **Work on one test at a time**
   - Focus on single failing test
   - Fix it completely before moving to next
   
2. **Use version control**
   - Commit after each successful emitter update
   - Easy to revert if something breaks

3. **Document your fixes**
   - Add comments explaining why alignment is needed
   - Reference CDR spec sections if applicable

4. **Test incrementally**
   - Run single test first: `.\run_tests_only.ps1 -Filter "TestName"`
   - Then run category: `.\run_tests_only.ps1 -Filter "Part2"`
   - Finally run all: `.\rebuild_and_test.ps1`

5. **Keep CDR analysis handy**
   - Open analysis document in second monitor/window
   - Reference it when interpreting hex diffs

### DON'T ❌

1. **Don't edit multiple files at once**
   - Hard to track what fixed what
   - Increases risk of regressions

2. **Don't skip emitter update**
   - Manual fixes are temporary
   - Must backport to emitter for permanence

3. **Don't ignore regressions**
   - If previously passing test fails, investigate immediately
   - Don't proceed until resolved

4. **Don't trust memory**
   - Document your fix patterns
   - You'll forget details in a week

5. **Don't modify golden data manually**
   - Always regenerate via script
   - Manual edits will cause confusion

---

## Advanced Techniques

### Batch Testing Multiple Fixes

If you've fixed multiple topics manually:

```powershell
# Test all your fixes at once
.\run_tests_only.ps1 -Filter "TestArrayString|TestSequenceInt|TestUnionChar"
```

### Using xUnit Traits for Organization

Add traits to test methods:

```csharp
[Fact]
[Trait("Category", "Collections")]
[Trait("Encoding", "XCDR2")]
public void TestArrayStringAppendable() { ... }
```

Then filter by trait:

```powershell
.\run_tests_only.ps1 -Filter "Category=Collections"
```

### Automated Diff Analysis

Create helper script to compare hex:

```powershell
# compare_hex.ps1
param([string]$Expected, [string]$Actual)

$exp = $Expected -split ' '
$act = $Actual -split ' '

for ($i = 0; $i -lt $exp.Length; $i++) {
    if ($exp[$i] -ne $act[$i]) {
        Write-Host "First diff at offset $i" -ForegroundColor Red
        Write-Host "Expected: $($exp[$i])" -ForegroundColor Yellow
        Write-Host "Actual:   $($act[$i])" -ForegroundColor Yellow
        break
    }
}
```

Usage:

```powershell
.\compare_hex.ps1 -Expected "00 01 00 00 42" -Actual "00 01 42"
# Output: First diff at offset 2
```

### Profiling Test Performance

Add timing to specific tests:

```csharp
[Fact]
public void TestArrayStringAppendable()
{
    var sw = Stopwatch.StartNew();
    VerifySymmetry<ArrayStringTopicAppendable>(...);
    sw.Stop();
    
    Assert.True(sw.ElapsedMilliseconds < 50, 
        $"Test too slow: {sw.ElapsedMilliseconds}ms");
}
```

### Creating Test Groups

Organize related tests:

```csharp
public class StringTests : SymmetryTestBase
{
    [Theory]
    [InlineData("StringTopic", 1420)]
    [InlineData("BoundedStringTopic", 1421)]
    [InlineData("StringTopicAppendable", 1422)]
    public void TestStringVariants(string topicName, int seed)
    {
        VerifySymmetry<StringTopic>(...);
    }
}
```

---

## Performance Tips

### Maximizing Iteration Speed

1. **Use specific filters:**
   ```powershell
   # Slow: Runs all Part2 tests
   .\run_tests_only.ps1 -Filter "Part2"
   
   # Fast: Runs only one test
   .\run_tests_only.ps1 -Filter "TestArrayStringAppendable"
   ```

2. **Keep VS Code open:**
   - Editor keeps files in memory
   - Faster saves and reloads

3. **Use SSD:**
   - File I/O for golden data is faster on SSD
   - Consider moving project to SSD if on HDD

4. **Close other applications:**
   - Free up CPU and memory
   - Faster test execution

5. **Disable antivirus scanning:**
   - Antivirus can slow file writes
   - Add project folder to exclusions

### Expected Timings

On a typical development machine:

| Operation | Expected Time |
|-----------|---------------|
| Single test (hot-patch) | 1-3 seconds |
| Category (e.g., Part1 with 30 tests) | 3-5 seconds |
| Full suite (110 tests, hot-patch) | 5-10 seconds |
| Full rebuild + full suite | 40-60 seconds |
| Golden data regeneration | 30-60 seconds |

If your timings are significantly worse, investigate:
- Slow disk I/O
- CPU throttling
- Background processes
- Antivirus interference

---

## Summary Workflow Chart

```
┌─────────────────────────────────────────────┐
│  START: Test Fails                          │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  1. Read error message & hex diff           │
│     Identify which bytes are wrong          │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  2. Review CDR analysis document            │
│     Understand expected structure           │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  3. Open generated .cs file                 │
│     obj/Generated/AtomicTests/Topic.*.cs    │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  4. Edit code (add/remove Align, etc.)      │
│     Save file (Ctrl+S)                      │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  5. Run: .\run_tests_only.ps1 -Filter ...  │
└──────────────┬──────────────────────────────┘
               │
         ┌─────┴─────┐
         │           │
         ▼           ▼
      [PASS]      [FAIL]
         │           │
         │           └──────┐
         │                  │
         │           ┌──────▼──────┐
         │           │  Analyze    │
         │           │  new error  │
         │           └──────┬──────┘
         │                  │
         │           ┌──────▼──────┐
         │           │  Go to      │
         │           │  Step 4     │
         │           └─────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│  6. Identify fix pattern                    │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  7. Update SerializerEmitter.cs             │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  8. Run: .\rebuild_and_test.ps1            │
│     Verify no regressions                   │
└──────────────┬──────────────────────────────┘
               │
         ┌─────┴─────┐
         │           │
         ▼           ▼
   [All Pass]  [Regression]
         │           │
         │           └──────┐
         │                  │
         │           ┌──────▼──────┐
         │           │  Refine     │
         │           │  emitter    │
         │           │  logic      │
         │           └──────┬──────┘
         │                  │
         │           ┌──────▼──────┐
         │           │  Go to      │
         │           │  Step 8     │
         │           └─────────────┘
         │
         ▼
┌─────────────────────────────────────────────┐
│  9. Commit changes                          │
│     DONE: Move to next failing test         │
└─────────────────────────────────────────────┘
```

---

## Conclusion

The **hot-patch workflow** is the key innovation of the Symmetry test framework. By editing generated code directly and using `--no-build`, you achieve:

- ⚡ **10-20x faster iteration** (seconds instead of minutes)
- 🎯 **Focused debugging** (one test at a time)
- 🔍 **Immediate feedback** (see results instantly)
- 🛡️ **Safety** (manual edits never committed)

**Remember:**
1. Edit generated code for quick fixes
2. Backport to emitter for permanence
3. Always validate with full regression suite
4. Document your patterns

With practice, you'll be fixing serialization bugs in 10-15 minutes that would have taken hours with traditional workflows.

---

**Quick Reference Card:**

```powershell
# Hot-patch single test (2-5 seconds)
.\run_tests_only.ps1 -Filter "TestName"

# Full rebuild + all tests (40-60 seconds)
.\rebuild_and_test.ps1

# Regenerate golden data (30-60 seconds)
.\generate_golden_data.ps1 -Force

# Test entire category
.\run_tests_only.ps1 -Filter "Part2"
```

**Files to edit:**
- ✅ Hot-patch: `obj/Generated/AtomicTests/TopicName.*.cs`
- ✅ Permanent fix: `tools/CycloneDDS.CodeGen/*Emitter.cs`
- ❌ Never edit: `GoldenData/*.txt` (regenerate instead)

---

**Document Status:** ✅ Ready for Use  
**Questions?** See [DESIGN.md](DESIGN.md) for architecture details or [TASK-DETAILS.md](TASK-DETAILS.md) for implementation tasks
