# FCDC-035: Loaned Sample Write API

**Task ID:** FCDC-035  
**Phase:** 5 - Advanced Features & Polish  
**Priority:** High (Major Performance Improvement)  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-017 (DdsWriter), FCDC-018A (Integration validation)  
**Design Reference:** `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` §2

---

## Objective

Implement zero-copy write API using DDS loaned samples for **2-3x performance improvement** on large messages.

---

## Problem Statement

**Current Write Path:**
```
User allocates → Populate fields → Pin → dds_write() → DDS copies to internal buffer
    ↓               ↓                  ↓          ↓
  Memory         Copy data         Copy data   Copy again!
```

**Issues:**
- **Double-copy overhead** for all data
- **GC allocations** for strings/sequences before marshalling
- **Pinning overhead** during write call

**Measured:** ~500ns overhead for 1KB message.

---

## Solution: Loaned Writes

**API:**
```csharp
using (var loan = writer.Loan())
{
    loan.Data.Id = 42;                   // Write directly to DDS memory
    loan.Data.Timestamp = GetTimestamp();
    loan.Write();                        // Zero-copy write
}
```

**Performance Target:** 2-3x faster for messages > 1KB.

---

## Implementation Steps

### Step 1: Add P/Invoke Declarations

**File:** `src/CycloneDDS.Runtime/Interop/DdsApi.cs`

```csharp
public static class DdsApi
{
    // Existing declarations...
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_request_loan(
        DdsEntity writer,
        ref IntPtr sample);
    
    [DllImport("ddsc", CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_return_loan(
        DdsEntity writer,
        ref IntPtr sample,
        int count);
}
```

### Step 2: Implement LoanedSample struct

**File:** `src/CycloneDDS.Runtime/LoanedSample.cs` (NEW)

```csharp
using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime;

/// <summary>
/// Represents a loaned DDS sample buffer for zero-copy writing.
/// MUST be disposed after use to return the loan.
/// Use in 'using' statement for safety.
/// </summary>
public ref struct LoanedSample<TNative> where TNative : unmanaged
{
    private readonly DdsWriter<TNative> _writer;
    private IntPtr _samplePtr;
    private bool _disposed;
    private bool _written;
    
    internal LoanedSample(DdsWriter<TNative> writer, IntPtr samplePtr)
    {
        _writer = writer;
        _samplePtr = samplePtr;
        _disposed = false;
        _written = false;
    }
    
    /// <summary>
    /// Access to the loaned native sample buffer.
    /// Write fields directly for zero-copy performance.
    /// </summary>
    public unsafe ref TNative Data
    {
        get
        {
            if (_disposed || _samplePtr == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(LoanedSample<TNative>));
            
            return ref *(TNative*)_samplePtr;
        }
    }
    
    /// <summary>
    /// Writes the loaned sample to DDS and returns the loan.
    /// Can only be called once.
    /// </summary>
    public void Write()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LoanedSample<TNative>));
        
        if (_written)
            throw new InvalidOperationException("Sample already written");
        
        var result = DdsApi.dds_write(_writer.Entity, _samplePtr);
        
        if (result < 0)
            throw new DdsException("Loaned write failed", (DdsReturnCode)result);
        
        _written = true;
        Dispose(); // Auto-dispose after write
    }
    
    /// <summary>
    /// Returns the loan without writing (if Write() not called).
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _samplePtr != IntPtr.Zero)
        {
            if (!_written)
            {
                // Return loan without writing
                DdsApi.dds_return_loan(_writer.Entity, ref _samplePtr, 1);
            }
            
            _samplePtr = IntPtr.Zero;
            _disposed = true;
        }
    }
}
```

### Step 3: Add Loan() Method to DdsWriter

**File:** `src/CycloneDDS.Runtime/DdsWriter.cs`

