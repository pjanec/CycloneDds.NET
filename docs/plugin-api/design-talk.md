
how good and usefull the current plugn api is? does it allow extensing the ddsmon in othe way but opening a new window unrelated to anything else? I noticed the pguin can;t even save its setting together with ddsmon other panels. It had to guess the config file save path itself. not much of a service from the dds monitor side. It looks pretty basic. How to improve the api to become a bit more useful?

You are entirely correct in your assessment. The current plugin API is quite basic and primarily acts as a bridge to host custom Blazor components in standalone windows, rather than deeply integrating with the core DdsMonitor UI.

Current State of the Plugin API

Right now, the `IMonitorPlugin` interface only provides two extension points:

-   **ConfigureServices(IServiceCollection services)**: This allows the plugin to register background workers and inject dependencies into the host's DI container. For example, the ECS plugin uses this to run its `EntityStore` in the background to aggregate DDS samples.-   **Initialize(IMonitorContext context)**: This provides access to an `IMonitorContext`. However, this context currently only exposes two registries:
    -   `MenuRegistry`: To add custom drop-down menus to the top application navigation bar.-   `PanelRegistry`: To register standalone floating windows (panels).

**The Settings Limitation:** As you noticed, the plugin API lacks unified state management. Because there is no central `IPluginSettingsManager` provided by the host, plugins are forced to do their own file I/O. The ECS plugin works around this by injecting `IWorkspaceState` to guess the application data directory, and then manually serializes its own `ecs-settings.json` file to sit alongside the main workspace file.

How to Improve the Plugin API

To make the API more powerful and deeply integrated, the following architectural improvements could be made based on existing DdsMonitor systems:

**1\. Unified Settings Management** Instead of forcing plugins to manage their own I/O, the `IMonitorContext` (or a dedicated injected service) should allow plugins to register setting objects. The host's `WorkspacePersistenceService` could automatically serialize these plugin configurations directly into the main `workspace.json` file.

**2\. Deep UI Integration (Context Menus & Toolbars)** Currently, right-click context menus in core panels like the `SamplesPanel` or `InstancesPanel` are hardcoded with fixed options like "Show Detail" or "Clone to Send". The API should be expanded to allow plugins to register contextual actions. For example, the ECS plugin could register an action that appears when you right-click a raw DDS sample, allowing you to "Inspect as ECS Entity".

**3\. Exposing Formatters and Type Drawers** DdsMonitor already has sophisticated internal registries for how data is displayed and edited, which plugins should be able to tap into via `IMonitorContext`:

-   **IValueFormatterRegistry**: Exposing this would allow plugins to dictate how specific data types are rendered inline in the data grids (e.g., custom syntax highlighting or custom string formatting).-   **ITypeDrawerRegistry**: Exposing this would allow plugins to provide custom UI input controls for the `DynamicForm` panel when editing specific payload types.

**4\. Exposing the Event Broker** While plugins can currently request the `IEventBroker` via constructor dependency injection, explicitly adding event subscription capabilities to the `IMonitorContext` would standardize how plugins listen to core application events (like when a user selects a sample in the UI or changes a partition).

plugins should be able to add context menus also to the "topics" panel rows. Any other potential extension points?

Adding plugin-injected context menus to the **Topic Explorer** rows is a great idea. Given the current architecture of DdsMonitor, that same contextual approach could be applied to several other core systems.

Here are other highly valuable extension points that could be exposed to plugins based on the application's existing internal services:

**1\. Unified Context Menu Registry** Beyond just the Topic panel, the application also has right-click context menus hardcoded into the **SamplesPanel** (e.g., "Clone to Send/Emulator") and the **InstancesPanel** (e.g., "Show Instance Samples"). Exposing a generic `IContextMenuRegistry` would allow a plugin to inject custom actions based on the clicked context—whether the user right-clicks a Topic, a raw Sample, or an Instance.

**2\. Custom Export & Import Formats** Currently, the **IExportService** and **IImportService** are strictly designed to stream data to and from JSON files. Exposing a registry for these services would allow plugins to provide custom data converters. For example, a plugin could add the ability to export a filtered sample list to CSV, PCAP, or a domain-specific format like MAT files.

**3\. Rich Tooltip Generators** The UI utilizes a global **TooltipService** that currently renders basic syntax-highlighted JSON when a user hovers over a payload string or a "D" (Detail) button. If plugins could register custom tooltip providers based on payload types, they could render domain-specific HTML popups—such as showing a mini-map preview when hovering over a geospatial coordinate, or a status gauge when hovering over health metrics.

**4\. Custom Filter Functions** The application evaluates user search queries using an **IFilterCompiler** backed by Dynamic LINQ. An extension point here could allow plugins to register custom macro functions or operators that users can type into the filter bar. For example, the ECS plugin could register a `DistanceTo()` function, allowing users to type `Payload.DistanceTo(lat, lon) < 1000` into the standard Samples Panel filter.

**5\. Programmatic Topic Coloring** The **TopicColorService** currently assigns colors to topics deterministically based on a hash of their `ShortName`, or via manual user overrides saved to disk. An extension point here would allow plugins to register programmatic coloring rules. A plugin could ensure that any topic name containing "Error" or "Alarm" is always rendered in red across all panels.

what about custom content dumping for whole topic in 'sample detail' panel?

Currently, the **Detail Panel** is strictly designed to inspect a **single sample** at a time. While you can view and copy the raw JSON representation of that individual sample's payload from its "JSON" tab, it does not support bulk dumping.

If you want to dump the content for a **whole topic**, that functionality is located in the **Samples Panel**.

Here is how the current dumping system works:

-   **Bulk Export:** The Samples Panel toolbar includes an **"Export visible samples to file"** button. This grabs a snapshot of your currently filtered view (or the whole topic if no filter is applied) and triggers an export.-   **Memory-Safe Streaming:** Behind the scenes, this uses the `IExportService`. The `ExportService` is highly optimized: it uses a `Utf8JsonWriter` tied directly to a `FileStream` to stream the data to disk in chunks. This guarantees O(1) memory allocation, allowing you to safely dump millions of samples without causing an `OutOfMemoryException`.

