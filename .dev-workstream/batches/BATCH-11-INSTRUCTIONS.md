# BATCH-11: Arena Memory Manager + P/Invoke Declarations (COMBINED)

**Batch Number:** BATCH-11  
**Tasks:** FCDC-014 (Arena), FCDC-015 (P/Invoke)  
**Phase:** Phase 3 - Runtime Components  
**Estimated Effort:** 6-7 days  
**Priority:** CRITICAL  
**Dependencies:** None (starting new phase)

---

## 🎉 PHASE 2 COMPLETE! Starting Phase 3: Runtime Components

**Completed:** Full code generator (IDL, Native, Managed, Marshaller, Metadata, Tests)  
**Next:** Runtime memory management and C library bindings

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **Task 1 (Arena):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (P/Invoke):** Implement → Write tests → **ALL tests pass** ✅

---

## 📋 Required Reading

1. **Tasks:** `docs/FCDC-TASK-MASTER.md` → FCDC-014, FCDC-015
2. **Design:** `docs/FCDC-DETAILED-DESIGN.md` → §7.1 Arena Design, §11.1 P/Invoke
3. **Previous Quality Standard:** `.dev-workstream/reviews/BATCH-07-REVIEW.md`

**Report:** `.dev-workstream/reports/BATCH-11-REPORT.md`

---

## 🎯 Objectives

**Part 1: Arena Memory Manager**
1. Bump-pointer allocation (fast, no GC pressure)
2. Geometric growth strategy
3. Reset/Rewind for reuse
4. Trim policy (MaxRetainedCapacity)
5. Disposable pattern

**Part 2: P/Invoke Declarations**
6. Essential DDS C API functions
7. Handle wrappers (DdsEntity, DdsTopic, etc.)
8. Resource management
9. Error code handling

---

## ✅ Task 1: Arena Memory Manager

**File:** `src/CycloneDDS.Runtime/Memory/Arena.cs` (NEW)

```csharp
using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Memory;

/// <summary>
/// Arena allocator for efficient, GC-free memory allocation.
/// Uses bump-pointer allocation with geometric growth.
/// </summary>
public sealed class Arena : IDisposable
{
    private const int DefaultInitialCapacity = 4096;
    private const int MaxRetainedCapacity = 1024 * 1024; // 1MB default
    
    private IntPtr _buffer = IntPtr.Zero;
    private int _capacity = 0;
    private int _position = 0;
    private bool _disposed = false;
    
    public Arena() : this(DefaultInitialCapacity) { }
    
    public Arena(int initialCapacity)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        
        _capacity = initialCapacity;
        _buffer = Marshal.AllocHGlobal(initialCapacity);
    }
    
    /// <summary>
    /// Allocate bytes. Returns IntPtr to allocated memory.
    /// </summary>
    public IntPtr Allocate(int size)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Arena));
        
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        
        // Align to 8 bytes
        size = (size + 7) & ~7;
        
        // Check if we need to grow
        if (_position + size > _capacity)
        {
            Grow(size);
        }
        
        var ptr = IntPtr.Add(_buffer, _position);
        _position += size;
        return ptr;
    }
    
    /// <summary>
    /// Allocate typed array. Returns pointer to T[count].
    /// </summary>
    public unsafe IntPtr Allocate<T>(int count) where T : unmanaged
    {
        return Allocate(sizeof(T) * count);
    }
    
    /// <summary>
    /// Reset arena position to 0, reusing buffer.
    /// </summary>
    public void Reset()
    {
        _position = 0;
    }
    
    /// <summary>
    /// Get current mark for rewind.
    /// </summary>
    public int GetMark() => _position;
    
    /// <summary>
    /// Rewind to previous mark.
    /// </summary>
    public void Rewind(int mark)
    {
        if (mark < 0 || mark > _position)
            throw new ArgumentOutOfRangeException(nameof(mark));
        _position = mark;
    }
    
    /// <summary>
    /// Trim buffer if over MaxRetainedCapacity.
    /// </summary>
    public void Trim()
    {
        if (_capacity > MaxRetainedCapacity)
        {
            Marshal.FreeHGlobal(_buffer);
            _capacity = MaxRetainedCapacity;
            _buffer = Marshal.AllocHGlobal(_capacity);
            _position = 0;
        }
    }
    
    private void Grow(int requiredSize)
    {
        // Geometric growth: 2x capacity or required size, whichever is larger
        var newCapacity = Math.Max(_capacity * 2, _capacity + requiredSize);
        
        var newBuffer = Marshal.AllocHGlobal(newCapacity);
        
        // Copy existing data
        if (_position > 0)
        {
            unsafe
            {
                Buffer.MemoryCopy(_buffer.ToPointer(), newBuffer.ToPointer(), newCapacity, _position);
            }
        }
        
        Marshal.FreeHGlobal(_buffer);
        _buffer = newBuffer;
        _capacity = newCapacity;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
    
    // Properties for testing
    public int Capacity => _capacity;
    public int Position => _position;
    public int Available => _capacity - _position;
}
```

---

## ✅ Task 2: P/Invoke Declarations

**File:** `src/CycloneDDS.Runtime/Interop/DdsApi.cs` (NEW)

