# ME2 — Task Tracker

**Project:** DDS Monitor Feature Extensions (Monitoring Extensions 2)  
**Status:** In Progress  
**Last Updated:** 2026-03-19

**Reference:** See [ME2-TASK-DETAILS.md](./ME2-TASK-DETAILS.md) for detailed task descriptions.  
**Design:** See [ME2-DESIGN.md](./ME2-DESIGN.md) for architecture and rationale.

---

## Phase 1 — Bug Fixes

**Goal:** Resolve four confirmed bugs affecting workspace compatibility, subscriptions, sorting, and timestamp readability.

- [x] **ME2-T01** Workspace ComponentTypeName Compatibility → [details](./ME2-TASK-DETAILS.md#me2-t01--workspace-componenttypename-forward-compatibility)
- [x] **ME2-T02** Reset Does Not Lose Subscriptions → [details](./ME2-TASK-DETAILS.md#me2-t02--reset-does-not-lose-subscriptions)
- [x] **ME2-T03** Ordinal Sort Broken in All Samples → [details](./ME2-TASK-DETAILS.md#me2-t03--ordinal-sort-broken-in-all-samples)
- [x] **ME2-T04** Timestamp Display Formatting → [details](./ME2-TASK-DETAILS.md#me2-t04--timestamp-display-formatting)

---

## Phase 2 — Detail Panel Value Rendering

**Goal:** Fix null value invisibility; add value-type syntax highlighting; fix union rendering in Table and Tree tabs.

- [x] **ME2-T05** Null String Visibility + Value Type Syntax Highlighting → [details](./ME2-TASK-DETAILS.md#me2-t05--null-string-visibility--value-type-syntax-highlighting)
- [x] **ME2-T06** Union Rendering Improvements → [details](./ME2-TASK-DETAILS.md#me2-t06--union-rendering-improvements)

---

## Phase 3 — CodeGen Quick Fix

**Goal:** Improve build log clarity for multi-project solutions.

- [x] **ME2-T07** Schema Compiler Project Name in Build Log → [details](./ME2-TASK-DETAILS.md#me2-t07--schema-compiler-project-name-in-build-log)

---

## Phase 4 — Filter & Column System

**Goal:** Expose non-payload metadata fields (Topic, InstanceState) to the filter and column picker; add quick-filter context menu; decouple hardcoded columns.

- [x] **ME2-T08** Expose Non-Payload Fields to Filter and Column Picker → [details](./ME2-TASK-DETAILS.md#me2-t08--expose-non-payload-fields-to-filter-and-column-picker)
- [x] **ME2-T09** "Filter Out Topic" Context Menu → [details](./ME2-TASK-DETAILS.md#me2-t09--filter-out-topic-context-menu)
- [x] **ME2-T10** Decouple Hardcoded Columns — Make Metadata Fields Selectable → [details](./ME2-TASK-DETAILS.md#me2-t10--decouple-hardcoded-columns--make-metadata-fields-selectable)

---

## Phase 5 — Samples Panel Track Mode

**Goal:** Make sort work in all-topics mode (superset of T03); implement performant autoscroll track mode.

- [x] **ME2-T11** Sort Fix + Autoscroll Track Mode → [details](./ME2-TASK-DETAILS.md#me2-t11--sort-fix--autoscroll-track-mode)

> Note: ME2-T11 is a superset of ME2-T03. Implementing T11 replaces the need for T03. Mark T03 done when T11 is done.

---

## Phase 6 — Topic Properties Panel

**Goal:** New non-modal topic schema inspector; right-click access from TopicExplorer and TopicSources.

- [x] **ME2-T12** New TopicPropertiesPanel Component → [details](./ME2-TASK-DETAILS.md#me2-t12--new-topicpropertiespanel-component)
- [x] **ME2-T13-A** Topic Explorer Right-Click → Topic Properties → [details](./ME2-TASK-DETAILS.md#me2-t13-a--topic-explorer-right-click--topic-properties)
- [x] **ME2-T13-B** Topic Sources Panel Improvements → [details](./ME2-TASK-DETAILS.md#me2-t13-b--topic-sources-panel-improvements)
- [x] **ME2-T26** Global Colorized Topic Names: Introduce distinct auto-hashed layout coloration mapping for mapped strings across all panels, with explicit visual controls bound locally inside Topic Properties Panel.

> Note: T13-A, T13-B, and T26 depend on T12 being complete first.

---

## Phase 7 — Folder-Based Assembly Scanning

**Goal:** Allow a folder path as a topic source; auto-scan all DLLs; display assembly path in the UI; FileDialog supports folder selection.

- [ ] **ME2-T14** Folder-Based Assembly Scanning → [details](./ME2-TASK-DETAILS.md#me2-t14--folder-based-assembly-scanning)

---

## Phase 8 — Send Sample Optional/Nullable Support

**Goal:** Allow sending literal null strings or excluding optional values completely.

- [x] **ME2-T15** Nullable/Optional Field Support in Send Sample Panel → (added by Dev Lead, see BATCH-02)

---

## Phase 9 — Replay Stability, Null Serialization, and UX Adjustments

**Goal:** Resolve BATCH-02 UI UX requests, fix dynamic null-string payloads, ensure deterministic Replay cache behavior, correct negative Time Delays.

- [x] **ME2-T20** (Tech Debt) `ApplySortToViewCache` determinism hazard for Replay Mode.
- [x] **ME2-T16** Fix dynamic string serialization so `null` remains `null`.
- [x] **ME2-T17** Samples panel filter edit box `[x]` clear button.
- [x] **ME2-T18** Samples panel column config persistence with `Topic` & `Timestamp` defaults alongside easy reset capabilities.
- [x] **ME2-T19** Delay column timing arithmetic correction to avoid negative metrics.

---

## Phase 10 — Send Sample Optional/Nullable Schema Metadata

**Goal:** Accurately reflect IDL `@optional` tags inside the Monitor to correctly enforce nullable strings against mandatory constraints, and resolve deep reflective panel performance traps.

- [x] **ME2-T22-A** (Tech Debt) `IsUnionArmVisible` traverses fields using O(N^2) LINQ every render-run; extract to a dictionary cache keyed by Discriminator.
- [x] **ME2-T22-B** (Tech Debt) `GetUnionInfo` triggers expensive MemberInfo reflection continuously on union rows; needs ConcurrentDictionary caching.
- [x] **ME2-T21** Restrict sending empty strings/null checkboxes purely to `[DdsOptional]` or `IsValueType == false` parameters using explicit field metadata binding. 

---

## Phase 11 — Send Sample Dynamic Form Array & Union Struct Fixes

**Goal:** Resolve critical nested component logic faults where deeply nested structures inside Union array lists fail to expand, and list instantiations fail via `InvalidCastException`.

- [x] **ME2-T23** Union List Item Structure Expansion Fix: Render complete hierarchical sub-forms for structures existing as active union arms inside dynamic collection instances.
- [x] **ME2-T24** `AddArrayElement` InvalidCastException Fix: Safely evaluate runtime sequence instantiation avoiding hardcoded array type conversions (e.g., dynamically projecting `T[]` vs `List<T>`) before invoking strictly-typed metadata setters.

---

## Phase 12 — Workspace Tech Debt & Extensibility

**Goal:** Secure underlying framework architecture allowing decoupled modular assembly tracking and safe workspace serialization mappings.

- [x] **ME2-T25-A** (Tech Debt) `GetPanelBaseName` Name Sanitization Fix: Standardize workspace serialization lengths to truncate safely for deeply-nested AQN dependencies.
- [x] **ME2-T25-B** (Tech Debt) `Type.GetType` Extensibility Fix: Enhance Desktop.razor type loading to scan properly decoupled external framework plugins natively instead of failing on executing-assembly scoping.

---

## Phase 13 — Startup Tech Debt (DI Framework)

**Goal:** Resolve Service Locator and Scope architecture faults blocking runtime environments.

- [ ] **ME2-T27** (Tech Debt) `TopicColorService` Scoped Dependency Injection Fix: Safe encapsulation matching Blazor scoped lifetimes (`IWorkspaceState`) against incorrectly declared Singletons blocking App startup. 

---