**Regarding "Custom" Content Dumping:** At the moment, the `ExportService` is hardcoded to serialize the output strictly as JSON.

As we discussed in the previous API improvements, if you wanted to dump a whole topic into a _custom_ format (such as CSV, a proprietary binary format, or a domain-specific layout), the application would need to expose an `IExportService` or `IImportService` registry to the plugin context. This would allow a plugin to hook into that "Export" button and provide its own custom streaming logic instead of relying on the default JSON serializer.

of course i meant the sample of a concrete topic - show it in customized way

To customize how a specific concrete topic (or its nested data types) is displayed in the Detail Panel, the DDS Monitor architecture provides a tiered formatting system. You can achieve this either at compile-time via schema attributes or at runtime via plugins.

**1\. Compile-Time Customization:** **\[DdsTypeFormat\]** If you have access to the C# schema definition, you can decorate your struct or class with the **\[DdsTypeFormat\]** attribute.

-   This attribute accepts a format template string combining literal text with placeholders in the format `{FieldName:FormatString:TokenType}`.-   For example: `[DdsTypeFormat("[{X:0.000:Number}, {Y:0.000:Number}]")]`.-   During compilation, the code generator automatically parses this template and emits a `GetFormatTokens()` method.-   The `DetailPanel` uses reflection to detect this method and automatically renders your sample inline with rich syntax highlighting instead of a standard string.

**2\. Runtime Customization via Plugins:** **IValueFormatter** If you cannot change the schema or want more complex logic (like domain-specific data conversion), a plugin can implement the **IValueFormatter** interface.

-   **Implementation:** You provide a `DisplayName` and implement `FormatText()` and `FormatTokens()` to dictate exactly how the CLR type should be rendered as a sequence of styled tokens.-   **Scoring System:** Your formatter must implement `GetScore(Type type, object? value)`. If you return a score `>= 0.8`, your custom formatter is automatically applied as the default view for that type. If the score is between `0` and `0.8`, it becomes an available alternative view.-   **UI Integration:** Inside the `DetailPanel`, when a user right-clicks on a formatted value, it opens a context menu. If your custom formatter is registered, a **"View As"** submenu appears, allowing the user to seamlessly toggle between the "Default" representation and any custom formatters you provided.-   **Registration:** These formatters are injected into the host application by registering them with the `IValueFormatterRegistry`.

will the IValueFormatter work for the whole sample? For example instead of the tree view in the sample detail I would like to show a customized form

No, the `IValueFormatter` will not work to replace the entire tree view with a customized form.

The `IValueFormatter` is strictly designed as a "Tier 1 value formatter contract" to provide "custom text and token-based rendering" for specific types. Its interface only allows you to return a plain string (`FormatText`) or a sequence of text tokens (`FormatTokens`). It does not support returning HTML or Blazor UI components. When the `DetailPanel` detects a custom formatter, it simply renders your provided text tokens inline _within_ the existing tree row.

If you want to display a customized form or entirely replace the default inspection UI for a specific topic, you have two different paths depending on your goal:

**1\. For Inspecting/Viewing a Whole Sample (Custom Panel)** If you want a completely bespoke UI to view a sample, you should bypass the default `DetailPanel` entirely and register a brand new custom panel via the `PanelRegistry`. For example, the ECS plugin uses this exact approach: instead of relying on the default tree view, it registers its own custom `"ECS Entity Detail"` panel to render a specialized, domain-specific UI for its entities.

**2\. For Editing a Whole Sample (Type Drawers)** If your goal is to build a customized form for _editing_ and sending a sample (such as in the "Send Sample" panel), you should use the `ITypeDrawerRegistry`. This registry allows plugins to map a specific CLR type to a Blazor `RenderFragment`, enabling you to inject fully interactive, custom UI input controls directly into the application's `DynamicForm` engine.

i do not need different panel. I need to replace the most often used part of the sample detail panel which is the first tab - the tree view, with something completely customized for concrete type of sample.

Based on the current source code for the application, **you cannot replace the entire Tree View tab within the standard Sample Detail panel.**

If we look at how the standard detail window is built in `DetailPanel.razor`, the "Tree" tab explicitly calls a hardcoded `@RenderTreeView()` method. This method recursively builds the UI node-by-node using `@RenderNode(...)`. It only consults the `IValueFormatterRegistry` when it reaches an individual value to format it as inline text; it never asks any registry if it should replace the entire root tree layout.

To achieve exactly what you want—seamlessly hijacking the main view of the standard Detail Panel for concrete sample types—you would need to modify the DdsMonitor host architecture.

Here is exactly how you could add this extension point to the source code:

**1\. Create a Detail View Registry** Similar to the existing `ITypeDrawerRegistry` used for editing forms, you need a registry that maps a CLR Type to a read-only Blazor component.

```
public interface ISampleViewRegistry
{
    void Register(Type type, RenderFragment<SampleData> viewer);
    RenderFragment<SampleData>? GetViewer(Type type);
}
```

**2\. Expose it to Plugins** Add this new `ISampleViewRegistry` to the `IMonitorContext` so that plugins can register their custom UI fragments during the `Initialize(IMonitorContext context)` phase.

**3\. Modify** **DetailPanel.razor** Inject the new registry into `DetailPanel.razor` and modify the `RenderTreeView()` method to check for a custom viewer before falling back to the default tree:

```
private RenderFragment RenderTreeView() => @<text>
    @if (_currentSample != null)
    {
        var customViewer = SampleViewRegistry.GetViewer(_currentSample.TopicMetadata.TopicType);
        if (customViewer != null)
        {
            @customViewer(_currentSample)
        }
        else 
        {
            var traversal = new HashSet<object>(ReferenceEqualityComparer.Instance);
            <div class="detail-tree">
                @RenderNode(_currentSample.Payload, _currentSample.TopicMetadata.ShortName, string.Empty, traversal, 0)
            </div>
        }
    }
</text>;
```

