# ME2-BATCH-05 Report

**Batch:** ME2-BATCH-05  
**Tasks:** ME2-T23, ME2-T24  
**Status:** ✅ Complete  
**Date:** 2026-03-19  

---

## Summary

Both tasks are implemented, tested, and passing. The full DdsMonitor.Engine.Tests suite (414 tests) passes with 0 failures. The DdsMonitor Blazor project builds cleanly with 0 errors.

---

## Task ME2-T23 — Union List Item Structure Expansion Fix

### Root Cause

In `DynamicForm.razor`, the rendering pipeline for a field's control ends with:

```
if (drawer != null)         → scalar drawer
else if (IsFixedSizeArray)  → fixed-size array widget
else if (IsArrayField)      → dynamic array / list widget
else if (ValueType.IsArray) → read-only count fallback
else                        → <span class="dynamic-form__unsupported">
                                @value.ToString()          ← BUG IS HERE
                              </span>
```

When a union arm's type is a complex struct (e.g. `Vec3`, `Pose`, `FloatBuf8`), none of the early branches trigger:

- No registered drawer for the struct type
- `IsFixedSizeArray = false` (the arm field itself is not marked as a fixed array)
- `IsArrayField = false`
- `ValueType.IsArray = false`

The result: the struct value falls through to the `unsupported` span and renders as its full type name or `ToString()` output — **fields are never shown, making the arm uneditable**.

### Why Union Arm Structs Are Not Flattened

`AppendFields` in `TopicMetadata.cs` explicitly skips flattening for union arms:

```csharp
// Union arm members (even if they happen to be flattenable structs) are
// kept as atomic FieldMetadata so the union visibility logic works correctly.
if (!isDiscriminatorField && !isUnionArm && IsFlattenable(memberType))
{
    AppendFields(...);  // recurse + flatten
    continue;
}
```

This is correct — the union arm must remain one `FieldMetadata` node so `IsUnionArmVisible` can gate it. But it means the struct's fields never surface at the top level; the rendering layer must handle them inline.

### Fix

In the final `else` clause of the control rendering block, check whether `TopicMetadata.GetComplexFields(capturedField.ValueType)` returns non-empty fields. If it does, render a collapsible expand/collapse widget backed by a recursive `DynamicForm`:

```razor
else
{
    var complexFields23 = TopicMetadata.GetComplexFields(capturedField.ValueType);
    if (complexFields23.Count > 0)
    {
        var isStructExpanded = IsExpanded(capturedField, -1);
        var structVal = capturedField.Getter(Payload!);
        <div class="dynamic-form__complex-elem" ...>
            <div class="dynamic-form__complex-header">
                <button @onclick="() => ToggleExpand(capturedField, -1)">
                    ▼/▶ StructTypeName
                </button>
            </div>
            @if (isStructExpanded && structVal != null)
            {
                <div class="dynamic-form__complex-body">
                    <DynamicForm Fields="complexFields23"
                                 Payload="structVal"
                                 OnPayloadMutated="v => SetStructArm(capturedField, v)" />
                </div>
            }
        </div>
    }
    else
    {
        <span class="dynamic-form__unsupported">@value.ToString()</span>
    }
}
```

A `SetStructArm` helper method is added to `@code` to handle value-type copy propagation:

```csharp
private void SetStructArm(FieldMetadata field, object? value)
{
    field.Setter(Payload!, value);
    OnPayloadMutated?.Invoke(Payload!);
    StateHasChanged();
}
```

**Key properties of the fix:**

- `GetComplexFields` results are cached in a `ConcurrentDictionary` inside `TopicMetadata`, so the call in the render loop is O(1) after the first invocation.
- The expand/collapse key `StructuredName[-1]` doesn't conflict with any array-index key (which are `[0]`, `[1]`, …).
- The existing `IsUnionArmVisible` visibility gating continues to work because the fix operates at the field **rendering** level, not the field **enumeration** level.

---

## Task ME2-T24 — `AddArrayElement` InvalidCastException Fix

### Root Cause

`AddArrayElement` used a simple two-branch pattern:

```csharp
if (raw is IList list && !list.IsFixedSize)
{
    list.Add(defaultElem);
    field.Setter(Payload!, list);     // ← works for existing List<T>
}
else
{
    var src = raw as Array ?? Array.CreateInstance(field.ElementType, 0);
    var newArr = Array.CreateInstance(field.ElementType, src.Length + 1);
    Array.Copy(src, newArr, src.Length);
    newArr.SetValue(defaultElem, src.Length);
    field.Setter(Payload!, newArr);   // ← CRASH when setter expects List<T>
}
```

The `else` branch is reached in two scenarios:
1. `raw` is a `T[]` (fixed-size — `IList.IsFixedSize == true` for arrays).
2. `raw` is `null` (the reference-type list field has not been initialized).

In both cases, the branch unconditionally builds a `T[]` and passes it to the setter. When the generated property is declared as `List<float>` (IDL `sequence<float>` with list binding), the setter casts the incoming value — `(List<float>)value` — and the runtime throws `InvalidCastException`.

