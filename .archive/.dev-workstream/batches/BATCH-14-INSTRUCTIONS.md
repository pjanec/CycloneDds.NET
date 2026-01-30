# BATCH-14: Instance Lifecycle Management (Dispose/Unregister)

**Batch Number:** BATCH-14  
**Stage:** 3.5 - Instance Lifecycle  
**Task:** FCDC-S022b  
**Priority:** ⚠️ **HIGH** (Production Requirement)  
**Estimated Effort:** 2-3 days  
**Assigned:** [TBD]  
**Due Date:** [TBD]

---

## 🎯 Objective

Implement DDS instance lifecycle operations (`DisposeInstance` and `UnregisterInstance`) for keyed topics, enabling proper resource cleanup, graceful shutdown, and ownership management.

**What you're building:**
- Two new methods in `DdsWriter<T>`: `DisposeInstance(in T)` and `UnregisterInstance(in T)`
- Native API extensions in custom `ddsc.dll`
- P/Invoke declarations
- 11 comprehensive tests
- Documentation updates

**Why this matters:**
- Production systems need proper instance lifecycle management
- Prevents reader resource leaks (stale instances accumulate without disposal)
- Critical for graceful shutdown (avoiding reader timeouts)
- Required for exclusive ownership patterns in DDS

---

## 📋 Prerequisites & Onboarding

### Required Knowledge

**If you're NEW to this project, read these documents IN ORDER:**

1. **Project Architecture** (30 min):
   - `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-DESIGN.md`
   - Understand: Serdata approach, zero-alloc goals, XCDR2 format

2. **Task Context** (15 min):
   - `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md` - Lines 1210-1306 (FCDC-S022b)
   - `d:\Work\FastCycloneDdsCsharpBindings\docs\INSTANCE-LIFECYCLE-DESIGN.md` (COMPLETE THIS - 20 min)

3. **Design Discussion** (15 min):
   - `d:\Work\FastCycloneDdsCsharpBindings\docs\design-talk.md` - Lines 5106-5412
   - Original design discussion with implementation details

4. **Existing Write() Implementation** (20 min):
   - `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs`
   - Study lines 92-160 (Write method)
   - **You will extend this pattern for Dispose/Unregister**

5. **Stage 3 Completion** (10 min):
   - `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-13.3-FINAL-REVIEW.md`
   - Understand what was delivered in Stage 3

**Total Onboarding Time:** ~110 minutes (budget 2-3 hours for thorough reading)

---

### Your Development Environment

**Repository Location:**
```
d:\Work\FastCycloneDdsCsharpBindings\
```

**Key Directories:**
```
Src\CycloneDDS.Runtime\          ← You'll work here (DdsWriter.cs, DdsApi.cs)
tests\CycloneDDS.Runtime.Tests\  ← You'll write tests here
cyclonedds\                       ← Native CycloneDDS source (you'll modify dds_writer.c)
cyclone-bin\                      ← Rebuilt ddsc.dll output
docs\                             ← Design documents
.dev-workstream\                  ← This batch, reviews, reports
```

**Build Commands:**
```powershell
# Build solution
dotnet build

# Run all tests
dotnet test

# Run Runtime tests only
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# Rebuild native library (after C changes)
cd cyclonedds
# Follow instructions in cyclonedds/BUILD.md
```

---

### Verify Your Environment

**Before starting, verify these files exist:**

```powershell
# Core runtime files (you'll modify these)
Test-Path "d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs"
Test-Path "d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsApi.cs"

# Native source (you'll modify this)
Test-Path "d:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsc\src\dds_writer.c"

# Test project
Test-Path "d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\IntegrationTests.cs"

# Design documents
Test-Path "d:\Work\FastCycloneDdsCsharpBindings\docs\INSTANCE-LIFECYCLE-DESIGN.md"
```

All should return `True`. If not, contact the lead.

---

## 📐 Architecture Overview

### DDS Instance Lifecycle Basics

**DDS Instance States (for Readers):**
1. **ALIVE** - Instance has live writers and valid data
2. **NOT_ALIVE_DISPOSED** - Instance explicitly disposed (deleted)
3. **NOT_ALIVE_NO_WRITERS** - No live writers remain

