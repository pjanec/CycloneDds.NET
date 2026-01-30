# BATCH-12: Managed Types Support ([DdsManaged])

**Batch Number:** BATCH-12  
**Tasks:** FCDC-S015 - [DdsManaged] Support for Managed Types  
**Phase:** Stage 2 - Code Generation (Final Generator Feature)  
**Estimated Effort:** 3-4 days  
**Priority:** HIGH  
**Dependencies:** FCDC-S011 (Variable Types), FCDC-S016 (Testing Suite)

---

## 📋 Welcome to the Project!

### About This Project

You're working on **FastCycloneDDS C# Bindings** - a high-performance DDS (Data Distribution Service) implementation for .NET.

**What is DDS?**  
DDS is a publish-subscribe middleware standard for distributed systems. Think of it like a super-fast message bus for real-time systems (robotics, autonomous vehicles, defense systems).

**What we're building:**  
C# bindings for Cyclone DDS (a C library) that generate serialization code at compile time for maximum performance.

**Where you fit in:**  
You're adding support for "managed types" - allowing developers to use familiar C# types like `string` and `List<T>` instead of our custom `BoundedSeq<T>`. This makes the API much more user-friendly.

**Current Status:**  
- ✅ Stage 1 Complete: CDR serialization primitives working
- ✅ Stage 2 Nearly Complete: Code generator works for structs, unions, sequences, optionals
- ⏳ **YOU ARE HERE:** Adding managed types support
- 🔵 Stage 3 Next: Runtime DDS integration

---

## 🗂 Project Structure & File Locations

**Repository Root:**  
```
d:\Work\FastCycloneDdsCsharpBindings\
```

**Source Code:**
```
d:\Work\FastCycloneDdsCsharpBindings\src\
├── CycloneDDS.Core\          - Serialization primitives (CdrWriter, CdrReader)
├── CycloneDDS.Schema\         - Attributes ([DdsManaged], [DdsUnion], etc.)
└── CycloneDDS.Runtime\        - DDS API bindings (not used yet)
```

**Code Generator:**
```
d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\
├── SerializerEmitter.cs       - YOU WILL MODIFY THIS
├── DeserializerEmitter.cs     - YOU WILL MODIFY THIS
├── TypeMapper.cs              - YOU MAY MODIFY THIS
├── TypeInfo.cs                - Type metadata classes
└── CodeGenerator.cs           - Main orchestrator
```

**Tests:**
```
d:\Work\FastCycloneDdsCsharpBindings\tests\
├── CycloneDDS.Core.Tests\     - Core serialization tests (57 tests)
├── CycloneDDS.Schema.Tests\   - Attribute tests (10 tests)
└── CycloneDDS.CodeGen.Tests\  - Generator tests (87 tests) - YOU WILL ADD TESTS HERE
```

**Documentation:**
```
d:\Work\FastCycloneDdsCsharpBindings\docs\
├── SERDATA-TASK-MASTER.md     - All tasks defined
└── (other design docs)
```

**Your Workspace:**
```
d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\
├── batches\                   - Batch instructions (YOU ARE HERE)
├── reports\                   - YOU WILL SUBMIT REPORT HERE
├── reviews\                   - Lead's reviews
├── questions\                 - ASK QUESTIONS HERE IF STUCK
└── TASK-TRACKER.md            - Overall progress
```

---

## 📚 Required Reading (READ IN THIS ORDER)

**1. Workflow Guide (15 minutes):**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
**Why:** Understand how to work with batches, submit reports, ask questions

