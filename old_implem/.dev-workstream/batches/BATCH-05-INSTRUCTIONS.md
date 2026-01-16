# BATCH-05: Alignment and Layout Calculator + BATCH-04 Fixes

**Batch Number:** BATCH-05  
**Tasks:** FCDC-008 (Alignment Calculator), BATCH-04 Fixes (Quaternion typedef, BoundedSeq test)  
**Phase:** Phase 2 - Code Generator  
**Estimated Effort:** 3-4 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-04 (IDL Code Emitter)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch implements C-compatible **struct/union alignment and layout calculation**, which is CRITICAL for generating correct native types in later batches. You'll calculate padding, field offsets, and total sizes according to C alignment rules.

Additionally, this batch includes **minor fixes from BATCH-04** review.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md`
2. **Task Definition:** `docs/FCDC-TASK-MASTER.md` → See FCDC-008
3. **Design Document:** `docs/FCDC-DETAILED-DESIGN.md` → §5.3 Alignment and Padding Calculation
4. **Previous Review:** `.dev-workstream/reviews/BATCH-04-REVIEW.md` - Learn from BATCH-04 feedback
5. **BATCH-04 Instructions:** `.dev-workstream/batches/BATCH-04-INSTRUCTIONS.md` - Context for fixes

### Source Code Location

- **Primary Work Area:** `tools/CycloneDDS.CodeGen/`
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/`

### Report Submission

**When done, create:**  
`.dev-workstream/reports/BATCH-05-REPORT.md`

---

## 🎯 Objectives

Implement alignment and layout calculation for C-compatible structs and unions:

1. **Fix BATCH-04 Issues** - Correct Quaternion typedef, add BoundedSeq test
2. **Alignment Calculator** - Calculate field alignments per C rules
3. **Struct Layout** - Compute field offsets with padding
4. **Union Layout** - Calculate payload offset based on max discriminator/arm alignment
5. **Size Calculation** - Total struct/union size with trailing padding
6. **Validation** - Debug assertions for future sizeof/offsetof checks

---

## ✅ Tasks

### Task 1: Fix BATCH-04 Issue - Quaternion Typedef

**File:** `tools/CycloneDDS.CodeGen/Emitters/IdlTypeMapper.cs` (MODIFY)

**Problem:** Line 111 has incorrect IDL syntax for Quaternion:
```csharp
_ when csType.Contains("Quaternion") => "typedef QuaternionF32x4 { float x, y, z, w; };",
```

**Fix:** Change to struct definition instead of typedef:
```csharp
_ when csType.Contains("Quaternion") => "struct QuaternionF32x4 { float x; float y; float z; float w; };",
```

**Why:** The current syntax is mixing typedef and struct. IDL requires separate statements.

**Test:** Add test in `IdlEmitterTests.cs`:
```csharp
[Fact]
public void StructWithQuaternion_EmitsStructDefinition()
{
    var csCode = @"
[DdsTopic(""QuaternionTopic"")]
public partial class QuaternionType
{
    public Quaternion Rotation;
}";
    
    var type = ParseType(csCode);
    var emitter = new IdlEmitter();
    var idl = emitter.GenerateIdl(type, "QuaternionTopic");
    
    Assert.Contains("struct QuaternionF32x4 { float x; float y; float z; float w; };", idl);
    Assert.Contains("QuaternionF32x4 Rotation;", idl);
}
```

---

### Task 2: Fix BATCH-04 Issue - Add BoundedSeq Test

**File:** `tests/CycloneDDS.CodeGen.Tests/IdlEmitterTests.cs` (MODIFY)

**Add test for BoundedSeq<T,N> type mapping:**