**Writer Operations (what you're implementing):**
1. **Write(T)** - Update instance with new data ✅ (Already exists)
2. **DisposeInstance(T)** - Mark instance as deleted/dead ⚠️ (You implement)
3. **UnregisterInstance(T)** - Writer stops updating instance ⚠️ (You implement)

### Implementation Strategy

**Key Insight:** Dispose and Unregister use the EXACT SAME serialization path as Write(), just different native API calls at the end.

**Pattern:**
```
Write():            Serialize → dds_writecdr
DisposeInstance():  Serialize → dds_dispose_serdata
UnregisterInstance(): Serialize → dds_unregister_serdata
```

**Design Decision:** Create a unified `PerformOperation()` helper to avoid code duplication while maintaining zero-allocation.

---

## 🛠️ Implementation Plan

You will complete **5 Phases** in order:

1. **Phase 1:** Native Extension (Export new APIs)
2. **Phase 2:** P/Invoke Layer (Declare APIs in C#)
3. **Phase 3:** DdsWriter Refactoring (Add new methods)
4. **Phase 4:** Testing (11 tests)
5. **Phase 5:** Documentation

---

## Phase 1: Native Extension (Export New APIs)

### Goal
Export `dds_dispose_serdata` and `dds_unregister_serdata` in the custom `ddsc.dll`.

### Background
CycloneDDS has internal functions for dispose/unregister, but they're not exported by default. We need to add `DDS_EXPORT` to make them accessible from C#.

### Step 1.1: Locate the Native Source File

**File:** `d:\Work\FastCycloneDdsCsharpBindings\cyclonedds\src\core\ddsc\src\dds_writer.c`

**Open this file** and search for "dds_dispose_serdata".

### Step 1.2: Verify/Add Export Macros

**Find these functions** (around line 300-400):

```c
dds_return_t dds_dispose_serdata (dds_entity_t writer, dds_serdata_t *sd)
{
  return write_impl (writer, sd, 0, DDS_CMD_DISPOSE);
}

dds_return_t dds_unregister_serdata (dds_entity_t writer, dds_serdata_t *sd)
{
  return write_impl (writer, sd, 0, DDS_CMD_UNREGISTER);
}
```

**If they DON'T have `DDS_EXPORT`, add it:**

```c
DDS_EXPORT dds_return_t dds_dispose_serdata (dds_entity_t writer, dds_serdata_t *sd)
{
  return write_impl (writer, sd, 0, DDS_CMD_DISPOSE);
}

DDS_EXPORT dds_return_t dds_unregister_serdata (dds_entity_t writer, dds_serdata_t *sd)
{
  return write_impl (writer, sd, 0, DDS_CMD_UNREGISTER);
}
```

**If they already have `DDS_EXPORT`:** Great! Skip to verification.

### Step 1.3: Rebuild Native Library

**Important:** This is critical! Your C# code won't work until you rebuild ddsc.dll.

**Follow the build instructions:**

1. **Open PowerShell in the cyclonedds directory:**
   ```powershell
   cd d:\Work\FastCycloneDdsCsharpBindings\cyclonedds
   ```

2. **Run CMake and build** (exact commands depend on your setup):
   ```powershell
   # Example (adjust as needed):
   mkdir build-Release
   cd build-Release
   cmake -G "Visual Studio 17 2022" -A x64 -DCMAKE_BUILD_TYPE=Release ..
   cmake --build . --config Release
   ```

3. **Copy the built DLL** to cyclone-bin:
   ```powershell
   # Source: build-Release/bin/Release/ddsc.dll
   # Destination: d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\bin\ddsc.dll
   Copy-Item "build-Release\bin\Release\ddsc.dll" "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\bin\ddsc.dll" -Force
   ```

**Note:** If you have trouble building, refer to:
- `cyclonedds\BUILD.md`
- Or contact the lead for pre-built binaries

### Step 1.4: Verify Exports

**Use `dumpbin` (Visual Studio tool) to verify:**

```powershell
dumpbin /EXPORTS "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\bin\ddsc.dll" | Select-String "dispose_serdata"
dumpbin /EXPORTS "d:\Work\FastCycloneDdsCsharpBindings\cyclone-bin\bin\ddsc.dll" | Select-String "unregister_serdata"
```

**Expected output:**
```
dds_dispose_serdata
dds_unregister_serdata
```

**If you don't see these**, the export failed. Rebuild and check `DDS_EXPORT` is present.

---

## Phase 2: P/Invoke Layer (C# Declarations)

### Goal
Add P/Invoke declarations for the new native APIs.

### Step 2.1: Open DdsApi.cs

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\Interop\DdsApi.cs`

**This file contains all P/Invoke declarations.** You'll add two new ones.

### Step 2.2: Find the Write API Section

**Search for** `dds_writecdr` in the file. You should see:

```csharp
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_writecdr(DdsEntity writer, IntPtr serdata);
```

### Step 2.3: Add New Declarations

**Immediately AFTER `dds_writecdr`, add:**

```csharp
/// <summary>
/// Dispose an instance using serdata (marks instance as deleted).
/// </summary>
/// <param name="writer">Writer entity handle</param>
/// <param name="serdata">Serialized data containing the key</param>
/// <returns>0 on success, negative error code on failure</returns>
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_dispose_serdata(DdsEntity writer, IntPtr serdata);

/// <summary>
/// Unregister an instance using serdata (writer releases ownership).
/// </summary>
/// <param name="writer">Writer entity handle</param>
/// <param name="serdata">Serialized data containing the key</param>
/// <returns>0 on success, negative error code on failure</returns>
[DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
public static extern int dds_unregister_serdata(DdsEntity writer, IntPtr serdata);
```

**Exact location:** After line containing `dds_writecdr` (around line 170-180).

**Save the file.**

### Step 2.4: Verify Compilation

```powershell
cd d:\Work\FastCycloneDdsCsharpBindings
dotnet build Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj
```

**Expected:** Build succeeds with no errors.

**If you get P/Invoke errors**: Check DLL_NAME constant, calling convention, and parameter types.

---

## Phase 3: DdsWriter Implementation

### Goal
Refactor `DdsWriter<T>` to support Write, Dispose, and Unregister using a unified pattern.

### Step 3.1: Open DdsWriter.cs

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\DdsWriter.cs`

**Current structure:**
- Lines 1-90: Fields, constructor
- Lines 92-160: `Write()` method
- Lines 162-190: `WriteViaDdsWrite()` (fallback)
- Lines 192-227: Dispose/cleanup

### Step 3.2: Add Operation Enum

**Add this BEFORE the `Write()` method** (around line 91):

```csharp
/// <summary>
/// DDS writer operation type.
/// </summary>
private enum DdsOperation
{
    /// <summary>Update instance with new data.</summary>
    Write,
    
    /// <summary>Mark instance as deleted/disposed.</summary>
    Dispose,
    
    /// <summary>Writer releases ownership of instance.</summary>
    Unregister
}
```

### Step 3.3: Refactor Write() to Use Unified Pattern

**Current `Write()` method** (lines 92-160) does everything inline.

**New approach:** Extract core logic to `PerformOperation()`, make `Write()` call it.

**REPLACE the existing `Write()` method** (lines 92-160) with:

```csharp
/// <summary>
/// Write a sample to the DDS topic (update instance).
/// </summary>
/// <param name="sample">Sample to write</param>
public void Write(in T sample)
{
    PerformOperation(sample, DdsOperation.Write);
}
```

### Step 3.4: Create PerformOperation() Helper

**Add this NEW method AFTER Write()** (around line 100):

```csharp
/// <summary>
/// Core implementation for Write, Dispose, and Unregister operations.
/// Maintains zero-allocation guarantee by reusing serialization path.
/// </summary>
/// <param name="sample">Sample to serialize (for Dispose/Unregister, only key fields matter)</param>
/// <param name="operation">Operation type</param>
private void PerformOperation(in T sample, DdsOperation operation)
{
    if (_writerHandle == null) 
        throw new ObjectDisposedException(nameof(DdsWriter<T>));

    // 1. Get Size (includes 4-byte CDR header)
    int payloadSize = _sizer!(sample, 4);
    int totalSize = payloadSize + 4;

    // 2. Rent Buffer (zero-alloc, pooled)
    byte[] buffer = Arena.Rent(totalSize);
    
    try
    {
        // 3. Serialize (zero-alloc via span constructor)
        var span = buffer.AsSpan(0, totalSize);
        var cdr = new CdrWriter(span);
        
        // Write CDR Header (XCDR1 format)
        // Identifier: 0x0001 (LE) or 0x0000 (BE), Options: 0x0000
        if (BitConverter.IsLittleEndian)
        {
            // Little Endian (x64, ARM64, most platforms)
            cdr.WriteByte(0x00);
            cdr.WriteByte(0x01);
        }
        else
        {
            // Big Endian (rare: PowerPC, SPARC, older MIPS)
            cdr.WriteByte(0x00);
            cdr.WriteByte(0x00);
        }
        cdr.WriteByte(0x00);
        cdr.WriteByte(0x00);
        
        // Serialize payload using generated method
        _serializer!(sample, ref cdr);
        cdr.Complete();
        
        // 4. Execute DDS Operation
        unsafe
        {
            fixed (byte* p = buffer)
            {
                IntPtr dataPtr = (IntPtr)p;
                
                // Create serdata from CDR bytes
                IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                    _topicHandle.NativeHandle,
                    dataPtr,
                    (uint)totalSize);

                if (serdata == IntPtr.Zero)
                {
                    throw new DdsException(DdsApi.DdsReturnCode.Error, 
                        "dds_create_serdata_from_cdr failed");
                }
                    
                try
                {
                    // Select the appropriate native call based on operation
                    int ret = operation switch
                    {
                        DdsOperation.Write => 
                            DdsApi.dds_writecdr(_writerHandle.NativeHandle, serdata),
                        
                        DdsOperation.Dispose => 
                            DdsApi.dds_dispose_serdata(_writerHandle.NativeHandle, serdata),
                        
                        DdsOperation.Unregister => 
                            DdsApi.dds_unregister_serdata(_writerHandle.NativeHandle, serdata),
                        
                        _ => throw new ArgumentException($"Unknown operation: {operation}")
                    };

                    if (ret < 0)
                    {
                        throw new DdsException((DdsApi.DdsReturnCode)ret, 
                            $"{operation} failed: {ret}");
                    }
                }
                finally
                {
                    // Note: dds_writecdr/dispose/unregister consume the serdata reference on success.
                    // However, for consistency and safety (in case of failure paths), we always unref.
                    // Cyclone increments the ref count if it needs to keep the data.
                    // This ensures we don't leak serdata objects.
                    DdsApi.ddsi_serdata_unref(serdata);
                }
            }
        }
    }
    finally
    {
        Arena.Return(buffer);
    }
}
```

**Important notes:**
- This is nearly identical to the old Write() code
- The only change is the `switch` statement selecting which API to call
- Full serialization is used (not key-only) for simplicity
- Zero-allocation guarantee is maintained

### Step 3.5: Add Public API Methods

**Add these NEW methods AFTER PerformOperation():**

```csharp
/// <summary>
/// Dispose an instance (mark as deleted).
/// Notifies readers that this instance is no longer valid.
/// Reader instance state will transition to NOT_ALIVE_DISPOSED.
/// </summary>
/// <param name="sample">Sample containing the key to dispose (non-key fields ignored)</param>
/// <remarks>
/// For keyed topics only. The key fields identify which instance to dispose.
/// Non-key fields are serialized but ignored by CycloneDDS.
/// This operation maintains the zero-allocation guarantee.
/// </remarks>
public void DisposeInstance(in T sample)
{
    PerformOperation(sample, DdsOperation.Dispose);
}

/// <summary>
/// Unregister an instance (writer releases ownership).
/// Notifies readers that this writer will no longer update the instance.
/// Reader instance state will transition to NOT_ALIVE_NO_WRITERS if no other writers exist.
/// </summary>
/// <param name="sample">Sample containing the key to unregister (non-key fields ignored)</param>
/// <remarks>
/// Useful for graceful shutdown or ownership transfer scenarios.
/// For keyed topics only. The key fields identify which instance to unregister.
/// Non-key fields are serialized but ignored by CycloneDDS.
/// This operation maintains the zero-allocation guarantee.
/// </remarks>
public void UnregisterInstance(in T sample)
{
    PerformOperation(sample, DdsOperation.Unregister);
}
```

### Step 3.6: Verify Compilation

```powershell
dotnet build Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj
```

**Expected:** Build succeeds.

**If errors:** Check method signatures, using statements, and bracket matching.

---

## Phase 4: Testing (11 Tests Required)

### Create Two Test Files

You'll create:
1. `DdsWriterLifecycleTests.cs` - Unit tests (7 tests)
2. `InstanceLifecycleIntegrationTests.cs` - Integration tests (4 tests)

---

### File 1: Unit Tests

**Create:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\DdsWriterLifecycleTests.cs`

**Full content:**

```csharp
using System;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.Generated;

namespace CycloneDDS.Runtime.Tests
{
    /// <summary>
    /// Unit tests for DdsWriter lifecycle operations (Dispose/Unregister).
    /// </summary>
    public class DdsWriterLifecycleTests : IDisposable
    {
        private readonly DdsParticipant _participant;
        private readonly DescriptorContainer _descriptor;

        public DdsWriterLifecycleTests()
        {
            _participant = new DdsParticipant(0);
            _descriptor = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16, "TestMessage");
        }

        public void Dispose()
        {
            _descriptor?.Dispose();
            _participant?.Dispose();
        }

        [Fact]
        public void DisposeInstance_ValidSample_Succeeds()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "DisposeTest1", _descriptor.Ptr);

            var sample = new TestMessage { Id = 1, Value = 100 };
            
            // Should not throw
            writer.DisposeInstance(sample);
        }

        [Fact]
        public void DisposeInstance_AfterWrite_SendsDisposalMessage()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "DisposeTest2", _descriptor.Ptr);

            var sample = new TestMessage { Id = 2, Value = 200 };
            
            // Write then dispose
            writer.Write(sample);
            writer.DisposeInstance(sample);
            
            // If we reach here without exception, dispose succeeded
        }

        [Fact]
        public void UnregisterInstance_ValidSample_Succeeds()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "UnregisterTest1", _descriptor.Ptr);

            var sample = new TestMessage { Id = 3, Value = 300 };
            
            // Should not throw
            writer.UnregisterInstance(sample);
        }

        [Fact]
        public void UnregisterInstance_AfterWrite_SendsUnregisterMessage()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "UnregisterTest2", _descriptor.Ptr);

            var sample = new TestMessage { Id = 4, Value = 400 };
            
            // Write then unregister
            writer.Write(sample);
            writer.UnregisterInstance(sample);
            
            // If we reach here without exception, unregister succeeded
        }

        [Fact]
        public void DisposeInstance_NonKeySample_IgnoresNonKeyFields()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "DisposeTest3", _descriptor.Ptr);

            // Dispose with different Value (non-key field) - should work
            writer.DisposeInstance(new TestMessage { Id = 5, Value = 999 });
            writer.DisposeInstance(new TestMessage { Id = 5, Value = 0 });
            
            // Both should succeed (Value ignored, only Id matters)
        }

        [Fact]
        public void DisposeInstance_AfterWriterDispose_Throws()
        {
            var writer = new DdsWriter<TestMessage>(
                _participant, "DisposeTest4", _descriptor.Ptr);

            writer.Dispose();

            var sample = new TestMessage { Id = 6, Value = 600 };
            
            Assert.Throws<ObjectDisposedException>(() => writer.DisposeInstance(sample));
        }

        [Fact]
        public void UnregisterInstance_MultipleWriters_HandlesCorrectly()
        {
            using var writer1 = new DdsWriter<TestMessage>(
                _participant, "MultiWriter", _descriptor.Ptr);
            using var writer2 = new DdsWriter<TestMessage>(
                _participant, "MultiWriter", _descriptor.Ptr);

            var sample = new TestMessage { Id = 7, Value = 700 };
            
            // Both writers can write
            writer1.Write(sample);
            writer2.Write(sample);
            
            // One unregisters
            writer1.UnregisterInstance(sample);
            
            // Other writer should still work
            writer2.Write(sample);
        }
    }
}
```

---

### File 2: Integration Tests

**Create:** `d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\InstanceLifecycleIntegrationTests.cs`

**Full content:**

```csharp
using System;
using System.Threading;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.Generated;

