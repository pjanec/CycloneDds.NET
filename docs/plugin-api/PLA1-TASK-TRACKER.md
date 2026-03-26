# DdsMonitor Plugin API — Task Tracker

**Project:** DdsMonitor Plugin API Enhancements  
**Prefix:** PLA1  
**Last Updated:** 2026-03-27 (PLA1-BATCH-10 reviewed — PLA1 stream complete)

**Reference Documents:**
- [PLA1-DESIGN.md](PLA1-DESIGN.md) — Architecture and design decisions
- [PLA1-TASK-DETAIL.md](PLA1-TASK-DETAIL.md) — Full task specifications with success criteria
- [PLA1-DEBT-TRACKER.md](PLA1-DEBT-TRACKER.md) — PLA1 technical debt and target batches

---

## Project Status

**Current Phase:** PLA1 complete (maintenance mode) — all phases 1–8 delivered; debts **020–023** closed (incl. host **output** `plugins\` for Feature Demo, **PLA1-DEBT-023**)  
**Progress:** Phases 1–8 delivered; post–Phase 8 hardening reviewed in **PLA1-BATCH-10**  
**Estimated Duration:** 6–9 weeks (phased approach)

**Debt tracker:** [PLA1-DEBT-TRACKER.md](PLA1-DEBT-TRACKER.md)

**Active batch:** none (PLA1 phases 1–8 complete; open a new batch only for discretionary follow-ups).

**Completed batches:**
- [.dev-workstream/reviews/PLA1-BATCH-10-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-10-REVIEW.md) — PLA1-DEBT-020–022 + **023** (report: [PLA1-BATCH-10-REPORT.md](../../.dev-workstream/reports/PLA1-BATCH-10-REPORT.md))
- [.dev-workstream/reviews/PLA1-BATCH-09-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-09-REVIEW.md) — PLA1-DEBT-018–019, PLA1-P8-T01–T05 (+ follow-up debt 020–022)
- [.dev-workstream/reviews/PLA1-BATCH-08-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-08-REVIEW.md) — PLA1-DEBT-016–017, PLA1-P7-T01–T03 (+ follow-up debt 018–019)
- [.dev-workstream/reviews/PLA1-BATCH-07-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-07-REVIEW.md) — PLA1-DEBT-011–015, PLA1-P6-T06–T09 (+ follow-up debt 016–017)
- [.dev-workstream/reviews/PLA1-BATCH-06-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-06-REVIEW.md) — PLA1-DEBT-008/010, PLA1-P5-T03–T04, PLA1-P6-T01–T05 (+ follow-up debt 011–015)
- [.dev-workstream/reviews/PLA1-BATCH-05-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-05-REVIEW.md) — PLA1-DEBT-009, PLA1-P5-T01–T02 (+ lead P1 corrective for first-run enablement)
- [.dev-workstream/reviews/PLA1-BATCH-04-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-04-REVIEW.md) — PLA1-DEBT-006/007, PLA1-P4-T01–T04
- [.dev-workstream/reviews/PLA1-BATCH-03-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-03-REVIEW.md) — PLA1-DEBT-004/005, PLA1-P3-T01–T04
- [.dev-workstream/reviews/PLA1-BATCH-02-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-02-REVIEW.md) — PLA1-DEBT-001–003, PLA1-P2-T04–T06
- [.dev-workstream/reviews/PLA1-BATCH-01-REVIEW.md](../../.dev-workstream/reviews/PLA1-BATCH-01-REVIEW.md) — PLA1-P1-T01–T04, PLA1-P2-T01–T03

---

## Phase 1 — Capability-Querying Context (Future-Proof Foundation)

**Goal:** Replace the rigid `IMonitorContext` property bag with a `GetFeature<T>()` capability-querying pattern. Migrate all existing code to the new API.  
**Design:** [§4](PLA1-DESIGN.md#4-phase-1--capability-querying-context-future-proof-foundation)

- [x] **PLA1-P1-T01** Redesign `IMonitorContext` to Capability-Querying [details](PLA1-TASK-DETAIL.md#pla1-p1-t01-redesign-imonitorcontext-to-capability-querying) — PLA1-BATCH-01
- [x] **PLA1-P1-T02** Update `MonitorContext` Concrete Implementation [details](PLA1-TASK-DETAIL.md#pla1-p1-t02-update-monitorcontext-concrete-implementation) — PLA1-BATCH-01
- [x] **PLA1-P1-T03** Register Core Registries in Host DI Container [details](PLA1-TASK-DETAIL.md#pla1-p1-t03-register-core-registries-in-host-di-container) — PLA1-BATCH-01
- [x] **PLA1-P1-T04** Migrate ECS Plugin to New API [details](PLA1-TASK-DETAIL.md#pla1-p1-t04-migrate-ecs-plugin-to-new-api) — PLA1-BATCH-01

---

## Phase 2 — Context Menu Registry

**Goal:** Allow plugins to inject right-click actions into Topic Explorer, Samples Panel, and Instances Panel via a type-safe, composable `IContextMenuRegistry`.  
**Design:** [§5](PLA1-DESIGN.md#5-phase-2--context-menu-registry)

- [x] **PLA1-P2-T01** Create `IContextMenuRegistry` Interface [details](PLA1-TASK-DETAIL.md#pla1-p2-t01-create-icontextmenuregistry-interface) — PLA1-BATCH-01
- [x] **PLA1-P2-T02** Implement `ContextMenuRegistry` [details](PLA1-TASK-DETAIL.md#pla1-p2-t02-implement-contextmenuregistry) — PLA1-BATCH-01
- [x] **PLA1-P2-T03** Register `IContextMenuRegistry` in Host DI [details](PLA1-TASK-DETAIL.md#pla1-p2-t03-register-icontextmenuregistry-in-host-di) — PLA1-BATCH-01
- [x] **PLA1-P2-T04** Integrate Context Menus into `TopicExplorerPanel.razor` [details](PLA1-TASK-DETAIL.md#pla1-p2-t04-integrate-context-menus-into-topicexplorerpanelrazor) — PLA1-BATCH-02
- [x] **PLA1-P2-T05** Integrate Context Menus into `SamplesPanel.razor` [details](PLA1-TASK-DETAIL.md#pla1-p2-t05-integrate-context-menus-into-samplespanelrazor) — PLA1-BATCH-02
- [x] **PLA1-P2-T06** Integrate Context Menus into `InstancesPanel.razor` [details](PLA1-TASK-DETAIL.md#pla1-p2-t06-integrate-context-menus-into-instancespanelrazor) — PLA1-BATCH-02

---

## Phase 3 — Detail View Hijacking

**Goal:** Allow plugins to fully replace the tree-view tab in `DetailPanel` with a custom Blazor component for specific payload types.  
**Design:** [§6](PLA1-DESIGN.md#6-phase-3--detail-view-hijacking-isampleviewregistry)

- [x] **PLA1-P3-T01** Create `ISampleViewRegistry` Interface [details](PLA1-TASK-DETAIL.md#pla1-p3-t01-create-isampleviewregistry-interface) — PLA1-BATCH-03
- [x] **PLA1-P3-T02** Implement `SampleViewRegistry` [details](PLA1-TASK-DETAIL.md#pla1-p3-t02-implement-sampleviewregistry) — PLA1-BATCH-03
- [x] **PLA1-P3-T03** Register `ISampleViewRegistry` in Host DI [details](PLA1-TASK-DETAIL.md#pla1-p3-t03-register-isampleviewregistry-in-host-di) — PLA1-BATCH-03
- [x] **PLA1-P3-T04** Modify `DetailPanel.razor` to Consult Registry [details](PLA1-TASK-DETAIL.md#pla1-p3-t04-modify-detailpanelrazor-to-consult-registry) — PLA1-BATCH-03

---

## Phase 4 — Workspace Settings Integration

**Goal:** Enable plugins to save and restore their settings inside the main `workspace.json` file using event-driven, dictionary-based patterns — no custom file I/O required.  
**Design:** [§7](PLA1-DESIGN.md#7-phase-4--workspace-settings-integration)

- [x] **PLA1-P4-T01** Add `WorkspaceSavingEvent` and `WorkspaceLoadedEvent` [details](PLA1-TASK-DETAIL.md#pla1-p4-t01-add-workspacesavingevent-and-workspaceloadedevent) — PLA1-BATCH-04
- [x] **PLA1-P4-T02** Extend `WorkspaceDocument` with `PluginSettings` [details](PLA1-TASK-DETAIL.md#pla1-p4-t02-extend-workspacedocument-with-pluginsettings) — PLA1-BATCH-04
- [x] **PLA1-P4-T03** Integrate Save/Load Events into `WindowManager` [details](PLA1-TASK-DETAIL.md#pla1-p4-t03-integrate-saveload-events-into-windowmanager) — PLA1-BATCH-04
- [x] **PLA1-P4-T04** Migrate ECS Plugin to Workspace Settings [details](PLA1-TASK-DETAIL.md#pla1-p4-t04-migrate-ecs-plugin-to-workspace-settings) — PLA1-BATCH-04

---

## Phase 5 — Plugin Manager UI

**Goal:** Give users explicit control over which plugins are activated. Plugins are discovered from the directory but only loaded when the user enables them in a Plugin Manager panel.  
**Design:** [§8](PLA1-DESIGN.md#8-phase-5--plugin-manager-ui)

- [x] **PLA1-P5-T01** Create `DiscoveredPlugin` DTO and `PluginConfigService` [details](PLA1-TASK-DETAIL.md#pla1-p5-t01-create-discoveredplugin-dto-and-pluginconfigservice) — PLA1-BATCH-05
- [x] **PLA1-P5-T02** Update `PluginLoader` for Two-Phase Loading [details](PLA1-TASK-DETAIL.md#pla1-p5-t02-update-pluginloader-for-two-phase-loading) — PLA1-BATCH-05 (+ post-review first-run enablement)
- [x] **PLA1-P5-T03** Create `PluginManagerPanel.razor` [details](PLA1-TASK-DETAIL.md#pla1-p5-t03-create-pluginmanagerpanelrazor) — PLA1-BATCH-06
- [x] **PLA1-P5-T04** Wire Plugin Manager into Application Menu [details](PLA1-TASK-DETAIL.md#pla1-p5-t04-wire-plugin-manager-into-application-menu) — PLA1-BATCH-06

---

## Phase 6 — Advanced Extension Points

**Goal:** Expose the remaining internal host services — value formatters, type drawers, topic coloring, export formats, tooltips, and filter macros — as first-class plugin extension points.  
**Design:** [§9](PLA1-DESIGN.md#9-phase-6--advanced-extension-points)

- [x] **PLA1-P6-T01** Expose `IValueFormatterRegistry` via `GetFeature` [details](PLA1-TASK-DETAIL.md#pla1-p6-t01-expose-ivalueformatterregistry-via-getfeature) — PLA1-BATCH-06
- [x] **PLA1-P6-T02** Expose `ITypeDrawerRegistry` via `GetFeature` [details](PLA1-TASK-DETAIL.md#pla1-p6-t02-expose-itypedrawerregistry-via-getfeature) — PLA1-BATCH-06
- [x] **PLA1-P6-T03** Add `RegisterColorRule` to `TopicColorService` [details](PLA1-TASK-DETAIL.md#pla1-p6-t03-add-registercolorrule-to-topiccolorservice) — PLA1-BATCH-06
- [x] **PLA1-P6-T04** Create `IExportFormatRegistry` [details](PLA1-TASK-DETAIL.md#pla1-p6-t04-create-iexportformatregistry) — PLA1-BATCH-06
- [x] **PLA1-P6-T05** Expose Export Formats in Samples Panel Export Button [details](PLA1-TASK-DETAIL.md#pla1-p6-t05-expose-export-formats-in-samples-panel-export-button) — PLA1-BATCH-06
- [x] **PLA1-P6-T06** Create `ITooltipProviderRegistry` [details](PLA1-TASK-DETAIL.md#pla1-p6-t06-create-itooltipproviderregistry) — PLA1-BATCH-07
- [x] **PLA1-P6-T07** Consult `ITooltipProviderRegistry` in `TooltipPortal.razor` [details](PLA1-TASK-DETAIL.md#pla1-p6-t07-consult-itooltipproviderregistry-in-tooltipportalrazor) — PLA1-BATCH-07
- [x] **PLA1-P6-T08** Create `IFilterMacroRegistry` [details](PLA1-TASK-DETAIL.md#pla1-p6-t08-create-ifiltermacroregistry) — PLA1-BATCH-07
- [x] **PLA1-P6-T09** Integrate Filter Macros into `FilterCompiler` [details](PLA1-TASK-DETAIL.md#pla1-p6-t09-integrate-filter-macros-into-filtercompiler) — PLA1-BATCH-07

---

## Phase 7 — Kitchen Sink Demo Plugin

**Goal:** Build a "Feature Demo" plugin that exercises every extension point, serving as both a live documentation sample and regression guard.  
**Design:** [§10](PLA1-DESIGN.md#10-phase-7--kitchen-sink-demo-plugin)

- [x] **PLA1-P7-T01** Create Demo Plugin Project [details](PLA1-TASK-DETAIL.md#pla1-p7-t01-create-demo-plugin-project) — PLA1-BATCH-08
- [x] **PLA1-P7-T02** Implement `FeatureDemoPlugin` [details](PLA1-TASK-DETAIL.md#pla1-p7-t02-implement-featuredemoplugin) — PLA1-BATCH-08 + **PLA1-BATCH-09** (**TopicColorService** / §10.2)
- [x] **PLA1-P7-T03** Create `DemoDashboardPanel.razor` [details](PLA1-TASK-DETAIL.md#pla1-p7-t03-create-demodashboardpanelrazor) — PLA1-BATCH-08

---

## Phase 8 — Autonomous CI Testing

**Goal:** Establish a fully autonomous (CI-friendly) test suite covering plugin registration, event-driven settings, graceful degradation, Blazor component rendering, and headless integration.  
**Design:** [§11](PLA1-DESIGN.md#11-phase-8--autonomous-ci-testing)

- [x] **PLA1-P8-T01** Create `DdsMonitor.Plugins.FeatureDemo.Tests` Project [details](PLA1-TASK-DETAIL.md#pla1-p8-t01-create-ddsmonitorpluginsfeaturedemotests-project) — PLA1-BATCH-09
- [x] **PLA1-P8-T02** Plugin Registration Unit Tests [details](PLA1-TASK-DETAIL.md#pla1-p8-t02-plugin-registration-unit-tests) — PLA1-BATCH-09 (see **PLA1-DEBT-021**)
- [x] **PLA1-P8-T03** bUnit Blazor Component Tests [details](PLA1-TASK-DETAIL.md#pla1-p8-t03-bunit-blazor-component-tests) — PLA1-BATCH-09
- [x] **PLA1-P8-T04** `PluginManagerPanel` bUnit Tests [details](PLA1-TASK-DETAIL.md#pla1-p8-t04-pluginmanagerpanel-bunit-tests) — PLA1-BATCH-06 stub + BATCH-09 canonical note
- [x] **PLA1-P8-T05** Headless Integration Test [details](PLA1-TASK-DETAIL.md#pla1-p8-t05-headless-integration-test) — PLA1-BATCH-09 (see **PLA1-DEBT-020** for full task-detail scope)
