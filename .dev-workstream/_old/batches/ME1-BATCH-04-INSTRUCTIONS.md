# ME1-BATCH-04: Tech Debt & Cleanup (Phase 6)

**Batch Number:** ME1-BATCH-04
**Tasks:** ME1-C04, ME1-C02, ME1-C03, ME1-C05, ME1-C06, ME1-C07, ME1-C08
**Phase:** Phase 6 — Tech Debt & Cleanup
**Estimated Effort:** ~8 hours
**Priority:** HIGH
**Dependencies:** ME1-BATCH-03 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to ME1-BATCH-04! This is a cleanup batch aimed at resolving technical debt identified during the completion of the UI and Code Generator phases. Your primary focus will be enhancing the UI metadata propagation for `InlineArray` structs within Union arms, and repairing an IDL warning emitted from the `IdlEmitter`.

**CRITICAL:** Be completely autonomous. If you encounter missing configuration or errors, do **not** stop and ask for permission. Fix the root cause, write necessary tests, ensure tests pass, and proceed. Please provide a full account of your actions in the final report.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - How to work with batches
2. **Onboarding:** `docs/mon-ext-1/ME1-ONBOARDING.md`
3. **Previous Review:** `.dev-workstream/reviews/ME1-BATCH-03-REVIEW.md` (See *Identified Debt*)

### Source Code Location
- **Schema Discovery:** `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`
- **IDL Code Generation:** `tools/CycloneDDS.CodeGen/IdlEmitter.cs`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME1-BATCH-04-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (ME1-C04):** Evaluate Debt → Write tests/fixes → **ALL tests pass** ✅
2. **Task 2 (ME1-C02):** Implement → Write tests → **ALL tests pass** ✅
3. **Task 3 (ME1-C03):** Implement → Write tests → **ALL tests pass** ✅
4. **Task 4 (ME1-C05):** Implement → Write tests → **ALL tests pass** ✅
5. **Task 5 (ME1-C06):** Implement → Write tests → **ALL tests pass** ✅
6. **Task 6 (ME1-C07):** Implement → Write tests → **ALL tests pass** ✅
7. **Task 7 (ME1-C08):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until the current one is entirely complete and tests pass cleanly locally.

---

## ✅ Tasks

### Task 1: Evaluate & Resolve Remaining Backlog (ME1-C04 / Fix D01-D04)
**Files:**
- `docs/mon-ext-1/ME1-DEBT-TRACKER.md`
- Code files affected by the debt elements.

**Description:**
Review items `D01` through `D04` from the `ME1-DEBT-TRACKER.md` file since they were opened several batches ago.
Evaluate if these statements are still accurate and if they represent significant, addressable blockers to code quality. Rectify any valid structural flaws without introducing functional regressions. If an item is deemed no longer applicable or safely resolved by recent architecture evolution, close it directly in the tracker with a reasoned justification appended in your Developer Report.

**Validation Details:**
- Update `ME1-DEBT-TRACKER.md` so that `D01`, `D02`, `D03`, and `D04` are definitively evaluated and marked as Fixed/Closed.

### Task 2: InlineArray struct visibility in Union Arms (ME1-C02 / Fix D05)
**Files:**
- `tools/DdsMonitor/DdsMonitor.Engine/Metadata/TopicMetadata.cs`

**Description:**
The `TopicMetadata` schema discovery detects `[DdsUnion]` types and assigns crucial metadata (e.g. `IsDiscriminatorField`, `DependentDiscriminatorPath`, `ActiveWhenDiscriminatorValue`) to union fields, which the UI uses to hide inactive arms. However, union arms defined as `[InlineArray]` structs (like `FixedString32` or `FloatBuf8`) are currently being processed by an early fallback and miss out on this metadata augmentation. Consequently, the UI always renders `[InlineArray]` union arms completely ignorant of the active discriminator. You need to adjust the field resolution logic so that inline arrays nested inside unions receive the proper discriminator metadata context.

**Validation Details:**
- Adjust `AppendFields` or related methods so that `InlineArray` fields inherit the union metadata block correctly.
- Add failing tests to verify that an `InlineArray` union arm correctly sets `DependentDiscriminatorPath` and `ActiveWhenDiscriminatorValue`.

### Task 3: Clean up `@topic` parameter generation (ME1-C03 / Fix D06)
**Files:**
- `tools/CycloneDDS.CodeGen/IdlEmitter.cs`

**Description:**
The IDL generator produces `@topic(name="...")` for topics inheriting a custom name. This syntax throws warnings inside CycloneDDS's external `idlc` tool (`@topic::name parameter is currently ignored`). Update `IdlEmitter.cs` to emit only a plain `@topic` declaration regardless of custom topic naming parameters.

**Validation Details:**
- `idlc` compatibility: `[DdsTopic("CustomName")]` should emit as `@topic` without parenthesis and without `name=...`.
- Modify `IdlEmitter` tests checking for `@topic` parameterization. 

### Task 4: JSON Enum Serialization (ME1-C05)
**Files:**
- Relevant JSON serialization paths for `ExportService` and the `Sample Detail` panel JSON tab.

**Description:**
The JSON tab on the sample detail panel and the JSON exporter currently represent Enums as numbers. This should be adjusted so both the JSON tab and the explicit JSON export serialize Enums converting them cleanly to strings. Inversely, ensure JSON import accurately interprets string enum representations back into their values.

### Task 5: Inline Array Expansion in Sample Detail Tree Tab (ME1-C06)
**Files:**
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` (or related Tree rendering code)

**Description:**
The Tree tab on the sample detail panel is currently failing to expand Inline Arrays. Implement structural adjustments inside the sample tree renderer so that Inline Arrays expand effectively as normal arrays. 

### Task 6: Linked Sample Detail Restoration Bug (ME1-C07)
**Files:**
- Workspace state management or `DataInspector.razor`.

**Description:**
Linked Sample Detail panels currently restore unlinked on startup, which holds them trapped in an unresponsive state. Users currently have to close and reopen them. Ensure linked detail panels either restore linked and target properly, or are properly synchronized with active samples on initialization.

### Task 7: JSON Export Union Arm Removal (ME1-C08)
**Files:**
- JSON serialization mechanisms (e.g., in `ExportService`).

**Description:**
Implementation of inactive union arm removal must be tied directly into the JSON export process. The exported JSON representation of samples should exclude any union arms that are inactive, to avoid useless JSON payload generation for data-less paths.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task ME1-C04 correctly evaluates and resolves items D01-D04.
- [ ] Task ME1-C02 completed successfully and metadata applies correctly to InlineArrays inside Unions.
- [ ] Task ME1-C03 completed, and plain `@topic` headers generate.
- [ ] Task ME1-C05 completed; JSON serialization outputs enum strings both in UI and export/import.
- [ ] Task ME1-C06 completed; Inline Arrays can expand inside the tree UI.
- [ ] Task ME1-C07 completed; Linked sample views revive correctly without stranding state on startup.
- [ ] Task ME1-C08 completed; Exported JSON strips inactive union arms identically to UI visualization.
- [ ] NO test regressions or warnings.
- [ ] Build compiles via CLI and tests pass successfully.
- [ ] Developer Report submitted detailing any anomalies or discoveries.