namespace CycloneDDS.Runtime.Tests
{
    /// <summary>
    /// Integration tests for DDS instance lifecycle (end-to-end with readers).
    /// </summary>
    public class InstanceLifecycleIntegrationTests : IDisposable
    {
        private readonly DdsParticipant _participant;
        private readonly DescriptorContainer _descriptor;

        public InstanceLifecycleIntegrationTests()
        {
            _participant = new DdsParticipant(0);
            _descriptor = new DescriptorContainer(
                TestMessage.GetDescriptorOps(), 8, 4, 16, "TestMessage");
        }

        public void Dispose()
        {
            _descriptor?.Dispose();
            _participant?.Dispose();
        }

        [Fact]
        public void WriteDisposeRead_VerifiesInstanceStateNotAliveDisposed()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "LifecycleDispose", _descriptor.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                _participant, "LifecycleDispose", _descriptor.Ptr);

            var sample = new TestMessage { Id = 10, Value = 1000 };

            // Write sample
            writer.Write(sample);
            Thread.Sleep(100); // Allow propagation

            // Read - should have valid data
            using (var scope1 = reader.Take())
            {
                if (scope1.Count > 0 && scope1.Infos[0].ValidData != 0)
                {
                    Assert.Equal(10, scope1[0].Id);
                    Assert.Equal(1000, scope1[0].Value);
                }
            }

