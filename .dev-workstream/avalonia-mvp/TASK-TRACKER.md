# Task Tracker — DdsMonitor.Avalonia V1

**Reference:** See [TASK-DETAIL.md](./TASK-DETAIL.md) for detailed task descriptions.  
**Design:** See [DESIGN.md](./DESIGN.md) for architecture and rationale.

---

## Phase 0: Engine Purification

**Goal:** Remove all `Microsoft.AspNetCore.Components` references from `DdsMonitor.Engine` so it compiles as a Blazor-free library.

- [x] **TASK-A001** Remove Blazor Types from Engine Registries — [details](./TASK-DETAIL.md#task-a001--remove-blazor-types-from-engine-registries)

---

## Phase 1: Empty Shell

**Goal:** A running Avalonia desktop process with Generic Host, dual-boot decision, empty `ShellWindow` with menu/toolbar, and plugin loading infrastructure.

- [x] **TASK-B001** Create `DdsMonitor.Avalonia.Core` Project — [details](./TASK-DETAIL.md#task-b001--create-ddsmonitoravaloniacore-project)
- [x] **TASK-B002** Create `DdsMonitor.Avalonia` Shell Project — [details](./TASK-DETAIL.md#task-b002--create-ddsmonitoravalonia-shell-project) ✅ P1 disposal fix in BATCH-02
- [x] **TASK-B003** Integrate PluginLoader + InitializePlugins into Shell — [details](./TASK-DETAIL.md#task-b003--integrate-pluginloader--initializeplugins-into-shell)

---

## Phase 2: Schema & Topic Discovery

**Goal:** Prove dynamic schema DLL hot-load and live topic list rendering from a plugin.

- [x] **TASK-C001** WorkspaceManagerPlugin: Schema Sources Panel — [details](./TASK-DETAIL.md#task-c001--workspacemanagerplugin-schema-sources-panel)
- [x] **TASK-C002** TopicExplorerPlugin — [details](./TASK-DETAIL.md#task-c002--topicexplorerplugin)

---

## Phase 3: Backend Prover

**Goal:** Prove headless execution, CLI argument binding per-plugin, and cross-plugin context menu injection.

- [x] **TASK-D001** DummyDataGeneratorPlugin — [details](./TASK-DETAIL.md#task-d001--dummydatageneratorplugin)

---

## Phase 4: Firehose UI

**Goal:** Prove 5 kHz DDS data consumption via `SampleView`, zero-allocation virtualized grid, and expression-tree-based detail inspection.

- [x] **TASK-E001** SamplesViewerPlugin: Grid & Filtering — [details](./TASK-DETAIL.md#task-e001--samplesviewerplugin-grid--filtering)
- [x] **TASK-E002** DetailInspectorPlugin: Linked Inspector Panel — [details](./TASK-DETAIL.md#task-e002--detailinspectorplugin-linked-inspector-panel)

---

## Phase 5: Data Authoring & Network Configuration

**Goal:** Prove two-way expression-tree binding for payload authoring and dynamic DDS participant management.

- [x] **TASK-F001** SendSamplePlugin — [details](./TASK-DETAIL.md#task-f001--sendsampleplugin)
- [x] **TASK-F002** WorkspaceManagerPlugin: Network Configurator Panel — [details](./TASK-DETAIL.md#task-f002--workspacemanagerplugin-network-configurator-panel)

---

## Phase 6: Workspace Polish

**Goal:** Prove per-panel `IStatefulViewModel` persistence round-trip and `workspace.json` compatibility.

- [ ] **TASK-G001** IStatefulViewModel Persistence Round-Trip — [details](./TASK-DETAIL.md#task-g001--istatefulviewmodel-persistence-round-trip)

---

## Phase 7: DDS Diagnostics Depth & Tooling

**Goal:** Port the critical diagnostic features missing from V1: instance lifecycle tracking,
data export, manual layout management, visual filter builder, and custom column picker.
All tasks in this phase are post-V1 and reference only Engine + Core.

- [ ] **TASK-H001** InstancesViewerPlugin — DDS instance lifecycle tracking — [details](./TASK-DETAIL.md#task-h001--instancesviewerplugin)
- [ ] **TASK-H002** ExportPlugin: Data Export Pipeline — [details](./TASK-DETAIL.md#task-h002--exportplugin-data-export-pipeline)
- [ ] **TASK-H003** WorkspaceManagerPlugin: Manual Layout Import/Export — [details](./TASK-DETAIL.md#task-h003--workspacemanagerplugin-manual-layout-importexport)
- [ ] **TASK-H004** FilterBuilderPlugin: Visual Filter Builder — [details](./TASK-DETAIL.md#task-h004--filterbuilderplugin-visual-filter-builder)
- [ ] **TASK-H005** Custom Column Picker — [details](./TASK-DETAIL.md#task-h005--custom-column-picker)
