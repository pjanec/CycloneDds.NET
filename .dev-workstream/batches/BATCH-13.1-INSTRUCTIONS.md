# BATCH-13.1: Runtime Integration - Corrective (Generator Integration)

**Batch Number:** BATCH-13.1 (Corrective)  
**Parent Batch:** BATCH-13  
**Estimated Effort:** 2-3 days  
**Priority:** HIGH (Corrective)

---

## 📋 Onboarding & Workflow

### Background

This is a **corrective batch** addressing a critical misunderstanding in BATCH-13.

**Original Batch:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\batches\BATCH-13-INSTRUCTIONS.md`  
**Your Report:** `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-13-REPORT.md`

Please read both before starting.

### The Problem

You reported being blocked by "missing descriptor generator" (line 23 of your report), but this is **incorrect**:

1. ❌ **You created `MockDescriptor.cs`** - This is wrong! We don't need manual descriptors.
2. ❌ **You don't have generated serializers** - No `.g.cs` files for test types
3. ❌ **You disabled serdata APIs** - They exist in `ddsc.dll`; line 108-115 of DdsWriter.cs
4. ✅ **Stage 2 is 100% COMPLETE** - The code generator is READY and WORKING

**The descriptor generator EXISTS and is WORKING.** It's in:
- `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\DescriptorParser.cs`
- Part of the CLI tool you should be using

### What You Missed

**Stage 2 (BATCH-01 through BATCH-12.1) is 100% COMPLETE:**
- ✅ Code generator works (`tools\CycloneDDS.CodeGen`)
- ✅ Generates `Serialize()` and `GetSerializedSize()` methods
- ✅ Integrates with idlc to get descriptors
- ✅ 162 passing tests prove it works

**You should have:**
1. Created a simple C# test type with `[DdsTopic]`
2. Run the code generator on it
3. Got generated serializers (`.g.cs` files)
4. Used those generated descriptors

**Instead you:**
1. Created manual `MockDescriptor.cs`
2. Have no generated code
3. Disabled serdata APIs
4. Are completely blocked

---

## 🎯 Objectives

This corrective batch fixes the integration gap:

1. **Remove MockDescriptor:** Delete the manual descriptor code
2. **Create Test Types:** Define simple C# types with `[DdsTopic]`
3. **Run Code Generator:** Use the existing tools to generate serializers
4. **Get Real Descriptors:** Extract from generated code or

 idlc output
5. **Enable Serdata APIs:** Re-enable the APIs you disabled
6. **Integration Test:** Prove end-to-end works

---

## ✅ Tasks

### Task 1: Understand Stage 2 Integration

**READ THIS CAREFULLY:**

**The Code Generator Pipeline (Already Working):**

```
C# Schema Type ([DdsTopic])
    ↓
tools\CycloneDDS.CodeGen.exe  ← CLI tool, already built
    ↓
Generates 3 things:
    1. Serializers (.g.cs files with Serialize/GetSerializedSize)
    2. IDL files (.idl)
    3. Runs idlc.exe to get descriptors
    ↓
Output: Generated code + Topic Descriptors
```

**Location of Code Generator:**
- **Executable:** `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\bin\Debug\net8.0\CycloneDDS.CodeGen.exe`
- **Source:** `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\*.cs`

**How to Use It:**

```powershell
cd D:\Work\FastCycloneDdsCsharpBindings

# Build the generator (if not already built)
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

# Run it on a project
dotnet run --project tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- ^
  --source tests\CycloneDDS.Runtime.Tests ^
  --output tests\CycloneDDS.Runtime.Tests\Generated
```

This will:
1. Find `[DdsTopic]` types in your test project
2. Generate serializers
3. Generate IDL
4. Run idlc.exe
5. Extract descriptors

**You should have done this from the beginning!**

---

### Task 2: Delete MockDescriptor (Clean Up)

**File to DELETE:**  
`D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\MockDescriptor.cs`

```powershell
cd D:\Work\FastCycloneDdsCsharpBindings
rm tests\CycloneDDS.Runtime.Tests\MockDescriptor.cs
```

**Why:** We don't need manual descriptors. The generator creates them.

---

### Task 3: Create Simple Test Type

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\TestTypes\SimpleMessage.cs` (NEW FILE)

