# BATCH-19: Async/Await + Content Filtering + Status Events

**Batch Number:** BATCH-19  
**Tasks:** FCDC-EXT02 (Async/Await Support), FCDC-EXT03 (Content Filtering), FCDC-EXT04 (Status & Discovery Events)  
**Phase:** Stage 3.75 - Extended DDS API - Modern C# Idioms  
**Estimated Effort:** 6-9 days  
**Priority:** **CRITICAL** (Core async patterns + discovery for production systems)  
**Dependencies:** BATCH-18 complete (Type Auto-Discovery + Read/Take)

---

## 📋 Onboarding & Workflow

### Developer Instructions

Welcome to **BATCH-19**, continuing **Stage 3.75: Extended DDS API**! This batch implements three interconnected features that make the FastCycloneDDS bindings truly production-ready:

1. **FCDC-EXT02:** Async/Await Support – Non-blocking data waits with `WaitDataAsync()` and `StreamAsync()`
2. **FCDC-EXT03:** Content Filtering – Lambda-based reader-side predicates (zero allocation)
3. **FCDC-EXT04:** Status & Discovery Events – Connection monitoring, liveliness tracking, `WaitForReaderAsync()`

**Why These Together:**
- EXT02 provides async infrastructure (listeners, TaskCompletionSource patterns)
- EXT04 reuses async infrastructure for discovery events
- EXT03 is independent but complements filtered data access
- All three are essential for production DDS applications

### Required Reading (IN ORDER)

**READ THESE BEFORE STARTING:**

1. **Workflow Guide:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\README.md`  
   - Batch system, report requirements, testing standards

2. **Task Definitions:** `d:\Work\FastCycloneDdsCsharpBindings\docs\SERDATA-TASK-MASTER.md`  
   - Section: FCDC-EXT02 (lines 1723-1765) – Async/Await details
   - Section: FCDC-EXT03 (lines 1768-1810) – Content Filtering details
   - Section: FCDC-EXT04 (lines 1813-1855) – Status & Discovery details

3. **Design Document:** `d:\Work\FastCycloneDdsCsharpBindings\docs\EXTENDED-DDS-API-DESIGN.md` ← **CRITICAL**
   - **Section 5: Async/Await (WaitDataAsync)** – Implementation patterns, listener callbacks, GC pinning
   - **Section 6: Content Filtering** – Predicate patterns, enumerator modification
   - **Section 7: Status & Discovery (Events)** – Event structs, status APIs, WaitForReaderAsync pattern
   - Read complete sections for P/Invoke signatures, code examples, edge cases

4. **Previous Batch Review:** `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reviews\BATCH-18-REVIEW.md`  
   - Learn from Type Auto-Discovery + Read/Take feedback

5. **Cyclone DDS Documentation:**
   - Listener API: https://cyclonedds.io/docs/cyclonedds/latest/api/listeners.html
   - Status conditions: https://cyclonedds.io/docs/cyclonedds/latest/api/status.html

### Repository Structure

```
d:\Work\FastCycloneDdsCsharpBindings\
├── Src\
│   └── CycloneDDS.Runtime\           # ← YOU WORK HERE
│       ├── DdsReader.cs              # ← MODIFY (add WaitDataAsync, SetFilter)
│       ├── DdsWriter.cs              # ← MODIFY (add PublicationMatched event)
│       ├── ViewScope.cs              # ← MODIFY (add filter support to enumerator)
│       └── Interop\
│           └── DdsApi.cs             # ← MODIFY (add listener + status APIs)
│
├── tests\
│   └── CycloneDDS.Runtime.Tests\     # Runtime tests
│       ├── AsyncTests.cs             # ← NEW FILE (4+ tests for EXT02)
│       ├── FilteringTests.cs         # ← NEW FILE (3+ tests for EXT03)
│       ├── DiscoveryTests.cs         # ← NEW FILE (4+ tests for EXT04)
│       └── IntegrationTests.cs       # ← MODIFY (update existing tests if needed)
│
├── cyclone-compiled\                 # Cyclone DDS native binaries
│   └── bin\
│       └── ddsc.dll                  # DDS native library (custom build)
│
└── .dev-workstream\
    ├── batches\
    │   └── BATCH-19-INSTRUCTIONS.md  # ← This file
    └── reports\
        └── BATCH-19-REPORT.md        # ← Submit your report here
