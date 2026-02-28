# DDS Monitor — Task Tracker

**Reference:** See [TASK-DETAIL.md](./TASK-DETAIL.md) for detailed task descriptions and success conditions.  
**Design:** See [DESIGN.md](./DESIGN.md) for the overall design.

---

## Phase 1: Foundation — Headless Data Engine

**Goal:** Build the core data engine (no UI). Topic discovery, DDS bridge, sample/instance stores, filtering, and the ingestion pipeline.

- [x] **DMON-001** — Solution & project scaffolding  &nbsp; [details](./TASK-DETAIL.md#dmon-001--create-solution--project-scaffolding)
- [x] **DMON-002** — TopicMetadata & FieldMetadata types &nbsp; [details](./TASK-DETAIL.md#dmon-002--topicmetadata--fieldmetadata-types)
- [x] **DMON-003** — TopicDiscoveryService (assembly scanning) &nbsp; [details](./TASK-DETAIL.md#dmon-003--topicdiscoveryservice-assembly-scanning)
- [x] **DMON-004** — Synthetic (computed) fields &nbsp; [details](./TASK-DETAIL.md#dmon-004--synthetic-computed-fields)
- [x] **DMON-005** — SampleData record &nbsp; [details](./TASK-DETAIL.md#dmon-005--sampledata-record)
- [x] **DMON-006** — IDynamicReader / IDynamicWriter interfaces &nbsp; [details](./TASK-DETAIL.md#dmon-006--idynamicreader--idynamicwriter-interfaces)
- [x] **DMON-007** — DynamicReader\<T\> implementation &nbsp; [details](./TASK-DETAIL.md#dmon-007--dynamicreadert-implementation)
- [x] **DMON-008** — DynamicWriter\<T\> implementation &nbsp; [details](./TASK-DETAIL.md#dmon-008--dynamicwritert-implementation)
- [ ] **DMON-009** — DdsBridge service &nbsp; [details](./TASK-DETAIL.md#dmon-009--ddsbridge-service)
- [ ] **DMON-010** — SampleStore (chronological ledger) &nbsp; [details](./TASK-DETAIL.md#dmon-010--samplestore-chronological-ledger)
- [ ] **DMON-011** — InstanceStore (keyed instance tracking) &nbsp; [details](./TASK-DETAIL.md#dmon-011--instancestore-keyed-instance-tracking)
- [ ] **DMON-012** — FilterCompiler (Dynamic LINQ) &nbsp; [details](./TASK-DETAIL.md#dmon-012--filtercompiler-dynamic-linq)
- [ ] **DMON-013** — DdsIngestionService (background worker) &nbsp; [details](./TASK-DETAIL.md#dmon-013--ddsingestionservice-background-worker)
- [ ] **DMON-014** — Application host & DI wiring &nbsp; [details](./TASK-DETAIL.md#dmon-014--application-host--di-wiring)

---

## Phase 2: Blazor Shell & Core UI

**Goal:** Build the Web Desktop shell and all primary UI panels — Topic Explorer, Data Grid, Inspector, keyboard navigation, theming, and workspace persistence.

- [ ] **DMON-015** — EventBroker (pub/sub) &nbsp; [details](./TASK-DETAIL.md#dmon-015--eventbroker-pubsub)
- [ ] **DMON-016** — PanelState model & IWindowManager &nbsp; [details](./TASK-DETAIL.md#dmon-016--panelstate-model--iwindowmanager-interface)
- [ ] **DMON-017** — Desktop.razor shell & panel chrome &nbsp; [details](./TASK-DETAIL.md#dmon-017--desktoprazor-shell--panel-chrome)
- [ ] **DMON-018** — Topic Explorer panel &nbsp; [details](./TASK-DETAIL.md#dmon-018--topic-explorer-panel)
- [ ] **DMON-019** — Topic Picker (reusable component) &nbsp; [details](./TASK-DETAIL.md#dmon-019--topic-picker-reusable-component)
- [ ] **DMON-020** — Column Picker dialog &nbsp; [details](./TASK-DETAIL.md#dmon-020--column-picker-dialog)
- [ ] **DMON-021** — Samples Panel (virtualized data grid) &nbsp; [details](./TASK-DETAIL.md#dmon-021--samples-panel-virtualized-data-grid)
- [ ] **DMON-022** — Sample Detail Panel (inspector) &nbsp; [details](./TASK-DETAIL.md#dmon-022--sample-detail-panel-inspector)
- [ ] **DMON-023** — Hover JSON tooltip &nbsp; [details](./TASK-DETAIL.md#dmon-023--hover-json-tooltip)
- [ ] **DMON-024** — Text View Panel &nbsp; [details](./TASK-DETAIL.md#dmon-024--text-view-panel)
- [ ] **DMON-025** — Keyboard navigation &nbsp; [details](./TASK-DETAIL.md#dmon-025--keyboard-navigation)
- [ ] **DMON-026** — Context menu system &nbsp; [details](./TASK-DETAIL.md#dmon-026--context-menu-system)
- [ ] **DMON-027** — Dark/light theme toggle &nbsp; [details](./TASK-DETAIL.md#dmon-027--darklight-theme-toggle)
- [ ] **DMON-028** — Workspace persistence (save/load layout) &nbsp; [details](./TASK-DETAIL.md#dmon-028--workspace-persistence-saveload-layout)

---

## Phase 3: Advanced UI Features

**Goal:** Visual Filter Builder, Expand All mode, grid settings export, sparklines, and quick-add columns.

- [ ] **DMON-029** — Visual Filter Builder &nbsp; [details](./TASK-DETAIL.md#dmon-029--visual-filter-builder)
- [ ] **DMON-030** — Expand All mode (JSON tree per row) &nbsp; [details](./TASK-DETAIL.md#dmon-030--expand-all-mode-json-tree-per-row)
- [ ] **DMON-031** — Grid settings export/import &nbsp; [details](./TASK-DETAIL.md#dmon-031--grid-settings-exportimport)
- [ ] **DMON-032** — Sparkline charts in Topic Explorer &nbsp; [details](./TASK-DETAIL.md#dmon-032--sparkline-charts-in-topic-explorer)
- [ ] **DMON-033** — Quick-Add column from Inspector &nbsp; [details](./TASK-DETAIL.md#dmon-033--quick-add-column-from-inspector)

---

## Phase 4: Operational Tools

**Goal:** Send Sample emulator, Import/Export, and the Replay Engine.

- [ ] **DMON-034** — Send Sample panel (message emulator) &nbsp; [details](./TASK-DETAIL.md#dmon-034--send-sample-panel-message-emulator)
- [ ] **DMON-035** — Dynamic form components (recursive editors) &nbsp; [details](./TASK-DETAIL.md#dmon-035--dynamic-form-components-recursive-editors)
- [ ] **DMON-036** — Custom Type Drawer registry &nbsp; [details](./TASK-DETAIL.md#dmon-036--custom-type-drawer-registry)
- [ ] **DMON-037** — Export service (streaming JSON write) &nbsp; [details](./TASK-DETAIL.md#dmon-037--export-service-streaming-json-write)
- [ ] **DMON-038** — Import service (streaming JSON read) &nbsp; [details](./TASK-DETAIL.md#dmon-038--import-service-streaming-json-read)
- [ ] **DMON-039** — Replay Engine &nbsp; [details](./TASK-DETAIL.md#dmon-039--replay-engine)
- [ ] **DMON-040** — Replay Panel UI &nbsp; [details](./TASK-DETAIL.md#dmon-040--replay-panel-ui)

---

## Phase 5: Plugin Architecture

**Goal:** Hot-loadable Razor Class Library plugins with custom panels, menus, and formatters.

- [ ] **DMON-041** — Plugin loading infrastructure &nbsp; [details](./TASK-DETAIL.md#dmon-041--plugin-loading-infrastructure)
- [ ] **DMON-042** — Plugin panel registration &nbsp; [details](./TASK-DETAIL.md#dmon-042--plugin-panel-registration)
- [ ] **DMON-043** — Plugin menu registration &nbsp; [details](./TASK-DETAIL.md#dmon-043--plugin-menu-registration)
- [ ] **DMON-044** — IFormatterRegistry (custom value formatters) &nbsp; [details](./TASK-DETAIL.md#dmon-044--iformatterregistry-custom-value-formatters)

---

## Phase 6: Domain Entity Plugins (BDC / TKB)

**Goal:** Ship the BDC and TKB entity aggregation/tracking as isolated plugins with grid, tree, detail, and time-travel views.

- [ ] **DMON-045** — EntityStore core (aggregation engine) &nbsp; [details](./TASK-DETAIL.md#dmon-045--entitystore-core-aggregation-engine)
- [ ] **DMON-046** — BDC Entity Grid panel &nbsp; [details](./TASK-DETAIL.md#dmon-046--bdc-entity-grid-panel)
- [ ] **DMON-047** — TKB Entity Folder Tree panel &nbsp; [details](./TASK-DETAIL.md#dmon-047--tkb-entity-folder-tree-panel)
- [ ] **DMON-048** — Entity Detail Inspector &nbsp; [details](./TASK-DETAIL.md#dmon-048--entity-detail-inspector)
- [ ] **DMON-049** — Historical State (time-travel) &nbsp; [details](./TASK-DETAIL.md#dmon-049--historical-state-time-travel)

---

## Phase 7: Polish & UX Enhancements

**Goal:** Remote monitoring, multi-tab isolation, accessibility shortcuts, diff view, performance dashboard, and notification toasts.

- [ ] **DMON-050** — Remote monitoring support &nbsp; [details](./TASK-DETAIL.md#dmon-050--remote-monitoring-support)
- [ ] **DMON-051** — Multi-tab workspace isolation &nbsp; [details](./TASK-DETAIL.md#dmon-051--multi-tab-workspace-isolation)
- [ ] **DMON-052** — Unified search bar (power user text filter) &nbsp; [details](./TASK-DETAIL.md#dmon-052--unified-search-bar-power-user-text-filter)
- [ ] **DMON-053** — Notification toast system &nbsp; [details](./TASK-DETAIL.md#dmon-053--notification-toast-system)
- [ ] **DMON-054** — Drag-and-drop panel docking &nbsp; [details](./TASK-DETAIL.md#dmon-054--drag-and-drop-panel-docking)
- [ ] **DMON-055** — Sample diff view &nbsp; [details](./TASK-DETAIL.md#dmon-055--sample-diff-view)
- [ ] **DMON-056** — Keyboard shortcut system &nbsp; [details](./TASK-DETAIL.md#dmon-056--keyboard-shortcut-system)
- [ ] **DMON-057** — Performance metrics dashboard &nbsp; [details](./TASK-DETAIL.md#dmon-057--performance-metrics-dashboard)