By making this architectural change, any plugin could completely take over the first tab of the `DetailPanel` to render a custom dashboard, gauge, or stylized form for its specific concrete types, while still benefiting from all the standard Detail Panel features (like the JSON tab, Sample Info tab, and context menus).

pls detail your idea of the context menu registry

To implement a unified context menu registry, we need to bridge the existing global `ContextMenuService` with the plugin initialization pipeline. Currently, right-click menus are hardcoded directly into the UI components; for example, the `SamplesPanel` explicitly creates a `List<ContextMenuItem>` with options like "Show Detail" and "Clone to Send/Emulator" inside its `HandleRowMouseDown` method, and the `ContextMenuService` simply renders whatever items it is handed.

Here is exactly how the architecture could be updated to support plugin-injected context menus:

**1\. Create the** **IContextMenuRegistry** **Interface** We need a strongly-typed registry that allows plugins to provide menu items based on the specific type of data the user right-clicked.

```
public interface IContextMenuRegistry
{
    // Allows a plugin to register a function that yields custom menu items
    void RegisterProvider<TContext>(Func<TContext, IEnumerable<ContextMenuItem>> provider);

    // Used by the UI panels to retrieve all injected items for a given context
    IEnumerable<ContextMenuItem> GetItems<TContext>(TContext context);
}
```

The `TContext` type parameter is crucial because different panels operate on different data structures. For example:

-   The Topic Explorer panel would pass `TopicMetadata`.-   The Samples panel would pass `SampleData`.-   The Instances panel would pass `InstanceData` or `InstanceRow`.-   The Detail panel would pass the raw `object` value of a specific field.

**2\. Expose the Registry to Plugins** The new registry would be added to the `IMonitorContext` interface, allowing any class implementing `IMonitorPlugin` to register its providers during the `Initialize` phase.

```
public interface IMonitorContext
{
    IMenuRegistry MenuRegistry { get; }
    PluginPanelRegistry PanelRegistry { get; }
    IContextMenuRegistry ContextMenuRegistry { get; } // NEW
}
```

**3\. Example Plugin Usage** With this API, the ECS plugin could easily inject a domain-specific action whenever a user right-clicks a raw sample anywhere in the application:

```
public void Initialize(IMonitorContext context)
{
    context.ContextMenuRegistry.RegisterProvider<SampleData>(sample => 
    {
        if (sample.TopicMetadata.ShortName.StartsWith("ECS"))
        {
            yield return new ContextMenuItem(
                Label: "Inspect ECS Entity", 
                Icon: "🔍", 
                Action: () => OpenEcsInspector(sample)
            );
        }
    });
}
```

**4\. Update the Core UI Panels** Finally, the hardcoded UI panels would be updated to inject the `IContextMenuRegistry` and append any plugin-provided items to their default lists before showing the menu.

For instance, the `OpenRowContextMenu` method in `SamplesPanel.razor` would change from this:

```
private void OpenRowContextMenu(IndexedSample row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Detail (New Window)", null, () => ...),
        new("Clone to Send/Emulator", null, () => ...),
        new("Filter Out Topic", null, () => ...)
    };

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

To this:

```
private void OpenRowContextMenu(IndexedSample row, MouseEventArgs args)
{
    var items = new List<ContextMenuItem>
    {
        new("Show Detail (New Window)", null, () => ...),
        new("Clone to Send/Emulator", null, () => ...),
        new("Filter Out Topic", null, () => ...)
    };

    // NEW: Query the registry for plugin actions targeting SampleData
    var pluginItems = ContextMenuRegistry.GetItems(row.Sample);
    if (pluginItems.Any())
    {
        items.Add(new ContextMenuItem("── Plugins ──", null, null)); // Separator
        items.AddRange(pluginItems);
    }

    ContextMenuService.Show(new ContextMenuState(items, args.ClientX, args.ClientY));
}
```

By implementing this registry pattern, plugins can seamlessly extend the application's core data-exploration workflows without needing to build completely separate windows.

how to make the plugin api proof to future extensions? decouple it, somehow negotiate the extension points or something similar? allowing using new plugin with older versions of dds monitor; or older one with newer dds monitro no longer supporting some old etension points?

To make the plugin API robust against future extensions and version mismatches, you must move away from rigid, hardcoded interfaces and adopt a **Capability Querying** (or Feature Provider) architecture.

Currently, adding a new registry to the `IMonitorContext` interface (like `IContextMenuRegistry`) breaks binary compatibility. An older plugin compiled against the old interface would throw a `MissingMethodException` when loaded into a new host, and a new plugin would fail to load in an old host.

While the current source code does not implement this yet, here is exactly how you can refactor the existing C# architecture to negotiate extension points and achieve forward and backward compatibility:

1\. Shift to a "Feature Provider" Pattern

Instead of hardcoding every extension point as a property on `IMonitorContext`, expose a way to query for capabilities dynamically. Since DdsMonitor heavily utilizes `Microsoft.Extensions.DependencyInjection`, you can expose the `IServiceProvider` or a dedicated `GetFeature<T>` method.

**Redesigning the Context:**

```
public interface IMonitorContext
{
    // The plugin asks for what it needs at runtime
    TFeature? GetFeature<TFeature>() where TFeature : class;
    
