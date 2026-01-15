# BATCH-10: Fix BATCH-09 Issues + Generator Testing Suite (COMBINED)

**Batch Number:** BATCH-10  
**Tasks:** BATCH-09 fixes (test quality, memory leak), FCDC-013 (generator test suite)  
**Phase:** Phase 2 - Code Generator  
**Estimated Effort:** 5-7 days  
**Priority:** CRITICAL  
**Dependencies:** BATCH-09

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: Complete tasks in sequence with passing tests:**

1. **Task 1 (Fix BATCH-09):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (Generator Suite):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to Task 2 until Task 1 complete with all tests passing.

---

## 📋 Required Reading

1. **BATCH-09 Review:** `.dev-workstream/reviews/BATCH-09-REVIEW.md` - **READ ISSUES!**
2. **Tasks:** `docs/FCDC-TASK-MASTER.md` → FCDC-013
3. **BATCH-07 Review:** `.dev-workstream/reviews/BATCH-07-REVIEW.md` - **TEST QUALITY STANDARD**

**Report:** `.dev-workstream/reports/BATCH-10-REPORT.md`

---

## 🎯 Objectives

**Part 1: Fix BATCH-09 Issues**
1. Replace Assert.Contains tests with runtime validation (compile + invoke)
2. Add Disposable pattern for array marshalling (free allocated memory)

**Part 2: Generator Testing Suite**
3. Integration tests for full code generation pipeline
4. Snapshot testing for generated code
5. Error reporting tests

---

## ✅ Task 1: Fix BATCH-09 Test Quality

### Task 1.1: Replace Array Marshaller Tests

**File:** `tests/CycloneDDS.CodeGen.Tests/MarshallerTests.cs` (MODIFY)

Replace lines 445-510 (current Assert.Contains tests) with RUNTIME validation:

```csharp
[Fact]
public void Marshaller_MarshalsPrimitiveArray_Runtime()
{
    var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int[] Numbers;
    }
}";
    var type = ParseType(csCode);
    var emitter = new MarshallerEmitter();
    var marshallerCode = emitter.GenerateMarshaller(type, "Test");
    var nativeEmitter = new NativeTypeEmitter();
    var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");
    
    var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
    var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
    var topicType = assembly.GetType("Test.TestTopic");
    var nativeType = assembly.GetType("Test.TestTopicNative");
    
    var marshaller = Activator.CreateInstance(marshallerType);
    var topic = Activator.CreateInstance(topicType);
    topicType.GetField("Numbers").SetValue(topic, new int[] { 1, 2, 3 });
    
    var native = Activator.CreateInstance(nativeType);
    
    // Marshal
    var marshalMethod = marshallerType.GetMethod("Marshal");
    var args = new object[] { topic, native };
    marshalMethod.Invoke(marshaller, args);
    native = args[1];
    
    // Verify IntPtr allocated and length set
    var ptr = (IntPtr)nativeType.GetField("Numbers_Ptr").GetValue(native);
    var length = (int)nativeType.GetField("Numbers_Length").GetValue(native);
    
    Assert.NotEqual(IntPtr.Zero, ptr); // ACTUAL allocation check
    Assert.Equal(3, length); // ACTUAL length check
}

[Fact]
public void Marshaller_UnmarshalsPrimitiveArray_Runtime()
{
    // Similar - create native with IntPtr, invoke Unmarshal, verify array created
    // ... implementation
}

[Fact]
public void Marshaller_EmptyArray_SetsZeroPointer()
{
    // Test null/empty array -> IntPtr.Zero
    // ... implementation
}
```

### Task 1.2: Replace Union Marshaller Tests

**File:** `tests/CycloneDDS.CodeGen.Tests/UnionMarshallerTests.cs` (MODIFY)

Replace all Assert.Contains with runtime validation:

```csharp
[Fact]
public void Marshaller_MarshalUnion_OnlyActiveArm()
{
    var csCode = @"
namespace Test {
    [DdsUnion]
    public partial class TestUnion {
        [DdsDiscriminator] public int D;
        [DdsCase(1)] public float Value;
        [DdsCase(2)] public int Count;
    }
}";
    // Generate marshaller + native
    // Compile
    // Create union with D=1, Value=42.5f
    // Marshal
    // Verify native.D = 1, native.Value = 42.5f
    // Count field should be uninitialized (don't check it)
}

[Fact]
public void Marshaller_UnmarshalUnion_ReadsCorrectArm()
{
    // Create native with D=2, Count=100
    // Unmarshal
    // Verify managed.D = 2, managed.Count = 100
}
```

### Task 1.3: Replace Metadata Registry Tests

**File:** `tests/CycloneDDS.CodeGen.Tests/MetadataRegistryTests.cs` (MODIFY)

Replace Assert.Contains with compilation + runtime:

```csharp
[Fact]
public void MetadataRegistry_GetMetadata_ReturnsCorrectData()
{
    // Generate registry code
    // Compile
    // Invoke GetMetadata("Topic1")
    // Verify TopicName, TypeName properties match
}

[Fact]
public void MetadataRegistry_KeyFieldIndices_Runtime()
{
    // Generate registry with key fields
    // Compile
    // Invoke GetMetadata
    // Verify KeyFieldIndices array contains correct indices
}
```

---

## ✅ Task 2: Add Disposable Pattern

**File:** `tools/CycloneDDS.CodeGen/Emitters/MarshallerEmitter.cs` (MODIFY)

Add IDisposable to marshallers with array fields:

