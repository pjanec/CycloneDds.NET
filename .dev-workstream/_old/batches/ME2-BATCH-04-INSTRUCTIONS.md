# ME2-BATCH-04: Tech Debt Burn-Down & Schema Optional Metadata

**Batch Number:** ME2-BATCH-04  
**Tasks:** ME2-T22-A (Tech Debt), ME2-T22-B (Tech Debt), ME2-T21  
**Phase:** Phase 10 (Send Sample Optional/Nullable Schema Metadata)
**Estimated Effort:** 8-10 hours  
**Priority:** HIGH  
**Dependencies:** ME2-BATCH-03  

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! Per instructions, this batch prioritizes resolving technical debt before tackling feature tasks so we don't accumulate inefficiencies. Afterward, we apply a crucial exact-schema-based rule to the nullable checkbox logic.

> **IMPORTANT ANNOUNCEMENT (AI Coding Agent Note):** 
> As an AI coding agent, you have access to the Playwright MCP server to control the browser natively. **You MUST NOT ask the user for manual UI testing.** Open the browser using your tools, run the web application, interact with the UI, and verify your changes directly. You must finish the whole batch autonomously until all functionality is perfectly working.

**Important Rule:** Finish the batch without stopping. Do not ask for permission to do obvious things like running tests or fixing root causes until everything works. Laziness is not allowed. 

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-LEAD-GUIDE.md`
2. **Task Tracker:** `docs/mon-ext-2/ME2-TASK-TRACKER.md` (Review Phase 10 goals).
3. **ME2-BATCH-01 Report:** `.dev-workstream/reports/ME2-BATCH-01-REPORT.md` - Please read Q5 regarding the reflection/LINQ impacts on `detailPanel` rendering to resolve T22-A and T22-B.

### Source Code Location
- **Main Toolset Application:** `tools/DdsMonitor/` (Blazor UI and Engine)

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME2-BATCH-04-REPORT.md`

---

## 🎯 Batch Objectives
- **Secure Code Foundations:** Strip away O(N^2) LINQ loops and exhaustive reflective sweeps impacting struct table rendering per-cycle. 
- **Exact DDS Constraints:** Rely on IDL strict schema rules tracking `[DdsOptional]` annotations on data members to validate null/checkbox inclusion over strings, blocking un-annotated reference types from inadvertently sending zero representations in strictly-declared struct fields.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
3. ...

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

---

## ✅ Tasks

### Task 1: (Tech Debt) Fix `IsUnionArmVisible` LINQ traversal performance (ME2-T22-A)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`
**Requirements:**
- Eliminate the O(N^2) lookup happening via `FirstOrDefault` running inside the per-field loop of `IsUnionArmVisible`. 
- Since the table layout is dependent on discriminant maps, extract these paths to a precomputed `Dictionary<string, FieldMetadata>` cache inside the engine or component so active verification executes in O(1). 

### Task 2: (Tech Debt) Fix `GetUnionInfo` Reflection cycle traps (ME2-T22-B)
**Files:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`
**Requirements:**
- Stop using heavy `GetCustomAttribute` reflection natively per rendering tree.
- Map and retrieve `UnionMeta` logic using a robust `ConcurrentDictionary<Type, ...>` layer preventing continuous CPU overhead.

### Task 3: Restrict UI Checkboxes by IDL `@optional` tags (ME2-T21)

Because the DDS is not able to send null string (null on send is received as empty string on receive) it makes no sense to use 'non-null' checkbox for dynamic string in the 'send sample' panel. The edit field for the dynamic field should be always present (unless the string itself is annotated as @optional in the IDL - then the "non-null" checkbox should be present as for any other @optional field, including structures).

**Files:** `FieldMetadata.cs`, `TopicMetadata.cs`, `DynamicForm.razor`
**Description:** DdsMonitor now can't recognize `@optional` fields for strings, blinding standard reference types to send checkboxes.
**Requirements:**

**A. Update FieldMetadata.cs (`tools/DdsMonitor/DdsMonitor.Engine/Metadata/FieldMetadata.cs`)**
Add `IsOptional` boolean parameter:
```csharp
    public FieldMetadata(
        // ... previous variables ...
        bool isDiscriminatorField = false,
        bool isOptional = false) // NEW
    {
        // ... assignments ...
        IsDiscriminatorField = isDiscriminatorField;
        IsOptional = isOptional; // NEW
    }

    /// <summary>
    /// Gets a value indicating whether this field is explicitly marked as optional.
    /// </summary>
    public bool IsOptional { get; }