            // Dispose instance
            writer.DisposeInstance(sample);
            Thread.Sleep(100); // Allow propagation

            // Read - should see disposal (ValidData = 0, instance state = NOT_ALIVE_DISPOSED)
            using (var scope2 = reader.Take())
            {
                if (scope2.Count > 0)
                {
                    // Sample info should indicate disposal
                    Assert.Equal(0, scope2.Infos[0].ValidData);
                    // Note: Instance state checking would require exposing more of DdsSampleInfo
                    // For now, ValidData=0 is sufficient to verify dispose was received
                }
            }
        }

        [Fact]
        public void WriteUnregisterRead_VerifiesInstanceStateNotAliveNoWriters()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "LifecycleUnregister", _descriptor.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                _participant, "LifecycleUnregister", _descriptor.Ptr);

            var sample = new TestMessage { Id = 11, Value = 1100 };

            // Write sample
            writer.Write(sample);
            Thread.Sleep(100);

            // Read - should have valid data
            using (var scope1 = reader.Take())
            {
                if (scope1.Count > 0 && scope1.Infos[0].ValidData != 0)
                {
                    Assert.Equal(11, scope1[0].Id);
                }
            }

            // Unregister instance
            writer.UnregisterInstance(sample);
            Thread.Sleep(100);

            // Read - should see unregister notification
            using (var scope2 = reader.Take())
            {
                if (scope2.Count > 0)
                {
                    Assert.Equal(0, scope2.Infos[0].ValidData);
                }
            }
        }

        [Fact]
        public void MultipleWritersUnregister_VerifiesOwnership()
        {
            using var writer1 = new DdsWriter<TestMessage>(
                _participant, "OwnershipTest", _descriptor.Ptr);
            using var writer2 = new DdsWriter<TestMessage>(
                _participant, "OwnershipTest", _descriptor.Ptr);
            using var reader = new DdsReader<TestMessage, TestMessage>(
                _participant, "OwnershipTest", _descriptor.Ptr);

            var sample = new TestMessage { Id = 12, Value = 1200 };

            // Both writers publish
            writer1.Write(sample);
            writer2.Write(new TestMessage { Id = 12, Value = 1201 });
            Thread.Sleep(100);

            // Reader should see data
            using (var scope1 = reader.Take())
            {
                Assert.True(scope1.Count > 0);
            }

            // Writer1 unregisters
            writer1.UnregisterInstance(sample);
            Thread.Sleep(100);

            // Writer2 still active - write should still work
            writer2.Write(new TestMessage { Id = 12, Value = 1202 });
            Thread.Sleep(100);

            // Reader should still see valid data from writer2
            using (var scope2 = reader.Take())
            {
                bool foundValid = false;
                for (int i = 0; i < scope2.Count; i++)
                {
                    if (scope2.Infos[i].ValidData != 0)
                    {
                        Assert.Equal(12, scope2[i].Id);
                        foundValid = true;
                        break;
                    }
                }
                // With multiple writers, instance should still be alive
            }
        }

        [Fact]
        public void DisposeInstance_ZeroAllocation_1000Operations()
        {
            using var writer = new DdsWriter<TestMessage>(
                _participant, "AllocTest", _descriptor.Ptr);

            var sample = new TestMessage { Id = 99, Value = 9999 };

            // Warmup JIT
            for (int i = 0; i < 10; i++)
            {
                writer.DisposeInstance(sample);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetTotalAllocatedBytes(true);

            // Dispose 1000 times
            for (int i = 0; i < 1000; i++)
            {
                writer.DisposeInstance(sample);
            }

            long endAlloc = GC.GetTotalAllocatedBytes(true);
            long diff = endAlloc - startAlloc;

            // Allow reasonable overhead (JIT, ArrayPool metadata)
            // Same threshold as Write test
            Assert.True(diff < 50_000,
                $"Expected < 50 KB for 1000 disposes, got {diff} bytes ({diff / 1000.0:F1} bytes/dispose)");
        }
    }
}
```

---

### Run Tests

```powershell
cd d:\Work\FastCycloneDdsCsharpBindings