```csharp
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.TestTypes
{
    [DdsTopic("SimpleMessage")]
    public partial struct SimpleMessage
    {
        [DdsKey]
        public int Id;
        
        public double Value;
        
        public FixedString32 Status;
    }
}
```

**Why partial?** The generator adds methods to it via partial class.

---

### Task 4: Run Code Generator on Test Project

**Command:**

```powershell
cd D:\Work\FastCycloneDdsCsharpBindings

# Ensure generator is built
dotnet build tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj

# Run generator
dotnet run --project tools\CycloneDDS.CodeGen\CycloneDDS.CodeGen.csproj -- ^
  --source tests\CycloneDDS.Runtime.Tests ^
  --output tests\CycloneDDS.Runtime.Tests\Generated ^
  --idlc-path cyclone-bin\Release\idlc.exe
```

**Expected Output:**

```
tests\CycloneDDS.Runtime.Tests\Generated\
    SimpleMessage.Serialization.g.cs  ← Serializer methods
    SimpleMessage.Deserializer.g.cs   ← Deserializer + view
    SimpleMessage.idl                 ← IDL for idlc
    SimpleMessage_desc.c              ← Descriptor from idlc
    SimpleMessage_TypeSupport.g.cs    ← Descriptor array
```

**Verify it worked:**

```powershell
ls tests\CycloneDDS.Runtime.Tests\Generated\*.g.cs
```

You should see multiple `.g.cs` files.

---

### Task 5: Inspect Generated Code

**View the serializer:**

```powershell
cat tests\CycloneDDS.Runtime.Tests\Generated\SimpleMessage.Serialization.g.cs
```

**You should see:**

```csharp
partial struct SimpleMessage
{
    public int GetSerializedSize(int currentOffset) { /* ... */ }
    public void Serialize(ref CdrWriter writer) { /* ... */ }
}
```

**View the TypeSupport:**

```powershell
cat tests\CycloneDDS.Runtime.Tests\Generated\SimpleMessage_TypeSupport.g.cs
```

**You should see:**

```csharp
public static class SimpleMessage_TypeSupport
{
    private static readonly byte[] DescriptorOps = new byte[] { /* from idlc */ };
    public static IntPtr GetDescriptor() { /* pins and returns */ }
}
```

**This is what you were missing!**

---

### Task 6: Update Test Project to Include Generated Files

**File:** `D:\Work\FastCycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj`

Add:

```xml
<ItemGroup>
  <!-- Include generated files -->
  <Compile Include="Generated\**\*.g.cs" />
</ItemGroup>
```

**Rebuild:**

```powershell
dotnet build tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

**Verify:** No compilation errors. `SimpleMessage` now has `GetSerializedSize()` and `Serialize()` methods.

---

### Task 7: Re-Enable Serdata APIs in DdsWriter

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs`

**Find lines 108-115 (you disabled this):**

```csharp
// DISABLED: dds_create_serdata_from_cdr is missing in current binaries
// IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
//    _topicDescriptor,
//    dataPtr,
//    length);

IntPtr serdata = IntPtr.Zero;
Console.WriteLine("[WARNING] Skipping dds_create_serdata_from_cdr (Missing API)");
```

**Replace with:**

```csharp
IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
    _topicDescriptor,
    dataPtr,
    length);

if (serdata == IntPtr.Zero)
    throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create serdata");
```

**Why:** The API is NOT missing. It's in `ddsc.dll`. You just didn't try it.

---

### Task 8: Fix DdsWriter to Use Generated Descriptor

**File:** `D:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs`

**Current constructor signature:**