**2. Task Definition (10 minutes):**  
`d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
**Find:** Section "FCDC-S015: [DdsManaged] Support (Managed Types)" (line ~895)  
**Why:** Understand the official requirement

**3. Current Test Patterns (20 minutes):**  
Browse these files to understand how tests are structured:
- `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\SerializerEmitterTests.cs`
- `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\ComplexCombinationTests.cs`
- `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\CodeGenTestBase.cs`

**4. Existing Emitter Code (30 minutes):**  
Study how current types are handled:
- `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SerializerEmitter.cs` - Lines 1-500
- `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\DeserializerEmitter.cs` - Lines 1-500

**5. Review Recent Work (15 minutes):**  
See what the previous developer did:
- `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-11.1-REVIEW.md`

**Total Reading Time:** ~90 minutes (take your time, make notes)

---

## 🎯 Your Mission

### Context: What Worked Before

**Current Approach (BoundedSeq<T>):**
```csharp
public struct MyData
{
    public BoundedSeq<int> Numbers;     // Custom type, no GC
    public FixedString32 Name;           // Fixed-size, no GC
}
```

This is **zero-allocation** but **not user-friendly** for C# developers.

### What You're Adding (Managed Types)

**New Approach with [DdsManaged]:**
```csharp
[DdsManaged]  // <-- NEW ATTRIBUTE
public struct MyData
{
    public List<int> Numbers;     // Standard C# List<T>
    public string Name;           // Standard C# string
}
```

**Benefits:**
- Familiar C# types
- Easier to use
- Trades performance for convenience

**Your Job:**
1. Add `[DdsManaged]` attribute to Schema package
2. Modify `SerializerEmitter` to detect and handle `List<T>` and `string`
3. Modify `DeserializerEmitter` to allocate `List<T>` and `string`
4. Add diagnostic analyzer to REQUIRE `[DdsManaged]` attribute (prevent accidental GC)
5. Write comprehensive tests

---

## ✅ Task 1: Add [DdsManaged] Attribute

**Duration:** 30 minutes

### Step 1.1: Create Attribute Class

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\DdsManagedAttribute.cs` (NEW FILE)

**Content:**
```csharp
using System;

namespace CycloneDDS.Schema
{
    /// <summary>
    /// Indicates that this type uses managed C# types (string, List&lt;T&gt;) that may allocate.
    /// Types using string or List&lt;T&gt; MUST be marked with this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false)]
    public sealed class DdsManagedAttribute : Attribute
    {
    }
}
```

**Why this file location:** `CycloneDDS.Schema` contains all DDS attributes ([DdsUnion], [DdsCase], [DdsId], etc.)

**✅ CHECKPOINT:** File created, compiles without errors.

---

### Step 1.2: Add Attribute to Project File

**File:** `d:\Work\FastCycloneDdsCsharpBindings\src\CycloneDDS.Schema\CycloneDDS.Schema.csproj`

**Action:** Verify `DdsManagedAttribute.cs` is included (should auto-include, but check)

**Run:**
```cmd
cd /d d:\Work\FastCycloneDdsCsharpBindings

dotnet build src/CycloneDDS.Schema/CycloneDDS.Schema.csproj
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**✅ CHECKPOINT:** Schema project builds successfully.

---

## ✅ Task 2: Detect Managed Types in TypeInfo

**Duration:** 1 hour

### Step 2.1: Add IsManagedType Helper

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\TypeInfo.cs`

**Find:** The `TypeInfo` class definition (around line 10-50)

**Add this method to TypeInfo class:**
```csharp
public bool IsManagedType()
{
    return Attributes != null && 
           Attributes.Any(a => a.Name == "DdsManaged");
}
```

**Why:** Convenient helper to check if a type is marked [DdsManaged].

**✅ CHECKPOINT:** Code compiles.

---

### Step  2.2: Add IsManagedField Helper

**File:** Same file (`TypeInfo.cs`)

**Find:** The `FieldInfo` class definition

**Add this method to FieldInfo class:**
```csharp
public bool IsManagedFieldType()
{
    return TypeName == "string" || 
           TypeName.StartsWith("List<") ||
           TypeName.StartsWith("System.Collections.Generic.List<");
}
```

**Why:** Detects if a field uses managed types (string or List<T>).

**✅ CHECKPOINT:** Code compiles.

---

## ✅ Task 3: Modify SerializerEmitter for Managed Types