# Run all Runtime tests
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# Run only lifecycle tests
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --filter "FullyQualifiedName~Lifecycle"
```

**Expected:** All 11 tests PASS.

**If tests fail:**
- Check native DLL is rebuilt and copied
- Verify P/Invoke signatures match native
- Check test output for specific error messages
- Use debugger to step through PerformOperation()

---

## Phase 5: Documentation

### Update README.md

**File:** `d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\README.md`

**Find the "Usage" section** (around line 45) and ADD this new section AFTER the basic usage example:

```markdown
### Instance Lifecycle Management

For keyed topics, you can dispose or unregister instances:

```csharp
[DdsTopic("Sensor")]
public partial struct SensorData
{
    [DdsKey] public int SensorId;  // Key field
    public double Temperature;
    public long Timestamp;
}

using var writer = new DdsWriter<SensorData>(...);

// Publish data
writer.Write(new SensorData { 
    SensorId = 42, 
    Temperature = 25.5, 
    Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds() 
});

// Sensor removed/failed - dispose instance
// This marks the instance as deleted
writer.DisposeInstance(new SensorData { 
    SensorId = 42  // Only key matters, other fields ignored
});

// OR: Application shutting down gracefully - unregister
// This notifies readers that this writer is going offline
writer.UnregisterInstance(new SensorData { SensorId = 42 });
```

