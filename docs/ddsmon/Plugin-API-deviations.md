The ECS plugin design and tasks require several core APIs from the DdsMonitor engine to aggregate data and visualize entities. These APIs **are available** in the codebase, but as noted in the `MON-BATCH-28-REPORT.md`, some architectural changes were made to how plugins access them.

Here is a breakdown of the required APIs, their availability, and how the Batch 28 changes affect the implementation:

### 1. Required APIs & Codebase Availability

*   **Instance Lifecycle Tracking:** To aggregate descriptors, the `EntityStore` background engine must listen to DDS instance transitions (Alive, Disposed, NoWriters).
    *   *Availability:* **Yes.** The codebase implements `IInstanceStore.OnInstanceChanged`, which provides an `IObservable<InstanceTransitionEvent>` that the ECS plugin can subscribe to.
*   **Historical Data Access (Time-Travel):** The Historical State (Time-Travel) algorithm requires querying a chronologically sorted ledger of samples to binary-search for past states.
    *   *Availability:* **Yes.** The `ISampleStore.GetTopicSamples(Type topicType)` API is fully implemented and returns an `ITopicSamples` collection containing the chronological history of samples.
*   **Window Management & Pub/Sub:** The ECS UI panels must spawn new detail grids and broadcast events (like selecting an entity).
    *   *Availability:* **Yes.** The codebase provides `IWindowManager.SpawnPanel` and `IEventBroker.Publish` which UI components can use to interact with the broader Web Desktop.

### 2. API Changes from `MON-BATCH-28-REPORT.md`

The design and task documents (DMON-041 through DMON-045) assumed that plugins would access core services directly through an `IMonitorContext` object passed during plugin initialization. However, the Batch 28 report introduced necessary deviations to handle Blazor's DI (Dependency Injection) scopes.

*   **Removal of Scoped Services from Context:** The original design stated `IMonitorContext` would contain `IWindowManager` and `ISampleStore`. Because `IWindowManager` is "Scoped" (one instance per browser tab) and plugins are "Singleton" (loaded once at startup), passing the window manager via context would cause a captive dependency issue. `IMonitorContext` now exposes a single `GetFeature<TFeature>()` method for capability-querying; plugins call `context.GetFeature<IMenuRegistry>()` and `context.GetFeature<PluginPanelRegistry>()` to obtain the registries (see [PLA1-DESIGN.md §4](../plugin-api/PLA1-DESIGN.md#4-phase-1--capability-querying-context-future-proof-foundation)).
*   **New Panel Registration Pattern:** The design expected plugins to register panels by calling `context.WindowManager.RegisterPanelType(...)`. To solve the lifecycle mismatch, plugins now register their custom UI panels (like `EcsEntityGridPanel` and `EntityDetailPanel`) using `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)`. The null-conditional prevents exceptions when the registry is not available. The UI shell will safely resolve these registrations across all browser tabs.
*   **Accessing Data Stores:** Because `ISampleStore` and `IInstanceStore` were removed from the `IMonitorContext`, plugins cannot access them during the `Initialize()` phase. Instead, plugins must use the DI container.

### 3. Compatibility Assessment

**Yes, the design and tasks are still entirely compatible with the current codebase**, but the developer implementing them must adopt the updated DI patterns:

1.  **Backend Services (`EntityStore`):** Instead of pulling `IInstanceStore` from the `IMonitorContext`, the ECS plugin should register its `EntityStore` as a background service inside its `ConfigureServices(IServiceCollection services)` method. The `EntityStore` will then automatically receive `IInstanceStore` via constructor injection.
2.  **UI Panels:** The custom ECS Blazor panels (`EcsEntityGridPanel`, etc.) will just use the standard `@inject ISampleStore` and `@inject IWindowManager` directives at the top of their Razor files to access the data and window APIs within the correct browser tab scope.
3.  **Initialization:** The plugin's `Initialize(IMonitorContext context)` method will strictly be used to register the custom ECS panels via `context.GetFeature<PluginPanelRegistry>()?.RegisterPanelType(...)` and add ECS-specific menu items via `context.GetFeature<IMenuRegistry>()?.AddMenuItem(...)`.