    // Expose the raw DI provider for advanced decoupling
    IServiceProvider Services { get; }
}
```

**How this ensures compatibility:**

-   **New Plugin on Old Host:** The new plugin calls `context.GetFeature<IContextMenuRegistry>()`. Because the old host doesn't know what this is, it returns `null`. The plugin detects this and **gracefully degrades** (e.g., it registers its standalone panel but skips adding context menus).-   **Old Plugin on New Host:** The old plugin only ever asks for `context.GetFeature<IMenuRegistry>()` and `context.GetFeature<PluginPanelRegistry>()`. The new host still provides these, so the plugin works perfectly without needing to know about the newer extension points.

2\. Event-Driven Decoupling via `IEventBroker`

You can decouple UI actions from strict API contracts by heavily leaning on the existing `IEventBroker`.

Instead of forcing a plugin to register a strongly-typed UI component into a specific host panel, the host can simply broadcast lifecycle and UI events.

-   **Future Proofing:** If a new version of DDS Monitor introduces a "Timeline View", it can simply start broadcasting `TimelineSelectionEvent`.-   Older plugins will ignore it because they don't subscribe to it.-   Newer plugins can subscribe to it via the broker without the host needing to explicitly define a `TimelinePluginAPI`.

3\. Feature-Specific Plugin Interfaces

Instead of putting all initialization logic into a single `Initialize(IMonitorContext context)` method, the host can use interface segregation to check what a plugin is capable of.

```
// Core required interface
public interface IMonitorPlugin {
    string Name { get; }
    void ConfigureServices(IServiceCollection services);
}

// Optional extension interface
public interface IHasContextMenus {
    void RegisterContextMenus(IContextMenuRegistry registry);
}

// Optional extension interface
public interface IHasCustomSettings {
    void ConfigureSettings(ISettingsRegistry registry);
}
```

During startup, the host's `PluginLoader` checks if the loaded plugin implements these specific interfaces.

-   If a new host sees an old plugin, it sees it doesn't implement `IHasCustomSettings` and just skips that initialization step.-   If a new plugin is loaded into an old host, the old host only looks for `IMonitorPlugin` and safely ignores the extra interfaces it doesn't understand.

4\. Isolate the API Contract

Currently, `IMonitorPlugin` and `IMonitorContext` are defined directly inside `DdsMonitor.Engine.Plugins`.

To truly future-proof the API, these interfaces should be extracted into a separate, highly stable NuGet package (e.g., `DdsMonitor.Extensibility`). This assembly should contain _only_ interfaces and pure data transfer objects (like the `SampleData` record). By keeping this contract assembly lightweight and strictly versioned (following Semantic Versioning), you guarantee that plugin developers compile against a stable ABI (Application Binary Interface) that does not drag in the heavy dependencies of the main engine.

can it be done without external higly stable assembly? nothing is highly stable. maybe some more relaxed string matching or something?

Yes, it is absolutely possible to build a resilient, future-proof plugin system without relying on a highly stable external contract assembly. In fact, the DdsMonitor source code already heavily utilizes "relaxed string matching" and weak typing to survive assembly version mismatches in other parts of its architecture.

You can apply these exact same principles to your plugin API. Here is how you can achieve a decoupled extension system using techniques already present in the codebase:

1\. "Duck Typing" via String-Based Reflection

Instead of forcing plugins to implement a strict `IHasContextMenus` interface from a shared assembly, the host can use string-based reflection to check if a plugin has a specific method.

The DdsMonitor codebase already uses this exact pattern for its serialization engine. Instead of forcing DDS message types to inherit from a base class, the `DdsWriter` and `DdsReader` look for methods by their string names (e.g., `"GetNativeSize"` or `"MarshalToNative"`) using `GetMethod()`.

You could do the same in the `PluginLoader`:

```
// Inside PluginLoader.cs
var contextMenuMethod = pluginType.GetMethod("GetContextMenuItems", BindingFlags.Public | BindingFlags.Instance);
if (contextMenuMethod != null)
{
    // The plugin has the method, we can safely invoke it dynamically!
    var items = contextMenuMethod.Invoke(pluginInstance, new object[] { clickedTopicName });
}
```

If an older plugin doesn't have the `"GetContextMenuItems"` method, it simply returns null and the host safely ignores it without throwing a `MissingMethodException`.

2\. Relaxed Type Name Matching (The ME2-T25-B Pattern)

If your plugins need to spawn panels or request types, relying on fully qualified assembly names (AQNs) will break as soon as version numbers change.

DdsMonitor already solves this using a relaxed string matching fallback known internally as the **ME2-T25-B** pattern. In `Desktop.razor` and `TopicPropertiesPanel.razor`, if a strict `Type.GetType()` fails, the code intentionally strips out the strict assembly version and culture tags by cutting the string at the first comma:

```
// Extract just the FullName (stripping version/assembly info)
var commaIndex = componentTypeName.IndexOf(',');
if (commaIndex > 0)
{
    fullName = componentTypeName[..commaIndex].Trim();
}
// Scan all loaded assemblies using just the string name
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    var found = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
    if (found != null) return found;
}
```

By adopting this, plugins can request host services or UI components purely by their `FullName` string, insulating them from underlying NuGet or DLL version bumps.

3\. Dictionary-Based State Passing

Rather than passing strongly-typed configuration objects between the host and plugins, you can use `Dictionary<string, object>`.

This is exactly how the `IWindowManager` currently spawns windows. The `SpawnPanel` method accepts a `string componentTypeName` and a `Dictionary<string, object>? initialState`.

-   When the ECS plugin spawns an Entity Detail panel, it simply passes `["EntityId"] = entity.EntityId` as a dictionary string key.-   The host then dynamically attempts to convert those dictionary values into the Blazor component parameters using `TryConvertParameterValue`.

If you expand the Plugin API to allow plugins to pass `Dictionary<string, object>` to the host for custom settings or menu definitions, you completely eliminate the need for shared data-transfer structs.

4\. String-Path Registries

You can define UI extension points entirely via strings. The current `IMenuRegistry` already does this: plugins add menus by passing a slash-delimited string path like `"Plugins/ECS"` rather than dealing with nested object trees.

You could implement future extension points the exact same way:

-   `Host.RegisterHook("ContextMenu/TopicPanel", myCallback);`-   `Host.RegisterHook("Export/CustomFormat", myCallback);`

By using weak dictionaries, string-based reflection (Duck Typing), and relaxed type resolution, your plugins and the DdsMonitor host can evolve completely independently. If a string hook or method name isn't found, the system gracefully ignores it.

what about for exmaple accessing sample metadata or daat of some other element? again hardcoded contract interface is not desired. Some kind of key-value disctionary, maybe a 'dynamic' type?

Using a key-value dictionary is an excellent architectural choice for decoupling, and it is exactly how the DdsMonitor host already passes arbitrary state to UI components. When the `IWindowManager` spawns a panel, it accepts and stores a `Dictionary<string, object>` as the `ComponentState`.

However, relying on a `Dictionary<string, object>` or a `dynamic` type to pass **high-frequency sample data** or metadata (which can arrive at thousands of messages per second) would generate massive amounts of garbage collection overhead and DLR (Dynamic Language Runtime) penalty.

To solve this, the DdsMonitor architecture already includes a highly optimized "schema and getter" pattern that acts like a strongly-typed, zero-allocation dictionary. You can expose this to plugins to achieve the exact decoupling you want:

**1\. The** **FieldMetadata** **and** **TopicMetadata** **Pattern** Instead of giving the plugin a hardcoded `SampleData` interface, the host passes an opaque `object` alongside a `TopicMetadata` definition. The `TopicMetadata` contains a collection called **AllFields**, which lists every available data point as a **FieldMetadata** object.

**2\. String-Based Paths (****StructuredName****)** Each `FieldMetadata` has a `StructuredName` property. This acts as the string key in your conceptual dictionary. Plugins can look for data simply by asking for the string path, such as `"Position.X"` or `"Speed"`.

**3\. High-Performance Compiled Getters** Instead of the slow late-binding of the `dynamic` keyword, each `FieldMetadata` provides a pre-compiled **Func<object, object?> Getter** delegate. Behind the scenes, the engine builds and compiles an expression tree for this getter when the topic is first discovered. This allows the plugin to extract values from an unknown `object` payload with near-native execution speed.

**4\. "Synthetic" Fields for Metadata** The beauty of this system is that it does not just apply to the DDS payload; it already applies to the sample metadata as well. The engine automatically creates **"Synthetic" fields** for metadata like `Timestamp`, `Ordinal`, and `Topic`.

For example, the `Timestamp` metadata is injected into `AllFields` as a synthetic field with its own compiled getter.

How a Plugin Would Use This

If a plugin wanted to read the `Timestamp` metadata and a `Position.X` payload value without knowing any compile-time interfaces, it would simply do this:

```
// 1. Plugin finds the definitions by string key (done once)
var timeField = topicMeta.AllFields.First(f => f.StructuredName == "Timestamp");
var posXField = topicMeta.AllFields.First(f => f.StructuredName == "Position.X");