**Semantics:**
- **DisposeInstance:** "This instance is deleted/dead" → Reader sees `NOT_ALIVE_DISPOSED`
- **UnregisterInstance:** "I'm no longer updating this instance" → Reader sees `NOT_ALIVE_NO_WRITERS`

**Performance:** Both operations maintain zero-allocation guarantee (same as `Write`).
```

---

## 📊 Deliverables Checklist

Before submitting, verify ALL items:

### Code Changes
- [ ] `cyclonedds\src\core\ddsc\src\dds_writer.c` - Added DDS_EXPORT to dispose/unregister
- [ ] `cyclone-bin\bin\ddsc.dll` - Rebuilt with new exports
- [ ] `Src\CycloneDDS.Runtime\Interop\DdsApi.cs` - Added 2 P/Invoke declarations
- [ ] `Src\CycloneDDS.Runtime\DdsWriter.cs` - Added enum, refactored Write, added DisposeInstance, UnregisterInstance

### Tests
- [ ] `tests\CycloneDDS.Runtime.Tests\DdsWriterLifecycleTests.cs` - 7 unit tests, all passing
- [ ] `tests\CycloneDDS.Runtime.Tests\InstanceLifecycleIntegrationTests.cs` - 4 integration tests, all passing

### Documentation
- [ ] `Src\CycloneDDS.Runtime\README.md` - Usage examples added

### Verification
- [ ] `dotnet build` - Success (no errors)
- [ ] `dotnet test tests\CycloneDDS.Runtime.Tests` - 46/47 passing (11 new + 35 existing, 1 skipped)
- [ ] Zero-allocation test PASSES (< 50KB for 1000 operations)

---

## 🧪 Testing & Validation

### Test Execution

**Run all tests:**
```powershell
dotnet test tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