```

### Critical Tool & Library Locations

**DDS Native Library:**
- **Location:** `d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\ddsc.dll`
- **Usage:** Runtime tests link against this (custom build with serdata + listener exports)
- **Do NOT modify:** Already configured with all required exports

**Projects to Build:**

Build order (dependencies):
```powershell
# 1. Runtime (DDS API)
dotnet build d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# 2. Tests
dotnet build d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj

# 3. Run all tests
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj
```

### Report Submission

**When done, submit your report to:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-19-REPORT.md`

**If you have questions, create:**  
`d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\questions\BATCH-19-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (EXT02 - Async):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (EXT03 - Filtering):** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3 (EXT04 - Events):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including BATCH-18 tests: 44 passing + your new tests)

**Why:** Ensures each component is solid before building on top of it. EXT04 reuses EXT02's listener infrastructure – if EXT02 is broken, EXT04 will cascade fail.

**After EACH task completion:**
```powershell
# Verify ALL tests pass (not just new ones)
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build
# Expected: 44 (BATCH-18) + 4 (EXT02) + 3 (EXT03) + 4 (EXT04) = 55+ tests passing
```

---

## Context

This batch continues the **Stage 3.75 Extended DDS API** work started in BATCH-18. You are adding:

1. **Modern async patterns** – Making DDS play nicely with async/await, eliminating thread-burning polling
2. **Declarative filtering** – Lambda-based data selection at the reader (zero-copy maintained)
3. **Discovery events** – Connectivity monitoring essential for distributed systems

**Related Tasks:**
- [FCDC-EXT02](../docs/SERDATA-TASK-MASTER.md#fcdc-ext02-asyncawait-support-waitdataasync) – Async/Await Support
- [FCDC-EXT03](../docs/SERDATA-TASK-MASTER.md#fcdc-ext03-content-filtering-reader-side-predicates) – Content Filtering
- [FCDC-EXT04](../docs/SERDATA-TASK-MASTER.md#fcdc-ext04-status--discovery-events) – Status & Discovery Events

**Design Context:**
All three tasks are specified in `EXTENDED-DDS-API-DESIGN.md` with complete implementation patterns, P/Invoke signatures, and usage examples. Reference the design doc for detailed specifications.

---

## 🎯 Batch Objectives

**Goal:** Transform FastCycloneDDS from a high-performance core into a production-ready library with:
- Async patterns that integrate with .NET ecosystem
- Declarative filtering for clean application code
- Discovery/health monitoring for distributed system reliability

**Why It Matters:**
- **Async:** Production apps use async/await everywhere. Polling is unacceptable.
- **Filtering:** Application logic should be declarative, not imperative loops
- **Events:** Distributed systems need connectivity monitoring ("did my peer go down?")

---

## ✅ Tasks

### Task 1: FCDC-EXT02 - Async/Await Support (WaitDataAsync)

**Priority:** CRITICAL  
**Estimated Effort:** 3-4 days  
**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 5

#### Overview

Implement non-blocking data waiting using DDS listeners bridged to .NET `async/await` via `TaskCompletionSource`. Critical: listeners should be **lazy** (only created when `WaitDataAsync()` is first called).

#### Files to Modify

**1. P/Invoke Layer:** `Src\CycloneDDS.Runtime\Interop\DdsApi.cs`

Add listener APIs:
- `dds_create_listener(IntPtr participant)` → IntPtr
- `dds_delete_listener(IntPtr listener)` → int
- `dds_lset_data_available(IntPtr listener, IntPtr callback)` → void
- `dds_reader_set_listener(IntPtr reader, IntPtr listener)` → int

**Design doc reference:** Section 5.2 – P/Invoke declarations with exact signatures

**2. Reader Implementation:** `Src\CycloneDDS.Runtime\DdsReader.cs`

Add:
- `WaitDataAsync(CancellationToken cancellationToken = default)` → Task
- `StreamAsync(CancellationToken cancellationToken = default)` → IAsyncEnumerable<ViewScope<TView>>
- Private fields: `_listener` (IntPtr), `_listenerHandle` (GCHandle), `_waitTaskSource` (TaskCompletionSource)
- Native callback: `OnDataAvailable(IntPtr reader)`
- Lazy listener creation: `EnsureListenerAttached()`
- Cleanup: Update `Dispose()` to free listener + GCHandle

**Design doc reference:** Section 5.3-5.5 – Implementation patterns with code examples

#### Implementation Requirements

**Lazy Listener Pattern (CRITICAL):**
```csharp
// Only create listener on first WaitDataAsync() call
private void EnsureListenerAttached()
{
    if (_listener != IntPtr.Zero) return; // Already attached
    
    _listener = DdsApi.dds_create_listener(_participant.Handle);
    // ... callback setup with GC pinning
}
```

**Native Callback Bridge:**
```csharp
// Must pin delegate to prevent GC collection
private static void OnDataAvailable(IntPtr readerHandle)
{
    // Find reader instance, complete TaskCompletionSource
}
```

**Cancellation Support:**
```csharp
public async Task WaitDataAsync(CancellationToken cancellationToken = default)
{
    // Register cancellation, attach listener, await TaskCompletionSource
}
```

**Design doc reference:** Section 5.6 – GC pinning patterns, Section 5.7 – Cancellation handling

#### Edge Cases to Handle

1. **Multiple concurrent waits:** Only one `TaskCompletionSource` active at a time
2. **Dispose during wait:** Cancel pending wait, cleanup listener
3. **Data already available:** Complete immediately (check cache first)
4. **Listener creation failure:** Throw `DdsException` with clear message

#### Tests Required (Minimum 4)

**File:** `tests\CycloneDDS.Runtime.Tests\AsyncTests.cs` (NEW)

1. **`WaitDataAsync_CompletesWhenDataArrives`**
   - Setup: Writer + Reader, start `WaitDataAsync()` task
   - Action: Write data from another task
   - Verify: Wait task completes within 1 second
   - Verify: Data is available via `Take()`

2. **`WaitDataAsync_RespectsCancellation`**
   - Setup: Reader with no writer
   - Action: Call `WaitDataAsync()` with 100ms timeout
   - Verify: Task throws `OperationCanceledException` within 200ms

3. **`Polling_NoListener_NoOverhead`**
   - Setup: Reader, only use `Take()` (never call `WaitDataAsync`)
   - Verify: Internal `_listener` field is `IntPtr.Zero` (use reflection or expose for testing)
   - Why: Proves lazy listener creation

4. **`DisposeWithListener_NoLeaks`**
   - Setup: Reader, call `WaitDataAsync()` once (creates listener)
   - Action: Dispose reader
   - Verify: No exception, no memory leaks (manual verification acceptable)
   - Bonus: Use weak reference to verify GCHandle is freed

**Additional Test (Bonus):**
5. **`StreamAsync_YieldsMultipleSamples`**
   - Setup: Writer + Reader
   - Action: Use `await foreach` on `StreamAsync()`, write 3 samples
   - Verify: Enumerator yields 3 `ViewScope` instances

#### Success Criteria

- ✅ `WaitDataAsync()` completes on data arrival
- ✅ Cancellation works correctly (respects `CancellationToken`)
- ✅ Listener only created when needed (lazy initialization verified)
- ✅ No memory leaks on dispose (GCHandle freed, listener deleted)
- ✅ All 4+ tests pass
- ✅ All existing tests still pass (44 from BATCH-18)

---

### Task 2: FCDC-EXT03 - Content Filtering (Reader-Side Predicates)

**Priority:** HIGH  
**Estimated Effort:** 1-2 days  
**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 6

#### Overview

Add lambda-based filtering to `DdsReader`. Filters execute during `ViewScope` iteration with zero allocation overhead (no intermediate collections).

#### Files to Modify

**1. Reader API:** `Src\CycloneDDS.Runtime\DdsReader.cs`

Add:
- `SetFilter(Predicate<TView>? filter)` – Set or clear filter
- Private field: `_filter` (Predicate<TView>?)

**2. ViewScope Iteration:** `Src\CycloneDDS.Runtime\ViewScope.cs`

Modify:
- Add `_filter` field (passed from reader)
- Update `Enumerator.MoveNext()` to skip filtered samples

**Design doc reference:** Section 6.2-6.3 – Implementation patterns

#### Implementation Requirements

**Filter Storage (Thread-Safe):**
```csharp
// DdsReader.cs
private volatile Predicate<TView>? _filter; // Atomic assignment for thread safety