// 2. Plugin extracts the data from an opaque object (done per sample)
object timestampValue = timeField.Getter(opaqueSampleObject);
object posXValue = posXField.Getter(opaquePayloadObject);
```

By exposing data through `TopicMetadata` and `FieldMetadata` rather than hardcoded interfaces, your plugins can dynamically explore, read, and write to any data element or metadata property purely via string paths and fast delegates, making them completely immune to future changes in the underlying data contracts.

that sound good! pls summarize what we discussed so far

1.  potential extension points
2.  the concepts for highly decoupled flexible API

**1\. Potential Extension Points**

-   **Unified Context Menu Registry:** Exposing an `IContextMenuRegistry` so plugins can inject custom right-click actions into core panels (like the Topic Explorer, Samples Panel, and Instances Panel) based on contextual data objects such as `SampleData` or `TopicMetadata`.-   **Detail View Hijacking:** Introducing an `ISampleViewRegistry` that allows plugins to completely replace the default tree layout in the Sample Detail panel with custom Blazor dashboards tailored to specific concrete payload types.-   **Data Formatting and Editing:** Opening access to the internal `IValueFormatterRegistry` to allow custom inline text and token rendering for specific data types, and the `ITypeDrawerRegistry` for injecting custom UI input controls for editing payloads.-   **Custom Data Exporting:** Expanding the `IExportService` to allow plugins to register custom streaming converters, enabling bulk data dumps into formats like CSV or proprietary binaries, rather than just JSON.-   **Advanced UI & Processing Hooks:** Allowing plugins to register domain-specific search macros via the `IFilterCompiler`, rich HTML popups via the `TooltipService`, and programmatic topic coloring rules via the `TopicColorService`.-   **Unified Settings Management:** Enabling plugins to inject their configuration objects into the host's `WorkspacePersistenceService`, saving everything neatly into the main application workspace file.

**2\. Concepts for a Highly Decoupled, Flexible API**

-   **Feature Provider (Capability Querying):** Moving away from hardcoded interface properties and instead using a `GetFeature<T>()` or `IServiceProvider` pattern. Plugins dynamically ask the host for capabilities at runtime, allowing them to gracefully degrade if loaded into an older host version.-   **Event-Driven Decoupling:** Relying heavily heavily on the `IEventBroker` to broadcast lifecycle and UI events. Plugins can subscribe to events (like a user selecting a sample) without needing rigid, highly-versioned API hooks.-   **Duck Typing via String Reflection:** Using string-based reflection to check if a plugin exposes certain extension methods before invoking them. This prevents `MissingMethodException` crashes when mixing new plugins with old hosts.-   **Relaxed Type Matching (The ME2-T25-B Pattern):** Resolving plugin types and UI components by their `FullName` string while deliberately stripping out strict assembly versions or culture tags, ensuring updates to underlying DLLs don't break compatibility.-   **Dictionary-Based State Passing:** Spawning windows and passing configuration state using a simple `Dictionary<string, object>`. This completely eliminates the need for shared, strictly-typed data transfer objects between the host and plugins.-   **String-Path Registries:** Defining UI extension points using straightforward slash-delimited string paths (e.g., `"Plugins/ECS"`) instead of nested object trees.-   **The Schema and Getter Pattern:** To handle high-frequency sample data without introducing massive dynamic language runtime (DLR) penalties or garbage collection overhead, plugins explore opaque payload objects using `TopicMetadata` and `FieldMetadata`. They locate the data they need via string paths (`StructuredName`) and extract it using pre-compiled, fast-execution `Getter` delegates.

how can we enable the plugins to save their settings to the same file as the rest of ddsmonitor, while keeping the decoupled flexible API architecture?

To enable plugins to save their global settings directly into the main `workspace.json` file while maintaining the decoupled, future-proof architecture we discussed, you can combine the **Event-Driven Decoupling** and **Dictionary-Based State Passing** patterns.

Currently, DdsMonitor serializes the workspace purely as a `List<PanelState>`, and the ECS plugin is forced to manually read/write its own `ecs-settings.json` file via its `EcsSettingsPersistenceService`.

Here is how you can seamlessly integrate plugin settings into the main workspace file without introducing rigid API contracts:

1\. Upgrade the Workspace JSON Structure

First, the host's `IWindowManager.SaveWorkspaceToJson` needs to be updated to serialize a root object containing both panels and plugin data, rather than just an array of panels.

The new `workspace.json` format would look like this:

```
{
  "Panels": [ ... ],
  "PluginSettings": {
    "ECS": {
      "NamespacePrefix": "company.ECS.",
      "EntityIdPattern": "(?i)\\bEntityId\\b"
    }
  }
}
```

2\. The Decoupled Hook: EventBroker

Instead of forcing plugins to implement an `IHasSettings` interface, you can leverage the existing `IEventBroker`. The host will broadcast events when the workspace is being saved or loaded.

You would define two simple event records in the host:

```
// Fired by the host just before serializing workspace.json
public sealed record WorkspaceSavingEvent(Dictionary<string, object> PluginSettings);