**Duration:** 2-3 hours

### Step 3.1: Handle `string` Serialization

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\SerializerEmitter.cs`

**Find:** Method `EmitFieldWrite` (where field serialization happens)

**Current Code (around line 250-300):** Handles `BoundedSeq`, primitives, structs, unions.

**Add Case for `string`:**

Find where it checks field types and add BEFORE the default case:

```csharp
// In EmitFieldWrite method, add this case:

if (field.TypeName == "string")
{
    // Managed string - serialize as XCDR2 string (4-byte length + UTF-8 + null terminator)
    sb.AppendLine($"            writer.WriteString(value.{field.Name});");
    return;
}
```

**Note:** `CdrWriter` already has `WriteString(string)` method from previous batches.

**✅ CHECKPOINT:** Code compiles.

---

### Step 3.2: Handle `List<T>` Serialization

**Same file, same method (`EmitFieldWrite`):**

**Add before the string case:**

```csharp
// Handle List<T>
if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
{
    // Extract element type
    string elementType = ExtractGenericType(field.TypeName);
    
    // Serialize as sequence: 4-byte count + elements
    sb.AppendLine($"            writer.WriteUInt32((uint)value.{field.Name}.Count);");
    sb.AppendLine($"            foreach (var item in value.{field.Name})");
    sb.AppendLine($"            {{");
    
    // Write element (primitive or complex)
    if (IsPrimitiveType(elementType))
    {
        string writeMethod = GetWriteMethod(elementType);
        sb.AppendLine($"                writer.{writeMethod}(item);");
    }
    else
    {
        sb.AppendLine($"                item.Serialize(ref writer);");
    }
    
    sb.AppendLine($"            }}");
    return;
}
```

**Helper Method Needed:** Add `ExtractGenericType` if not exists:

```csharp
private string ExtractGenericType(string typeName)
{
    int start = typeName.IndexOf('<') + 1;
    int end = typeName.LastIndexOf('>');
    return typeName.Substring(start, end - start).Trim();
}
```

**✅ CHECKPOINT:** Code compiles, serializer emits code for List<T>.

---

## ✅ Task 4: Modify DeserializerEmitter for Managed Types

**Duration:** 2-3 hours

### Step 4.1: Handle `string` Deserialization

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\DeserializerEmitter.cs`

**Find:** Method `EmitFieldRead` (where field deserialization happens)

**Add Case for `string`:**

```csharp
// In EmitFieldRead method:

if (field.TypeName == "string")
{
    sb.AppendLine($"            result.{field.Name} = reader.ReadString();");
    return;
}
```

**Note:** `CdrReader` already has `ReadString()` method.

**✅ CHECKPOINT:** Code compiles.

---

### Step 4.2: Handle `List<T>` Deserialization

**Same file, same method:**

```csharp
// Handle List<T>
if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
{
    string elementType = ExtractGenericType(field.TypeName);
    
    // Read count and allocate List
    sb.AppendLine($"            uint count_{field.Name} = reader.ReadUInt32();");
    sb.AppendLine($"            result.{field.Name} = new List<{elementType}>((int)count_{field.Name});");
    sb.AppendLine($"            for (uint i = 0; i < count_{field.Name}; i++)");
    sb.AppendLine($"            {{");
    
    if (IsPrimitiveType(elementType))
    {
        string readMethod = GetReadMethod(elementType);
        sb.AppendLine($"                result.{field.Name}.Add(reader.{readMethod}());");
    }
    else
    {
        sb.AppendLine($"                result.{field.Name}.Add({elementType}.Deserialize(ref reader));");
    }
    
    sb.AppendLine($"            }}");
    return;
}
```

**✅ CHECKPOINT:** Code compiles, deserializer emits code for List<T>.

---

## ✅ Task 5: Add Diagnostic Analyzer (Prevent Unmarked Managed Types)

**Duration:** 2-3 hours