```

**B. Update TopicMetadata.cs (`tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`)**
Inject exact mapping inside `AppendFields`:
```csharp
            // Determine if the field is optional
            bool isOptional = Nullable.GetUnderlyingType(memberType) != null || 
                              member.GetCustomAttribute<DdsOptionalAttribute>() != null;

            var fieldMetadata = new FieldMetadata(
               // ... previous parameters ...
               isDiscriminatorField: isDiscriminatorField,
               isOptional: isOptional); // Pass it here
```

**C. Update DynamicForm.razor (`tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`)**
Extract exact schema knowledge inside row creation. Remove the naive `CanBeNull(Type type)` helper entirely from `@code`. Add the precise variable declaration:
```csharp
            var currentValue = capturedField.Getter(Payload!);
            
            // Only show the checkbox if the field is explicitly optional
            var canBeNull = capturedField.IsOptional && !capturedField.IsFixedSizeArray;
            var isNull = canBeNull && currentValue == null;

            <div class="dynamic-form__row @(isNested ? "dynamic-form__row--nested" : string.Empty)">
```

---

### Task 4: Editing union list in dynamic form

the send panel now does not support editing individual items of a list of unions. The union item is displayed just as the data type name with full namespace like the following


[0] MyNamepsace.MyUnion
[1] MyNamepsace.MyUnion
[2] MyNamepsace.MyUnion


I need to be able to edit the items - it needs to be shown as expandable item where the internal fields are normally editable and using the dynamic hiding of inactive union arms as already used elsewhere.

To implement this, we need to allow the `DynamicForm.razor` component to recursively render nested sub-forms for complex types inside arrays/lists. 

Currently, `DynamicForm` only supports root-level `TopicMetadata`, and standard drawers only support scalar fields. By extracting the flattened field generation logic into a generic method for any struct/union type, we can recursively spawn `<DynamicForm>` instances for array items and bind their mutations back into the parent array.

Here are the step-by-step changes (take as inspiration, might be outdated and no longer matching existing code)

#### 1. Add `GetComplexFields` to `TopicMetadata.cs`
We will expose a new static method that runs the existing field extraction logic on arbitrary struct/union types (caching the results so rendering large lists is fast).

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`
Add the following right at the top of the `TopicMetadata` class (around line 18):
```csharp
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<FieldMetadata>> _complexFieldsCache = new();

    /// <summary>
    /// Computes and caches flattened FieldMetadata for any struct/union type (not just root topics).
    /// Used by DynamicForm to dynamically render nested complex types in arrays.
    /// </summary>
    public static IReadOnlyList<FieldMetadata> GetComplexFields(Type type)
    {
        return _complexFieldsCache.GetOrAdd(type, t =>
        {
            var allFields = new List<FieldMetadata>();
            var keyFields = new List<FieldMetadata>();
            var visited = new HashSet<Type>();
            AppendFields(t, t, new List<MemberInfo>(), string.Empty, allFields, keyFields, visited);
            return allFields;
        });
    }
```

#### 2. Update `DynamicForm.razor` to Support Nested Rendering
Update `DynamicForm.razor` to accept a raw `Fields` list for nested elements, implement expand/collapse state for list items, and bubble payload mutations back to the parent so value-type elements correctly update their arrays.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/Components/DynamicForm.razor`
Replace the **entire file** with this updated content:

```html
@using System.Collections.Generic
@using System.Collections
@using System.Linq
@using DdsMonitor.Engine.Ui
@using DdsMonitor.Engine

@inject ITypeDrawerRegistry DrawerRegistry