// Fired by the host immediately after parsing workspace.json
public sealed record WorkspaceLoadedEvent(IReadOnlyDictionary<string, object> PluginSettings);
```

Inside `WindowManager.SaveWorkspaceToJson()`, the host gathers the data:

```
var pluginSettings = new Dictionary<string, object>(StringComparer.Ordinal);
_eventBroker.Publish(new WorkspaceSavingEvent(pluginSettings));

// Serialize both the filtered panels and the populated pluginSettings...
```

3\. Dictionary-Based State Passing

To remain decoupled, plugins will not pass their strongly typed objects (like `EcsSettingsDto`) to the host. Instead, they convert their settings into a `Dictionary<string, object>`.

The beauty of this is that the DdsMonitor host already has a highly robust `TrySanitizeValue` method used for panel states, which recursively sanitizes and safely serializes `Dictionary<string, object>` into JSON. The host doesn't need to know anything about the ECS plugin's schema to save it perfectly.

4\. Refactoring the ECS Plugin

With this architecture, you can completely delete the file I/O logic in the `EcsSettingsPersistenceService`. Instead, the plugin simply subscribes to the workspace events during initialization.

Here is how the ECS plugin would interact with the host:

```
public class EcsSettingsManager : IDisposable
{
    private readonly EcsSettings _settings;
    private readonly IDisposable _saveSub;
    private readonly IDisposable _loadSub;

    public EcsSettingsManager(EcsSettings settings, IEventBroker broker)
    {
        _settings = settings;
        // Subscribe to the decoupled events
        _saveSub = broker.Subscribe<WorkspaceSavingEvent>(OnWorkspaceSaving);
        _loadSub = broker.Subscribe<WorkspaceLoadedEvent>(OnWorkspaceLoaded);
    }

    private void OnWorkspaceSaving(WorkspaceSavingEvent evt)
    {
        // 1. Pack settings into a generic dictionary
        var mySettings = new Dictionary<string, object>
        {
            ["NamespacePrefix"] = _settings.NamespacePrefix,
            ["EntityIdPattern"] = _settings.EntityIdPattern
        };

        // 2. Hand it to the host
        evt.PluginSettings["ECS"] = mySettings;
    }

    private void OnWorkspaceLoaded(WorkspaceLoadedEvent evt)
    {
        // 1. Check if the host has our settings
        if (evt.PluginSettings.TryGetValue("ECS", out var raw) && 
            raw is Dictionary<string, object> dict) // Or JsonElement depending on deserializer
        {
            // 2. Unpack the dictionary back into the live EcsSettings
            if (dict.TryGetValue("NamespacePrefix", out var ns))
                _settings.NamespacePrefix = ns.ToString();
        }
    }
}
```

Why this is Future-Proof

-   **Backward Compatible:** If an older plugin doesn't know about `WorkspaceSavingEvent`, it just ignores it and continues doing its own file I/O.-   **Forward Compatible:** If you load a new plugin into an older DdsMonitor host that doesn't fire `WorkspaceSavingEvent`, the plugin's `OnWorkspaceSaving` callback simply never runs. You could easily add a fallback timeout in the plugin to revert to local file saving if the host doesn't broadcast the load event at startup.-   **No Contract Dependencies:** Because the data is passed as a `Dictionary<string, object>` over the `IEventBroker`, the plugin API contract requires zero new interfaces or shared data transfer objects.

the plugins shoudl not load automatically just because the dll is present. DdsMonitor needs some plugin manager panel similar to the Topic Sources panel. Here the user can see what plugins are avaialble and can individually enable or disable them (might requiring restart of the app to apply the changes - this is acceptable); list of enabled plugin is saved to dds monitor settings.

To implement a "Plugin Manager" that stops plugins from loading automatically and allows users to explicitly enable/disable them, we need to transition the plugin architecture from a **"Load Everything"** model to a **"Discover All, Activate Selected"** model.

Since `ConfigureServices` modifies the host's Dependency Injection container before the application is built, changing a plugin's state will indeed require an application restart.

Here is how you can implement this feature using the application's existing design patterns:

1\. Create a Plugin Configuration Service

Instead of saving to the read-only `appsettings.json`, we should mimic the `AssemblySourceService` pattern. We will create a JSON file in the user's `AppData` directory to store the names of enabled plugins.

```
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DdsMonitor.Engine.Plugins;