```csharp
public string GenerateMarshaller(TypeDeclarationSyntax type, string namespaceName)
{
    // ... existing code ...
    
    var hasArrays = type.Members.OfType<FieldDeclarationSyntax>()
        .Any(f => f.Declaration.Type.ToString().EndsWith("[]"));
    
    if (hasArrays)
    {
        EmitLine($"public class {marshallerName} : IMarshaller<{managedTypeName}, {nativeTypeName}>, IDisposable");
        // ... marshal/unmarshal methods ...
        
        // Add Dispose
        EmitLine("    public void Dispose()");
        EmitLine("    {");
        EmitLine("        // Free allocated memory for arrays");
        
        foreach (var field in arrayFields)
        {
            var fieldName = field.Declaration.Variables.First().Identifier.Text;
            EmitLine($"        if (_native{fieldName}_Ptr != IntPtr.Zero)");
            EmitLine($"            Marshal.FreeHGlobal(_native{fieldName}_Ptr);");
        }
        
        EmitLine("    }");
    }
}
```

---

## ✅ Task 3: Generator Testing Suite

**File:** `tests/CycloneDDS.CodeGen.Tests/GeneratorIntegrationTests.cs` (NEW)

```csharp
using Xunit;
using CycloneDDS.CodeGen;
using System.IO;

namespace CycloneDDS.CodeGen.Tests;

public class GeneratorIntegrationTests
{
    [Fact]
    public void Generator_CompleteWorkflow_GeneratesAllFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var topicCode = @"
using CycloneDDS.Core;

[DdsTopic(""TestTopic"")]
public partial class TestMessage
{
    [DdsKey]
    public int Id;
    public string Data;
}";
        var sourceFile = Path.Combine(tempDir, "Test.cs");
        File.WriteAllText(sourceFile, topicCode);
        
        // Run generator
        var generator = new CodeGenerator();
        var result = generator.Generate(sourceFile);
        
        // Verify files created
        Assert.True(File.Exists(Path.Combine(tempDir, "Generated/TestMessage.idl")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Generated/TestMessageNative.g.cs")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Generated/TestMessageManaged.g.cs")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Generated/TestMessageMarshaller.g.cs")));
        Assert.True(File.Exists(Path.Combine(tempDir, "Generated/MetadataRegistry.g.cs")));
        
        Directory.Delete(tempDir, true);
    }
    
    [Fact]
    public void Generator_InvalidSchema_ReportsErrors()
    {
        // Test with invalid schema (e.g., duplicate key fields)
        // Verify diagnostics contain errors
    }
    
    [Fact]
    public void Generator_SnapshotTest_IDLOutput()
    {
        // Generate IDL, compare to snapshot
        // Use Verify library or custom snapshot comparison
    }
}
```

---

## 🧪 Testing Requirements

**Minimum 15 Tests (replacing 12 bad + 3 new):**

**Part 1: BATCH-09 Fixes (12 tests - REPLACE existing)**
1. ✅ `Marshaller_MarshalsPrimitiveArray_Runtime` (replace)
2. ✅ `Marshaller_UnmarshalsPrimitiveArray_Runtime` (replace)
3. ✅ `Marshaller_EmptyArray_SetsZeroPointer` (replace)
4. ✅ `Marshaller_MarshalUnion_OnlyActiveArm` (replace)
5. ✅ `Marshaller_UnmarshalUnion_ReadsCorrectArm` (replace)
6. ✅ `Marshaller_Union_SwitchesOnDiscriminator` (replace)
7. ✅ `MetadataRegistry_GetMetadata_ReturnsCorrectData_Runtime` (replace)
8. ✅ `MetadataRegistry_KeyFieldIndices_Runtime` (replace)
9. ✅ `MetadataRegistry_TryGetMetadata_Runtime` (replace)
10. ✅ `MetadataRegistry_GetAllTopics_Runtime` (replace)
11. ✅ `Marshaller_Dispose_FreesArrayMemory` (new)
12. ✅ `Marshaller_MultipleArrays_DisposesAll` (new)

**Part 2: Generator Suite (3 new tests)**
13. ✅ `Generator_CompleteWorkflow_GeneratesAllFiles`
14. ✅ `Generator_InvalidSchema_ReportsErrors`
15. ✅ `Generator_SnapshotTest_IDLOutput`

**ALL tests MUST use compilation + runtime validation. NO Assert.Contains on generated code.**

---

## 📊 Report Requirements

1. **BATCH-09 Issues Fixed** (confirm each)
2. **Test Results** (113+ tests: 98 previous + 15 updated/new)
3. **Developer Insights:**
   - Q1: How did you ensure all Assert.Contains were replaced?
   - Q2: What's the disposal strategy for nested marshallers?
   - Q3: How do snapshot tests prevent regressions?
4. **Checklist:** All items checked

---

## 🎯 Success Criteria

1. ✅ ZERO Assert.Contains tests on generated code (except syntax errors)
2. ✅ All marshaller tests use runtime validation
3. ✅ All registry tests use compilation + invocation
4. ✅ Disposable pattern implemented
5. ✅ Array memory freed in Dispose
6. ✅ Integration tests run full pipeline
7. ✅ 15+ tests passing
8. ✅ All 98 previous tests still passing (113 total)
9. ✅ Test quality matches BATCH-07/08 standard

---

## ⚠️ Common Pitfalls

1. **Forgetting a test** - grep for all Assert.Contains, replace ALL
2. **Dispose without tracking** - Need to store IntPtr for disposal
3. **Snapshot brittleness** - Normalize whitespace, timestamps
4. **Integration test cleanup** - Always delete temp directories

---

**Focus: Fix BATCH-09 test quality regression, add disposable pattern, create generator integration tests. GOLD STANDARD quality required.**