@if (ActiveFields.Count == 0 || Payload == null)
{
    <div class="dynamic-form dynamic-form--empty">
        <span class="dynamic-form__placeholder">No fields available.</span>
    </div>
}
else
{
    <div class="dynamic-form">
        @{
            string? currentGroup = null;
        }
        @foreach (var field in EditableFields)
        {
            var groupName = GetGroupName(field.StructuredName);
            var isNested = groupName != null;

            @if (groupName != currentGroup)
            {
                if (currentGroup != null)
                {
                    <div class="dynamic-form__group-end"></div>
                }
                currentGroup = groupName;
                if (groupName != null)
                {
                    <div class="dynamic-form__group-header">@groupName</div>
                }
            }

            var capturedField = field;
            var drawer = DrawerRegistry.GetDrawer(field.ValueType);
            
            var currentValue = capturedField.Getter(Payload!);
            
            // Only show the checkbox if the field is explicitly optional
            var canBeNull = capturedField.IsOptional && !capturedField.IsFixedSizeArray;
            var isNull = canBeNull && currentValue == null;

            <div class="dynamic-form__row @(isNested ? "dynamic-form__row--nested" : string.Empty)">
                <label class="dynamic-form__label" title="@capturedField.StructuredName">
                    @capturedField.DisplayName
                </label>
                
                @if (canBeNull)
                {
                    <input type="checkbox"
                           class="dynamic-form__checkbox"
                           title="Include this optional/nullable field"
                           checked="@(!isNull)"
                           @onchange="e => ToggleNull(capturedField, e.Value)"
                           style="margin-right: 8px; flex-shrink: 0;" />
                }

                <div class="dynamic-form__control" style="@(isNull ? "opacity: 0.4; pointer-events: none;" : string.Empty)">
                    @if (drawer != null)
                    {
                        // Scalar field with a registered drawable.
                        var fieldKey = capturedField.StructuredName;
                        var ctx = new DrawerContext(
                            capturedField.DisplayName,
                            capturedField.ValueType,
                            () => capturedField.Getter(Payload!),
                            value =>
                            {
                                capturedField.Setter(Payload!, value);
                                OnPayloadMutated?.Invoke(Payload!);
                                StateHasChanged();
                            },
                            this,
                            onValidationError: err =>
                            {
                                if (err == null)
                                    _fieldErrors.Remove(fieldKey);
                                else
                                    _fieldErrors[fieldKey] = err;
                                StateHasChanged();
                            });
                        @drawer(ctx)
                        @if (_fieldErrors.TryGetValue(fieldKey, out var fieldErr) && fieldErr != null)
                        {
                            <span class="dynamic-form__error-icon" title="@fieldErr">
                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>
                            </span>
                        }
                    }
                    else if (capturedField.IsFixedSizeArray && capturedField.ElementType != null)
                    {
                        var elemDrawer = DrawerRegistry.GetDrawer(capturedField.ElementType);
                        <div class="dynamic-form__array-fixed">
                            @for (int idx = 0; idx < capturedField.FixedArrayLength; idx++)
                            {
                                var capturedIdx = idx;
                                var arr = capturedField.Getter(Payload!) as Array;
                                var elemVal = arr != null && capturedIdx < arr.Length ? arr.GetValue(capturedIdx) : null;
                                
                                <div class="dynamic-form__array-elem">
                                    <span class="dynamic-form__array-idx">[<b>@capturedIdx</b>]</span>
                                    @if (elemDrawer != null)
                                    {
                                        var elemCtx = new DrawerContext(
                                            $"[{capturedIdx}]",
                                            capturedField.ElementType,
                                            () => elemVal,
                                            v => SetArrayElement(capturedField, capturedIdx, v),
                                            this);
                                        @elemDrawer(elemCtx)
                                    }
                                    else
                                    {
                                        var isElemExpanded = IsExpanded(capturedField, capturedIdx);
                                        <div class="dynamic-form__complex-elem" style="flex:1; min-width:0;">
                                            <div class="dynamic-form__complex-header">
                                                <button type="button" class="dynamic-form__complex-toggle" @onclick="() => ToggleExpand(capturedField, capturedIdx)">
                                                    @(isElemExpanded ? "\u25bc" : "\u25ba") @(elemVal?.GetType().Name ?? capturedField.ElementType.Name)
                                                </button>
                                            </div>
                                            @if (isElemExpanded && elemVal != null)
                                            {
                                                <div class="dynamic-form__complex-body">
                                                    <DynamicForm Fields="TopicMetadata.GetComplexFields(capturedField.ElementType)"
                                                                 Payload="elemVal"
                                                                 OnPayloadMutated="v => SetArrayElement(capturedField, capturedIdx, v)" />
                                                </div>
                                            }
                                        </div>
                                    }
                                </div>
                            }
                        </div>
                    }
                    else if (capturedField.IsArrayField && capturedField.ElementType != null)
                    {
                        var elemDrawer = DrawerRegistry.GetDrawer(capturedField.ElementType);
                        var rawValue = capturedField.Getter(Payload!);
                        var currentArr = ToObjectArray(rawValue);

                        <div class="dynamic-form__array-dynamic">
                            @for (int idx = 0; idx < currentArr.Count; idx++)
                            {
                                var capturedIdx = idx;
                                var elemVal = currentArr[capturedIdx];
                                <div class="dynamic-form__array-elem">
                                    <span class="dynamic-form__array-idx">[<b>@capturedIdx</b>]</span>
                                    @if (elemDrawer != null)
                                    {
                                        var elemCtx = new DrawerContext(
                                            $"[{capturedIdx}]",
                                            capturedField.ElementType,
                                            () => elemVal,
                                            v => SetArrayElement(capturedField, capturedIdx, v),
                                            this);
                                        @elemDrawer(elemCtx)
                                        <button type="button" class="dynamic-form__array-remove" title="Remove element" @onclick="() => RemoveArrayElement(capturedField, capturedIdx)">×</button>
                                    }
                                    else
                                    {
                                        var isElemExpanded = IsExpanded(capturedField, capturedIdx);
                                        <div class="dynamic-form__complex-elem" style="flex:1; min-width:0;">
                                            <div class="dynamic-form__complex-header">
                                                <button type="button" class="dynamic-form__complex-toggle" @onclick="() => ToggleExpand(capturedField, capturedIdx)">
                                                    @(isElemExpanded ? "\u25bc" : "\u25ba") @(elemVal?.GetType().Name ?? capturedField.ElementType.Name)
                                                </button>
                                                <button type="button" class="dynamic-form__array-remove" title="Remove element" @onclick="() => RemoveArrayElement(capturedField, capturedIdx)">×</button>
                                            </div>
                                            @if (isElemExpanded && elemVal != null)
                                            {
                                                <div class="dynamic-form__complex-body">
                                                    <DynamicForm Fields="TopicMetadata.GetComplexFields(capturedField.ElementType)"
                                                                 Payload="elemVal"
                                                                 OnPayloadMutated="v => SetArrayElement(capturedField, capturedIdx, v)" />
                                                </div>
                                            }
                                        </div>
                                    }
                                </div>
                            }
                            <div class="dynamic-form__array-add-row">
                                <button type="button" class="dynamic-form__array-add" title="Add element" @onclick="() => AddArrayElement(capturedField)">
                                    + Add
                                </button>
                            </div>
                        </div>
                    }
                    else if (field.ValueType.IsArray)
                    {
                        // Fallback: array type without metadata – read-only count.
                        <span class="dynamic-form__unsupported" title="Array editing not supported for this type">
                            [@((capturedField.Getter(Payload!) as Array)?.Length ?? 0) items]
                        </span>
                    }
                    else
                    {
                        <span class="dynamic-form__unsupported">
                            @(capturedField.Getter(Payload!)?.ToString() ?? "(null)")
                        </span>
                    }
                </div>
            </div>
        }
        @if (currentGroup != null)
        {
            <div class="dynamic-form__group-end"></div>
        }
    </div>
}