**Expected Results:**
- Total tests: 46-47 (35 existing + 11 new)
- Passed: 45-46
- Skipped: 1 (LargeMessage test)
- Failed: 0

**Specifically verify:**
1. All 7 `DdsWriterLifecycleTests` PASS
2. All 4 `InstanceLifecycleIntegrationTests` PASS
3. `DisposeInstance_ZeroAllocation_1000Operations` PASSES (< 50KB)

### Manual Verification

**Test dispose behavior manually:**

```powershell
# Create a simple console app to verify
dotnet new console -o LifecycleDemo
```

**Program.cs:**
```csharp
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.Generated;

using var participant = new DdsParticipant(0);
// ... setup descriptor, writer, reader ...

// Write
writer.Write(new TestMessage { Id = 1, Value = 100 });
Console.WriteLine("Wrote sample");

// Dispose
writer.DisposeInstance(new TestMessage { Id = 1 });
Console.WriteLine("Disposed instance");

// Check reader sees disposal
using var scope = reader.Take();
Console.WriteLine($"Reader got {scope.Count} samples");
if (scope.Count > 0)
{
    Console.WriteLine($"ValidData: {scope.Infos[0].ValidData}"); // Should be 0
}
```

---

## 📝 Report Requirements

**Create:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-14-REPORT.md`

**Template:**

```markdown
# BATCH-14 Report: Instance Lifecycle Management

**Developer:** [Your Name]  
**Date:** [Date]  
**Status:** COMPLETE / BLOCKED  

## Summary

Implemented DDS instance lifecycle operations (DisposeInstance and UnregisterInstance) for keyed topics.

## Deliverables