### Step 5.1: Create Validator

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\ManagedTypeValidator.cs` (NEW FILE)

**Content:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    public class ManagedTypeValidator
    {
        public List<Diagnostic> Validate(TypeInfo type)
        {
            var diagnostics = new List<Diagnostic>();
            
            // Check if type uses managed fields but isn't marked [DdsManaged]
            bool hasManagedFields = type.Fields.Any(f => f.IsManagedFieldType());
            bool isMarkedManaged = type.IsManagedType();
            
            if (hasManagedFields && !isMarkedManaged)
            {
                diagnostics.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Error,
                    Message = $"Type '{type.Name}' uses managed types (string or List<T>) but is not marked with [DdsManaged] attribute. " +
                              $"Add [DdsManaged] to acknowledge GC allocations."
                });
            }
            
            return diagnostics;
        }
    }
}
```

**✅ CHECKPOINT:** File created, compiles.

---

### Step 5.2: Integrate Validator into CodeGenerator

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\CodeGenerator.cs`

**Find:** Method where types are processed (likely `Generate` or `ProcessTypes`)

**Add Validation:**

```csharp
// In ProcessTypes or Generate method, add:

var validator = new ManagedTypeValidator();
foreach (var type in types)
{
    var validationErrors = validator.Validate(type);
    diagnostics.AddRange(validationErrors);
}

// Fail if errors
if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
{
    // Report errors and exit
    return false;
}
```

**✅ CHECKPOINT:** Generator fails compilation if unmarked managed types found.

---

## ✅ Task 6: Write Comprehensive Tests

**Duration:** 3-4 hours

### Step 6.1: Create Test File

**File:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\ManagedTypeTests.cs` (NEW FILE)

**Template:**

```csharp
using System;
using System.Collections.Generic;
using System.Buffers;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    public class ManagedTypeTests : CodeGenTestBase
    {
        [Fact]
        public void ManagedString_RoundTrip()
        {
            // Define type with string field
            var type = new TypeInfo
            {
                Name = "StringData",
                Namespace = "Managed",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Message", TypeName = "string" }
                }
            };

            var emitter = new SerializerEmitter();
            string code = @"
using CycloneDDS.Schema;
using System.Collections.Generic;

namespace Managed
{
    [DdsManaged]
    public partial struct StringData
    {
        public int Id;
        public string Message;
    }
}
";
            code += emitter.EmitSerializer(type, false) + "\n";
            code += new DeserializerEmitter().EmitDeserializer(type, false) + "\n";
            code += GenerateTestHelper("Managed", "StringData");

            var assembly = CompileToAssembly(code, "ManagedStringTest");
            var tData = assembly.GetType("Managed.StringData");

            var instance = Activator.CreateInstance(tData);
            SetField(instance, "Id", 123);
            SetField(instance, "Message", "Hello World");

            var helper = assembly.GetType("Managed.TestHelper");
            var buffer = new ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });

            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });

            Assert.Equal(123, GetField(result, "Id"));
            Assert.Equal("Hello World", GetField(result, "Message"));
        }
        
        // TODO: Add more tests (see Step 6.2)
    }
}
```

**✅ CHECKPOINT:** Single test compiles and passes.

---

### Step 6.2: Add Required Tests

**Add these tests to ManagedTypeTests.cs:**

1. **`ManagedList_Primitives_RoundTrip()`**
   - Type with `List<int>` field
   - Test data: [10, 20, 30, 40, 50]
   - Verify count and values after roundtrip

2. **`ManagedList_Strings_RoundTrip()`**
   - Type with `List<string>` field
   - Test data: ["One", "Two", "Three"]
   - Verify all strings survive

3. **`ManagedList_ComplexStruct_RoundTrip()`**
   - Type with `List<InnerStruct>` where InnerStruct has `{int X; double Y;}`
   - Test data: 3 structs with different values
   - Verify nested data correct

4. **`UnmarkedManagedType_ThrowsError()`**
   - Type with `string` but NO `[DdsManaged]` attribute
   - Verify compilation/validation FAILS with diagnostic error
   - Assert error message mentions "[DdsManaged]"