@code {
    [Parameter]
    public TopicMetadata? Meta { get; set; }

    [Parameter]
    public IReadOnlyList<FieldMetadata>? Fields { get; set; }

    [Parameter]
    public object? Payload { get; set; }
    
    [Parameter]
    public Action<object>? OnPayloadMutated { get; set; }

    private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expandedElements = new();
    
    private TopicMetadata? _lastMeta;
    private IReadOnlyList<FieldMetadata>? _lastFields;

    public bool HasValidationErrors => _fieldErrors.Count > 0;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastMeta, Meta) || !ReferenceEquals(_lastFields, Fields))
        {
            _lastMeta = Meta;
            _lastFields = Fields;
            _fieldErrors.Clear();
            _expandedElements.Clear();
        }
    }
    
    private IReadOnlyList<FieldMetadata> ActiveFields =>
        Meta?.AllFields ?? Fields ?? Array.Empty<FieldMetadata>();

    private IEnumerable<FieldMetadata> EditableFields =>
        ActiveFields.Where(f => !f.IsSynthetic && IsUnionArmVisible(f));

    private bool IsUnionArmVisible(FieldMetadata field)
    {
        if (field.IsDiscriminatorField) return true;
        if (field.DependentDiscriminatorPath == null) return true;
        if (Payload == null) return true;

        var discField = ActiveFields.FirstOrDefault(
            f => string.Equals(f.StructuredName, field.DependentDiscriminatorPath, StringComparison.Ordinal));
        if (discField == null) return true;

        var currentDisc = discField.Getter(Payload);

        if (field.IsDefaultUnionCase)
        {
            return !ActiveFields.Any(f =>
                string.Equals(f.DependentDiscriminatorPath, field.DependentDiscriminatorPath, StringComparison.Ordinal) &&
                !f.IsDefaultUnionCase &&
                f.ActiveWhenDiscriminatorValue != null &&
                UnionValuesEqual(f.ActiveWhenDiscriminatorValue, currentDisc));
        }

        return field.ActiveWhenDiscriminatorValue != null &&
               UnionValuesEqual(field.ActiveWhenDiscriminatorValue, currentDisc);
    }

    private static bool UnionValuesEqual(object a, object? b)
    {
        if (b == null) return false;
        if (a.Equals(b)) return true;
        try { return Convert.ToInt64(a) == Convert.ToInt64(b); }
        catch { return false; }
    }

    private static string? GetGroupName(string structuredName)
    {
        var dot = structuredName.LastIndexOf('.');
        return dot < 0 ? null : structuredName[..dot];
    }
    
    private bool IsExpanded(FieldMetadata field, int index)
    {
        return _expandedElements.Contains($"{field.StructuredName}[{index}]");
    }

    private void ToggleExpand(FieldMetadata field, int index)
    {
        var key = $"{field.StructuredName}[{index}]";
        if (!_expandedElements.Remove(key))
            _expandedElements.Add(key);
    }

    private void ToggleNull(FieldMetadata field, object? isCheckedObj)
    {
        if (Payload == null) return;

        bool isChecked = isCheckedObj is bool b && b;
        if (!isChecked)
        {
            field.Setter(Payload, null);
            _fieldErrors.Remove(field.StructuredName);
        }
        else
        {
            var targetType = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
            object? defaultValue = null;

            if (targetType == typeof(string))
            {
                defaultValue = string.Empty;
            }
            else if (targetType.IsArray)
            {
                defaultValue = Array.CreateInstance(targetType.GetElementType()!, 0);
            }
            else
            {
                try { defaultValue = Activator.CreateInstance(targetType); } catch { }
            }

            field.Setter(Payload, defaultValue);
        }
        
        OnPayloadMutated?.Invoke(Payload!);
        StateHasChanged();
    }

    private void SetArrayElement(FieldMetadata field, int index, object? value)
    {
        var raw = field.Getter(Payload!);

        if (raw is IList list && !list.IsFixedSize)
        {
            if (index < list.Count)
            {
                list[index] = value;
                field.Setter(Payload!, list);
            }
        }
        else if (raw is Array arr)
        {
            if (index < arr.Length)
            {
                arr.SetValue(value, index);
                field.Setter(Payload!, arr);
            }
        }
        
        OnPayloadMutated?.Invoke(Payload!);
        StateHasChanged();
    }

    private void AddArrayElement(FieldMetadata field)
    {
        if (field.ElementType == null) return;

        var raw = field.Getter(Payload!);
        object? defaultElem = null;
        
        if (field.ElementType == typeof(string))
        {
            defaultElem = string.Empty;
        }
        else if (field.ElementType.IsArray)
        {
            defaultElem = Array.CreateInstance(field.ElementType.GetElementType()!, 0);
        }
        else
        {
            try { defaultElem = Activator.CreateInstance(field.ElementType); } catch {}
        }

        if (raw is IList list && !list.IsFixedSize)
        {
            list.Add(defaultElem);
            field.Setter(Payload!, list);
        }
        else
        {
            var src = raw as Array ?? Array.CreateInstance(field.ElementType, 0);
            var newArr = Array.CreateInstance(field.ElementType, src.Length + 1);
            Array.Copy(src, newArr, src.Length);
            newArr.SetValue(defaultElem, src.Length);
            field.Setter(Payload!, newArr);
        }
        
        OnPayloadMutated?.Invoke(Payload!);
        StateHasChanged();
    }

    private void RemoveArrayElement(FieldMetadata field, int index)
    {
        if (field.ElementType == null) return;

        var raw = field.Getter(Payload!);

        if (raw is IList list && !list.IsFixedSize)
        {
            if (index < list.Count)
            {
                list.RemoveAt(index);
                field.Setter(Payload!, list);
            }
        }
        else if (raw is Array src)
        {
            if (src.Length == 0) return;

            var newArr = Array.CreateInstance(field.ElementType, src.Length - 1);
            Array.Copy(src, 0, newArr, 0, index);
            Array.Copy(src, index + 1, newArr, index, src.Length - index - 1);
            field.Setter(Payload!, newArr);
        }

        OnPayloadMutated?.Invoke(Payload!);
        StateHasChanged();
    }

    private static IList<object?> ToObjectArray(object? raw)
    {
        if (raw is Array arr)
        {
            var result = new List<object?>(arr.Length);
            for (int i = 0; i < arr.Length; i++) result.Add(arr.GetValue(i));
            return result;
        }

        if (raw is IEnumerable<object?> seq) return seq.ToList();
        if (raw is System.Collections.IEnumerable enumerable)
        {
            var result = new List<object?>();
            foreach (var item in enumerable) result.Add(item);
            return result;
        }

        return Array.Empty<object?>();
    }

    private static string RenderArrayAsText(Array? arr, int maxLen)
    {
        if (arr == null) return "(null)";
        var count = Math.Min(arr.Length, maxLen > 0 ? maxLen : arr.Length);
        var parts = new string[count];
        for (int i = 0; i < count; i++) parts[i] = arr.GetValue(i)?.ToString() ?? "0";
        return string.Join(", ", parts);
    }
}
```

### 3. Add Component CSS Styles
Update your app stylesheet to seamlessly render the nested dropdown headers and recursive sub-forms.

**File:** `tools/DdsMonitor/DdsMonitor.Blazor/wwwroot/app.css`
Add this to the bottom of the file (or near the other dynamic-form styles):
```css
/* ─── Complex nested form element (for struct/union in arrays) ───────────────── */
.dynamic-form__complex-elem {
    display: flex;
    flex-direction: column;
    gap: 2px;
    width: 100%;
}