```csharp
[Fact]
public void StructWithBoundedSeq_EmitsBoundedSequence()
{
    var csCode = @"
[DdsTopic(""BoundedSeqTopic"")]
public partial class BoundedSeqType
{
    public BoundedSeq<int, 100> LimitedData;
}";
    
    var type = ParseType(csCode);
    var emitter = new IdlEmitter();
    var idl = emitter.GenerateIdl(type, "BoundedSeqTopic");
    
    // Should emit: sequence<long, 100> LimitedData;
    Assert.Contains("sequence<long, 100> LimitedData;", idl);
}
```

**Verify:** Existing `IdlTypeMapper.MapCustomType` handles this correctly (lines 61-75).

---

### Task 3: Implement Alignment Calculator

**File:** `tools/CycloneDDS.CodeGen/Layout/AlignmentCalculator.cs` (NEW)

Calculates field alignments according to C rules:

```csharp
namespace CycloneDDS.CodeGen.Layout;

public static class AlignmentCalculator
{
    /// <summary>
    /// Get the alignment requirement for a C type in bytes.
    /// </summary>
    public static int GetAlignment(string cType)
    {
        return cType switch
        {
            // Primitives
            "octet" or "int8" or "uint8" => 1,
            "int16" or "uint16" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            "boolean" => 1,
            "char" or "wchar" => 1, // or 2 depending on encoding
            
            // String/sequence pointers
            "string" => IntPtr.Size, // Pointer size (4 or 8 bytes)
            _ when cType.StartsWith("sequence<") => IntPtr.Size,
            
            // Fixed arrays - alignment of element type
            _ when cType.Contains("[") => GetArrayAlignment(cType),
            
            // Assume user-defined struct/enum - need more context
            // For now, default to pointer alignment for safety
            _ => 4 // Conservative default
        };
    }
    
    private static int GetArrayAlignment(string cType)
    {
        // Extract element type from "octet[32]" -> "octet"
        var openBracket = cType.IndexOf('[');
        if (openBracket > 0)
        {
            var elementType = cType.Substring(0, openBracket).Trim();
            return GetAlignment(elementType);
        }
        return 1;
    }
    
    /// <summary>
    /// Calculate the next aligned offset for a given alignment.
    /// </summary>
    public static int AlignUp(int offset, int alignment)
    {
        if (alignment <= 1) return offset;
        return (offset + alignment - 1) & ~(alignment - 1);
    }
    
    /// <summary>
    /// Calculate padding needed to align offset.
    /// </summary>
    public static int CalculatePadding(int currentOffset, int alignment)
    {
        var aligned = AlignUp(currentOffset, alignment);
        return aligned - currentOffset;
    }
}
```

---

### Task 4: Implement Struct Layout Calculator

**File:** `tools/CycloneDDS.CodeGen/Layout/StructLayoutCalculator.cs` (NEW)