```csharp
public DdsWriter(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
```

**Problem:** You're passing `IntPtr.Zero` from tests. Use generated descriptor instead!

**Solution 1 (Recommended):** Auto-discover descriptor from generated TypeSupport:

```csharp
public DdsWriter(DdsParticipant participant, string topicName)
{
    // Discover descriptor via reflection
    string typeSupportClassName = $"{typeof(T).Name}_TypeSupport";
    var type = typeof(T).Assembly.GetType($"{typeof(T).Namespace}.{typeSupportClassName}");
    
    if (type == null)
        throw new InvalidOperationException($"No TypeSupport found for {typeof(T).Name}. Did you run the code generator?");
    
    var method = type.GetMethod("GetDescriptor", BindingFlags.Public | BindingFlags.Static);
    if (method == null)
        throw new InvalidOperationException($"TypeSupport.GetDescriptor not found");
    
    IntPtr descriptor = (IntPtr)method.Invoke(null, null)!;
    
    // Now use descriptor...
    var topic = DdsApi.dds_create_topic(participant.NativeEntity, descriptor, topicName, IntPtr.Zero, IntPtr.Zero);
    // ...
}
```

**Solution 2 (Simpler for MVP):** Accept descriptor as parameter but get it from generated code in tests.

---

### Task 9: Update Tests to Use Generated Types

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\DdsWriterTests.cs`

**Replace mock-based tests with:**

```csharp
using CycloneDDS.Runtime.Tests.TestTypes;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    public class DdsWriterTests
    {
        [Fact]
        public void CreateWriter_WithGeneratedType_Success()
        {
            using var participant = new DdsParticipant(0);
            
            // Get descriptor from generated TypeSupport
            IntPtr descriptor = SimpleMessage_TypeSupport.GetDescriptor();
            
            using var writer = new DdsWriter<SimpleMessage>(participant, "SimpleMessage", descriptor);
            
            Assert.NotNull(writer);
        }
        
        [Fact]
        public void Write_SimpleMessage_NoException()
        {
            using var participant = new DdsParticipant(0);
            IntPtr descriptor = SimpleMessage_TypeSupport.GetDescriptor();
            using var writer = new DdsWriter<SimpleMessage>(participant, "SimpleMessage", descriptor);
            
            var msg = new SimpleMessage 
            { 
                Id = 42, 
                Value = 3.14,
                Status = new FixedString32("OK")
            };
            
            // Should not throw
            writer.Write(msg);
        }
        
        [Fact]
        public void Write1000Samples_ZeroGCAllocations()
        {
            using var participant = new DdsParticipant(0);
            IntPtr descriptor = SimpleMessage_TypeSupport.GetDescriptor();
            using var writer = new DdsWriter<SimpleMessage>(participant, "SimpleMessage", descriptor);
            
            var msg = new SimpleMessage { Id = 1, Value = 1.0, Status = new FixedString32("OK") };
            
            // Warmup
            for (int i = 0; i < 10; i++)
                writer.Write(msg);
            
            // Measure
            long before = GC.GetTotalAllocatedBytes(precise: true);
            
            for (int i = 0; i < 1000; i++)
                writer.Write(msg);
            
            long after = GC.GetTotalAllocatedBytes(precise: true);
            long allocated = after - before;
            
            Assert.True(allocated < 10_000, $"Expected < 10 KB, got {allocated} bytes");
        }
    }
}
```

---

### Task 10: Create Integration Test with Generated Types

**File:** `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\IntegrationTests.cs` (NEW FILE)

```csharp
using System.Threading;
using CycloneDDS.Runtime.Tests.TestTypes;
using Xunit;