.dynamic-form__complex-header {
    display: flex;
    align-items: center;
    gap: 8px;
}

.dynamic-form__complex-toggle {
    background: transparent;
    border: none;
    color: var(--panel-text);
    cursor: pointer;
    font-size: 12px;
    padding: 4px 0;
    font-family: var(--font-display);
    text-align: left;
    flex: 1 1 auto;
}

.dynamic-form__complex-toggle:hover {
    color: var(--accent-2);
}

.dynamic-form__complex-body {
    margin-left: 20px;
    padding-left: 10px;
    border-left: 1px dashed var(--panel-border);
    margin-top: 4px;
    margin-bottom: 8px;
}
```

## 🧪 Testing Requirements
- Confirm the fast UI rendering speeds up visibly for complex struct unions loaded through the topic.
- Assert validation over `TopicMetadata` accurately reflects the new `IsOptional` flag for primitive arrays.

## 📊 Report Requirements

Provide professional developer insights matching Q1-Q5 guidelines. Expose exactly how the reflection caches resolved structural rendering bugs or slowdowns over hierarchical depth processing. List exactly which elements were correctly detected via `DdsOptional` attributes.

---

## 🎯 Success Criteria
- [ ] Task ME2-T22-A O(N^2) evaluation eliminated from view structure updates.
- [ ] Task ME2-T22-B Reflection maps reliably extract using ConcurrentDictionary instances.
- [ ] Task ME2-T21 `IsValueType` logic natively wiped in favor of the strict `IsOptional` data contract from TopicMetadata maps.
- [ ] UI checkboxes apply only to `@optional` matching entities, while typical standard strings are permanently enabled inside `SendSamplePanel`.
- [ ] 100% test coverage passed clean.
