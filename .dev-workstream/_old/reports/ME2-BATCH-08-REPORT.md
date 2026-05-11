# ME2-BATCH-08 Report

**Batch:** ME2-BATCH-08  
**Task:** ME2-T28  
**Developer:** AI Coding Agent  
**Date:** 2026-03-19  
**Status:** ✅ COMPLETE

---

## Q1 — Root Cause Analysis

**Problem:** `System.ArgumentException: Cannot bind to the target method because its signature is not compatible with that of the delegate type` thrown at `Delegate.CreateDelegate` inside `DdsTypeSupport.GetKeyDescriptors<T>()`.

**Root cause — Assembly Load Context type identity mismatch:**  
When the folder-based scanner loads external DLLs via `Assembly.LoadFrom` / `Assembly.LoadFile`, .NET places the assemblies in a different `AssemblyLoadContext` than the default one hosting the main application. The `CycloneDDS.Runtime` assembly that the external DLL was compiled against may therefore resolve to a distinct CLR object from the `CycloneDDS.Runtime` already loaded in the default context.

Because of this, the `DdsKeyDescriptor` struct referenced inside the external DLL's `GetKeyDescriptors()` method is technically a **different CLR type** from the `DdsKeyDescriptor` known to the calling runtime — even though both have identical layout, field names, and namespace.

`Delegate.CreateDelegate(typeof(Func<DdsKeyDescriptor[]>), method)` enforces strict type identity for every parameter and return type in the target `MethodInfo`. When the method's return type is `ExternalContext::CycloneDDS.Runtime.DdsKeyDescriptor[]` and the delegate's expected return type is `DefaultContext::CycloneDDS.Runtime.DdsKeyDescriptor[]`, the CLR rejects the binding with `ArgumentException`.

---

## Q2 — API Layout Mismatch Detail

| Element | Expected (Default Context) | Actual (Foreign Context) |
|---|---|---|
| `typeof(Func<DdsKeyDescriptor[]>)` | `Func<DefaultCtx::DdsKeyDescriptor[]>` | N/A — delegate type |
| `method.ReturnType` | `DefaultCtx::DdsKeyDescriptor[]` | `ForeignCtx::DdsKeyDescriptor[]` |
| CLR type identity check | `ReferenceEquals(T1, T2)` | **false** → `ArgumentException` |
| Struct layout (actual bytes) | `{Name, Offset, Index}` | **identical** |

The layout is byte-for-byte identical. The rejection is purely a CLR metadata identity enforcement, not a real semantic incompatibility.

---

## Q3 — Fix Implementation

**File:** `src/CycloneDDS.Runtime/DdsTypeSupport.cs`

**Strategy: try-fast-path / catch-fallback**

```csharp
// Fast path: direct delegate binding (same-context types)
try
{
    return (Func<DdsKeyDescriptor[]>)Delegate.CreateDelegate(
        typeof(Func<DdsKeyDescriptor[]>), method);
}
catch (ArgumentException)
{
    // Slow path: foreign assembly load context.
    // Fall back to MethodInfo.Invoke + field-by-field conversion.
    return () => ConvertExternalKeyDescriptors(method.Invoke(null, null));
}
```

The fallback invokes the method via `MethodInfo.Invoke` (boxing overhead, but safe), then reads each element of the returned array using `FieldInfo.GetValue` and constructs fresh `DdsKeyDescriptor` instances in the default context.

**Key design decisions:**
- The fast path is unchanged for same-context types — zero performance regression for all existing internal usage.
- The slow path is only entered once per type and the resulting delegate is stored in `_keysCache`, so the `MethodInfo.Invoke` overhead is amortized.
- The fallback gracefully handles `null` returns (keyless topics) and empty arrays.
- Missing fields (`Offset`, `Index`) default to `0` to tolerate older generated variants.

---

## Q4 — Test Coverage

**New file:** `tests/CycloneDDS.Runtime.Tests/DdsTypeSupportTests.cs`  
**12 tests added:**

| Test | Coverage |
|---|---|
| `GetKeyDescriptors_KeyedType_ReturnsDescriptors` | Happy path, single key |
| `GetKeyDescriptors_CompositeKeyType_ReturnsAllKeys` | Happy path, multiple keys |
| `GetKeyDescriptors_NonKeyedType_ReturnsNull` | keyless topic → `null` |
| `GetKeyDescriptors_MissingMethod_ThrowsInvalidOperationException` | Invalid type guard |
| `GetKeyDescriptors_IsCached_ReturnsSameArrayReference` | `ConcurrentDictionary` cache |
| `ConvertExternalKeyDescriptors_Null_ReturnsNull` | Fallback: null guard |
| `ConvertExternalKeyDescriptors_EmptyArray_ReturnsEmptyArray` | Fallback: empty |
| `ConvertExternalKeyDescriptors_SingleElement_MapsFieldsCorrectly` | Fallback: field mapping |
| `ConvertExternalKeyDescriptors_MultipleElements_PreservesOrder` | Fallback: ordering |
| `ConvertExternalKeyDescriptors_MissingOffsetField_DefaultsToZero` | Fallback: partial struct |
| `GetDescriptorOps_ValidType_ReturnsNonEmpty` | Sanity: ops passthrough |
| `GetDescriptorOps_InvalidType_Throws` | Sanity: ops invalid guard |

**Test results:**  
- `CycloneDDS.Runtime.Tests`: **146 passed, 0 failed, 1 skipped** (147 total)  
- Full `CycloneDDS.NET.Core.slnf` solution: **All projects 0 failed**

---

## Q5 — Impact & Risk Assessment

**Impact:** Eliminates CircuitHost crashes in `TopicExplorerPanel.AutoSubscribeAll` when the folder-based scanner populates dynamically loaded topics.  

**Risk:** Minimal. The fast path (existing behavior) is completely unchanged for all types loaded in the same assembly context. The slow path is new code behind a narrow `catch (ArgumentException)` guard.

**Performance:** The slow path (`MethodInfo.Invoke` + reflection) is entered at most once per foreign type — the result is cached in `_keysCache`. Subsequent calls to `GetKeyDescriptors<T>()` for that type hit the cache directly.

---

## Debt Tracker Update

| ID | Status |
|---|---|
| MON-DEBT-021 | ✅ Resolved — ME2-BATCH-08 |