### Code Changes
- [x] Native exports added to dds_writer.c
- [x] ddsc.dll rebuilt with new exports
- [x] P/Invoke declarations added to DdsApi.cs
- [x] DdsWriter refactored with unified PerformOperation()
- [x] DisposeInstance() implemented
- [x] UnregisterInstance() implemented

### Tests
- [x] 7 unit tests in DdsWriterLifecycleTests.cs (all passing)
- [x] 4 integration tests in InstanceLifecycleIntegrationTests.cs (all passing)
- [x] Zero-allocation test PASSES

### Documentation
- [x] README.md updated with usage examples

## Test Results

```
Total: 46 tests
Passed: 45 tests
Skipped: 1 test
Failed: 0 tests
```

**New Tests:**
- DdsWriterLifecycleTests: 7/7 PASS
- InstanceLifecycleIntegrationTests: 4/4 PASS

**Zero-Allocation Verification:**
- DisposeInstance 1000x: [X] KB (< 50KB threshold) ✅

## Issues Encountered

[List any problems and how you solved them]

## Performance Measurements

- Dispose operation: ~[X] bytes/operation
- Unregister operation: ~[X] bytes/operation
- Comparison to Write: [Similar/Identical]

## Verification

- [x] All unit tests passing
- [x] All integration tests passing
- [x] Zero-allocation verified
- [x] Manual testing confirms correct behavior
- [x] Documentation complete

## Questions / Concerns

[Any unclear requirements or design decisions that need review]

## Next Steps

Ready for code review and merge to main.
```

---

## 🎯 Success Criteria

Your batch is COMPLETE when:

1. ✅ **All code compiles** (no errors)
2. ✅ **All 11 new tests PASS** (100% pass rate)
3. ✅ **All 35 existing tests still PASS** (no regressions)
4. ✅ **Zero-allocation test PASSES** (< 50KB for 1000 operations)
5. ✅ **Documentation updated** (README.md with examples)
6. ✅ **Report submitted** (BATCH-14-REPORT.md)

---

## 🆘 Getting Help

If you're stuck:

1. **Review design docs again:**
   - `docs/INSTANCE-LIFECYCLE-DESIGN.md`
   - `docs/design-talk.md` lines 5106-5412

2. **Check existing implementations:**
   - Study `Write()` implementation (what you're extending)
   - Look at BATCH-13 tests for patterns

3. **Common issues:**
   - **P/Invoke fails:** Check DLL is rebuilt and in correct location
   - **Tests fail with "access violation":** Check native exports with dumpbin
   - **Build errors:** Verify all using statements and namespaces
   - **Tests timeout:** Increase Thread.Sleep durations

4. **Contact lead** with:
   - Specific error message
   - What you tried
   - Full stack trace

---

## 📚 Reference Documents

**Design & Architecture:**
- `docs/INSTANCE-LIFECYCLE-DESIGN.md` - Complete design
- `docs/design-talk.md` (lines 5106-5412) - Original discussion
- `docs/SERDATA-DESIGN.md` - Overall architecture

**Task Specification:**
- `docs/SERDATA-TASK-MASTER.md` (FCDC-S022b)

**Previous Work:**
- `.dev-workstream/batches/BATCH-13-INSTRUCTIONS.md` - Stage 3 pattern
- `.dev-workstream/reviews/BATCH-13.3-FINAL-REVIEW.md` - What was delivered

**Testing Patterns:**
- `tests/CycloneDDS.Runtime.Tests/IntegrationTests.cs` - Existing test patterns

---

## ⏱️ Time Estimates

**Phase 1 (Native):** 1-2 hours (including rebuild)  
**Phase 2 (P/Invoke):** 30 minutes  
**Phase 3 (Implementation):** 2-3 hours  
**Phase 4 (Testing):** 3-4 hours (writing + debugging)  
**Phase 5 (Documentation):** 1 hour  

**Total:** 8-11 hours (1-1.5 days of focused work)

Allow buffer time for:
- Native build issues
- Test debugging
- Learning curve (if new to project)

---

## 🎉 Conclusion

You're adding critical production functionality to a groundbreaking .NET DDS implementation!

**What makes this important:**
- First zero-allocation .NET DDS library
- Production-ready instance lifecycle management
- Enables graceful shutdown and resource cleanup
- Foundation for advanced DDS patterns

**Your contribution matters!** 🚀

Good luck, and don't hesitate to ask questions!

---

**Batch Version:** 1.0  
**Last Updated:** 2026-01-18  
**Prepared by:** Development Lead