```csharp
public sealed class DdsWriter<TNative> where TNative : unmanaged
{
    // Existing methods...
    
    /// <summary>
    /// Requests a loaned sample from DDS for zero-copy writing.
    /// The returned loan MUST be disposed after use.
    /// 
    /// Performance: 2-3x faster than Write() for large messages.
    /// 
    /// Example:
    /// using (var loan = writer.Loan())
    /// {
    ///     loan.Data.Id = 42;
    ///     loan.Write();
    /// }
    /// </summary>
    public LoanedSample<TNative> Loan()
    {
        if (_writerHandle == null)
            throw new ObjectDisposedException(nameof(DdsWriter<TNative>));
        
        IntPtr samplePtr = IntPtr.Zero;
        var result = DdsApi.dds_request_loan(_writerHandle.Entity, ref samplePtr);
        
        if (result < 0)
        {
            throw new DdsException(
                "Failed to request loan. This may occur if DDS is out of resources.",
                (DdsReturnCode)result);
        }
        
        if (samplePtr == IntPtr.Zero)
            throw new DdsException("dds_request_loan returned null pointer", DdsReturnCode.Error);
        
        return new LoanedSample<TNative>(this, samplePtr);
    }
}
```

---

## Testing Requirements

### Unit Tests

**File:** `tests/CycloneDDS.Runtime.Tests/LoanedSampleTests.cs` (NEW)

```csharp
[Fact]
public void LoanedSample_Write_Succeeds()
{
    using var participant = new DdsParticipant();
    using var writer = new DdsWriter<SimpleMessageNative>(participant);
    using var reader = new DdsReader<SimpleMessageNative>(participant);
    
    Thread.Sleep(100); // Discovery
    
    // Write using loan
    using (var loan = writer.Loan())
    {
        loan.Data.Id = 42;
        loan.Data.Name = Marshal.StringToHGlobalAnsi("LoanTest");
        loan.Data.Value = 3.14;
        loan.Write();
    }
    
    // Verify received
    var received = ReadWithTimeout(reader);
    Assert.Equal(42, received.Id);
}

[Fact]
public void LoanedSample_DisposeWithoutWrite_ReturnsLoan()
{
    using var participant = new DdsParticipant();
    using var writer = new DdsWriter<SimpleMessageNative>(participant);
    
    using (var loan = writer.Loan())
    {
        loan.Data.Id = 99;
        // Don't call Write() - just dispose
    }
    
    // Should not throw - loan returned gracefully
}

[Fact]
public void LoanedSample_DoubleWrite_Throws()
{
    using var participant = new DdsParticipant();
    using var writer = new DdsWriter<SimpleMessageNative>(participant);
    
    using (var loan = writer.Loan())
    {
        loan.Write();
        Assert.Throws<InvalidOperationException>(() => loan.Write());
    }
}
```

### Performance Benchmarks

**File:** `benchmarks/CycloneDDS.Benchmarks/LoanedWriteBenchmark.cs` (NEW)

```csharp
[MemoryDiagnoser]
public class LoanedWriteBenchmark
{
    private DdsParticipant _participant;
    private DdsWriter<LargeMessageNative> _writer;
    
    [GlobalSetup]
    public void Setup()
    {
        _participant = new DdsParticipant();
        _writer = new DdsWriter<LargeMessageNative>(_participant);
    }
    
    [Benchmark(Baseline = true)]
    public void RegularWrite_1KB()
    {
        var msg = new LargeMessageNative { /* 1KB data */ };
        _writer.Write(ref msg);
    }
    
    [Benchmark]
    public void LoanedWrite_1KB()
    {
        using var loan = _writer.Loan();
        loan.Data.Id = 42;
        // ... populate 1KB
        loan.Write();
    }
}
```

**Expected Results:**
- 1KB: 2.5x faster
- 10KB: 2.6x faster
- Near-zero allocations for loaned writes

---

## Documentation Requirements

1. **XML Comments:** All public APIs
2. **Usage Example:** In `DdsWriter` class documentation
3. **Performance Guide:** Document when to use loans vs regular writes
4. **Safety Notes:** Lifetime constraints, thread-safety

---

## Acceptance Criteria

1. ✅ Loan() returns valid pointer
2. ✅ Write() succeeds and data received correctly
3. ✅ Dispose() without Write() returns loan safely
4. ✅ Double-write throws exception
5. ✅ Performance: ≥2x faster for 1KB messages
6. ✅ Zero allocations in hot path
7. ✅ All integration tests pass

---

## Design Reference

See `docs/ADVANCED-OPTIMIZATIONS-DESIGN.md` Section 2: Loaned Sample Write API

**Key Design Points:**
- ref struct for stack-only allocation
- Auto-dispose after Write() for convenience
- Safety: prevents use-after-dispose
- Performance target: 2-3x speedup