5. **`EmptyList_RoundTrip()`**
   - Type with `List<int>` but list is empty (count = 0)
   - Verify empty list survives roundtrip

6. **`LargeList_RoundTrip()`**
   - Type with `List<int>` with 1000 elements
   - Verify all 1000 elements correct

7. **`MixedManagedAndUnmanaged_RoundTrip()`**
   - Type with BOTH `string` AND `BoundedSeq<int>`
   - Verify both serialize/deserialize correctly

8. **`NullString_RoundTrip()`**
   - Type with `string?` field set to null
   - Verify null survives (or decide on null handling strategy)

**Minimum Required:** 8 tests

**✅ CHECKPOINT:** All 8+ tests written and passing.

---

## ✅ Task 7: Integration with Existing Tests

**Duration:** 1 hour

### Step 7.1: Run Full Test Suite

**Command:**
```cmd
cd /d d:\Work\FastCycloneDdsCsharpBindings

dotnet test
```

**Expected Output:**
```
Test summary: total: 162+; failed: 0; succeeded: 162+; skipped: 0;
```

**Breakdown should be approximately:**
- CycloneDDS.Core.Tests: 57 tests ✅
- CycloneDDS.Schema.Tests: 10 tests ✅
- CycloneDDS.CodeGen.Tests: 95+ tests (87 existing + 8+ new) ✅

**IF ANY TESTS FAIL:**
1. Copy FULL error output to your report
2. Debug and fix
3. Re-run until 100% pass rate

**✅ CHECKPOINT:** ALL tests passing, no regressions.

---

## 📊 Success Criteria

This batch is DONE when:

### Code Changes:
- ✅ `[DdsManaged]` attribute added to Schema package
- ✅ `SerializerEmitter` handles `string` and `List<T>`
- ✅ `DeserializerEmitter` handles `string` and `List<T>`
- ✅ `ManagedTypeValidator` prevents unmarked managed types
- ✅ Integration into `CodeGenerator`

### Tests:
- ✅ Minimum 8 new tests in `ManagedTypeTests.cs`
- ✅ All 162+ tests passing (no regressions)
- ✅ Tests cover: primitives, strings, nested structs, empty lists, large lists, errors

### Report:
- ✅ Comprehensive report submitted (see below)
- ✅ Report includes full `dotnet test` output
- ✅ Implementation challenges documented
- ✅ Design decisions explained

---

## 📝 Report Requirements

**CRITICAL:** You MUST write a comprehensive report.

**Submit to:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-12-REPORT.md`

**Questions to Ask:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-12-QUESTIONS.md`

### Required Sections in Report

**1. Executive Summary (5-10 lines)**
- What was accomplished
- Test count (before vs after)
- Any bugs found/fixed
- Overall status

**2. Implementation Details**

**Serializer Changes:**
- How did you detect managed types?
- Where did you add code for `string` serialization?
- Where did you add code for `List<T>` serialization?
- Any edge cases handled (null, empty)?

**Deserializer Changes:**
- How does `List<T>` allocation work?
- Any performance considerations?

**Validation:**
- How does the diagnostic analyzer work?
- When does it trigger?
- What error message does it show?

**3. Test Results**
- **MUST INCLUDE:** Full `dotnet test` output showing all tests passing
- List of 8+ new tests added
- Any interesting edge cases discovered during testing?

**4. Design Decisions & Trade-offs**

Answer these questions:

**Q1:** Why require `[DdsManaged]` attribute instead of auto-detecting?  
**Your Answer:** (Explain your reasoning)

**Q2:** How do you handle null strings? Do you serialize them as empty string or throw?  
**Your Answer:** (Document your decision)

**Q3:** What's the performance cost of using `List<T>` vs `BoundedSeq<T>`?  
**Your Answer:** (Estimate based on allocations)

