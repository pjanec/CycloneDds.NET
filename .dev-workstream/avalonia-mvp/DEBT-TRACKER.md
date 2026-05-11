# Technical Debt Tracker — DdsMonitor.Avalonia V1

**Project:** `avalonia-mvp`  
**Maintained by:** Dev Lead

> **Rules:**
> - P1 items → Corrective Task 0 in next batch (never enter this tracker)
> - P2/P3 items → added here with source batch, description, target batch
> - When resolved → mark ✅ (do not delete rows)

---

## Open Items

| ID | Priority | Source Batch | Description | Target Batch | Status |
|----|----------|-------------|-------------|--------------|--------|
| DT-001 | P2 | BATCH-01 | `TypeDrawerRegistry` primitive stubs return `null` silently. Callers outside the Blazor/Avalonia adapter stacks may get null without a clear error. Should document convention in the interface or add a sentinel/guard that throws if no adapter has registered real factories. | BATCH-03 | Open |
| DT-002 | P2 | BATCH-01 | `IWindowManager.RegisterPanelType` parameter named `blazorComponentType` — Blazor-specific name leaks into the general Engine interface. Should rename to `viewModelType` or `componentType`. | BATCH-03 | ✅ Resolved |
| DT-003 | P2 | BATCH-02 | `TopicExplorerViewModel_ShowHidden_False_FiltersHiddenTopics` test does not cover an actual hidden topic (`_`-prefixed). No CLR type with `_` prefix available. Add `_HiddenSample` sentinel type to test project to test the `IsHidden` predicate properly. | BATCH-03 | ✅ Resolved |

---

## Resolved Items

| ID | Priority | Source Batch | Description | Resolved In |
|----|----------|-------------|-------------|-------------|
| DT-DT-P1-01 | P1 | BATCH-01 | `AvaloniaWindowManager.OnWindowClosed` never called `IDisposable.Dispose()` on closed panel ViewModels — VMs rooted in `IEventBroker` subscriptions indefinitely. | BATCH-02 (CORRECTIVE-0) |
| DT-004 | P2 | BATCH-03 | `SamplesViewerView.axaml` has no `DataTemplate` for sample rows — renders `.ToString()`. | BATCH-04 | ✅ Already resolved in BATCH-03 |
| DT-005 | P2 | BATCH-03 | `DetailInspectorView.axaml` field tree is flat — `Depth` not reflected in view (no indentation). | BATCH-04 | ✅ Resolved — `IndentMargin` computed property + AXAML binding |
| DT-006 | P3 | BATCH-04 | `NetworkConfigView` DomainId uses `TextBox` string binding — non-numeric input silently fails. | BATCH-05 | Open |
| DT-007 | P2 | BATCH-04 | `NetworkConfigViewModel.Apply()` accumulates DDS participants on repeated clicks (no clear/diff before re-adding). | BATCH-05 | Open |
| DT-008 | P3 | BATCH-04 | `SendSampleViewModel` does not implement `IStatefulViewModel` — payload authoring state lost on panel close (deferred by spec). | Deferred | Open |
| DT-009 | P3 | BATCH-04 | `StandardDrawerRegistrar` is `public` instead of `internal`. Add `InternalsVisibleTo` to revert. | BATCH-05 | Open |