public void SetFilter(Predicate<TView>? filter)
{
    _filter = filter; // Atomic write
}
```

**Filter Application (Zero-Copy):**
```csharp
// ViewScope.Enumerator.MoveNext()
public bool MoveNext()
{
    while (_index < _samples.Length)
    {
        _current = _samples[_index++];
        
        // Apply filter if present
        if (_filter != null && !_filter(_current))
            continue; // Skip filtered sample
            
        return true;
    }
    return false;
}
```

**Design doc reference:** Section 6.4 – Enumerator modification, Section 6.5 – Thread safety

#### Edge Cases to Handle

1. **Null filter:** All samples visible (no filtering)
2. **Filter that rejects all:** Iteration yields zero samples (valid)
3. **Filter throws exception:** Let exception propagate to caller (no catch)
4. **Runtime filter update:** Next `Take()`/`Read()` uses new filter

#### Tests Required (Minimum 3)

**File:** `tests\CycloneDDS.Runtime.Tests\FilteringTests.cs` (NEW)

1. **`Filter_Applied_OnlyMatchingSamples`**
   - Setup: Write 3 samples: {Value=1}, {Value=5}, {Value=10}
   - Action: `reader.SetFilter(v => v.Value > 3)`, Take()
   - Verify: Iteration yields only {5, 10}

2. **`Filter_UpdatedAtRuntime_NewFilterApplied`**
   - Setup: Write 3 samples: {Value=1}, {Value=5}, {Value=10}
   - Action 1: `SetFilter(v => v.Value > 5)`, Take() → verify only {10}
   - Action 2: Write 3 more, `SetFilter(v => v.Value < 8)`, Take() → verify {1, 5}
   - Why: Proves runtime filter updates work

3. **`Filter_Null_AllSamplesReturned`**
   - Setup: Write 3 samples, `SetFilter(v => v.Value > 100)` (none match)
   - Action: `SetFilter(null)`, Take()
   - Verify: All 3 samples visible

**Additional Test (Bonus):**
4. **`Filter_ComplexPredicate_Compiles`**
   - Setup: `SetFilter(v => v.Value > 5 && v.Value < 20 && v.Id != 10)`
   - Verify: Compiles and filters correctly

#### Success Criteria

- ✅ Filter predicates execute during iteration
- ✅ Filter updates are thread-safe (volatile field, atomic assignment)
- ✅ Zero allocation overhead (no List<T>, no intermediate buffers)
- ✅ JIT inlining verified for simple predicates (manual verification acceptable)
- ✅ All 3+ tests pass
- ✅ All existing tests still pass (48+ from EXT02)

---

### Task 3: FCDC-EXT04 - Status & Discovery (Events)

**Priority:** HIGH  
**Estimated Effort:** 2-3 days  
**Design Reference:** `EXTENDED-DDS-API-DESIGN.md` Section 7

#### Overview

Map DDS status callbacks to C# events. Expose connectivity monitoring (`PublicationMatched`, `SubscriptionMatched`) and health tracking. Add `WaitForReaderAsync()` helper to solve "lost first message" problem.

#### Files to Modify

**1. P/Invoke Layer:** `Src\CycloneDDS.Runtime\Interop\DdsApi.cs`

Add status structs:
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct DdsPublicationMatchedStatus
{
    public int TotalCount;
    public int TotalCountChange;
    public int CurrentCount;
    public int CurrentCountChange;
}

[StructLayout(LayoutKind.Sequential)]
public struct DdsSubscriptionMatchedStatus
{
    // Same fields as PublicationMatchedStatus
}
```