**Concrete trigger:** `SelfTestPose.Samples` is `List<float>`. On the very first `+Add` click, `raw == null` → `Array.CreateInstance(float, 1)` → `setter((List<float>)float[1])` → crash.

### Casting Boundary Analysis

The CLR type system does not allow implicit casting between `T[]` and `List<T>`, even though both implement `IList<T>`:

| Runtime type | `is IList` | `IsFixedSize` | Safe to assign to `List<float>` setter |
|---|---|---|---|
| `null` | — | — | No (null path → array branch → crash) |
| `float[]` | ✅ | ✅ (fixed) | No |
| `List<float>` | ✅ | ❌ (mutable) | Yes |

The fix must route based on **what the setter expects** (i.e. `field.ValueType`), not merely on the runtime shape of `raw`.

### Fix

A third branch is inserted between the existing two, keyed on `field.ValueType`:

```csharp
if (raw is IList list && !list.IsFixedSize)
{
    // Existing mutable IList path (List<T> already initialized).
    list.Add(defaultElem);
    field.Setter(Payload!, list);
}
else if (field.ValueType.IsGenericType
    && typeof(IList).IsAssignableFrom(field.ValueType)
    && !field.ValueType.IsArray)
{
    // ME2-T24: Setter expects a generic List<T>/IList<T>.
    // raw may be null (uninitialized) or a legacy T[] snapshot.
    // Build a properly-typed List<T> so the cast succeeds.
    var listType = typeof(List<>).MakeGenericType(field.ElementType);
    var newList = (IList)Activator.CreateInstance(listType)!;
    if (raw is IEnumerable existing)
    {
        foreach (var item in existing)
            newList.Add(item);
    }
    newList.Add(defaultElem);
    field.Setter(Payload!, newList);
}
else
{
    // Fallback: setter expects T[].
    var src = raw as Array ?? Array.CreateInstance(field.ElementType, 0);
    var newArr = Array.CreateInstance(field.ElementType, src.Length + 1);
    Array.Copy(src, newArr, src.Length);
    newArr.SetValue(defaultElem, src.Length);
    field.Setter(Payload!, newArr);
}
```

**Why the condition `field.ValueType.IsGenericType && typeof(IList).IsAssignableFrom(field.ValueType) && !field.ValueType.IsArray` is correct:**

- `IsGenericType` — filters out plain arrays and non-generic types; `float[]` is not generic.
- `typeof(IList).IsAssignableFrom(...)` — confirms the type is a list-like structure.
- `!IsArray` — a belts-and-suspenders guard to rule out `T[]` (unlikely given `IsGenericType`, but explicit).

This condition matches `List<T>` and custom generic list implementors without hardcoding `typeof(List<>)`.

---

## Files Changed

| File | Change |
|---|---|
| `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor` | ME2-T23: expand struct arms via recursive DynamicForm; add `SetStructArm` helper. ME2-T24: `AddArrayElement` new List<T> branch. |
| `tests/DdsMonitor.Engine.Tests/DdsTestTypes.cs` | Added `StructArmPayload`, `StructArmUnion`, `FloatListSequenceTopic` test types. |
| `tests/DdsMonitor.Engine.Tests/ME2Batch05Tests.cs` | New file: 14 tests covering ME2-T23 and ME2-T24. |
| `docs/mon-ext-2/ME2-TASK-TRACKER.md` | Marked ME2-T23 and ME2-T24 as complete. |

---

## Test Results

```
Total:  414
Passed: 414
Failed:   0
```

All pre-existing tests continue to pass. 14 new tests added.

---

## Q1–Q5 Developer Notes

**Q1: What casting boundaries were bypassed before the fix?**  
The `T[]` → `List<T>` boundary. Both implement `IList`, but the CLR does not allow reference-cast between them. `Array.CreateInstance` always returns `T[]`, not `List<T>`.

**Q2: How does the fix determine the correct collection type without hardcoding?**  
By inspecting `field.ValueType` (the declared property type captured in `FieldMetadata`). If it is a non-array generic `IList` implementor, `typeof(List<>).MakeGenericType(field.ElementType)` constructs the exact runtime type the setter expects.

**Q3: Why is the struct arm rendering fix safe for value types?**  
The `SetStructArm` callback calls `field.Setter(Payload!, v)` which boxing-writes the mutated copy back into the parent payload. `OnPayloadMutated?.Invoke(Payload!)` then propagates the change up the Blazor component tree to parent forms, preventing the mutation from being lost on the next render cycle.

**Q4: Why is `GetComplexFields` called in the render loop instead of being precomputed?**  
It is cached by `TopicMetadata._complexFieldsCache` (a `ConcurrentDictionary`) so subsequent calls for the same type are O(1) dictionary lookups. The call only occurs for fields that have no registered drawer, are not arrays, and are not scalars — a small fraction of all rendered fields.

**Q5: What happens when the union arm struct itself contains nested unions or arrays?**  
The recursive `DynamicForm` invocation with `Fields = GetComplexFields(structArm.ValueType)` handles this transparently: the nested form uses the same rendering pipeline, including the new struct-expansion check, dynamic array widgets, and union-arm visibility gating. Full recursion depth is supported.
