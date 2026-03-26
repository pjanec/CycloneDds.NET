# PLA1-BATCH-03: Test debt + Detail View registry (Phase 3)

**Batch Number:** PLA1-BATCH-03  
**Tasks:** PLA1-DEBT-004, PLA1-DEBT-005 (corrective), PLA1-P3-T01, PLA1-P3-T02, PLA1-P3-T03, PLA1-P3-T04  
**Phase:** PLA1 Phase 3 — Detail view hijacking  
**Estimated Effort:** 14–20 hours  
**Priority:** HIGH  
**Dependencies:** PLA1-BATCH-02 (approved)

---

## 📋 Onboarding & Workflow

### Developer instructions

Clear small **test debts** first so quality bars keep rising, then implement **`ISampleViewRegistry`** and integrate **`DetailPanel.razor`** per task detail and design §6.

### Required reading (in order)

1. **Workflow:** `.dev-workstream/guides/DEV-GUIDE.md`
2. **Onboarding:** `docs/plugin-api/PLA1-ONBOARDING.md`
3. **Previous review:** `.dev-workstream/reviews/PLA1-BATCH-02-REVIEW.md`
4. **Debt:** `docs/plugin-api/PLA1-DEBT-TRACKER.md` — PLA1-DEBT-004, PLA1-DEBT-005
5. **Design:** `docs/plugin-api/PLA1-DESIGN.md` — [§6 Phase 3](../../docs/plugin-api/PLA1-DESIGN.md#6-phase-3--detail-view-hijacking-isampleviewregistry)
6. **Task specs:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — Phase 3 tasks
7. **Standards:** `.dev-workstream/guides/CODE-STANDARDS.md`

### Source code locations

| Work | Path |
|------|------|
| Detail panel | `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor` |
| New registry types | `tools/DdsMonitor/DdsMonitor.Engine/Plugins/` |
| Host DI | `tools/DdsMonitor/DdsMonitor.Engine/Hosting/ServiceCollectionExtensions.cs` |
| Engine tests | `tests/DdsMonitor.Engine.Tests/` |
| Context menu panels (reference) | `TopicExplorerPanel.razor`, `SamplesPanel.razor`, `InstancesPanel.razor` |

### Builds and tests

```powershell
dotnet build tools/DdsMonitor/DdsMonitor.Engine/DdsMonitor.Engine.csproj
dotnet build tools/DdsMonitor/DdsMonitor.Blazor/DdsMonitor.csproj
dotnet test tests/DdsMonitor.Engine.Tests/
```

### Report / questions

- **Report:** `.dev-workstream/reports/PLA1-BATCH-03-REPORT.md`
- **Questions:** `.dev-workstream/questions/PLA1-BATCH-03-QUESTIONS.md`

### Anti-laziness

Run full **Engine.Tests** after changes. For `DetailPanel`, verify both **custom viewer** and **default tree** paths manually or via tests as you implement.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1. **PLA1-DEBT-005** → quick test strengthen → all Engine.Tests pass ✅  
2. **PLA1-DEBT-004** → new regression coverage → Engine.Tests (and any new test project entries) pass ✅  
3. **PLA1-P3-T01** → interface compiles ✅  
4. **PLA1-P3-T02** → implementation + **SampleViewRegistryTests** per task detail → all pass ✅  
5. **PLA1-P3-T03** → DI registration ✅  
6. **PLA1-P3-T04** → `DetailPanel` integration → build + tests pass ✅  

Extend P3-T02 tests if needed so **type hierarchy** resolution (exact → base types → interfaces) from the task **Scope** is actually verified—not only the three table rows if hierarchy logic is implemented.

---

## ✅ Tasks

### Task 0 — PLA1-DEBT-005: Strengthen `IMonitorContextTests`

**File:** `tests/DdsMonitor.Engine.Tests/Plugins/IMonitorContextTests.cs`  

In `GetFeature_ReturnsRegisteredService`, assert the resolved instance is **the same** as the `MenuRegistry` instance registered (e.g. `Assert.Same(menuRegistry, result)`).

---

### Task 1 — PLA1-DEBT-004: Regression tests for context menu composition

**Goal:** Automated guard for “defaults first, then optional separator, then plugin items” for at least **one** of the three panels’ composition patterns.

**Acceptable approaches (pick one):**

- **A:** Small **pure** helper (e.g. static method in Engine or Blazor) that takes default items + `IContextMenuRegistry` + context value and returns the final list; unit test with a fake registry / real registry.  
- **B:** **bUnit** test project / test if the repo already has Blazor component tests; mount a minimal cut of one panel with mocks.  
- **C:** If (B) is too heavy, document in the report why (A) was chosen and ensure tests would fail if plugin items were prepended or separator appears when count is 0.

Update **`docs/plugin-api/PLA1-DEBT-TRACKER.md`**: mark PLA1-DEBT-004 ✅ when done.

---

### Task 2 — PLA1-P3-T01: `ISampleViewRegistry`

**Spec:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — PLA1-P3-T01  

**File:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/ISampleViewRegistry.cs` (NEW)

---

### Task 3 — PLA1-P3-T02: `SampleViewRegistry`

**Spec:** `docs/plugin-api/PLA1-TASK-DETAIL.md` — PLA1-P3-T02  

**Files:** `tools/DdsMonitor/DdsMonitor.Engine/Plugins/SampleViewRegistry.cs` (NEW), `tests/DdsMonitor.Engine.Tests/Plugins/SampleViewRegistryTests.cs` (NEW)

Implement hierarchy walk per **Scope**; add tests that prove **base** or **interface** placement resolves when no exact match (if not in the minimal table, add tests anyway—the **Scope** requires it).

---

### Task 4 — PLA1-P3-T03: Register in DI

**Spec:** PLA1-P3-T03 — singleton `ISampleViewRegistry` / `SampleViewRegistry` in `ServiceCollectionExtensions.cs` (or consistent host file).

---

### Task 5 — PLA1-P3-T04: `DetailPanel.razor`

**Spec:** PLA1-P3-T04 — inject registry; first tab uses `GetViewer` for `currentSample.TopicMetadata.TopicType`; fall back to existing tree. JSON and sample-info tabs unchanged.

---

## 🎯 Success criteria

- [ ] PLA1-DEBT-004, PLA1-DEBT-005 resolved in debt tracker
- [ ] PLA1-P3-T01–T04 meet success criteria in `PLA1-TASK-DETAIL.md`
- [ ] `dotnet test tests/DdsMonitor.Engine.Tests/` passes
- [ ] Blazor project builds
- [ ] Report submitted

---

## 📚 References

- `docs/plugin-api/PLA1-TASK-TRACKER.md`
- `.dev-workstream/reviews/PLA1-BATCH-02-REVIEW.md`