```csharp
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen.Layout;

public class FieldLayout
{
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = "";
    public int Offset { get; set; }
    public int Size { get; set; }
    public int Alignment { get; set; }
    public int PaddingBefore { get; set; }
}

public class StructLayout
{
    public List<FieldLayout> Fields { get; set; } = new();
    public int TotalSize { get; set; }
    public int MaxAlignment { get; set; }
    public int TrailingPadding { get; set; }
}

public class StructLayoutCalculator
{
    /// <summary>
    /// Calculate C-compatible struct layout with padding.
    /// </summary>
    public StructLayout CalculateLayout(TypeDeclarationSyntax type)
    {
        var layout = new StructLayout();
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        
        int currentOffset = 0;
        int maxAlignment = 1;
        
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
            
            // Map C# type to IDL/C type for alignment calculation
            var idlType = IdlTypeMapper.MapToIdl(fieldType);
            var alignment = AlignmentCalculator.GetAlignment(idlType);
            var size = GetTypeSize(idlType);
            
            // Calculate padding needed
            var padding = AlignmentCalculator.CalculatePadding(currentOffset, alignment);
            var fieldOffset = currentOffset + padding;
            
            layout.Fields.Add(new FieldLayout
            {
                FieldName = fieldName,
                FieldType = idlType,
                Offset = fieldOffset,
                Size = size,
                Alignment = alignment,
                PaddingBefore = padding
            });
            
            currentOffset = fieldOffset + size;
            maxAlignment = Math.Max(maxAlignment, alignment);
        }
        
        // Add trailing padding to align struct to its max field alignment
        var trailingPadding = AlignmentCalculator.CalculatePadding(currentOffset, maxAlignment);
        
        layout.MaxAlignment = maxAlignment;
        layout.TotalSize = currentOffset + trailingPadding;
        layout.TrailingPadding = trailingPadding;
        
        return layout;
    }
    
    private int GetTypeSize(string idlType)
    {
        return idlType switch
        {
            "octet" or "int8" or "uint8" or "boolean" => 1,
            "int16" or "uint16" or "char" or "wchar" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            
            // Pointers for strings/sequences
            "string" => IntPtr.Size,
            _ when idlType.StartsWith("sequence<") => IntPtr.Size * 2, // ptr + length
            
            // Fixed arrays: "octet[32]" -> 32 bytes
            _ when idlType.Contains("[") => GetFixedArraySize(idlType),
            
            // Unknown types - conservative estimate
            _ => 4
        };
    }
    
    private int GetFixedArraySize(string idlType)
    {
        // Extract size from "octet[32]"
        var start = idlType.IndexOf('[');
        var end = idlType.IndexOf(']');
        if (start > 0 && end > start)
        {
            var sizeStr = idlType.Substring(start + 1, end - start - 1);
            if (int.TryParse(sizeStr, out var arraySize))
            {
                var elementType = idlType.Substring(0, start).Trim();
                var elementSize = GetTypeSize(elementType);
                return elementSize * arraySize;
            }
        }
        return 4; // Fallback
    }
}
```

---

### Task 5: Implement Union Layout Calculator

**File:** `tools/CycloneDDS.CodeGen/Layout/UnionLayoutCalculator.cs` (NEW)

```csharp
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen.Layout;

public class UnionLayout
{
    public string DiscriminatorType { get; set; } = "";
    public int DiscriminatorSize { get; set; }
    public int DiscriminatorAlignment { get; set; }
    public int PayloadOffset { get; set; } // Offset where union payload starts
    public int MaxArmSize { get; set; }
    public int MaxArmAlignment { get; set; }
    public int TotalSize { get; set; }
}

public class UnionLayoutCalculator
{
    /// <summary>
    /// Calculate C-compatible union layout.
    /// Union payload offset is determined by max(discriminator_alignment, max_arm_alignment).
    /// </summary>
    public UnionLayout CalculateLayout(TypeDeclarationSyntax type)
    {
        var layout = new UnionLayout();
        
        // Find discriminator
        var discriminatorField = type.Members
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsDiscriminator" or "DdsDiscriminatorAttribute"));
        
        if (discriminatorField == null)
        {
            // Error: union must have discriminator
            layout.TotalSize = 0;
            return layout;
        }
        
        var discriminatorType = discriminatorField.Declaration.Type.ToString();
        var idlDiscriminator = IdlTypeMapper.MapToIdl(discriminatorType);
        layout.DiscriminatorType = idlDiscriminator;
        layout.DiscriminatorSize = GetTypeSize(idlDiscriminator);
        layout.DiscriminatorAlignment = AlignmentCalculator.GetAlignment(idlDiscriminator);
        
        // Find all case arms
        var caseFields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsCase" or "DdsCaseAttribute" or "DdsDefaultCase" or "DdsDefaultCaseAttribute"));
        
        int maxArmSize = 0;
        int maxArmAlignment = 1;
        
        foreach (var arm in caseFields)
        {
            var armType = arm.Declaration.Type.ToString();
            var idlArmType = IdlTypeMapper.MapToIdl(armType);
            var armSize = GetTypeSize(idlArmType);
            var armAlignment = AlignmentCalculator.GetAlignment(idlArmType);
            
            maxArmSize = Math.Max(maxArmSize, armSize);
            maxArmAlignment = Math.Max(maxArmAlignment, armAlignment);
        }
        
        layout.MaxArmSize = maxArmSize;
        layout.MaxArmAlignment = maxArmAlignment;
        
        // Payload offset: discriminator, then padding to align to max arm alignment
        var payloadAlignment = Math.Max(layout.DiscriminatorAlignment, maxArmAlignment);
        layout.PayloadOffset = AlignmentCalculator.AlignUp(layout.DiscriminatorSize, payloadAlignment);
        
        // Total size: payload offset + max arm size + trailing padding
        var sizeBeforePadding = layout.PayloadOffset + maxArmSize;
        var unionAlignment = Math.Max(layout.DiscriminatorAlignment, maxArmAlignment);
        layout.TotalSize = AlignmentCalculator.AlignUp(sizeBeforePadding, unionAlignment);
        
        return layout;
    }
    
    private int GetTypeSize(string idlType)
    {
        // Reuse logic from StructLayoutCalculator
        // TODO: Extract to shared helper class to avoid duplication
        return idlType switch
        {
            "octet" or "int8" or "uint8" or "boolean" => 1,
            "int16" or "uint16" or "char" or "wchar" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            "string" => IntPtr.Size,
            _ when idlType.StartsWith("sequence<") => IntPtr.Size * 2,
            _ => 4
        };
    }
}
```