namespace CycloneDDS.Runtime.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public void FullRoundtrip_SimpleMessage_Success()
        {
            using var participant = new DdsParticipant(0);
            IntPtr descriptor = SimpleMessage_TypeSupport.GetDescriptor();
            
            using var writer = new DdsWriter<SimpleMessage>(participant, "TestTopic", descriptor);
            using var reader = new DdsReader<SimpleMessage, SimpleMessageView>(participant, "TestTopic", descriptor);
            
            // Write
            var sample = new SimpleMessage 
            { 
                Id = 42, 
                Value = 3.14,
                Status = new FixedString32("OK")
            };
            writer.Write(sample);
            
            // Wait for discovery + delivery
            Thread.Sleep(200);
            
            // Read
            using var scope = reader.Take();
            
            Assert.Equal(1, scope.Samples.Length);
            Assert.Equal(42, scope.Samples[0].Id);
            Assert.InRange(scope.Samples[0].Value, 3.13, 3.15);
        }
    }
}
```

---

## 🧪 Testing Requirements

**All tests must use GENERATED types, not mocks:**

- [ ] DdsWriterTests: 5 tests using `SimpleMessage` with generated descriptor
- [ ] DdsReaderTests: 4 tests using `SimpleMessage` with generated descriptor
- [ ] IntegrationTests: 3 end-to-end tests proving roundtrip works
- [ ] Zero GC allocations verified (performance test)

**Minimum:** 12 new/updated tests

---

## 📊 Report Requirements

### Questions to Answer

**Q1:** What was your confusion about the descriptor generator? Why did you think it was missing?

**Q2:** Now that you understand Stage 2 is complete, how does the integration work? Describe the flow from C# type → Generated code → Runtime.

**Q3:** Did the serdata APIs actually work once you re-enabled them? Any issues?

**Q4:** What would have made BATCH-13 instructions clearer to avoid this confusion?

---

## 🎯 Success Criteria

This batch is DONE when:

- [ ] `MockDescriptor.cs` deleted
- [ ] `SimpleMessage.cs` test type created with `[DdsTopic]`
- [ ] Code generator run successfully (`.g.cs` files exist)
- [ ] Generated code compiles and integrates
- [ ] Serdata APIs re-enabled in DdsWriter
- [ ] Tests updated to use generated types
- [ ] Integration test proves full roundtrip works
- [ ] Zero GC allocations verified
- [ ] Report submitted explaining the confusion and resolution

---

## ⚠️ Common Pitfalls (Again!)

1. **"I already ran the generator"**
   - VERIFY: Check for `.g.cs` files. If they don't exist, you didn't run it.
   - Location: `tests\CycloneDDS.Runtime.Tests\Generated\*.g.cs`

2. **"Serdata API still fails"**
   - Check DLL is copied: `tests\CycloneDDS.Runtime.Tests\bin\Debug\net8.0\ddsc.dll`
   - Check descriptor is valid (not IntPtr.Zero)
   - Check buffer is not empty

3. **"Type doesn't have Serialize method"**
   - Check the type is `partial struct`
   - Check `.g.cs` files are included in project
   - Rebuild the test project

4. **"Descriptor is still IntPtr.Zero"**
   - Check `SimpleMessage_TypeSupport.GetDescriptor()` exists
   - Check idlc ran successfully (check for `.idl` and `_desc.c` files)

---

## 📚 References

**Stage 2 Completion:**
- BATCH-12.1 Review: `D:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-12.1-REVIEW.md`
- Confirms: 162 tests passing, code generator 100% complete

**Code Generator:**
- Location: `D:\Work\FastCycloneDdsCsharpBindings\tools\CycloneDDS.CodeGen\`
- How to run: See Task 4 above

**Generated Code Examples:**
- Look at: `D:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.CodeGen.Tests\` for examples of generated code

---

## 🔑 Key Insight

**You were trying to manually build what Stage 2 already provides!**

- ❌ **Don't:** Create manual descriptors, reflection hacks, or mocks
- ✅ **Do:** Use the code generator that is already built and tested

**The code generator is YOUR TOOL. Use it!**

---

**Good luck! This should unblock you completely. The path forward is clear: generate the code, use the generated descriptors, enable the APIs, test it.**