**Q4:** Can a single struct mix managed (`List<T>`) and unmanaged (`BoundedSeq<T>`) fields?  
**Your Answer:** (Document if this works and why)

**5. Implementation Challenges**

**Required:**
- What was the hardest part of this batch?
- What took longer than expected?
- Any bugs you had to fix along the way?
- Any documentation gaps you found?
- Suggestions for improving these instructions?

**6. Code Quality Assessment**

**Required:**
- Are you confident the code is production-ready?
- Any known limitations or TODOs left?
- Any refactoring needed in the future?

**7. Next Steps Recommendations**

- What should be tested before moving to Stage 3?
- Any follow-up work needed?
- Any risks to flag for the next developer?

---

## ⚠️ Common Pitfalls to Avoid

1. **Not marking struct with [DdsManaged]:**
   - Your validator should catch this
   - Test it explicitly

2. **Forgetting null handling:**
   - Decide: throw on null? Serialize as empty? Allow nulls?
   - Document your choice in report

3. **Not testing edge cases:**
   - Empty lists
   - Very large lists (1000+ elements)
   - Null strings
   - Mixed managed/unmanaged fields

4. **Not running full test suite:**
   - ALWAYS run `dotnet test` (not just your new tests)
   - Check for regressions

5. **Incomplete report:**
   - Answer ALL questions in report template
   - Include full test output
   - Explain your design decisions

---

## 🆘 Getting Help

**If you get stuck:**

1. **Create a questions file:**  
   `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-12-QUESTIONS.md`

2. **Format your question:**
   ```markdown
   ## Question 1: How do I handle null strings?
   
   **What I'm trying to do:**
   (Explain the task)
   
   **What I tried:**
   (Show code you attempted)
   
   **What happened:**
   (Error message or unexpected behavior)
   
   **Specific question:**
   (Clear, focused question)
   ```

3. **Where to look for answers:**
   - Previous batches in `.dev-workstream/reviews/`
   - Existing emitter code for patterns
   - Tests for examples

---

## 📚 Reference Materials

**Existing Code to Study:**
- `SerializerEmitter.cs` - How sequences are currently handled (lines 200-400)
- `DeserializerEmitter.cs` - How sequences are currently deserialized
- `SerializerEmitterVariableTests.cs` - String/sequence test patterns
- `ComplexCombinationTests.cs` - How to write comprehensive tests

**XCDR2 Specification:**
- Located in reference materials (if needed for wire format questions)
- String encoding: 4-byte length + UTF-8 bytes + null terminator
- Sequence encoding: 4-byte count + elements

**C# Best Practices:**
- Use `List<T>` capacity constructor to avoid reallocations
- Handle null explicitly (don't assume strings are never null)

---

## 🎯 Final Checklist

Before submitting your report, verify:

- [ ] `[DdsManaged]` attribute compiles
- [ ] `SerializerEmitter` handles `string` (tested)
- [ ] `SerializerEmitter` handles `List<T>` (tested)
- [ ] `DeserializerEmitter` handles `string` (tested)
- [ ] `DeserializerEmitter` handles `List<T>` (tested)
- [ ] Validator prevents unmarked managed types (tested)
- [ ] 8+ new tests written
- [ ] ALL 162+ tests passing
- [ ] Full `dotnet test` output in report
- [ ] All report questions answered
- [ ] Implementation challenges documented
- [ ] Design decisions explained

---

**Estimated Time Breakdown:**
- Setup & Reading: 2 hours
- Task 1 (Attribute): 0.5 hours
- Task 2 (TypeInfo helpers): 1 hour
- Task 3 (Serializer): 2-3 hours
- Task 4 (Deserializer): 2-3 hours
- Task 5 (Validator): 2-3 hours
- Task 6 (Tests): 3-4 hours
- Task 7 (Integration): 1 hour
- Report Writing: 2 hours

**Total:** 16-20 hours (2-3 days of focused work)

---

**Good luck! This is the final generator feature before we move to runtime integration. Make it count! 🚀**