---

## 🧪 Testing Requirements

### Test Project: `tests/CycloneDDS.CodeGen.Tests/LayoutCalculatorTests.cs` (NEW)

**Minimum 12 tests required:**

1. ✅ `SimpleStruct_CalculatesCorrectLayout`
2. ✅ `StructWithPadding_InsertsCorrectPadding`
3. ✅ `StructWithTrailingPadding_AlignsToMaxField`
4. ✅ `StructWithInt64_AlignedTo8Bytes`
5. ✅ `StructWithMixedTypes_CorrectOffsets`
6. ✅ `StructWithFixedArray_CalculatesCorrectSize`
7. ✅ `Union_CalculatesPayloadOffset`
8. ✅ `UnionWithInt64Arm_PayloadAlignedTo8`
9. ✅ `UnionWithSmallDiscriminator_HasPadding`
10. ✅ `UnionWithLargeArm_CalculatesCorrectTotalSize`
11. ✅ `AlignmentCalculator_AlignUpWorksCorrectly`
12. ✅ `AlignmentCalculator_CalculatesPaddingCorrectly`

### Example Test:

```csharp
using Xunit;
using CycloneDDS.CodeGen.Layout;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class LayoutCalculatorTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }
    
    [Fact]
    public void SimpleStruct_CalculatesCorrectLayout()
    {
        var csCode = @"
public partial class SimpleStruct
{
    public byte B;   // offset 0, size 1
    public int I;    // offset 4 (aligned to 4), size 4
    public short S;  // offset 8, size 2
}";
        
        var type = ParseType(csCode);
        var calculator = new StructLayoutCalculator();
        var layout = calculator.CalculateLayout(type);
        
        Assert.Equal(3, layout.Fields.Count);
        
        // byte B: offset 0
        Assert.Equal(0, layout.Fields[0].Offset);
        Assert.Equal(1, layout.Fields[0].Size);
        
        // int I: aligned to 4, so offset 4 (3 bytes padding after B)
        Assert.Equal(4, layout.Fields[1].Offset);
        Assert.Equal(3, layout.Fields[1].PaddingBefore);
        
        // short S: aligned to 2, offset 8
        Assert.Equal(8, layout.Fields[2].Offset);
        
        // Max alignment is 4 (from int), so total size = 12 (10 + 2 trailing)
        Assert.Equal(4, layout.MaxAlignment);
        Assert.Equal(12, layout.TotalSize);
    }
    
    [Fact]
    public void Union_CalculatesPayloadOffset()
    {
        var csCode = @"
[DdsUnion]
public partial class MyUnion
{
    [DdsDiscriminator]
    public byte D;       // 1 byte, alignment 1
    [DdsCase(1)]
    public long Arm64;   // 8 bytes, alignment 8
}";
        
        var type = ParseType(csCode);
        var calculator = new UnionLayoutCalculator();
        var layout = calculator.CalculateLayout(type);
        
        // Discriminator is 1 byte
        Assert.Equal(1, layout.DiscriminatorSize);
        
        // Payload must be aligned to 8 (max arm alignment)
        // So payload offset = AlignUp(1, 8) = 8
        Assert.Equal(8, layout.PayloadOffset);
        
        // Total size = 8 (payload offset) + 8 (arm size) = 16
        Assert.Equal(16, layout.TotalSize);
    }
    
    // ... implement remaining 10 tests ...
}
```