public class PluginConfigService
{
    private readonly string _configFilePath;
    public HashSet<string> EnabledPlugins { get; private set; }

    public PluginConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DdsMonitor");
        Directory.CreateDirectory(dir);
        _configFilePath = Path.Combine(dir, "enabled-plugins.json");
        
        Load();
    }

    public void Save(HashSet<string> enabledPlugins)
    {
        EnabledPlugins = enabledPlugins;
        var json = JsonSerializer.Serialize(EnabledPlugins);
        File.WriteAllText(_configFilePath, json);
    }

    private void Load()
    {
        if (File.Exists(_configFilePath))
        {
            try
            {
                var json = File.ReadAllText(_configFilePath);
                EnabledPlugins = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new();
                return;
            }
            catch { }
        }
        EnabledPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
```

2\. Update `PluginLoader` for Two-Phase Loading

Currently, `PluginLoader` instantiates every `IMonitorPlugin` it finds and immediately calls `ConfigureServices`. We need to modify it so it safely instantiates the plugin just to read its `Name` and `Version`, but only activates it if it exists in our config file.

```
// Add a DTO to track discovery state
public class DiscoveredPlugin
{
    public IMonitorPlugin Instance { get; set; } = null!;
    public string AssemblyPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public sealed class PluginLoader
{
    // Existing fields...
    private readonly List<DiscoveredPlugin> _discovered = new();
    public IReadOnlyList<DiscoveredPlugin> DiscoveredPlugins => _discovered;

    public void LoadPlugins(IServiceCollection services)
    {
        var config = new PluginConfigService(); // Load user choices
        
        foreach (var directory in _pluginDirectories)
        {
            if (!Directory.Exists(directory)) continue;

            foreach (var dllPath in Directory.EnumerateFiles(directory, "*.dll"))
            {
                // Instantiate the load context and find IMonitorPlugin types [4]
                var assembly = /* ... load assembly ... */;
                foreach (var type in GetExportedTypesSafe(assembly))
                {
                    if (!typeof(IMonitorPlugin).IsAssignableFrom(type) || type.IsAbstract) continue;

                    var plugin = (IMonitorPlugin)Activator.CreateInstance(type)!;
                    bool isEnabled = config.EnabledPlugins.Contains(plugin.Name);
                    
                    _discovered.Add(new DiscoveredPlugin { 
                        Instance = plugin, 
                        AssemblyPath = dllPath, 
                        IsEnabled = isEnabled 
                    });

                    // Only mutate the host DI container if the user enabled it
                    if (isEnabled)
                    {
                        plugin.ConfigureServices(services);
                        _plugins.Add(plugin); 
                    }
                }
            }
        }
    }
    // InitializePlugins() remains unchanged, as it iterates over the activated _plugins list [6]
}
```

3\. Create the `PluginManagerPanel.razor` UI

Using the CSS and BEM structure from the `TopicSourcesPanel` as a template, we can build the UI window where users enable or disable the discovered plugins.

```
@using DdsMonitor.Engine.Plugins
@implements IDisposable

@inject PluginLoader PluginLoader

<div class="topic-sources">
    <div class="topic-sources__toolbar">
        <span class="topic-sources__title">Plugin Manager</span>
        @if (_requiresRestart)
        {
            <span class="topic-sources__badge topic-sources__badge--error" title="Restart DdsMonitor to apply">
                Restart Required
            </span>
        }
    </div>

    <div class="topic-sources__detail-list">
        @if (_plugins.Count == 0)
        {
            <div class="topic-sources__empty">No plugins discovered in the plugins directory.</div>
        }
        else
        {
            <table class="topic-sources__detail-table">
                <thead>
                    <tr>
                        <th>Enable</th>
                        <th>Plugin Name</th>
                        <th>Version</th>
                        <th>Path</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var plugin in _plugins)
                    {
                        <tr>
                            <td>
                                <input type="checkbox" checked="@plugin.IsEnabled" 
                                       @onchange="(e) => TogglePlugin(plugin, e)" />
                            </td>
                            <td>
                                <span class="topic-sources__clr-name">@plugin.Instance.Name</span>
                            </td>
                            <td>@plugin.Instance.Version</td>
                            <td>
                                <span class="topic-sources__clr-ns">@plugin.AssemblyPath</span>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>

@code {
    private List<DiscoveredPlugin> _plugins = new();
    private bool _requiresRestart = false;

    protected override void OnInitialized()
    {
        _plugins = PluginLoader.DiscoveredPlugins.ToList();
    }

    private void TogglePlugin(DiscoveredPlugin plugin, ChangeEventArgs e)
    {
        plugin.IsEnabled = (bool)(e.Value ?? false);
        _requiresRestart = true;
        
        // Save the new state to disk
        var config = new PluginConfigService();
        var enabledNames = new HashSet<string>(_plugins.Where(p => p.IsEnabled).Select(p => p.Instance.Name));
        config.Save(enabledNames);
    }
}
```

4\. Wire it up in the App Shell

Finally, to make this new panel accessible, register it in `MainLayout.razor` next to the `Topic Sources...` menu item:

```
<div class="app-menu__dropdown">
    <button type="button" class="app-menu__dropdown-item" @onclick="OpenTopicSourcesPanel">Topic Sources…</button>
    <button type="button" class="app-menu__dropdown-item" @onclick="OpenPluginManagerPanel">Plugin Manager…</button>
    <div class="app-menu__dropdown-separator"></div>
    ...
```

In the `@code` block of `MainLayout.razor`, add the spawner:

```
private void OpenPluginManagerPanel()
{
    CloseMenus();
    var typeName = typeof(PluginManagerPanel).FullName!;
    // Check if already open and bring to front, otherwise spawn...
    var panel = WindowManager.SpawnPanel(typeName);
    panel.Title = "Plugin Manager";
    panel.Width = 640;
    panel.Height = 400;
}
```

This effectively stops zero-day loading of foreign DLLs. `Activator.CreateInstance()` is still called on the plugin class to securely read the interface properties, but no business logic (`ConfigureServices` or `Initialize`) executes until the user explicitly checks the box and restarts the host application.

I would also like to have a sample demo/test plugin that uses all the extension points and demontrates how they are used;

I also need an autonomously (CI friendly) way of testing all the extension points.

Based on the decoupled architecture we designed in our conversation and the existing codebase, here is how you can implement a "Kitchen Sink" demo plugin and establish an autonomous CI testing pipeline.

1\. The "Kitchen Sink" Demo Plugin

To demonstrate all the extension points we discussed (while remaining backward/forward compatible), you would create a `FeatureDemoPlugin`. This plugin uses the **Capability Querying** (`GetFeature<T>`) and **Event-Driven** (`IEventBroker`) patterns to safely hook into the application.

Here is what the C# implementation would look like:

```
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine;

namespace DdsMonitor.Plugins.FeatureDemo;

public class FeatureDemoPlugin : IMonitorPlugin
{
    public string Name => "Feature Demo Plugin";
    public string Version => "1.0.0";

    // 1. Register background services
    public void ConfigureServices(IServiceCollection services)
    {
        // Registers a background worker, just like the ECS plugin does [1].
        services.AddSingleton<DemoBackgroundProcessor>();
    }

    // 2. Hook into the UI and Extension Points
    public void Initialize(IMonitorContext context)
    {
        // A. Standard API: Add Top-Level Menus [2]
        context.MenuRegistry.AddMenuItem("Plugins/Demo", "Show Dashboard", () => OpenDashboard());

        // B. Standard API: Register Custom Windows [2]
        context.PanelRegistry.RegisterPanelType("Demo Dashboard", typeof(DemoDashboardPanel));

        // C. FUTURE-PROOF API: Capability Querying
        // Gracefully degrades if loaded into an older DdsMonitor version that lacks these registries.

        // Context Menus
        var contextMenuRegistry = context.GetFeature<IContextMenuRegistry>();
        if (contextMenuRegistry != null)
        {
            contextMenuRegistry.RegisterProvider<SampleData>(sample => 
            {
                if (sample.TopicMetadata.ShortName == "FlightPosition")
                {
                    yield return new ContextMenuItem("Track Flight", "✈️", () => TrackFlight(sample));
                }
            });
        }

        // Value Formatters (Inline Text/Tokens)
        var formatterRegistry = context.GetFeature<IValueFormatterRegistry>();
        formatterRegistry?.Register(new GeoPositionCustomFormatter());

        // Detail View Hijacking (Replacing the Tree View)
        var viewRegistry = context.GetFeature<ISampleViewRegistry>();
        viewRegistry?.Register(typeof(FlightPosition), new FlightDashboardViewer());

        // D. EVENT-DRIVEN API: Unified Settings Management
        // Uses the IEventBroker [3] to seamlessly save/load settings into workspace.json
        var broker = context.GetFeature<IEventBroker>();
        if (broker != null)
        {
            broker.Subscribe<WorkspaceSavingEvent>(evt => 
            {
                evt.PluginSettings["FeatureDemo"] = new Dictionary<string, object> { ["DemoMode"] = true };
            });
            
            broker.Subscribe<WorkspaceLoadedEvent>(evt => 
            {
                if (evt.PluginSettings.TryGetValue("FeatureDemo", out var settings))
                    ApplySettings(settings);
            });
        }
    }

    private void OpenDashboard() { /* Spawn via WindowManager */ }
    private void TrackFlight(SampleData sample) { /* Logic */ }
    private void ApplySettings(object settings) { /* Logic */ }
}
```

2\. Autonomous (CI-Friendly) Testing Strategy

To autonomously test all these extension points in a CI environment without requiring a human to click through the Blazor UI, you can leverage the existing `FeatureDemo` and `HeadlessMode` infrastructure already built into the codebase:

**Phase A: Generate Live Traffic Autonomously** The solution already includes a `FeatureDemo` application equipped with an `AutonomousDemoRunner`. In your CI pipeline, you can launch this app in autonomous mode to generate a continuous stream of realistic DDS traffic (Chat, Sensor, Stock, FlightRadar, and BlackBox data):

```
dotnet run --project examples\FeatureDemo\FeatureDemo.csproj --mode autonomous
```

**Phase B: Run DdsMonitor in Headless Mode** Instead of spinning up the Blazor UI (which requires a browser), DdsMonitor natively supports a `HeadlessMode` configuration. By setting `HeadlessMode` to `Record`, the `HeadlessRunnerService` will boot the dependency injection container, load your plugins via the `PluginLoader`, and ingest all the incoming autonomous traffic directly from the DDS network.

```
// In your CI appsettings.json
{
  "DdsSettings": {
    "HeadlessMode": "Record",
    "HeadlessFilePath": "ci-test-output.json",
    "PluginDirectories": ["ci_plugins"]
  }
}
```

This guarantees that your plugin's background workers (like the `DemoBackgroundProcessor`) are successfully processing real data without needing a UI.

**Phase C: CI Unit/Integration Testing (xUnit + bUnit)** To autonomously test the _UI extension points_ (like the Context Menu injection and Detail View hijacking), you do not need to run the full application. You can write standard xUnit tests utilizing **bUnit** (a testing framework for Blazor):

-   **Mock the Context:** Instantiate your `FeatureDemoPlugin` and pass it a mocked `IMonitorContext`. Assert that it successfully registered its formatters, context menus, and event subscriptions.-   **Test the Event Broker:** Resolve the `IEventBroker` in your test container and publish a mock `SampleSelectedEvent` or `WorkspaceSavingEvent`. Assert that the plugin reacts correctly.-   **Test Blazor Rendering (bUnit):** Pass a fake `SampleData` object to your custom `FlightDashboardViewer` Blazor component using bUnit, and assert that the resulting HTML output contains the expected domain-specific customized formatting.

