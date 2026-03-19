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