---

## 📊 Report Requirements

### Required Sections

1. **Executive Summary**
   - BATCH-04 fixes completed
   - Alignment calculation implemented
   - Struct/union layout working

2. **Implementation Details**
   - How C alignment rules are implemented
   - Struct padding strategy
   - Union payload offset calculation

3. **Test Results**
   - All 12+ layout tests passing
   - BATCH-04 fixes verified
   - Example layout calculations

4. **Developer Insights**

   **Q1:** What was the trickiest part of union layout calculation?

   **Q2:** How would you extend this to handle nested structs with their own alignment?

   **Q3:** Did you find any edge cases in padding calculation that aren't tested yet?

   **Q4:** How would you validate that calculated layouts match actual C compiler output?

5. **Code Quality Checklist**
   - [ ] BATCH-04 Quaternion fix applied
   - [ ] BATCH-04 BoundedSeq test added
   - [ ] AlignmentCalculator implemented
   - [ ] StructLayoutCalculator implemented
   - [ ] UnionLayoutCalculator implemented
   - [ ] AlignUp and padding functions working
   - [ ] 12+ tests passing
   - [ ] All BATCH-04 tests still passing

---

## 🎯 Success Criteria

This batch is DONE when:

1. ✅ BATCH-04 Quaternion typedef fixed
2. ✅ BATCH-04 BoundedSeq test added and passing
3. ✅ All BATCH-04 tests still passing (11 tests)
4. ✅ AlignmentCalculator calculates correct alignments for C types
5. ✅ StructLayoutCalculator computes correct offsets with padding
6. ✅ UnionLayoutCalculator computes correct payload offset
7. ✅ Trailing padding calculated correctly
8. ✅ Minimum 12 new layout tests passing
9. ✅ Report submitted

---

## ⚠️ Common Pitfalls to Avoid

1. **Don't forget trailing padding** - Structs must be aligned to their max field alignment
2. **Union payload alignment** - Must be max(discriminator_align, max_arm_align), NOT just max_arm_align
3. **Fixed array sizes** - `octet[32]` is 32 bytes, not 4 bytes
4. **Pointer sizes** - Use IntPtr.Size for portability (4 on 32-bit, 8 on 64-bit)
5. **Test cross-platform** - Alignment rules should work on both 32-bit and 64-bit
6. **Extract duplication** - GetTypeSize appears in both calculators, consider shared helper

---

## 📚 Reference Materials

- **Task Definition:** `docs/FCDC-TASK-MASTER.md` (FCDC-008)
- **Design:** `docs/FCDC-DETAILED-DESIGN.md` (§5.3 Alignment and Padding Calculation)
- **BATCH-04 Review:** `.dev-workstream/reviews/BATCH-04-REVIEW.md` (Issues to fix)
- **C Alignment Rules:** https://en.cppreference.com/w/c/language/object#Alignment

---

**Focus: Implement CORRECT C-compatible alignment and layout calculation. This is foundational for all future native code generation.**