```csharp
using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Interop;

/// <summary>
/// P/Invoke declarations for Cyclone DDS C API.
/// </summary>
public static class DdsApi
{
    private const string DdsLib = "ddsc";
    
    // Entity handles
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsEntity
    {
        public IntPtr Handle;
        
        public static readonly DdsEntity Null = new DdsEntity { Handle = IntPtr.Zero };
        public bool IsValid => Handle != IntPtr.Zero;
    }
    
    // Return codes
    public const int DDS_RETCODE_OK = 0;
    public const int DDS_RETCODE_ERROR = -1;
    public const int DDS_RETCODE_TIMEOUT = -2;
    
    // Participant
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_participant(
        uint domain_id,
        IntPtr qos,
        IntPtr listener);
    
    // Topic
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_topic(
        DdsEntity participant,
        IntPtr descriptor,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr qos,
        IntPtr listener);
    
    // Writer
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_writer(
        DdsEntity participant_or_publisher,
        DdsEntity topic,
        IntPtr qos,
        IntPtr listener);
    
    // Reader
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_reader(
        DdsEntity participant_or_subscriber,
        DdsEntity topic,
        IntPtr qos,
        IntPtr listener);
    
    // Write
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_write(
        DdsEntity writer,
        IntPtr data);
    
    // Take
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_take(
        DdsEntity reader,
        IntPtr[] samples,
        IntPtr[] info,
        int max_samples,
        uint mask);
    
    // Return loan
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_return_loan(
        DdsEntity reader,
        IntPtr[] samples,
        int count);
    
    // Delete entity
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_delete(DdsEntity entity);
}
```

**File:** `src/CycloneDDS.Runtime/Interop/DdsEntityHandle.cs` (NEW)

```csharp
using System;

namespace CycloneDDS.Runtime.Interop;

/// <summary>
/// RAII wrapper for DDS entity handles.
/// </summary>
public sealed class DdsEntityHandle : IDisposable
{
    private DdsApi.DdsEntity _entity;
    private bool _disposed = false;
    
    public DdsEntityHandle(DdsApi.DdsEntity entity)
    {
        _entity = entity;
    }
    
    public DdsApi.DdsEntity Entity => _entity;
    public bool IsValid => _entity.IsValid && !_disposed;
    
    public void Dispose()
    {
        if (!_disposed && _entity.IsValid)
        {
            DdsApi.dds_delete(_entity);
            _entity = DdsApi.DdsEntity.Null;
            _disposed = true;
        }
    }
}
```

---

## 🧪 Testing Requirements

**Minimum 18 Tests:**

**Part 1: Arena Tests (10 tests)**
1. ✅ `Arena_Constructor_AllocatesInitialCapacity`
2. ✅ `Arena_Allocate_ReturnValidPointer`
3. ✅ `Arena_Allocate_AlignsTo8Bytes`
4. ✅ `Arena_Allocate_GrowsWhenNeeded`
5. ✅ `Arena_Reset_ReusesBuffer`
6. ✅ `Arena_GetMark_Rewind_Works`
7. ✅ `Arena_Trim_ReducesCapacity`
8. ✅ `Arena_Dispose_FreesMemory`
9. ✅ `Arena_TypedAllocate_CorrectSize`
10. ✅ `Arena_MultipleAllocations_Sequential`

**Part 2: P/Invoke Tests (8 tests)**
11. ✅ `DdsEntity_DefaultIsInvalid`
12. ✅ `DdsEntityHandle_Dispose_CallsDelete`
13. ✅ `DdsEntityHandle_DoubleDispose_Safe`
14. ✅ `DdsApi_CreateParticipant_Signature`
15. ✅ `DdsApi_CreateTopic_Signature`
16. ✅ `DdsApi_CreateWriter_Signature`
17. ✅ `DdsApi_Write_Signature`
18. ✅ `DdsApi_ReturnCodes_Defined`

**ALL tests must verify ACTUAL behavior (no Assert.Contains).**

---

## 📊 Report Requirements

1. **Implementation Summary**
2. **Test Results** (126+ tests: 108 previous + 18 new)
3. **Developer Insights:**
   - **Q1:** What issues did you encounter during Arena or P/Invoke implementation? How did you resolve them?
   - **Q2:** Did you spot any weak points in the existing codebase or new code? What would you improve?
   - **Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?
   - **Q4:** What edge cases or scenarios did you discover that weren't mentioned in the spec?

---

## 🎯 Success Criteria

1. ✅ Arena with bump-pointer allocation
2. ✅ Geometric growth working
3. ✅ Reset/Rewind functional
4. ✅ Trim policy implemented
5. ✅ All P/Invoke declarations defined
6. ✅ DdsEntityHandle RAII wrapper
7. ✅ 18+ tests passing
8. ✅ All 108 previous tests still passing (126 total)
9. ✅ NO Assert.Contains on implementation code

---

## ⚠️ Common Pitfalls

1. **Alignment** - Must align to 8 bytes for all allocations
2. **Growth** - Ensure Buffer.MemoryCopy preserves data
3. **Dispose** - Arena must free ALL allocated memory
4. **P/Invoke** - CallingConvention must be Cdecl
5. **Handles** - IntPtr.Zero means invalid entity

---

**Focus: Efficient arena allocator for DDS marshalling, P/Invoke foundation for Phase 3 runtime.**