Add listener P/Invoke:
- `dds_lset_publication_matched(IntPtr listener, IntPtr callback)` → void
- `dds_lset_subscription_matched(IntPtr listener, IntPtr callback)` → void
- `dds_get_publication_matched_status(IntPtr writer, out DdsPublicationMatchedStatus status)` → int
- `dds_get_subscription_matched_status(IntPtr reader, out DdsSubscriptionMatchedStatus status)` → int

**Design doc reference:** Section 7.2 – Status structs, Section 7.3 – P/Invoke APIs

**2. Writer Events:** `Src\CycloneDDS.Runtime\DdsWriter.cs`

Add:
- `event EventHandler<DdsPublicationMatchedStatus>? PublicationMatched`
- Property: `PublicationMatchedStatus CurrentStatus` (read-only)
- Method: `WaitForReaderAsync(TimeSpan timeout)` → Task<bool>
- Private: Lazy listener attachment (reuse pattern from EXT02)

**3. Reader Events:** `Src\CycloneDDS.Runtime\DdsReader.cs`

Add:
- `event EventHandler<DdsSubscriptionMatchedStatus>? SubscriptionMatched`
- Property: `SubscriptionMatchedStatus CurrentStatus` (read-only)
- Private: Lazy listener attachment (reuse pattern from EXT02)

**Design doc reference:** Section 7.4-7.5 – Event patterns, WaitForReaderAsync implementation

#### Implementation Requirements

**Event Pattern (Standard C#):**
```csharp
// DdsWriter.cs
public event EventHandler<DdsPublicationMatchedStatus>? PublicationMatched;

private void OnPublicationMatched(IntPtr writerHandle, DdsPublicationMatchedStatus status)
{
    // Update cached status
    _currentStatus = status;
    
    // Raise event
    PublicationMatched?.Invoke(this, status);
    
    // Complete WaitForReaderAsync if pending
    _waitForReaderTaskSource?.TrySetResult(true);
}
```

**WaitForReaderAsync Helper:**
```csharp
public async Task<bool> WaitForReaderAsync(TimeSpan timeout)
{
    // If already matched, return immediately
    if (_currentStatus.CurrentCount > 0)
        return true;
        
    // Setup TaskCompletionSource, subscribe to event, await with timeout
    using var cts = new CancellationTokenSource(timeout);
    // ...
}
```

**Design doc reference:** Section 7.6 – WaitForReaderAsync pattern with timeout handling

#### Edge Cases to Handle

1. **Event subscription after match:** Status still accessible via `CurrentStatus` property
2. **Multiple listeners:** One listener per writer/reader (share with WaitDataAsync listener)
3. **Dispose during wait:** Cancel `WaitForReaderAsync`, cleanup listener
4. **Status query failure:** Return default status, log warning

#### Tests Required (Minimum 4)

**File:** `tests\CycloneDDS.Runtime.Tests\DiscoveryTests.cs` (NEW)

1. **`PublicationMatched_EventFires_OnReaderCreation`**
   - Setup: Create writer, subscribe to `PublicationMatched` event
   - Action: Create reader
   - Verify: Event fires within 1 second
   - Verify: `CurrentCount == 1`

2. **`WaitForReaderAsync_CompletesOnDiscovery`**
   - Setup: Create writer, start `WaitForReaderAsync(2 seconds)` task
   - Action: Create reader from another task
   - Verify: Wait task completes within 2 seconds with `true`

3. **`PublicationMatched_EventFires_OnReaderDispose`**
   - Setup: Writer + Reader, both discovered
   - Action: Dispose reader
   - Verify: Writer's `PublicationMatched` event fires with `CurrentCountChange < 0`

4. **`SubscriptionMatched_CurrentCount_Accurate`**
   - Setup: Create reader, verify `CurrentCount == 0`
   - Action 1: Create writer 1 → verify `CurrentCount == 1`
   - Action 2: Create writer 2 → verify `CurrentCount == 2`
   - Action 3: Dispose writer 1 → verify `CurrentCount == 1`
   - Why: Proves accurate connection tracking

**Additional Test (Bonus):**
5. **`WaitForReaderAsync_Timeout_ReturnsFalse`**
   - Setup: Writer with no reader
   - Action: `WaitForReaderAsync(500ms)`
   - Verify: Returns `false` after timeout

#### Success Criteria

- ✅ Events fire correctly on discovery/loss
- ✅ `CurrentStatus` properties accurate (reflect actual connection counts)
- ✅ `WaitForReaderAsync()` solves "lost first message" problem
- ✅ Listener sharing works (WaitDataAsync + events use same listener)
- ✅ All 4+ tests pass
- ✅ All existing tests still pass (51+ from EXT02+EXT03)

---

## 🧪 Testing Requirements

### Test Counts

**Minimum Total:** 11 tests (4 EXT02 + 3 EXT03 + 4 EXT04)  
**Target:** 13-15 tests (add bonus tests for edge cases)

### Test Categories

1. **Async Tests (AsyncTests.cs):**
   - Data arrival triggering
   - Cancellation
   - Lazy listener creation
   - Disposal cleanup

2. **Filtering Tests (FilteringTests.cs):**
   - Filter application
   - Runtime filter updates
   - Null filter handling

3. **Discovery Tests (DiscoveryTests.cs):**
   - Event firing on discovery/loss
   - WaitForReaderAsync behavior
   - Connection count accuracy

### Test Quality Standards

**⚠️ CRITICAL: NO SHALLOW TESTS**

❌ **NOT ACCEPTABLE:**
```csharp
[Fact]
public void WaitDataAsync_Exists()
{
    var reader = new DdsReader<TestMessage>(...);
    Assert.NotNull(reader.WaitDataAsync); // Tests nothing
}
```

✅ **REQUIRED:**
```csharp
[Fact]
public async Task WaitDataAsync_CompletesWhenDataArrives()
{
    // Setup
    var writer = new DdsWriter<TestMessage>(...);
    var reader = new DdsReader<TestMessage>(...);
    
    // Start wait on background task
    var waitTask = Task.Run(async () => await reader.WaitDataAsync());
    await Task.Delay(100); // Ensure wait is active
    
    // Write data
    writer.Write(new TestMessage { Id = 42, Value = 100 });
    
    // Verify wait completes
    var completed = await Task.WhenAny(waitTask, Task.Delay(1000));
    Assert.Same(waitTask, completed); // Completed within 1 second
    
    // Verify data is available
    using var scope = reader.Take();
    Assert.Single(scope.Samples);
}
```

**All tests must verify ACTUAL BEHAVIOR, not just API existence.**

### Verification Commands

After completing EACH task:
```powershell
# Build
dotnet build d:\Work\FastCycloneDdsCsharpBindings\Src\CycloneDDS.Runtime\CycloneDDS.Runtime.csproj

# Run ALL tests
dotnet test d:\Work\FastCycloneDdsCsharpBindings\tests\CycloneDDS.Runtime.Tests\CycloneDDS.Runtime.Tests.csproj --no-build

# Expected progression:
# After EXT02: 48+ tests passing (44 BATCH-18 + 4 new)
# After EXT03: 51+ tests passing (48 + 3 new)
# After EXT04: 55+ tests passing (51 + 4 new)
```

---

## 📊 Report Requirements

### Report File

Submit to: `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\reports\BATCH-19-REPORT.md`

Use template: `d:\Work\FastCycloneDdsCsharpBindings\.dev-workstream\templates\BATCH-REPORT-TEMPLATE.md`

### Mandatory Sections

**1. Completion Checklist**
- [ ] FCDC-EXT02 (Async/Await) – Complete with 4+ tests passing
- [ ] FCDC-EXT03 (Content Filtering) – Complete with 3+ tests passing
- [ ] FCDC-EXT04 (Status Events) – Complete with 4+ tests passing
- [ ] All tests passing (55+ total)
- [ ] No compiler warnings
- [ ] No memory leaks (manual verification for listeners)

**2. Test Results**
```
Total tests: XX
Passed: XX
Failed: 0
Skipped: XX (if any - justify)

Test breakdown:
- BATCH-18 (existing): 44 passing
- AsyncTests.cs: X passing
- FilteringTests.cs: X passing
- DiscoveryTests.cs: X passing
```

**3. Implementation Notes**

Document for EACH task (EXT02, EXT03, EXT04):
- Challenges encountered
- Design decisions beyond spec
- Alternative approaches considered
- Integration with existing code

**4. Developer Insights (CRITICAL)**

Answer these questions:

**Q1: Async Infrastructure Challenges**
What challenges did you face implementing the listener callback bridge? How did you handle GC pinning and lifecycle management?

**Q2: Filter Performance**
Did you verify JIT inlining for simple predicates? What's the overhead of complex predicates? Any performance observations?

**Q3: Event Pattern Design**
How did you handle listener sharing between `WaitDataAsync()` and status events? Any race conditions or edge cases discovered?

**Q4: API Usability**
From a user perspective, how intuitive are these APIs? Any API design improvements you'd suggest?

**Q5: Testing Approach**
What was your strategy for testing async behavior? Any flaky test issues or timing challenges?

**Q6: Code Quality**
What areas of the codebase could be improved? Any technical debt introduced? Refactoring opportunities?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-EXT02 Complete:**
  - `WaitDataAsync()` and `StreamAsync()` implemented
  - Lazy listener attachment verified
  - 4+ tests passing (async completion, cancellation, lazy init, disposal)
  - All BATCH-18 tests still passing

- ✅ **FCDC-EXT03 Complete:**
  - `SetFilter()` implemented on `DdsReader`
  - ViewScope enumerator applies filters
  - 3+ tests passing (filter application, runtime updates, null handling)
  - Zero allocation verified (no intermediate collections)

- ✅ **FCDC-EXT04 Complete:**
  - Events on `DdsWriter` and `DdsReader`
  - `WaitForReaderAsync()` helper
  - 4+ tests passing (event firing, connection counts, timeout handling)
  - Status structs accurate

- ✅ **Quality Standards:**
  - All tests verify ACTUAL BEHAVIOR (not shallow)
  - No compiler warnings
  - No memory leaks (GCHandle cleanup verified)
  - Code compiles and runs on first try

- ✅ **Documentation:**
  - Report submitted with all mandatory sections
  - Developer insights capture challenges and decisions
  - Code comments for complex logic (callbacks, pinning, thread safety)

---

## ⚠️ Common Pitfalls to Avoid

### Listener Lifecycle Management

❌ **Don't:** Create listener in constructor
```csharp
// WRONG - creates listener even if never used
public DdsReader(...)
{
    _listener = DdsApi.dds_create_listener(_participant);
}
```

✅ **Do:** Lazy listener creation
```csharp
private void EnsureListenerAttached()
{
    if (_listener != IntPtr.Zero) return;
    _listener = DdsApi.dds_create_listener(_participant);
}

public async Task WaitDataAsync()
{
    EnsureListenerAttached(); // Only creates on first call
}
```

### GC Pinning

❌ **Don't:** Store delegate without pinning
```csharp
// WRONG - delegate can be GC collected
var callback = new SomeDelegate(OnDataAvailable);
DdsApi.dds_lset_data_available(_listener, callback);
```

✅ **Do:** Pin with GCHandle
```csharp
var callback = new SomeDelegate(OnDataAvailable);
_listenerHandle = GCHandle.Alloc(callback);
var ptr = Marshal.GetFunctionPointerForDelegate(callback);
DdsApi.dds_lset_data_available(_listener, ptr);
```

### TaskCompletionSource Reuse

❌ **Don't:** Reuse same TaskCompletionSource
```csharp
// WRONG - second wait will never complete
await reader.WaitDataAsync(); // Uses _tcs
await reader.WaitDataAsync(); // Still uses same _tcs (already completed)
```

✅ **Do:** Create new TaskCompletionSource per wait
```csharp
public async Task WaitDataAsync()
{
    var tcs = new TaskCompletionSource<bool>();
    _waitTaskSource = tcs; // Store for callback
    // ... register cancellation, attach listener
    await tcs.Task;
}
```

### Filter Thread Safety

❌ **Don't:** Use lock for filter field
```csharp
// WRONG - locking is overkill for simple assignment
private object _filterLock = new object();
public void SetFilter(Predicate<TView>? filter)
{
    lock (_filterLock) { _filter = filter; }
}
```

✅ **Do:** Use volatile for atomic assignment
```csharp
private volatile Predicate<TView>? _filter;
public void SetFilter(Predicate<TView>? filter)
{
    _filter = filter; // Atomic assignment
}
```

### Event Subscription Race

❌ **Don't:** Assume event fires before subscription
```csharp
// WRONG - event might fire before subscription
var writer = new DdsWriter<T>(...);
var reader = new DdsReader<T>(...); // Discovery happens immediately
writer.PublicationMatched += Handler; // Too late!
```

✅ **Do:** Subscribe before creating entities OR use CurrentStatus
```csharp
var writer = new DdsWriter<T>(...);
writer.PublicationMatched += Handler; // Subscribe first
var reader = new DdsReader<T>(...);

// OR check status explicitly
if (writer.CurrentStatus.CurrentCount > 0)
    // Already matched
```

---

## 📚 Reference Materials

### Task Definitions
- **SERDATA-TASK-MASTER.md:**
  - FCDC-EXT02 (lines 1723-1765)
  - FCDC-EXT03 (lines 1768-1810)
  - FCDC-EXT04 (lines 1813-1855)

### Design Documents
- **EXTENDED-DDS-API-DESIGN.md:**
  - Section 5: Async/Await (WaitDataAsync)
  - Section 6: Content Filtering
  - Section 7: Status & Discovery (Events)

### Previous Work
- **BATCH-18 Instructions:** `.dev-workstream\batches\BATCH-18-INSTRUCTIONS.md`
- **BATCH-18 Review:** `.dev-workstream\reviews\BATCH-18-REVIEW.md`

### External References
- **Cyclone DDS Listeners:** https://cyclonedds.io/docs/cyclonedds/latest/api/listeners.html
- **Cyclone DDS Status:** https://cyclonedds.io/docs/cyclonedds/latest/api/status.html
- **.NET Async Patterns:** https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/

---

**Good luck! Focus on test quality, proper async patterns, and clean API design. This batch makes FastCycloneDDS production-ready.** 🚀
