The DdsMonitor tool exposes a comprehensive feature set for interacting with DDS networks, underpinned by a highly modular, extensible Blazor-based architecture. Here is a breakdown of its core capabilities from a user perspective:

**Network Configuration & Topic Discovery**
*   **Multi-Participant Configuration:** Users can configure multiple DDS participants concurrently by specifying Domain ID and Partition Name pairs through the Participant Editor.
*   **Dynamic Topic Discovery:** The tool dynamically loads external DLL assemblies containing generated schema types (`TopicSourcesPanel`).
*   **Topic Explorer:** A master list provides an overview of all discovered topics, showing live metrics like sample counts, instances, and sparklines for frequency. Users can dynamically subscribe or unsubscribe from topics, or use "Subscribe All".

**Sample Visualization & Grid Layouts**
*   **Targeted and Global Views:** Users can open a `SamplesPanel` for a specific topic or a global "All Samples" view. 
*   **Data Grid Customization:** The grid uses virtualization for high performance. Users can pick custom columns, resize them, and toggle track mode to automatically scroll to the latest sample. Layouts can be exported and imported as `.samplepanelsettings` files.
*   **Filtering and Sorting:** The grid supports sorting by clicking headers and robust filtering. The tool includes a visual Filter Builder generating expressions (e.g., `Payload.Field == 42`) powered by Dynamic LINQ. 
*   **Transport Controls:** Global transport controls allow the user to Play (resume receiving), Pause (stop accepting new samples), and Reset (clear all stores and ordinal counters) the DDS bridge.

**Deep Inspection**
*   **Detail Panel:** When a sample is selected, the `DetailPanel` inspects the payload in multiple formats. Users can view data as a hierarchical collapsible tree, a flattened table, or raw JSON.
*   **Metadata & Sender Tracking:** Aside from the payload, the detail view surfaces DDS `SampleInfo` (timestamp, instance state) and extracted sender metadata (Process ID, Process Name, Machine IP) via receiver-only sender monitoring.
*   **Link/Detach Modes:** The detail panel can be "linked" to follow the active selection in a source grid, or detached/pinned to inspect a specific sample while continuing to browse.

**Data Injection & Playback**
*   **Sample Authoring:** The `SendSamplePanel` uses schema reflection to build a `DynamicForm` matching the topic's structure (handling nested types, unions, arrays, etc.). Users can manually author payloads and inject them into the network.
*   **Clone to Send:** Existing samples can be cloned directly into the send panel for rapid modification and re-transmission.
*   **Record and Replay:** Replay capabilities let users load exported JSON streams. Users can pause, step, jump to specific frames/times, and control playback speed. Samples can be replayed purely into the local UI store for inspection, or routed back into the live DDS network.

**Windowing & Workspace Management**
*   **Floating Window Manager:** The UI functions like a desktop environment with movable, resizable, and minimizable panels. Multiple instances of the same panel (e.g., two `SamplesPanels` for the same or different topics) are supported via unique panel IDs.
*   **Workspace Persistence:** The complete layout, active filters, selected columns, and subscription states are automatically debounced and saved to a `workspace.json` file. Users can also manually export/import workspace layouts.

**Plugin System & Extensibility**
*   The architecture is heavily decoupled, providing isolated `AssemblyLoadContext` environments for plugins. Key extension points include:
    *   **Custom Panels & Menus:** Plugins can register their own Blazor panels (`PluginPanelRegistry`) and inject items into the global top menu or row-level context menus.
    *   **Custom Sample Views:** The `ISampleViewRegistry` allows plugins to completely replace the default tree view for specific CLR types (e.g., the Feature Demo plugin rendering a custom "Demo Payload Viewer").
    *   **Value Formatters:** `IValueFormatterRegistry` allows plugins to provide Tier 1 syntax-highlighted inline representations of custom types (e.g., rendering `GeoCoord` objects seamlessly in grid cells).
    *   **Filter Macros & Export:** Plugins can inject custom macros into the Dynamic LINQ filter engine and register custom export formats.

**CLI & Headless Operations**
*   **Headless Mode:** The engine can run purely from the CLI without the Blazor UI (`HeadlessMode.Record` or `HeadlessMode.Replay`), piping live data straight to disk or streaming a file to the network.
*   **CLI Overrides:** Users can pass arguments (e.g., `--AppSettings:IncludeTopics`, `--NoBrowser true`) to override workspace configuration, enabling CI/CD integrations or constrained debugging sessions.

**Diagnostics & Statistics**
*   **Performance Counters:** A live statistics overlay tracks total samples, total bytes received, and bandwidth rates (MB/s). It also measures samples-per-second on the ingestion hot-path to diagnose bottlenecks.
*   **Devel Mode:** Allows developers to toggle high-frequency "self-sending" of mock data to stress-test the ingestion pipeline up to 10 kHz.






Now my DdsMonitor tool is based on blazor, see tools\DdsMonitor\DdsMonitor.Blazor. I would like to start developing a new project tools\DdsMonitor\DdsMonitor.Avalonia, where the app is strictly local, using Avalonia multiplatform framework. Using the existing tools\DdsMonitor\DdsMonitor.Engine, and replicating all of the user-perceived features of DdsMonitor.Blazor.

I would like to build it strictly as a plugin system, similar to for example how vs code is built. Meaning that everything is a plugin and the basic app is just a framework loading and running plugins. There is already some attempt to design the plugin api in the DdsMonitor.Blazor but this is far from holistic. The new system should be built as plugin from the ground up. With avalonia as the UI framwork. Every UI panel etc a plugin with lot's of extension points that can be extended by other plugins.

The plugins should be loaded on app start as assemblies from a dedicated folder (like now with the blazor version). A plugin assembly should be able to contain multiple plugins.

 How to approach it?





Transitioning from Blazor to a purely local Avalonia application with a VS Code-like strict plugin architecture is a fantastic approach. It will give you a highly modular, performant desktop app.

Since you already have `DdsMonitor.Engine` handling the heavy lifting (discovery, DDS bridging, data stores, basic plugin loading), your focus will be on replacing the Blazor rendering layer with an Avalonia shell and adapting the extensibility model to be UI-agnostic.

Here is a step-by-step architectural guide on how to approach building `DdsMonitor.Avalonia` from the ground up as a pure plugin host.

### Phase 1: Purify the Engine (Remove Blazor Bleed)

Currently, your `DdsMonitor.Engine` has slight dependencies on Blazor (e.g., `ISampleViewRegistry` uses `Microsoft.AspNetCore.Components.RenderFragment`, and `ITypeDrawerRegistry` uses `RenderFragment`).

1. **Decouple the UI:** Modify the `Engine` registries so they no longer reference Blazor. Instead of registering `RenderFragment`, plugins should register **ViewModels** (if using MVVM) or generic `object` types.
2. **Avalonia UI Extensibility Layer:** Create a new assembly (e.g., `DdsMonitor.Avalonia.Core`) that bridges the `Engine` and Avalonia. This will contain Avalonia-specific interfaces (like `IAvaloniaViewRegistry` where plugins can register Avalonia `Control` or `DataTemplate` objects).

### Phase 2: The "Empty Shell" Architecture

Like VS Code, your base Avalonia application should know almost nothing about DDS. It should simply be a bootstrapper.

1. **Host Setup:** Use `Microsoft.Extensions.Hosting` in your `Program.cs` / `App.axaml.cs` to set up the Dependency Injection container. This allows you to run all your existing background services (`DdsIngestionService`, `SelfSendService`) seamlessly.
2. **The Shell Window:** Create a `ShellWindow` containing:
   - A dynamic Main Menu bar.
   - A dynamic Toolbar/Activity Bar.
   - A **Docking Manager**. I highly recommend using **[Dock.Avalonia](https://github.com/wieslawsoltes/Dock)**, which provides the exact VS Code / Visual Studio style tearing, docking, and tabbed panel management out of the box.
3. **The Plugin Loader:** You can re-use your existing `PluginLoader` utilizing `AssemblyLoadContext`. Since it already iterates through `assembly.ExportedTypes` checking for `IMonitorPlugin`, it inherently supports multiple plugins per assembly.

### Phase 3: Extension Points Design

To make the application extensible from the ground up, the Shell must expose generic extension points. Using the existing `IMonitorContext.GetFeature<T>()`, you will expose registries such as:

- **`IMenuRegistry`**: Plugins push commands here. The Shell observes this and builds the Avalonia native `<Menu>`.
- **`IToolbarRegistry`**: Plugins push icons/commands (like "Play", "Pause", "Record"). The Shell renders these in the top or side activity bar.
- **`IViewRegistry`**: Maps a ViewModel to an Avalonia `Control`.
- **`IPanelRegistry` / `IWindowManager`**: Allows plugins to register dockable panels (e.g., "Explorer", "Bottom Panel").
- **`IContextMenuRegistry`**: Allows plugins to add right-click actions to specific data types (e.g., right-clicking a `SampleData` row).

### Phase 4: Implement Core Features as the "Default Plugin"

To ensure the plugin system is robust, force yourself to build the basic features of `DdsMonitor.Blazor` as plugins. Create a `DdsMonitor.Avalonia.StandardPlugin` assembly that implements `IMonitorPlugin`.

1. **Topic Explorer Plugin:** * Registers a `TopicExplorerViewModel` to the `IPanelRegistry` (Docked left).
   - Subscribes to `ITopicRegistry` to populate an Avalonia `TreeView` or `ListBox`.
2. **Samples Grid Plugin:**
   - Registers a `SamplesGridViewModel` to the `IPanelRegistry` (Docked center).
   - Uses Avalonia's `DataGrid` (or `TreeDataGrid`) to display the live DDS samples.
   - Reads from `ISampleStore`.
3. **Payload Inspector Plugin:**
   - Registers a `DetailViewModel` (Docked right/bottom).
   - Uses reflection (like your Blazor `DynamicForm`) to dynamically generate Avalonia controls (`TextBox`, `CheckBox`, `Expander`) based on the selected payload.

### Phase 5: Eventing & State Management

Because plugins are isolated, they should not reference each other directly.

1. **Event Broker:** Use your existing `IEventBroker` heavily. For example, when the *Topic Explorer Plugin* selects a topic, it publishes a `TopicSelectedEvent`. The *Samples Grid Plugin* subscribes to this event and updates its Avalonia `DataGrid` filter.
2. **MVVM Pattern:** Adopt the **CommunityToolkit.Mvvm** (ObservableObject, RelayCommand). It integrates perfectly with Avalonia and DI. Your Avalonia views will bind to properties on these ViewModels, meaning your background threads (`DdsIngestionService`) only need to update the ViewModel, and the UI updates automatically.

### Summary of Bootstrapping Flow

When the Avalonia app starts, the flow should look like this:

1. **Initialize DI:** `ServiceCollection` is created. Core engine services (EventBroker, Stores, Registries) are added.
2. **Discover Plugins:** `PluginLoader` scans `/plugins/*.dll`.
3. **Configure Services:** For each `IMonitorPlugin`, call `ConfigureServices(services)` so plugins can register their ViewModels.
4. **Build Host:** `IServiceProvider` is built. Background services start.
5. **Initialize Plugins:** For each plugin, call `Initialize(monitorContext)`. Plugins resolve the UI registries and register their Menus, Toolbars, and Dockable Panels.
6. **Show UI:** Avalonia renders the `ShellWindow`. The Shell queries the `IMenuRegistry` and `IPanelRegistry` to build the initial layout (Menus and Docking layout).







you mentioned the app should know nothing about dds. how the ddsMonitor.engine should be integrated? This is a central component that must be somehow made available to the plugins... How the plugins are going to access it? How the topic explorer will read the list of topics? How the SamplesGrid panel plugin will read samples?







When I say the Avalonia app (the "Shell") should know nothing about DDS, I mean it shouldn't contain any business logic, DDS bridging, or data processing. It shouldn't know what a `Topic` or a `Sample` is. The Shell's *only* job is window management, docking, menus, and hosting plugins.

However, the Shell is the **Host**. It owns the **Dependency Injection (DI) Container** (usually `IServiceProvider` from `Microsoft.Extensions.Hosting`).

The secret to connecting the "Dumb Shell" to the "Smart Plugins" via the `DdsMonitor.Engine` is **Shared Interfaces and Dependency Injection**.

Here is exactly how they wire together.

### 1. The Engine becomes a Shared Library

`DdsMonitor.Engine` acts as your domain layer. Both the Shell and the Plugins reference it.

- The Engine defines the interfaces: `ITopicRegistry`, `ISampleStore`, `IEventBroker`, etc.
- The Engine provides the background services that actually do the DDS work.

### 2. The Shell Sets Up the "World"

In your Avalonia app's entry point (e.g., `Program.cs` or `App.axaml.cs`), you configure the DI container. The Shell adds the Engine to the container, but it doesn't *use* the Engine itself.

C#

```
// Inside Avalonia App startup
var builder = Host.CreateApplicationBuilder(args);

// 1. Register the Core Engine (DDS services, Stores, Brokers)
builder.Services.AddDdsEngineCore(); // Extension method in your Engine assembly

// 2. Register Avalonia UI Registries
builder.Services.AddSingleton<IPanelRegistry, PanelRegistry>();
builder.Services.AddSingleton<IMenuRegistry, MenuRegistry>();

// 3. Discover and Register Plugins
var pluginLoader = new PluginLoader("plugins_folder");
pluginLoader.ConfigureServices(builder.Services);

var host = builder.Build();
host.Start(); // Starts DdsIngestionService in the background
```

### 3. How Plugins Access the Engine (Topic Explorer Example)

Because the Shell put the `Engine` services into the DI container, your plugins can simply ask for them via **Constructor Injection**.

Let's look at how the Topic Explorer plugin is built.

**Step A: The Plugin Registration**

C#

```
public class TopicExplorerPlugin : IMonitorPlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register the ViewModel
        services.AddTransient<TopicExplorerViewModel>();
    }

    public void Initialize(IServiceProvider provider)
    {
        var panelRegistry = provider.GetRequiredService<IPanelRegistry>();
        var viewModel = provider.GetRequiredService<TopicExplorerViewModel>();

        // The Shell will see this and render it in the Docking system
        panelRegistry.RegisterPanel("Topic Explorer", DockRegion.Left, viewModel);
    }
}
```

**Step B: The ViewModel gets the Engine Data**

Notice how the ViewModel asks for `ITopicRegistry`. The DI container provides the singleton instance running in the background.

C#

```
public partial class TopicExplorerViewModel : ObservableObject // From CommunityToolkit.Mvvm
{
    private readonly ITopicRegistry _topicRegistry; // Engine interface!
    private readonly IEventBroker _eventBroker;     // Engine interface!

    [ObservableProperty]
    private ObservableCollection<TopicItem> _topics = new();

    // The DI container injects the Engine components here
    public TopicExplorerViewModel(ITopicRegistry topicRegistry, IEventBroker eventBroker)
    {
        _topicRegistry = topicRegistry;
        _eventBroker = eventBroker;

        // Load initial
        LoadTopics();

        // Subscribe to live Engine updates
        _topicRegistry.TopicDiscovered += OnTopicDiscovered;
    }

    private void OnTopicDiscovered(object sender, TopicEventArgs e)
    {
        // Avalonia requires UI updates to happen on the UI thread
        Dispatcher.UIThread.InvokeAsync(() => {
            Topics.Add(new TopicItem(e.Topic));
        });
    }
    
    // Command bound to Avalonia TreeView selection
    [RelayCommand]
    private void SelectTopic(TopicItem topic)
    {
        // Tell the rest of the app a topic was clicked
        _eventBroker.Publish(new TopicSelectedEvent(topic.Name));
    }
}
```

### 4. How the Samples Grid Reads Samples

The exact same DI principle applies. The Samples Grid plugin has no idea *how* DDS gets the data; it just talks to the `ISampleStore` provided by the Engine.

C#

```
public partial class SamplesGridViewModel : ObservableObject
{
    private readonly ISampleStore _sampleStore;

    [ObservableProperty]
    private ObservableCollection<SampleData> _samples = new();

    public SamplesGridViewModel(ISampleStore sampleStore, IEventBroker eventBroker)
    {
        _sampleStore = sampleStore;

        // Listen to the EventBroker to know when the Topic Explorer changed selection
        eventBroker.Subscribe<TopicSelectedEvent>(OnTopicSelected);
    }

    private void OnTopicSelected(TopicSelectedEvent e)
    {
        // Query the Engine's sample store for the selected topic
        var history = _sampleStore.GetSamplesForTopic(e.TopicName);
        
        Samples.Clear();
        foreach(var sample in history)
        {
            Samples.Add(sample);
        }
        
        // You would also hook up to an event here to get new live samples
        // as they arrive in the engine.
    }
}
```

### Summary of the Architecture

1. **DdsMonitor.Engine**: Has no UI. It is a class library with interfaces (`ISampleStore`) and background logic (`DdsListener`).
2. **DdsMonitor.Avalonia (Shell)**: Has no DDS logic. It manages Windows and DI. It wires the `Engine` and `Plugins` together in `Program.cs`.
3. **Plugins**: Reference the `Engine` to get data, and reference the `Avalonia` abstraction layer to push UI to the screen.

By relying strictly on constructor injection (DI), your plugins get full access to the central DDS Engine, while the Avalonia Host remains blissfully ignorant of what the plugins are actually doing with the data.









Great! 
I would like to start with building the "empty shell" avalonia solution.
1. Base application, csharp .net8. That references all the avalonia nugets required, containing all the plugin loading and running infrastructure according to your suggestions (taking most parts from the ddsmonitor.blazor...), providing the UI parts you are describing (main menu, toolbar, dockable windows support, utilities like the message broker and all what you mentioned...)
2. Adding the DdsMonitor.Avalonia.StandardPlugin project containing
3. Adding some extra plugion as an example

what initial minimalistit content should we put to those parts to make the application run and load the plugins and use the ddsmonitor.engine and show some data from it, proving the concept is working?







This is a solid plan. To get a minimalistic Proof of Concept (POC) up and running that proves the Engine, Shell, and Plugins can talk to each other, we need to avoid getting bogged down in complex window management (like `Dock.Avalonia`) right away.

For step one, we will use a simple "Region" approach (like a left sidebar and a main content area) to prove the injection and rendering work. You can swap this for `Dock.Avalonia` once the data pipeline is proven.

Here is the minimalistic content you need for each part of the solution to make it run.

### 1. The Shared Extensibility Contract (`DdsMonitor.Avalonia.Core`)

Create a .NET 8 Class Library. Both the Shell and all Plugins will reference this. It defines how plugins talk to the Shell.

C#

```
// IMonitorPlugin.cs
using Microsoft.Extensions.DependencyInjection;

public interface IMonitorPlugin
{
    // 1. Called before the App builds the DI container. Add your ViewModels here.
    void ConfigureServices(IServiceCollection services);
    
    // 2. Called after the App starts. Register your UI components here.
    void Initialize(IServiceProvider provider);
}

// IPanelRegistry.cs
public enum PanelRegion { LeftSidebar, MainDocument }

public interface IPanelRegistry
{
    void RegisterPanel(string title, PanelRegion region, object viewModel);
    IEnumerable<object> GetPanels(PanelRegion region);
}
```

### 2. The Base Application Shell (`DdsMonitor.Avalonia`)

This is your Avalonia Desktop Application. It references `DdsMonitor.Engine` and `DdsMonitor.Avalonia.Core`.

**A. The Registry Implementation**

C#

```
// PanelRegistry.cs
using System.Collections.ObjectModel;

public class PanelRegistry : IPanelRegistry
{
    // Using ObservableCollection so Avalonia UI updates automatically when plugins register panels
    public ObservableCollection<object> LeftSidebarPanels { get; } = new();
    public ObservableCollection<object> MainDocumentPanels { get; } = new();

    public void RegisterPanel(string title, PanelRegion region, object viewModel)
    {
        if (region == PanelRegion.LeftSidebar) LeftSidebarPanels.Add(viewModel);
        else MainDocumentPanels.Add(viewModel);
    }

    public IEnumerable<object> GetPanels(PanelRegion region) => 
        region == PanelRegion.LeftSidebar ? LeftSidebarPanels : MainDocumentPanels;
}
```

**B. The Application Bootstrapper (`Program.cs`)**

This is where the magic happens. We combine the .NET Generic Host with Avalonia.

C#

```
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // 1. Register Engine (Mocking your existing Engine registration)
        // builder.Services.AddDdsEngineCore(); 
        
        // 2. Register Avalonia Core Registries
        builder.Services.AddSingleton<IPanelRegistry, PanelRegistry>();

        // 3. Load Plugins (Minimalist approach: hardcoded assembly load for the POC)
        // In reality, you'd use AssemblyLoadContext to scan a /plugins folder
        var plugins = new List<IMonitorPlugin>
        {
            new DdsMonitor.Avalonia.StandardPlugin.StandardPluginBootstrapper()
        };

        foreach (var plugin in plugins)
        {
            plugin.ConfigureServices(builder.Services);
        }

        var host = builder.Build();

        // 4. Initialize Plugins
        foreach (var plugin in plugins)
        {
            plugin.Initialize(host.Services);
        }

        // 5. Start Engine background tasks
        host.StartAsync();

        // 6. Start Avalonia UI, passing the DI container to the App
        BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
        => AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

**C. The Shell UI (`MainWindow.axaml`)**

A simple grid split into a left sidebar and a main area. It binds directly to the `PanelRegistry`.

XML

```
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="DdsMonitor.Avalonia.MainWindow"
        Title="DDS Monitor Plugin Host">

    <Grid ColumnDefinitions="250, 4, *">
        <ItemsControl Grid.Column="0" ItemsSource="{Binding LeftSidebarPanels}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <ContentControl Content="{Binding}" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <GridSplitter Grid.Column="1" Background="Gray" ResizeDirection="Columns"/>

        <ItemsControl Grid.Column="2" ItemsSource="{Binding MainDocumentPanels}">
             <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <ContentControl Content="{Binding}" />
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </Grid>
</Window>
```

### 3. The Standard Plugin (`DdsMonitor.Avalonia.StandardPlugin`)

This class library references `DdsMonitor.Engine` and `DdsMonitor.Avalonia.Core` (but *not* the Shell executable).

**A. The Plugin Entry Point**

C#

```
// StandardPluginBootstrapper.cs
using Microsoft.Extensions.DependencyInjection;

public class StandardPluginBootstrapper : IMonitorPlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register the ViewModel
        services.AddTransient<TopicExplorerViewModel>();
    }

    public void Initialize(IServiceProvider provider)
    {
        var registry = provider.GetRequiredService<IPanelRegistry>();
        var topicExplorerVm = provider.GetRequiredService<TopicExplorerViewModel>();

        // Inject the UI into the Shell's left sidebar
        registry.RegisterPanel("Topic Explorer", PanelRegion.LeftSidebar, topicExplorerVm);
    }
}
```

**B. The ViewModel (Consuming the Engine)**

C#

```
// TopicExplorerViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class TopicExplorerViewModel : ObservableObject
{
    // Assume ITopicRegistry comes from DdsMonitor.Engine
    // private readonly ITopicRegistry _engine; 

    [ObservableProperty]
    private ObservableCollection<string> _topics = new();

    public TopicExplorerViewModel(/* ITopicRegistry engine */)
    {
        // _engine = engine;
        
        // Mocking Engine Data for the POC
        Topics.Add("HelloWorld_Topic");
        Topics.Add("RobotTelemetry_Topic");
        Topics.Add("Command_Topic");
    }
}
```

**C. The View (Avalonia UI)**

Create an Avalonia `UserControl` named `TopicExplorerView.axaml` in the plugin project. Note: You will need to set up a basic Avalonia `ViewLocator` in the App so the Shell knows how to pair `TopicExplorerViewModel` to `TopicExplorerView`.

XML

```
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="DdsMonitor.Avalonia.StandardPlugin.Views.TopicExplorerView">
    <StackPanel Margin="10">
        <TextBlock Text="Discovered Topics" FontWeight="Bold" Margin="0,0,0,10"/>
        
        <ListBox ItemsSource="{Binding Topics}">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding}" />
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </StackPanel>
</UserControl>
```

### Summary of the POC

If you build and run this:

1. The App starts and creates the Host.
2. It manually loads `StandardPluginBootstrapper`.
3. The plugin registers `TopicExplorerViewModel` with mocked Engine data into the `PanelRegistry` under the `LeftSidebar` region.
4. The Avalonia `MainWindow` binds to the `PanelRegistry`.
5. Avalonia resolves the ViewModel to the `TopicExplorerView` and draws a ListBox of topics on the left side of the screen.

Once this runs successfully, you have proven the architecture. You can then replace the mocked Engine DI with your actual Engine services, implement dynamic `.dll` loading from a folder, and drop `Dock.Avalonia` into the `MainWindow`.









I will also need a flexible proven solution how the plugins can load/save their custom setting per user. Also how to give them access to command line arguments which might be specific to concrete plugins but some might be shared or "base" ones.

Best if the app could be able to run also in automatic non-interactive mode (like the existng blazor app can do), to replicate the ability to perform dds recording or playback, like a CLI tool.









To achieve a professional, VS Code-like architecture that supports user settings, command-line arguments, and a headless (CLI) mode, we need to lean heavily on the standard **.NET Generic Host** (`Microsoft.Extensions.Hosting`).

The beauty of using the Generic Host is that it already provides robust, proven solutions for all three of your requirements.

Here is how to design these three pillars in your `DdsMonitor.Avalonia` architecture.

------

### 1. Headless (CLI) Mode

To support an automatic, non-interactive mode (like DDS recording/playback), we need to separate the **background tasks** from the **UI tasks**.

In .NET, background tasks are built using `IHostedService` (or `BackgroundService`). Plugins register these services, and the Host runs them automatically, regardless of whether Avalonia is started.

**Modifying `Program.cs` to support headless mode:**

C#

```
internal class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args); // args are automatically parsed here!

        // 1. Setup Core, Registries, and load Plugins as before
        builder.Services.AddDdsEngineCore();
        LoadPlugins(builder.Services);

        var host = builder.Build();
        
        // 2. Initialize Plugins
        InitializePlugins(host.Services);

        // 3. Detect Headless Mode
        // Standard .NET Configuration automatically reads command line arguments
        var config = host.Services.GetRequiredService<IConfiguration>();
        bool isHeadless = config.GetValue<bool>("headless", false);

        if (isHeadless)
        {
            Console.WriteLine("Starting in headless mode. Press Ctrl+C to exit.");
            // Starts the engine, background services, recorders, etc.
            await host.RunAsync(); 
        }
        else
        {
            // Start the background services AND the Avalonia UI
            _ = host.StartAsync(); 
            BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
        }
    }
    
    // ... BuildAvaloniaApp method ...
}
```

Now, if you run `DdsMonitor.Avalonia.exe --headless=true`, the UI never loads, but all your plugin background services (like a `DdsRecorderService`) run perfectly.

------

### 2. Command Line Arguments

Because `Host.CreateApplicationBuilder(args)` automatically ingests command-line arguments into the `IConfiguration` system, both the Shell and the Plugins can access them instantly.

**How to format arguments:**

.NET Configuration expects key-value pairs using `--`.

```
DdsMonitor.Avalonia.exe --headless=true --RecordPlugin:Topic=RobotData --RecordPlugin:Duration=60
```

**How Plugins access them:**

A plugin doesn't need to parse the args string. It simply asks the DI container for `IConfiguration`.

C#

```
public class RecorderBackgroundService : BackgroundService
{
    private readonly string _targetTopic;
    private readonly int _duration;

    public RecorderBackgroundService(IConfiguration config)
    {
        // Reads from CLI args OR environment variables OR appsettings.json
        _targetTopic = config.GetValue<string>("RecordPlugin:Topic", "DefaultTopic");
        _duration = config.GetValue<int>("RecordPlugin:Duration", 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // If duration > 0, start headless recording logic...
    }
}
```

------

### 3. User Settings (Load/Save)

While `IConfiguration` is great for reading CLI args, it is strictly **read-only**. To allow plugins to save settings per user (like VS Code's `settings.json`), you need a writable abstraction.

Create a robust `IUserSettings` service in your `DdsMonitor.Avalonia.Core` assembly.

**Step A: The Interface**

C#

```
public interface IUserSettings
{
    // Gets a setting for a specific plugin, with a default fallback
    T Get<T>(string pluginId, string key, T defaultValue = default);
    
    // Sets a setting in memory
    void Set<T>(string pluginId, string key, T value);
    
    // Commits the settings to the user's hard drive
    Task SaveAsync();
}
```

**Step B: The Implementation (In the Shell)**

Save the settings in the user's AppData directory (e.g., `C:\Users\Name\AppData\Roaming\DdsMonitor\settings.json` on Windows, or `~/.config/DdsMonitor/settings.json` on Linux).

C#

```
using System.Text.Json;
using System.Text.Json.Nodes;

public class UserSettingsStore : IUserSettings
{
    private readonly string _settingsFilePath;
    private JsonObject _settingsData;

    public UserSettingsStore()
    {
        // Cross-platform standard for User Settings directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DdsMonitor");
        Directory.CreateDirectory(dir);
        
        _settingsFilePath = Path.Combine(dir, "settings.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_settingsFilePath))
        {
            var json = File.ReadAllText(_settingsFilePath);
            _settingsData = JsonSerializer.Deserialize<JsonObject>(json) ?? new JsonObject();
        }
        else
        {
            _settingsData = new JsonObject();
        }
    }

    public T Get<T>(string pluginId, string key, T defaultValue = default)
    {
        if (_settingsData.TryGetPropertyValue(pluginId, out var pluginNode) && pluginNode is JsonObject pluginObj)
        {
            if (pluginObj.TryGetPropertyValue(key, out var valueNode))
            {
                return valueNode.Deserialize<T>();
            }
        }
        return defaultValue;
    }

    public void Set<T>(string pluginId, string key, T value)
    {
        if (!_settingsData.ContainsKey(pluginId))
        {
            _settingsData[pluginId] = new JsonObject();
        }
        
        var pluginObj = _settingsData[pluginId] as JsonObject;
        pluginObj[key] = JsonSerializer.SerializeToNode(value);
    }

    public async Task SaveAsync()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_settingsData, options);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
}
```

**Step C: Registering it**

In `Program.cs`, add it as a Singleton:

C#

```
builder.Services.AddSingleton<IUserSettings, UserSettingsStore>();
```

**Step D: How a Plugin Uses Settings**

Let's say your `TopicExplorerPlugin` wants to remember if the user toggled "Show Hidden Topics".

C#

```
public partial class TopicExplorerViewModel : ObservableObject
{
    private readonly IUserSettings _settings;
    private const string PluginId = "TopicExplorer";

    [ObservableProperty]
    private bool _showHiddenTopics;

    public TopicExplorerViewModel(IUserSettings settings)
    {
        _settings = settings;
        
        // Load the saved state, default to false if not found
        ShowHiddenTopics = _settings.Get<bool>(PluginId, "ShowHiddenTopics", false);
    }

    // CommunityToolkit.Mvvm hook when a property changes
    partial void OnShowHiddenTopicsChanged(bool value)
    {
        // Save to memory
        _settings.Set(PluginId, "ShowHiddenTopics", value);
        
        // Fire and forget save to disk (or use a command/button to save globally)
        _ = _settings.SaveAsync(); 
    }
}
```

### Summary

1. **Headless:** Branch the `Program.cs` startup based on `isHeadless`. The `.NET Host` runs your DDS logic safely in the background without Avalonia.
2. **CLI Args:** Handled natively by `.NET Generic Host`. Plugins inject `IConfiguration` to read them effortlessly.
3. **Settings:** A singleton `IUserSettings` service serializes a global JSON file in the OS-native Application Data folder, allowing plugins isolated namespaces (`pluginId`) to read/write state persistently.





Based on the `DdsMonitor.Engine` and `DdsMonitor.Blazor` source code, I can see exactly how the existing command-line arguments are structured. You are currently using the standard .NET `IConfiguration` configuration binding.

Here are the existing command-line features and parameters you are using:

### 1. Existing Command-Line Parameters

**App Settings (`AppSettings.cs`)**

- `--AppSettings:WorkspaceFile="..."` (Loads a specific layout JSON)
- `--AppSettings:ConfigFolder="..."` (Overrides the `%APPDATA%` default directory)
- `--NoBrowser=true` or `--AppSettings:NoBrowser=true` (Prevents the Blazor app from spawning a Chrome/Edge window)
- `--AppSettings:IncludeTopics:0="..."` (Array indexing to selectively subscribe to specific topics)
- `--AppSettings:ExcludeTopics:0="..."` (Array indexing to ignore specific topics)
- `--AppSettings:TopicSources:0="..."` (Injects specific `.dll` paths for topic discovery)

**DDS Settings (`DdsSettings.cs`)**

- `--DdsSettings:DomainId=0` (The DDS Domain to monitor)
- `--DdsSettings:HeadlessMode=Record` (Runs in `None`, `Record`, or `Replay` mode)
- `--DdsSettings:HeadlessFilePath="..."` (Target file for recording/replaying)
- `--DdsSettings:ReplayRate=1.0` (Multiplier for playback speed)
- `--DdsSettings:FilterExpression="..."` (Startup filtering for headless/UI)
- `--DdsSettings:SelfSendEnabled=true` (For debugging/testing)

### 2. Are they compatible with the new architecture?

**Yes, 100% compatible.** In fact, they are perfectly tailored for it.

Because your existing Blazor app uses `Microsoft.AspNetCore.Builder.WebApplicationBuilder`, it relies on the exact same underlying configuration provider (`Microsoft.Extensions.Configuration.CommandLine`) that my suggested `Microsoft.Extensions.Hosting.HostApplicationBuilder` uses for the Avalonia shell.

This means the syntax (`--Section:Key=Value`), array indexers (`:0=`, `:1=`), and environment variable fallbacks will work **without changing a single line of your CLI scripts or deployment batch files.**

### 3. How to port them to the new architecture

Porting them is trivial. You don't need to write any custom string parsing.

**Step A: Keep the strongly-typed classes in the Engine**

Keep `AppSettings.cs` and `DdsSettings.cs` inside `DdsMonitor.Engine` (or `DdsMonitor.Avalonia.Core`).

**Step B: Bind them in the Avalonia `Program.cs`**

In the new Avalonia bootstrapper, you will map the `args` to these classes exactly as Blazor does:

C#

```
var builder = Host.CreateApplicationBuilder(args);

// 1. Automatically binds --AppSettings:xxx from args to your class
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));

// 2. Automatically binds --DdsSettings:xxx from args to your class
builder.Services.Configure<DdsSettings>(builder.Configuration.GetSection(DdsSettings.SectionName));
```

**Step C: How the Avalonia UI replaces `--NoBrowser`**

In the Blazor version, `--NoBrowser` stopped the `BrowserLifecycleService` from launching Chrome.

In Avalonia, you don't have a browser, but the parameter maps perfectly to controlling whether the Avalonia `MainWindow` should be rendered.

If `--DdsSettings:HeadlessMode=Record` is set, you bypass Avalonia entirely:

C#

```
var ddsSettings = builder.Configuration.GetSection(DdsSettings.SectionName).Get<DdsSettings>();

if (ddsSettings != null && ddsSettings.HeadlessMode != HeadlessMode.None)
{
    // Run CLI tool (Record/Replay)
    await host.RunAsync(); 
}
else
{
    // Start background services AND open the Desktop UI
    _ = host.StartAsync();
    BuildAvaloniaApp(host.Services).StartWithClassicDesktopLifetime(args);
}
```

### Summary

You have built your `DdsMonitor.Engine` very cleanly using the `Microsoft.Extensions.Options` pattern. Because of this foresight, moving from a Blazor Web Host to an Avalonia Generic Host requires virtually zero refactoring of your settings classes or CLI parameters. Plugins (and the Engine) will continue to request `IOptions<DdsSettings>` in their constructors and receive the CLI-overridden values flawlessly.







Let's thisnk about what features we should have in the first version of this solution. To demonstrate the interactive and headless mode, plugin loading, command line, saveable user configuration accessible to plugins, starting and using the dds services from plugins, also some  plugins to perform the backend job (something very minimalistic), also some plugins showing the avalonia user panels (like a plain list of topics), adding themselves & some action items to them main menu and to the main toolbar etc. Minimalistic but demonstrating and proving the basic concepts.







To build a solid V1 Proof of Concept (PoC) that validates all these architectural pillars, we need to design a feature set that is functionally minimalistic but architecturally complete.

Here is the blueprint for the first version, broken down by component. This defines exactly what needs to be built to prove the concepts work together.

### 1. The Core Framework & Shell (DdsMonitor.Avalonia)

This is the application host. It provides the infrastructure but does no DDS work.

- **Dual-Boot Strategy:** Reads the `--HeadlessMode` CLI argument. If true, it runs `await host.RunAsync()` (no UI). If false, it boots the Avalonia `MainWindow`.
- **Configuration Binder:** Automatically maps CLI arguments (e.g., `--DdsSettings:DomainId=0`) to the `IOptions<DdsSettings>` classes so plugins and the Engine can use them.
- **User Settings File:** Implements `IUserSettings` that saves/loads a `settings.json` file in the user's AppData folder.
- **The "Empty" UI Layout:** An Avalonia `MainWindow` containing:
  - A top `Menu` (bound to `IMenuRegistry`).
  - A top `Toolbar` (bound to `IToolbarRegistry`).
  - A Grid with a Left Sidebar (bound to `IPanelRegistry.LeftSidebar`) and a Main Document Area (bound to `IPanelRegistry.MainDocument`).

### 2. The Extensibility API (DdsMonitor.Avalonia.Core)

The shared contract library defining how plugins inject themselves into the Shell.

- **`IMonitorPlugin`**: The main interface with `ConfigureServices` and `Initialize`.
- **`IMenuRegistry`**: Allows plugins to add items (e.g., `RegisterMenuItem("View/Topic Explorer", Command)`).
- **`IToolbarRegistry`**: Allows plugins to add buttons/icons to the top bar.
- **`IPanelRegistry`**: Allows plugins to dock Avalonia `UserControl` ViewModels to screen regions.

### 3. Plugin 1: The "Backend" Demonstrator (Headless & CLI Proof)

**Name:** `DummyDataGeneratorPlugin`

- **Goal:** Prove background services run, prove CLI arguments work, and prove DDS services can be consumed/driven by a plugin.
- **CLI Trigger:** Can be activated via `--GeneratorPlugin:Enabled=true` and `--GeneratorPlugin:PublishRateMs=1000`.
- **Behavior:** * Registers an `IHostedService` (Background Service).
  - If `Enabled` is true, it uses the Engine's DDS primitives to create a dummy topic (e.g., "Heartbeat") and publishes a timestamp payload at the specified `PublishRateMs`.
  - **Headless Check:** This plugin runs regardless of whether the Avalonia UI is open or if the app was started with `--HeadlessMode=true`.

### 4. Plugin 2: The "Frontend" Demonstrator (UI & Settings Proof)

**Name:** `TopicExplorerPlugin`

- **Goal:** Prove Avalonia UI rendering, Menu/Toolbar integration, User Settings serialization, and live DDS data consumption.
- **UI Registration:** Registers a `TopicExplorerViewModel` to the `LeftSidebar` panel. It displays a simple Avalonia `ListBox` showing live discovered topics.
- **Menu Integration:** Registers a menu item: `View -> Show/Hide Topic Explorer`.
- **Toolbar Integration:** Registers a "Pause/Resume" button to the main toolbar. Clicking it stops the `ListBox` from updating with new topics.
- **Settings Proof:** Uses `IUserSettings` to save the state of a "Show Internal DDS Topics" checkbox. When the user checks it, it saves to disk. The next time the app opens, the plugin reads this setting and restores the checkbox state.

------

### How this V1 Proves Your Architecture:

1. **Headless Execution:** Run `DdsMonitor.Avalonia.exe --HeadlessMode=true --GeneratorPlugin:Enabled=true`. The app stays in the console, no UI opens, but the dummy data begins publishing via DDS.
2. **Plugin Loading:** The Shell knows nothing about `TopicExplorerPlugin` or `DummyDataGeneratorPlugin`, but they are loaded dynamically and execute.
3. **Command Line Parsing:** The Generator plugin reads `--GeneratorPlugin:PublishRateMs=500` without any manual string parsing.
4. **User Configuration:** The user clicks "Show Internal DDS Topics" in the UI. They close the app. They reopen the app. The checkbox remains checked because `IUserSettings` saved it.
5. **DDS Service Access:** The Frontend plugin reads from `ITopicRegistry` to populate its list, while the Backend plugin writes to DDS. They share the same singleton Engine state.

By building exactly these pieces and nothing more, you establish a rock-solid, extensible foundation. Once this skeleton works, migrating the rest of the Blazor features (like the Samples Grid, Hex Viewer, and Dynamic Forms) becomes a straightforward exercise of writing new UI plugins.







we should demonstrate also the inter-plugin communication concepts, defining the conventions where and how to define the messages or events, how to identify/address the UI panels, with some minimalistic but still working sample of that. Like if i double click a topic in the minimalistic topic explorer, a minimalistic sample viewer panel will open for that concrete topic. Best to mimic the existing ddsmonitor.blazor convention as we will be porting lots of stuff from there.







To build a robust, VS Code-like inter-plugin communication system that supports dynamic UI interactions (like double-clicking a topic to open a new tab), we need to establish **Conventions for Messaging** and **Dynamic Panel Management**.

Since you want to mimic the existing `DdsMonitor.Blazor` architecture, we will utilize an **Event Broker** (Pub/Sub pattern) and expand our `IPanelRegistry` to handle dynamic documents (Tabs) rather than just static sidebars.

Here is how we integrate this into the V1 Proof of Concept.

### 1. Conventions for Inter-Plugin Communication

**Where do messages live?**

Plugins should remain decoupled. If `Plugin A` and `Plugin B` need to talk, they should not reference each other. Instead, define message contracts (records/classes) in a shared location.

- **Core Messages** (UI routing, app lifecycle) live in `DdsMonitor.Avalonia.Core`.
- **Domain Messages** (DDS data arrived, topic discovered) live in `DdsMonitor.Engine`.

**The Event Broker:**

If `DdsMonitor.Engine` doesn't already expose a generic Pub/Sub broker, we define a simple `IEventBroker` in `Avalonia.Core`.

C#

```
// In DdsMonitor.Avalonia.Core
public interface IEventBroker
{
    void Publish<TMessage>(TMessage message);
    void Subscribe<TMessage>(Action<TMessage> handler);
}

// The Message Definition (Convention: Use immutable C# Records)
public record OpenTopicSamplesMessage(string TopicName);
```

### 2. Upgrading the Panel Registry (Dynamic Tabs)

To support opening a specific viewer for a specific topic, the `IPanelRegistry` needs to act like a Tab Control. It needs an `Id` to prevent opening duplicate tabs for the same topic, and a way to switch focus.

C#

```
// In DdsMonitor.Avalonia.Core
public interface IPanelRegistry
{
    // Static panels (Left Sidebar, Bottom Panel)
    void RegisterFixedPanel(string title, PanelRegion region, object viewModel);
    
    // Dynamic panels (Main Document Area / Tabs)
    void OpenDocument(string documentId, string title, object viewModel);
    void CloseDocument(string documentId);
}
```

*In the Avalonia Shell `MainWindow.axaml`, the Main Document area will now be an Avalonia `TabControl` bound to the documents registered here.*

### 3. The Publisher: Topic Explorer Plugin

We update the `TopicExplorerViewModel` to publish the message when a user double-clicks an item in the Avalonia `ListBox`.

C#

```
// In TopicExplorerPlugin
public partial class TopicExplorerViewModel : ObservableObject
{
    private readonly IEventBroker _eventBroker;

    [ObservableProperty]
    private ObservableCollection<string> _topics = new();

    public TopicExplorerViewModel(IEventBroker eventBroker)
    {
        _eventBroker = eventBroker;
        Topics.Add("HelloWorld_Topic");
        Topics.Add("RobotTelemetry_Topic");
    }

    // This command is bound to the DoubleTapped event of the Avalonia ListBox item
    [RelayCommand]
    private void OnTopicDoubleClicked(string selectedTopic)
    {
        if (string.IsNullOrEmpty(selectedTopic)) return;

        // Broadcast to the whole app: "The user wants to view this topic!"
        _eventBroker.Publish(new OpenTopicSamplesMessage(selectedTopic));
    }
}
```

### 4. The Subscriber: A New "Samples Viewer" Plugin

We create a completely separate plugin whose *only job* is to listen for that message and dynamically generate a UI panel for it.

Because we need a *new* ViewModel instance for every tab we open, we inject an `IServiceProvider` or an `IServiceScopeFactory` to resolve dynamic instances.

C#

```
// In SamplesViewerPlugin Bootstrapper
public class SamplesViewerPlugin : IMonitorPlugin
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register the ViewModel as Transient so we get a fresh instance per tab
        services.AddTransient<TopicSamplesViewModel>();
    }

    public void Initialize(IServiceProvider provider)
    {
        var eventBroker = provider.GetRequiredService<IEventBroker>();
        var panelRegistry = provider.GetRequiredService<IPanelRegistry>();

        // Subscribe to the global routing message
        eventBroker.Subscribe<OpenTopicSamplesMessage>(msg => 
        {
            // 1. Create a unique ID for this document tab
            string docId = $"samples_view_{msg.TopicName}";

            // 2. Resolve a fresh ViewModel using DI
            var viewModel = provider.GetRequiredService<TopicSamplesViewModel>();
            
            // 3. Initialize the ViewModel with the requested Topic
            viewModel.Initialize(msg.TopicName);

            // 4. Tell the Shell to open (or focus) this Tab
            panelRegistry.OpenDocument(docId, $"Samples: {msg.TopicName}", viewModel);
        });
    }
}
```

**The Dynamic ViewModel:**

C#

```
public partial class TopicSamplesViewModel : ObservableObject
{
    [ObservableProperty]
    private string _targetTopic;

    [ObservableProperty]
    private ObservableCollection<string> _mockSamples = new();

    public void Initialize(string topicName)
    {
        TargetTopic = topicName;
        MockSamples.Add($"[Live Data for {topicName} goes here...]");
    }
}
```

### Summary of the Flow

1. **User Action:** The user double-clicks "RobotTelemetry_Topic" in the `TopicExplorerView` (Avalonia Left Panel).
2. **Event Published:** `TopicExplorerViewModel` fires `OpenTopicSamplesMessage("RobotTelemetry_Topic")` via the singleton `IEventBroker`.
3. **Event Handled:** `SamplesViewerPlugin` catches the message. It asks the DI container for a new `TopicSamplesViewModel`.
4. **UI Update:** The plugin calls `panelRegistry.OpenDocument(...)`. The Shell's Avalonia `TabControl` sees the new document in its `ItemsSource` and instantly renders a new tab titled "Samples: RobotTelemetry_Topic".

This explicitly proves the inter-plugin communication and dynamic UI generation. `TopicExplorer` has zero knowledge of `SamplesViewer`. You can literally delete the `SamplesViewerPlugin.dll` from the output folder, and the application will still run perfectly (double-clicking will just do nothing, as no plugin is listening to the message). This is true VS Code-style extensibility.









it should not be oriented to tabs. The tab is just a generic UI panel that happens to be docked in some tab host. I need the stuff to be UI panel oriented. UI panel can be docked as tab, or living as standalone window. Nothing like main document area. The app is not about working with documents. It requires lots of separate and addressable UI panels, using some 'address' inclusing the UI panel type and instance ordinal, each supporting remembering the visual settings of it per address, simila to the ddsmonitor.blazor is doing. We do not need to deal with docking at this moment, we can start with that each UI panel is a standalone movable and resizable child window (with title, maximizing, minimizing..) 







You are absolutely right. If `DdsMonitor.Blazor` is already built around addressed, standalone UI panels (similar to multi-window diagnostic tools like CANalyzer or Wireshark), forcing a document/tab model is the wrong abstraction.

A panel-oriented architecture where every piece of UI is an addressable instance that can float, dock, or remember its own state is much more powerful for a data-monitoring application.

Let's redesign the V1 Proof of Concept to be strictly **Panel-Oriented** with standalone child/floating windows, an explicit addressing scheme, and per-instance state persistence.

### 1. The Addressing Scheme (`DdsMonitor.Avalonia.Core`)

Every panel needs a deterministic address. This allows the system to know if a panel is already open, route messages specifically to it, and save its visual state.

C#

```
// The fundamental identifier for any piece of UI in the system
public record PanelAddress(string PanelType, string InstanceId)
{
    public override string ToString() => $"{PanelType}_{InstanceId}";
}
```

- **`PanelType`**: e.g., `"TopicExplorer"`, `"SamplesViewer"`, `"HexViewer"`.
- **`InstanceId`**: e.g., `"Singleton"` (for the main explorer), or `"RobotTelemetry"` (for a specific topic's viewer).

### 2. The Panel Manager (Replaces PanelRegistry)

The Shell provides an `IPanelManager`. In V1, this manager simply spawns new Avalonia `Window` objects. Later, you can swap the internal implementation of this manager to use a docking framework without changing any plugin code.

C#

```
public interface IPanelManager
{
    // Opens a new floating window (or focuses it if already open)
    void OpenPanel(PanelAddress address, string title, object viewModel);
    
    // Allows plugins to close their own panels programmatically
    void ClosePanel(PanelAddress address);
}
```

### 3. State Persistence per Panel (The Shell's Job)

The beauty of this architecture is that the **Shell handles window state, not the plugins**. When `IPanelManager` creates the Avalonia Window, it uses `IUserSettings` to restore and track the window's size and position based on the `PanelAddress`.

C#

```
// Inside DdsMonitor.Avalonia (The Shell)
public class WindowPanelManager : IPanelManager
{
    private readonly Dictionary<PanelAddress, Window> _openWindows = new();
    private readonly IUserSettings _settings;

    public WindowPanelManager(IUserSettings settings) { _settings = settings; }

    public void OpenPanel(PanelAddress address, string title, object viewModel)
    {
        if (_openWindows.TryGetValue(address, out var existingWindow))
        {
            existingWindow.Activate(); // Bring to front if already open
            return;
        }

        var window = new Window
        {
            Title = title,
            Content = viewModel, // Avalonia resolves the View via DataTemplates
            // Load saved state per address
            Width = _settings.Get(address.ToString(), "Width", 800.0),
            Height = _settings.Get(address.ToString(), "Height", 450.0),
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        // Handle saving state when the user resizes the window
        window.Resized += (s, e) =>
        {
            _settings.Set(address.ToString(), "Width", window.Width);
            _settings.Set(address.ToString(), "Height", window.Height);
            _ = _settings.SaveAsync(); // Fire and forget
        };

        window.Closed += (s, e) => _openWindows.Remove(address);

        _openWindows[address] = window;
        window.Show();
    }
    
    public void ClosePanel(PanelAddress address) { /* ... */ }
}
```

### 4. Inter-Plugin Communication (Addressed Routing)

Now let's look at how the `TopicExplorer` plugin tells the `SamplesViewer` plugin to open a new addressed panel.

**The Contract Message:**

C#

```
// In Core. A generic request to open a viewer for a specific topic.
public record RequestTopicViewerMessage(string TopicName);
```

**The Publisher (Topic Explorer Plugin):**

C#

```
public partial class TopicExplorerViewModel : ObservableObject
{
    private readonly IEventBroker _eventBroker;

    [RelayCommand] // Triggered by UI double-click
    private void OnTopicDoubleClicked(string selectedTopic)
    {
        // We just broadcast the intent. We don't know who handles it.
        _eventBroker.Publish(new RequestTopicViewerMessage(selectedTopic));
    }
}
```

**The Subscriber (Samples Viewer Plugin):**

This plugin listens for the request, resolves a ViewModel, creates the `PanelAddress`, and asks the Shell to draw it.

C#

```
public class SamplesViewerPlugin : IMonitorPlugin
{
    public void Initialize(IServiceProvider provider)
    {
        var broker = provider.GetRequiredService<IEventBroker>();
        var panelManager = provider.GetRequiredService<IPanelManager>();

        broker.Subscribe<RequestTopicViewerMessage>(msg => 
        {
            // 1. Create the determinisitic address
            var address = new PanelAddress("SamplesViewer", msg.TopicName);

            // 2. Resolve the ViewModel for this instance
            var viewModel = provider.GetRequiredService<SamplesViewModel>();
            viewModel.Initialize(msg.TopicName); // Pass the DDS topic to the VM

            // 3. Ask the shell to open the panel
            panelManager.OpenPanel(address, $"Samples: {msg.TopicName}", viewModel);
        });
    }
}
```

### 5. Routing Data Updates to Specific Panels

Because every ViewModel knows its own `PanelAddress` (or at least its `InstanceId`), you can route high-frequency DDS data precisely.

Instead of every open `SamplesViewModel` receiving *all* DDS samples and filtering them, the Engine (or a background service) can publish addressed messages:

C#

```
// Core Message
public record DdsSampleArrivedMessage(string TopicName, byte[] Payload);

// In SamplesViewModel
public void Initialize(string topicName)
{
    _topicName = topicName;
    
    // Subscribe to global DDS events, but instantly filter by our instance ID
    _eventBroker.Subscribe<DdsSampleArrivedMessage>(msg => 
    {
        if (msg.TopicName == _topicName) 
        {
            // Process the sample and update the Avalonia UI
            Dispatcher.UIThread.InvokeAsync(() => AddSampleToGrid(msg.Payload));
        }
    });
}
```

### Summary of the Refined V1 PoC

1. **No Layout Enforcements:** The Shell no longer enforces "Left" or "Right" regions. The App starts, plugins initialize, and the `TopicExplorerPlugin` calls `panelManager.OpenPanel(new("TopicExplorer", "Main"), ...)` to spawn the first floating window.
2. **True Modularity:** Double-clicking a topic fires a message. The `SamplesViewerPlugin` handles it, creates a new `PanelAddress`, and tells the Shell to spawn another floating window.
3. **State Memory:** The user moves the "SamplesViewer_RobotTelemetry" window to their second monitor and makes it wide. They close it. When they double-click "RobotTelemetry" again, `WindowPanelManager` reads the `IUserSettings` using the address, and the window spawns exactly where they left it on the second monitor.
4. **Future Proofing:** When you are ready to add docking, you just rewrite `WindowPanelManager`. Instead of calling `new Window()`, it calls `DockFactory.CreateTool(...)`. The plugins remain 100% untouched because the `IPanelManager` contract hasn't changed.







I don't think we want to publish each delivered dds sample as a topic message. The samples are handled by the ddsmonitor.engine. And the plugins are using its services to get pre-processed data exactly to avoid the need to process individual samples (there could be thousads of them per second and the app must stay responsive, no beining overloaded by handling a flood of internal messages). But i got the point.

Pls check what features the current ddsmonitor has and whether some of them does nor require defining some concept (like we discovered the need for the panel management and panel addressing) to be provin early in the development process.





You are completely right about the DDS samples. Sending raw, high-frequency firehose data through a general-purpose UI `IEventBroker` is a classic anti-pattern that will freeze the UI thread instantly. The `IEventBroker` should be strictly for **app state and user intent** (e.g., "Open this panel", "Theme changed", "Recording started"). High-frequency data must remain in the Engine's specialized data stores, and the UI panels should use throttled polling or virtualized data binding to observe it safely. Good catch.

Looking at the typical features of your `DdsMonitor.Blazor` and the nature of DDS, there are **three critical architectural concepts** you must prove early in the Avalonia version. If these aren't designed into the foundation, they will require massive refactoring later.

Here are the missing concepts and how to prove them in your V1/V2 development phase:

### Concept 1: The "Firehose" UI Strategy (Data Virtualization)

Since we agreed the UI shouldn't receive individual sample events, we need a proven pattern for Avalonia panels to read from the Engine's `ISampleStore` without locking the UI thread when topics are pushing 10,000 Hz.

- **The Problem:** Blazor handles high-frequency DOM updates differently than a native desktop framework. Avalonia uses a strict UI Dispatcher thread.

- **The Concept:** **Throttled Observers and Virtualized UI.** * **How to prove it early:**

  In your `SamplesViewerPlugin` (the minimalist one), do not use a standard `DataGrid` or `ListBox`. Prove the use of **Avalonia.Controls.TreeDataGrid**. It is a specialized, highly optimized control designed precisely for massive datasets.

  Instead of an event, the `SamplesViewModel` should start an Avalonia `DispatcherTimer` (e.g., firing every 100ms - 10 FPS). On every tick, it asks the Engine's `ISampleStore`: *"Give me the count of new samples since my last tick"*. It then updates the virtualized `TreeDataGrid` source. This proves you can monitor a massive DDS firehose while the app remains butter-smooth.

### Concept 2: Contextual Extension Points (Right-Click Interoperability)

In VS Code, if you right-click a file in the tree (built by Microsoft), you might see "Git: View History" (built by a Git plugin). Your DDS monitor needs this. If the `TopicExplorerPlugin` draws a list of topics, the `RecorderPlugin` needs a way to add a "Start Recording" button to that specific topic's right-click menu, without the `TopicExplorerPlugin` knowing the `RecorderPlugin` exists.

- **The Problem:** How do independent plugins inject UI into other plugins' panels?

- **The Concept:** **Data-Bound Command Registries.** Instead of attaching menus to *UI panels*, plugins attach commands to *Data Types*.

- **How to prove it early:**

  Create an `ICommandRegistry` in `Avalonia.Core`.

  C#

  ```
  // RecorderPlugin registers a command for any 'string' representing a TopicName
  commandRegistry.RegisterContextCommand<string>("Start Recording", topicName => StartRec(topicName));
  ```

  In your `TopicExplorerPlugin`, when rendering the Avalonia ContextMenu for a list item, it queries the `ICommandRegistry` for all commands associated with the `string` type (or a custom `Topic` type) and generates the Avalonia `<MenuItem>` dynamically. This proves true decoupling.

### Concept 3: Dynamic Payload Rendering (The Type Drawer)

DDS payloads are dynamic. You don't know the struct at compile time (especially if using XTypes or importing IDL on the fly). `DdsMonitor.Blazor` likely uses reflection to build HTML forms dynamically. Avalonia needs a native equivalent to parse a DDS payload and draw an interactive tree or form.

- **The Problem:** We need a way to render arbitrary, nested data structures as native Avalonia controls (`TextBox`, `CheckBox`, `Expander`), and allow plugins to provide custom visualizers for specific types (e.g., an Image plugin that renders a `byte[]` array as a bitmap if the topic is a camera feed).

- **The Concept:** **The Visualizer Registry (Avalonia DataTemplates).**

- **How to prove it early:**

  Create an `ITypeVisualizerRegistry`.

  C#

  ```
  public interface ITypeVisualizerRegistry
  {
      // Maps a .NET Type (or DDS Typecode) to an Avalonia Control
      Control BuildControlForValue(object value, Type type); 
  }
  ```

  Prove this by having a `StandardVisualizerPlugin` that uses reflection to look at an object's properties and generate a vertical `StackPanel` of Avalonia `TextBlock`s. Then, create a `HexViewerPlugin` that registers a custom visualizer specifically for `byte[]`. If the payload contains a `byte[]`, the UI automatically renders a hex grid instead of a string.

### Summary of what to add to the Initial POC

To ensure the foundation is completely bulletproof, your minimalist Proof of Concept should demonstrate:

1. **Panel Management:** (As discussed previously) Spawning floating Avalonia windows addressed by `PanelId`.
2. **Settings:** (As discussed) Saving floating window locations per user.
3. **Throttled Binding:** A dummy `GeneratorPlugin` pumping 5,000 samples/sec into the Engine, and a `SamplesViewerPlugin` using a 100ms `DispatcherTimer` to read it smoothly.
4. **Context Menus:** The `TopicExplorerPlugin` rendering right-click menu items that were actually injected by the `GeneratorPlugin`.
5. **Dynamic Data:** A basic `DetailsPanel` that uses reflection to draw an Avalonia UI for an arbitrary dummy C# struct.

If you build this shell, every single complex feature from `DdsMonitor.Blazor` will have a clear, proven architectural path into the new Avalonia desktop app.







the ability to render/inspect the various data types in some kind of avaonia hierarchical expandable.collapsible syntax-colored user interface is crucial for tools like dds monitor.  I think we need some GC friendly CPU saving approach. Like using expression trees to build the builders for each dds topic sample data structure and cache these once when a new topic appears. And reuse these for building the sample monitoring UI. Maybe the ddsmonitor.blazor is doing something reusable in this regard already?







Yes, you are absolutely on the right track, and the great news is that the existing `DdsMonitor.Engine` **already does exactly this!** Because you built the Engine to be UI-agnostic, you can reuse its highly optimized, GC-friendly type parsing and expression tree compilation directly in your Avalonia application without rewriting any of it.

Here is how `DdsMonitor.Engine` is currently handling this (which you will reuse in Avalonia):

### 1. Pre-Compiled Expression Trees (`TopicMetadata` & `FieldMetadata`)

When a new DDS topic is discovered, the Engine doesn't use slow reflection every time a sample arrives. Instead, it parses the type structure once in `tools\DdsMonitor\DdsMonitor.Engine\Metadata\TopicMetadata.cs`.

It builds a flattened list of `FieldMetadata` objects for every nested property or field in the struct/class. For each field, it uses `System.Linq.Expressions` to compile high-performance delegates:

- **`Func<object, object?> Getter`**: Compiles an expression like `(object payload) => ((MyStruct)payload).Nested.MyField`.
- **`Action<object, object?> Setter`**: Compiles an expression like `(object payload, object value) => ((MyStruct)payload).Nested.MyField = (FieldType)value`.

It does this via a nested `MemberAccessorFactory` that uses `Expression.Lambda<...>(...).Compile()`. These compiled delegates perform almost identically to native compiled C# code, bypassing reflection overhead entirely.

### 2. Caching per Topic

These compiled accessors are cached inside the `TopicMetadata` instance. The Engine's `TopicRegistry` holds this metadata. So, when a topic appears, the heavy lifting of building the expression trees happens exactly **once**.

### 3. How to reuse this in Avalonia (Dynamic Detail Panel)

In your Avalonia `SamplesViewerPlugin` (or `DetailsViewerPlugin`), you simply ask the Engine for the `TopicMetadata`.

Instead of using Reflection to walk the payload, you bind your Avalonia controls directly to the `FieldMetadata` delegates.

**Example Avalonia ViewModel:**

C#

```
public class PayloadInspectorViewModel : ObservableObject
{
    // Bound to an Avalonia ItemsControl
    public ObservableCollection<FieldItemViewModel> Fields { get; } = new();

    public void LoadPayload(SampleData sample)
    {
        Fields.Clear();
        var meta = sample.TopicMetadata; 

        // meta.AllFields contains the pre-compiled expression trees!
        foreach(var fieldMeta in meta.AllFields) 
        {
            // We just invoke the compiled Func to get the value in O(1) time
            var currentValue = fieldMeta.Getter(sample.Payload);
            
            Fields.Add(new FieldItemViewModel(fieldMeta, currentValue));
        }
    }
}
```

### 4. The Type Drawer / Visualizer Concept

The Engine also has a UI-agnostic concept of an `ITypeDrawerRegistry` and `IValueFormatterRegistry` (found in `tools\DdsMonitor\DdsMonitor.Engine\Ui\`).

- **Formatters:** You can reuse `IValueFormatterRegistry` exactly as is to convert values to strings (e.g., formatting a `double` or a `byte[]` to Hex).
- **Drawers:** The current `ITypeDrawerRegistry` is slightly bound to Blazor (`RenderFragment`). As discussed earlier, you will create an Avalonia equivalent (e.g., `ITypeVisualizerRegistry`). When building the `FieldItemViewModel`, you ask this registry: *"What Avalonia control should I use to edit a `float`?"* and it returns a cached `NumericUpDown` template.

### Summary

Your intuition about GC pressure and CPU usage is spot-on for a high-frequency DDS tool. By directly consuming `TopicMetadata.AllFields` from the shared `DdsMonitor.Engine`, your Avalonia panels will automatically inherit the `Expression.Lambda` caching strategy. The UI just becomes a fast, thin projection of the compiled `Getter` and `Setter` delegates.





Lets recap what should be part of the MVP



Here is a comprehensive recap of the Minimum Viable Product (MVP) / Version 1 Proof of Concept for the new `DdsMonitor.Avalonia` architecture.

This MVP is designed specifically to prove all the high-risk architectural vectors (performance, decoupling, headless execution, and state persistence) before you commit to migrating the entire Blazor feature set.

### 1. The Core Infrastructure (`DdsMonitor.Avalonia` Shell)

This is the base executable. It contains no DDS logic and no specific UI layouts.

- **.NET Generic Host Integration:** Uses `Host.CreateApplicationBuilder(args)` to automatically bind existing command-line arguments (like `--DdsSettings:DomainId=0`) to the Engine's `IOptions` classes.
- **Dual-Boot Execution:** Checks `--HeadlessMode`. If true, it runs background services without Avalonia (`host.RunAsync()`). If false, it boots the Avalonia UI.
- **User Settings Store:** Implements an `IUserSettings` service that reads/writes a `settings.json` file in the user's OS AppData folder to persist plugin preferences and window states.
- **Window Panel Manager:** Implements `IPanelManager`. It manages standalone, floating Avalonia `Window` instances using a deterministic `PanelAddress` (e.g., `["SamplesViewer", "RobotTelemetry"]`), automatically restoring their last known screen position and size from `IUserSettings`.

### 2. The Extensibility Contracts (`DdsMonitor.Avalonia.Core`)

The shared class library that plugins use to interact with the Shell and each other.

- **`IMonitorPlugin`**: The bootstrapper interface (`ConfigureServices` and `Initialize`).
- **`IEventBroker`**: A lightweight Pub/Sub system for user intent and app state (e.g., "Open this panel"), *not* for high-frequency data.
- **`ICommandRegistry`**: Allows plugins to register contextual actions against specific data types (e.g., registering a "Start Recording" command against a `TopicName` string).
- **`ITypeVisualizerRegistry`**: Maps C# Types to Avalonia Control templates, allowing plugins to define how specific data (like a `byte[]` or a `Vector3`) should be rendered.

### 3. Plugin 1: The Backend Prover (`DummyDataGeneratorPlugin`)

- **Purpose:** Proves Headless mode, background services, and CLI parameter ingestion.
- **Features:** * Runs an `IHostedService` in the background.
  - Reads a `--GeneratorPlugin:PublishRateMs` CLI argument.
  - Publishes dummy data into the Engine's DDS pipeline at that rate.
  - Injects a contextual command ("Toggle Dummy Generator") into the `ICommandRegistry`.

### 4. Plugin 2: The Frontend Prover (`TopicExplorerPlugin`)

- **Purpose:** Proves Avalonia UI rendering, inter-plugin context menus, user settings persistence, and Pub/Sub intent routing.
- **Features:**
  - Spawns a primary "Topic Explorer" floating window via `IPanelManager`.
  - Renders a list of live topics.
  - Reads from `IUserSettings` to restore a "Show Hidden Topics" checkbox state.
  - Builds Avalonia Context Menus dynamically by querying the `ICommandRegistry` (proving it can show the "Toggle Dummy Generator" button injected by Plugin 1).
  - When a topic is double-clicked, it publishes a `RequestTopicViewerMessage` via the `IEventBroker`.

### 5. Plugin 3: The High-Performance UI Prover (`SamplesViewerPlugin`)

- **Purpose:** Proves dynamic addressed window spawning, high-frequency data virtualization, and GC-friendly dynamic payload parsing.
- **Features:**
  - Subscribes to `RequestTopicViewerMessage`. When received, asks the `IPanelManager` to spawn a new, uniquely addressed window for that specific topic.
  - **Throttled Observers:** Proves the UI won't freeze by using an Avalonia `DispatcherTimer` (e.g., 100ms ticks) to poll the Engine's `ISampleStore` for new data, rather than reacting to individual sample events.
  - **Dynamic Details UI:** When a user clicks a specific sample, it uses the Engine's existing, pre-compiled `TopicMetadata` expression trees to parse the payload in O(1) time without reflection, and uses the `ITypeVisualizerRegistry` to draw the Avalonia controls for the struct.

### Why this specific MVP?

If you can build exactly these pieces and make them run smoothly together, **the architecture is proven**.

You will have demonstrated that plugins can run without a UI, that floating windows remember where the user put them, that plugins can inject buttons into other plugins' windows, and that Avalonia can handle a 5,000 Hz DDS firehose using the Engine's expression trees without lagging. Once this MVP is stable, porting the remaining tools (Hex Viewer, Network Configurator, Recording Playback) is just a matter of writing more plugins against these established contracts.













The proposed MVP in the design document establishes a solid architectural foundation. Relying on the .NET Generic Host, adopting a strict plugin-based DI model, separating the UI from the DDS Engine, and utilizing Avalonia's `TreeDataGrid` with throttled observers effectively addresses the core desktop transition risks (UI thread starvation and headless execution). Reusing the Engine's pre-compiled expression trees (`TopicMetadata` and `FieldMetadata`) guarantees GC-friendly payload parsing without reflection overhead.

However, mapping the V1 MVP against the existing feature set of the Blazor application reveals several critical architectural gaps that need to be addressed early in the framework design:

**1. Dynamic Schema Assembly Loading vs. Plugin Loading** The MVP heavily focuses on loading *plugins* via `AssemblyLoadContext` from a `/plugins/` folder. However, it misses the mechanism for loading external *schema assemblies* (the user's generated DDS types).

- **The Flaw:** `TopicExplorerPlugin` reads from `ITopicRegistry`, but without loading external DLLs at runtime, the registry will be empty.
- **The Fix:** The MVP must include the `AssemblySourceService` equivalent. The .NET Generic Host configuration binder handles `--AppSettings:TopicSources`, but the shell must prove it can dynamically ingest these external DLLs, extract the types, and feed them into the `ITopicRegistry` without requiring an application restart.

**2. High-Performance Filtering Pipeline** The MVP specifies a `SamplesViewerPlugin` that uses a throttled `DispatcherTimer` to poll the `ISampleStore` for UI updates.

- **The Flaw:** Polling the raw `ISampleStore` directly bypasses the filtering and sorting engine. In the current architecture, filtering relies on Dynamic LINQ macros and is handled by a dedicated `ISampleView`. If the Avalonia UI polls raw samples and attempts to apply filters on the UI thread, it will freeze.
- **The Fix:** The MVP needs to prove that the Avalonia virtualized data grid can bind to a background-maintained `ISampleView`, ensuring that high-frequency Dynamic LINQ evaluation (`Payload.Field == 42`) executes entirely off the Avalonia UI thread.

**3. State Persistence for Addressed UI Panels** The design proposes `IUserSettings` and an `IPanelManager` that remembers window positions based on a `PanelAddress`.

- **The Flaw:** This only covers window placement. The current `SamplesPanel` relies on persisting complex, instance-specific state—such as selected custom columns, column widths, sort direction, and active filter strings—to the `workspace.json` file.
- **The Fix:** The `IPanelManager` contract must allow instantiated plugins to inject their own serializable state objects (like `GridSettings`) into the window manager's save cycle, ensuring that when the shell restores `SamplesViewer_RobotTelemetry`, the plugin receives its specific column layout and filter string back.

**4. Data Authoring vs. Data Visualization** The MVP defines an `ITypeVisualizerRegistry` to map C# types to Avalonia Control templates for inspecting payloads in the detail panel.

- **The Flaw:** This accounts for data *inspection* but neglects data *authoring* (the `SendSamplePanel` capability).
- **The Fix:** You need to explicitly design the Avalonia equivalent of `ITypeDrawerRegistry`. Generating dynamic input forms for complex nested DDS structs requires binding Avalonia controls (text boxes, numeric up/downs, combo boxes for enums) directly to the pre-compiled `Setter` delegates. The V1 MVP should include a rudimentary "Authoring Panel" to prove two-way data binding works with the expression trees.

**5. Multi-Participant & Domain Reconfiguration** The V1 MVP binds the `DdsSettings:DomainId` via CLI parameters on startup.

- **The Flaw:** The current application supports concurrent, dynamic multi-participant configurations mapped to different domains and partitions via the `ParticipantEditorDialog`.
- **The Fix:** The underlying `IDdsBridge` implementation must remain decoupled from a static startup configuration, exposing events (like `ParticipantsChangedEvent`) that allow the Avalonia shell to add or remove DDS domains at runtime without tearing down the Generic Host.





This is an incredibly sharp and accurate gap analysis. You have correctly identified the boundaries where a generic UI plugin architecture falls short of the highly specific, high-performance needs of a DDS diagnostic tool.

To ensure the V1 MVP doesn't paint you into a corner, we must fold these five requirements directly into the foundation. Here is how we refine the architecture and implementation strategy for each of these critical gaps in `DdsMonitor.Avalonia`.

------

### 1. Dynamic Schema Assembly Loading vs. Plugin Loading

**The Refinement: Dual-Track Assembly Load Contexts**

Plugins and Schema Assemblies must be treated entirely differently. Plugins are trusted code loaded at startup. Schema Assemblies are user-provided, dynamically loaded, and potentially updated/reloaded at runtime.

- **Implementation:**

  Create a dedicated `ISchemaLoaderService` within the Engine/Core. This service uses a separate `AssemblyLoadContext` (specifically, a collectible one, if you plan to support hot-reloading schemas).

- **Avalonia Integration:**

  Instead of hardcoding the registry, a `WorkspaceManagerPlugin` will observe the `--AppSettings:TopicSources` on startup and pass them to `ISchemaLoaderService`.

  Furthermore, you build a UI panel (e.g., "Schema Manager") that allows the user to browse for `.dll` or `.xml` (IDL) files. When added, the service loads the types, compiles the expression trees, and fires an `ISchemaRegistry.SchemasUpdated` event. The `TopicExplorerPlugin` listens to this and dynamically updates its UI.

### 2. High-Performance Filtering Pipeline

**The Refinement: The View-Model Proxy Pattern**

You are absolutely right: applying Dynamic LINQ or Regex on the UI thread will cause catastrophic UI freezes. The Avalonia UI must be a "dumb observer" of a background-maintained `ISampleView`.

- **Implementation:**

  The Engine provides `ISampleView` which runs a background worker loop processing new samples through the Dynamic LINQ expressions.

- **Avalonia Integration:**

  In your `SamplesViewModel`, you do not request raw samples. You request an `ISampleView` instance scoped to your filter string.

  C#

  ```
  // Inside SamplesViewModel
  public void ApplyFilter(string linqExpression)
  {
      // Engine creates a thread-safe, auto-updating view
      _activeView = _sampleStore.CreateView(TopicName, linqExpression);
  
      // The Avalonia TreeDataGrid binds to a Virtualized List
      // The Timer just tells the UI "The count changed, redraw what's visible"
      _refreshTimer.Tick += (s, e) => {
          if (_activeView.HasChanges) 
              Dispatcher.UIThread.InvokeAsync(() => Source.UpdateCount(_activeView.Count));
      };
  }
  ```

  This guarantees that $100\%$ of string parsing, expression evaluation, and memory allocation happens in the background.

### 3. State Persistence for Addressed UI Panels

**The Refinement: The `IPanelState` Contract**

`IUserSettings` managing window location is just the outer shell. The inner ViewModel must be given a slice of the settings dictionary bound to its `PanelAddress`.

- **Implementation:**

  Modify the `IPanelManager` and `IUserSettings` to support hierarchical JSON serialization.

- **Avalonia Integration:**

  Define an interface that plugins can optionally implement:

  C#

  ```
  public interface IStatefulViewModel
  {
      // Called by IPanelManager when the window opens
      void RestoreState(JsonObject state); 
  
      // Called by IPanelManager when the window closes, or periodically
      JsonObject SaveState(); 
  }
  ```

  Now, when `SamplesViewer_RobotTelemetry` is closed, the Shell asks the ViewModel for its state. The ViewModel serializes its column widths, custom column definitions, and active filter string into a JSON object. The Shell wraps this inside the `PanelAddress` node in `workspace.json`. Upon reopening, the ViewModel receives its exact configuration back.

### 4. Data Authoring vs. Data Visualization

**The Refinement: `IFieldEditor` and Two-Way Expression Binding**

Data inspection uses `Getter` delegates. Data authoring requires instantiating empty structs and binding Avalonia input controls directly to the `Setter` delegates.

- **Implementation:**

  Expand `ITypeVisualizerRegistry` to support `EditorTemplate` alongside `ViewTemplate`.

- **Avalonia Integration:**

  You will create a generic `DynamicFieldEditor` Avalonia Control.

  C#

  ```
  // The ViewModel for a single property in the "Send Sample" form
  public class EditableFieldViewModel : ObservableObject
  {
      private readonly FieldMetadata _meta;
      private readonly object _parentPayload;
  
      // Bound to the Avalonia TextBox/NumericUpDown
      public object CurrentValue
      {
          get => _meta.Getter(_parentPayload);
          set 
          {
              // Two-way binding invokes the compiled Setter!
              _meta.Setter(_parentPayload, Convert.ChangeType(value, _meta.Type));
              OnPropertyChanged();
          }
      }
  }
  ```

  The `SendSamplePlugin` uses `Activator.CreateInstance(TopicMetadata.DotNetType)`, wraps it in a tree of `EditableFieldViewModel`s, and when the user clicks "Send", the populated object is passed to `IDdsBridge.Publish(object)`.

### 5. Multi-Participant & Domain Reconfiguration

**The Refinement: Dynamic Participant Lifecycle Management**

The architecture cannot assume a 1:1 relationship between the Application and a DDS Domain. The Generic Host must act as a *fleet manager* for DDS Participants.

- **Implementation:**

  Remove `DomainId` from the static startup configuration logic. Introduce an `IParticipantManager` singleton in the Engine.

  C#

  ```
  public interface IParticipantManager
  {
      IEnumerable<IDdsParticipant> ActiveParticipants { get; }
      IDdsParticipant CreateParticipant(int domainId, string profileXml = null);
      void DestroyParticipant(Guid participantId);
  
      event EventHandler<ParticipantEventArgs> ParticipantAdded;
      event EventHandler<ParticipantEventArgs> ParticipantRemoved;
  }
  ```

- **Avalonia Integration:**

  The CLI argument `--DdsSettings:DomainId=0` simply becomes a startup script that calls `CreateParticipant(0)`.

  You then build a `NetworkConfiguratorPlugin` (the Avalonia equivalent of `ParticipantEditorDialog`). It interacts exclusively with `IParticipantManager`. Because all other components (like `ISampleStore` and `ITopicRegistry`) listen to the `ParticipantAdded/Removed` events, they dynamically attach/detach their native listeners without requiring the Avalonia UI or the Generic Host to restart.

------

### Conclusion on the Hardened MVP

By injecting these 5 requirements into the V1 blueprint, you transition from a "generic UI host" to a **domain-specific diagnostic framework**.

1. **Assemblies** are hot-loaded contexts.
2. **Filtering** is relegated to background worker views.
3. **State** is hierarchically bound to panel instances.
4. **Data Binding** utilizes both compiled Getters and Setters.
5. **DDS Participants** are dynamically managed entities.

This completely secures the architecture against the complexities of the existing Blazor toolset.





# ad 1. Dynamic Schema Assembly Loading vs. Plugin Loading



This specific dual-track `AssemblyLoadContext` architecture is already fully implemented in the existing `DdsMonitor.Engine` codebase. You do not need to build this from scratch; you can port the existing services directly to the Avalonia shell.

Here is how your architect's proposed design maps to the existing implementation:

**1. Dual-Track Assembly Load Contexts (Collectible)** The codebase already isolates user-provided schema assemblies from the application/plugin assemblies using a collectible `AssemblyLoadContext`.

- **Existing Component:** `TopicDiscoveryService` defines a nested `CollectiblePluginLoadContext` class which inherits from `AssemblyLoadContext(isCollectible: true)`.
- **Behavior:** When `LoadAndScanAssemblyDetailed` is called, it loads the user's DLL into this isolated context, extracts types decorated with `[DdsTopicAttribute]`, creates their `TopicMetadata` (compiling the expression trees), and adds them to the `ITopicRegistry`.

**2. Dynamic Schema Loader Service & CLI Integration** Your architect's `ISchemaLoaderService` and `WorkspaceManagerPlugin` concept maps directly to the existing `IAssemblySourceService`.

- **Existing Component:** `AssemblySourceService` handles the persistent list of user-configured external DLL assemblies and supports runtime dynamic loading.
- **CLI Support:** Its constructor injects `AppSettings` and specifically checks for `--AppSettings:TopicSources`. If present, it operates in a CLI-override mode, bypassing the saved workspace file.
- **Runtime Updates:** It exposes `Add(string dllPath)` and `Remove(int index)` methods, allowing schema DLLs to be loaded and unloaded at runtime.

**3. Eventing (`SchemasUpdated`)** Instead of `ISchemaRegistry.SchemasUpdated`, the current engine relies on standard event actions on the existing registries.

- **Existing Component:** `ITopicRegistry` exposes a `Changed` event which is fired whenever `Register(TopicMetadata meta)` adds a new topic.
- **UI Binding:** Additionally, `IAssemblySourceService` exposes a `Changed` event whenever the list of schema DLLs is modified. The current Blazor `TopicExplorerPanel` subscribes to `TopicRegistry.Changed` to automatically trigger an auto-subscription pass and update the UI when schemas are dynamically loaded.

**4. The "Schema Manager" UI Panel** The architect's suggested UI panel already exists as well, decoupling schema management from the main workspace.

- **Existing Component:** `TopicSourcesPanel.razor`.
- **Behavior:** It acts as a master-detail panel for managing external DLL assemblies. It leverages the `FileDialog` to let users browse for `.dll` files, adds them to the `AssemblySourceService`, and provides a breakdown of all DDS topic types extracted from the selected assembly.

**Conclusion** The gap identified for V1 is simply wiring these existing Engine components into your Avalonia UI. You can directly inject `IAssemblySourceService`, `TopicDiscoveryService`, and `ITopicRegistry` into your Avalonia `TopicSourcesPanelPlugin` and achieve the exact hot-loadable, expression-tree-compiled schema architecture your architect is describing.





# ad 2. High-Performance Filtering Pipeline



The proposed View-Model Proxy Pattern captures the exact right concurrency model, but the pseudo-code assumes API contracts that differ from how `DdsMonitor.Engine` is actually implemented. The engine already provides this background-filtered virtualization pipeline, but you need to wire it up according to its specific decoupled design.

Here are the architectural corrections needed to implement this against the existing engine:

**1. View Lifecycle and Instantiation** `ISampleStore` is strictly an append-only concurrent data structure and does not act as a factory for views. Instead, your Avalonia ViewModel must instantiate a `SampleView` directly, injecting the singleton `ISampleStore` into its constructor. The `SampleView` immediately spins up its own background worker task upon creation to process the firehose off the UI thread.

**2. Decoupled Filter Compilation** Passing a raw LINQ expression string to the view is avoided for performance reasons. The engine separates expression parsing from evaluation via the `IFilterCompiler` contract. Your ViewModel must first compile the string expression into a `Func<SampleData, bool>`. You then push this pre-compiled delegate to the background thread via `_activeView.SetFilter(compiledPredicate)`. This prevents string parsing overhead on the background worker loop.

**3. Event-Driven Updates Instead of Polling** While a throttled UI timer works, it introduces artificial latency or unnecessary ticks. `ISampleView` explicitly exposes an `OnViewRebuilt` event. The background worker raises this event precisely when a new batch of samples has been merged, filtered, and sorted. Your ViewModel should subscribe to `OnViewRebuilt` and use `Dispatcher.UIThread.InvokeAsync` to notify the Avalonia grid that `_activeView.CurrentFilteredCount` has changed.

**4. Zero-Allocation Slicing for Virtualization** To feed Avalonia's virtualized data grid, your items provider will call `_activeView.GetVirtualView(startIndex, count)`. This method acquires a brief lock to return a `ReadOnlyMemory<SampleData>` slice. This guarantees a zero-allocation read operation, and it contains built-in fast paths (such as reading directly from the tail of the backing array for descending-ordinal sorts) to completely avoid `Array.Reverse` overhead.

By binding the grid's total row count to `CurrentFilteredCount` and fulfilling range requests via `GetVirtualView`, you achieve the exact "dumb observer" architecture your architect is aiming for.







# ad 3. State Persistence for Addressed UI Panels

The proposed pattern addresses the requirement perfectly, but you don't need to build a new `IStatefulViewModel` contract or JSON serialization logic from scratch. The existing `DdsMonitor.Engine` already implements a highly decoupled version of this exact mechanism using the `PanelState` model.

Here is how the architecture currently handles it, which you can map directly to your Avalonia ViewModels:

**1. The `PanelState` and `ComponentState` Dictionary** Instead of the shell requesting a `JsonObject` from the plugin, the engine defines a `PanelState` class that the shell manages. Alongside standard window properties (X, Y, Width, Height), it exposes a `Dictionary<string, object> ComponentState`. This dictionary is the dedicated state slice for the instantiated panel.

**2. State Reading (Restoration)** In the current application, the `SamplesPanel` receives its `PanelState` instance (via a CascadingParameter in Blazor). During initialization, it simply checks the `ComponentState` dictionary for its specific keys (like `"SamplesPanel.FilterText"` or `"SamplesPanel.SelectedColumns"`) and restores its layout and filters.

- **Avalonia Mapping:** Your `IPanelManager` should pass the `PanelState.ComponentState` dictionary to your ViewModel's constructor or initialization routine. The ViewModel reads its saved state directly from this dictionary.

**3. State Writing (Saving)** When a user modifies the grid layout or filter in the `SamplesPanel`, the panel updates its values directly in the `PanelState.ComponentState` dictionary. It then calls the `WorkspacePersistenceService.RequestSave()` method, or publishes a `WorkspaceSaveRequestedEvent` via the `IEventBroker`.

- **Avalonia Mapping:** Your Avalonia ViewModels will bind to UI controls. When a property changes (e.g., column width adjusted), the ViewModel updates the injected `ComponentState` dictionary and publishes the `WorkspaceSaveRequestedEvent`. The shell's background services listen to this event and trigger a debounced save.

**4. Shell-Side Serialization & Sanitization** The shell's `WindowManager` handles the actual serialization to the `workspace.json` file. To prevent plugins from crashing the serialization pipeline by pushing non-serializable objects into the dictionary, the `WindowManager` runs a `FilterPersistableState` pass. This sanitizes the values, ensuring only safe types (strings, primitives, arrays, dictionaries) make it into the final `WorkspaceDocument`.

By reusing `PanelState.ComponentState`, your plugins remain strictly focused on their domain logic and dictionary manipulation, while the engine handles the hierarchical JSON serialization and state persistence completely transparently.



# ad  4. Data Authoring vs. Data Visualization



The proposed `EditableFieldViewModel` correctly identifies the two-way binding mechanism leveraging the pre-compiled expression trees, but you do not need to build the underlying registry and context abstractions from scratch. The `DdsMonitor.Engine` already provides a robust, UI-agnostic implementation of this exact pattern via `ITypeDrawerRegistry` and `DrawerContext`,.

Here is how your design maps to the existing engine and where it requires slight architectural adjustments for the Avalonia shell:

**1. Separation of Concerns in Registries** Instead of combining view and editor templates into a single `ITypeVisualizerRegistry`, the engine explicitly isolates data formatting (`IValueFormatterRegistry` for read-only inspection) from data authoring (`ITypeDrawerRegistry`). You should maintain this segregation. `ITypeDrawerRegistry` maps a CLR `Type` to a UI factory delegate,. For Avalonia, you will port this registry to map a `Type` to an Avalonia `Control` or `DataTemplate` instead of a Blazor `RenderFragment`.

**2. `DrawerContext` as the Binding Contract** Your proposed `EditableFieldViewModel` is functionally identical to the existing `DrawerContext`. The `DrawerContext` encapsulates the display label, the field's CLR type, a `ValueGetter` delegate (`Func<object?>`), and an `OnChange` callback (`Action<object?>`) for modifications,. When adapting this for Avalonia MVVM, your Avalonia editor controls will bind to an instance of `DrawerContext`, routing user input to the `OnChange` action, which inherently executes the pre-compiled `_meta.Setter` without reflection overhead.

**3. Type Conversion at the Control Level** Your proposed setter uses `Convert.ChangeType(value, _meta.Type)`, which is a fragile anti-pattern that breaks on nullables, complex nested structs, and enums. The engine mitigates this by handling type-safe conversions at the individual drawer level. For instance, the `TypeDrawerRegistry` maps numeric types to specific `TryParse` delegates, to validate input before invoking the setter. Your Avalonia implementation must follow this by providing strictly-typed control templates (like Avalonia's `NumericUpDown` or specific enum dropdowns) that output the exact expected type, removing the need for a blind runtime cast.

**4. Payload Instantiation and Transmission** Your approach to instantiating the empty payload via `Activator.CreateInstance(TopicMetadata.TopicType)` aligns exactly with how the existing `SendSamplePanel` initializes forms,. Once the Avalonia UI populates the fields via the bound setters, you pass the payload to the engine. However, the engine's contract does not use a global `IDdsBridge.Publish`. Instead, the panel must request a topic-specific writer via `IDdsBridge.GetWriter(TopicMetadata)`, and invoke `IDynamicWriter.Write(object payload)`,.





# ad 5. Multi-Participant & Domain Reconfiguration



The architectural refinement your architect proposes is exactly the right approach for decoupling the application lifecycle from the DDS domain lifecycle. You do not need to build this fleet manager pattern from scratch, as the `DdsMonitor.Engine` already implements this dynamic multi-participant architecture.

Here is how your architect's proposal maps directly to the existing Engine contracts, and how you will wire it up in the Avalonia shell:

**1. The Fleet Manager Contract** The role of your proposed `IParticipantManager` is already fulfilled by the central `IDdsBridge` interface.

- **Active Participants:** `IDdsBridge` exposes `IReadOnlyList<DdsParticipant> Participants` and `IReadOnlyList<ParticipantConfig> ParticipantConfigs` to track the fleet.
- **Dynamic Lifecycle:** Instead of `CreateParticipant` and `DestroyParticipant`, `IDdsBridge` provides `AddParticipant(uint domainId, string partitionName)` and `RemoveParticipant(int participantIndex)`.

**2. Dynamic Subscription Hot-Wiring** The engine handles the complexity of late-joining participants under the hood. When `AddParticipant` is called, the `DdsBridge` automatically instantiates the new `DdsParticipant` and immediately "hot-wires" all currently active topic subscriptions into it. This guarantees that `ISampleStore` seamlessly begins receiving data from the new domain without needing to be restarted or notified to re-subscribe.

**3. Event-Driven UI Updates** Rather than defining standard C# events on the manager interface (`ParticipantAdded`/`ParticipantRemoved`), the engine leverages the strictly decoupled `IEventBroker`. When the participant fleet changes, the system publishes a `ParticipantsChangedEvent(IReadOnlyList<ParticipantConfig> CurrentParticipants)`. Any Avalonia UI panel (like a status bar indicator showing active domains) simply subscribes to this global event to redraw itself.

**4. The Avalonia UI Integration** Your proposed `NetworkConfiguratorPlugin` maps 1:1 with the existing Blazor `ParticipantEditorDialog`. Your Avalonia ViewModel will:

1. Inject `IDdsBridge` and `IEventBroker`.
2. Maintain a working copy of the domain/partition configurations.
3. On "Apply", compute the diff and call `DdsBridge.RemoveParticipant` for removed domains and `DdsBridge.AddParticipant` for new domains.
4. Call `EventBroker.Publish(new ParticipantsChangedEvent(...))` so the rest of the shell updates.

**5. Generic Host & CLI Integration** You do not need a custom startup script to handle the CLI domain assignment. The engine's `DdsSettings` class already defines a `List<ParticipantConfig> Participants` property. Because you are using the .NET Generic Host configuration binder, passing array-indexed CLI arguments like `--DdsSettings:Participants:0:DomainId=0` automatically populates this list. During application startup, the `IDdsBridge` implementation is injected with this pre-populated list and spawns the initial participant fleet accordingly.









-----------



This is a fantastic revelation. The fact that `DdsMonitor.Engine` already implements these advanced, highly decoupled patterns—collectible assembly contexts, zero-allocation memory slicing, dictionary-backed persistence, safe type mapping, and dynamic fleet management—proves that it was built as a genuinely UI-agnostic powerhouse from day one.

Because the engine already owns the complex domain logic, the transition to Avalonia shifts from being a risky architectural rewrite to a highly deterministic UI-binding exercise.

Here is the definitive, corrected blueprint for your V1 Avalonia MVP, mapped perfectly to your existing engine:

### The Final `DdsMonitor.Avalonia` MVP Blueprint

1. **The Core Shell & Host**
   - **Bootstrapper:** `.NET Generic Host` maps `--DdsSettings` natively to populate the `Participants` list.
   - **State Management:** The Shell's `WindowManager` manages floating Avalonia Windows. It reads/writes the `workspace.json` and injects the specific `PanelState.ComponentState` dictionary into the Avalonia ViewModels upon instantiation.
   - **Fleet Spawning:** The DI container injects the populated `DdsSettings` into the existing `IDdsBridge` implementation, which spawns the initial participants seamlessly.
2. **The Frontend Plugin (`TopicExplorerPlugin`)**
   - Listens to `ITopicRegistry.Changed` and `IAssemblySourceService.Changed` to redraw the Avalonia tree.
   - Double-clicking fires an `IEventBroker` message to open a specific panel.
3. **The Schema & Network Plugin (`WorkspaceManagerPlugin`)**
   - **Schema UI:** Replicates `TopicSourcesPanel.razor`. Uses Avalonia's native `OpenFileDialog` to pass DLL paths to `IAssemblySourceService.Add()`, triggering the collectible context hot-load.
   - **Network UI:** Replicates `ParticipantEditorDialog`. Binds to `IDdsBridge.Participants` and calls `AddParticipant`/`RemoveParticipant`, publishing the `ParticipantsChangedEvent` via the broker.
4. **The High-Performance UI Plugin (`SamplesViewerPlugin`)**
   - **Virtualization:** The Avalonia `TreeDataGrid` requests data ranges via the ViewModel. The ViewModel calls `_activeView.GetVirtualView(startIndex, count)` for zero-allocation `ReadOnlyMemory<SampleData>` reads.
   - **Thread Safety:** The ViewModel subscribes to `SampleView.OnViewRebuilt` and dispatches a UI update containing `_activeView.CurrentFilteredCount`.
   - **Filtering:** The ViewModel compiles user input via `IFilterCompiler` and pushes the `Func<SampleData, bool>` to the background view.
5. **The Authoring Plugin (`SendSamplePlugin`)**
   - Replicates `SendSamplePanel`.
   - Queries the Avalonia port of `ITypeDrawerRegistry` to generate UI controls bound to `DrawerContext` instances.
   - When submitted, asks the engine for the writer via `IDdsBridge.GetWriter(TopicMetadata)` and executes `IDynamicWriter.Write(payload)`.





--------------





The refined MVP blueprint accurately maps the Avalonia UI integration points to the existing decoupled abstractions in the `DdsMonitor.Engine`. Your assessment of the Generic Host populating `DdsSettings`, the `WindowManager` passing `PanelState.ComponentState` for workspace persistence, the zero-allocation `GetVirtualView` slicing, and the `ITypeDrawerRegistry` bound to `DrawerContext` are all structurally sound.

However, assessing it for completeness reveals a few critical gaps that must be addressed to fulfill the original architectural requirements:

**1. Headless Execution is Missing** The blueprint focuses entirely on the UI plugins and misses the CLI/headless capability. The engine already provides a `HeadlessRunnerService` which implements `Record` and `Replay` modes driven by the `--DdsSettings:HeadlessMode` parameter. The Core Shell (Step 1) must explicitly implement the "dual-boot" strategy to run `await host.RunAsync()` when headless, bypassing the Avalonia window initialization entirely.

**2. Visualization vs. Authoring Asymmetry** Step 5 correctly identifies `ITypeDrawerRegistry` for two-way data authoring, but Step 4 (Samples Viewer) ignores payload data visualization. For deep inspection in the grid or details panel, the MVP needs to incorporate `IValueFormatterRegistry`, which provides Tier 1 syntax-highlighted token sequences and plain-text representations for specific CLR types.

**3. Extensibility API Context Menus** The blueprint relies on double-clicking to fire an `IEventBroker` message, but it omits the mechanism for contextual actions. A true VS Code-like shell requires the `IContextMenuRegistry` so that independent plugins can inject actions into other plugins' panels. For instance, the Authoring Plugin needs to inject "Clone to Send" into the Samples Grid row context menu without the Grid knowing the Authoring Plugin exists.

**4. Inter-Plugin Pub/Sub Details** While Step 2 mentions firing an `IEventBroker` message to open a panel, the engine already defines specific contracts for this, such as `SampleSelectedEvent` or `SpawnPanelEvent`. The MVP should explicitly specify using these existing message records for UI routing to maintain strict decoupling.

If you update the Core Shell to handle the headless branch and inject the formatter and context menu registries into your extension points, the blueprint will comprehensively cover the V1 requirements.



-----------





You are absolutely correct. Those four omissions are the exact connective tissue required to elevate this from a basic UI port to a true headless-capable, VS Code-style extensible framework.

Your integration of the `HeadlessRunnerService`, the distinction between `IValueFormatterRegistry` (read-only visualization) and `ITypeDrawerRegistry` (authoring), and the use of the existing `IContextMenuRegistry` and specific `IEventBroker` records (`SpawnPanelEvent`) perfectly round out the architecture.

With this finalized, completely bulletproof blueprint, we have a clear path to code.



----------------------





attempt to summarize the design


# DdsMonitor.Avalonia — V1 MVP Design

> Port of `DdsMonitor.Blazor` to a strictly local, multiplatform Avalonia desktop application, rebuilt from the ground up as a VS Code–style plugin shell on top of the existing `DdsMonitor.Engine`.

---

## 1. Goals & Non-Goals

### Goals
- **Multiplatform desktop**, not browser-hosted. Avalonia for the UI, .NET 8.
- **Strict plugin architecture**: the shell knows nothing about DDS. Every feature is delivered by a plugin loaded from a `/plugins` folder. A plugin assembly may contain multiple plugins.
- **Reuse `DdsMonitor.Engine` as-is** wherever possible. The Engine already implements expression-tree-compiled type metadata, a UI-agnostic broker/registry layer, dynamic participant management, collectible assembly contexts for schema DLLs, dictionary-backed panel state persistence, and a background-threaded filtering pipeline. The Avalonia work is mostly UI binding.
- **Headless dual-boot**: the same executable can run as either an interactive desktop app or a non-interactive CLI tool (record/replay), driven entirely by CLI arguments.
- **Compatibility with existing CLI arguments** (`--DdsSettings:DomainId=…`, `--AppSettings:TopicSources:0=…`, `--DdsSettings:HeadlessMode=Record`, etc.). No script changes for existing deployments.
- **Prove the architecture, not the feature set**. V1 deliberately ships a thin slice of features; the goal is to validate every high-risk vector (performance, decoupling, headless, persistence) before porting the rest.

### Non-Goals for V1
- Full feature parity with `DdsMonitor.Blazor`. Replay/record UI, hex viewer, filter builder, sparklines, statistics overlay, devel mode self-send UI, custom export formats, etc. are deferred.
- A docking framework. V1 uses **floating windows only**. `Dock.Avalonia` is a V2 swap-in once the panel manager contract is stable.
- Plugin sandboxing / signing / marketplace. Plugins are trusted code in V1.
- Hot-reload of plugin assemblies. Schema DLLs hot-load; plugins load once at startup.

---

## 2. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                     DdsMonitor.Avalonia (shell)                   │
│  Generic Host • DI • WindowManager • Plugin Loader • Main Menu   │
│  Toolbar • IUserSettings • workspace.json persistence            │
│   — does NOT reference DDS or Engine domain types directly —      │
└────────────┬──────────────────────────────────────┬──────────────┘
             │ resolves via DI                      │ loads at startup
             ▼                                      ▼
┌──────────────────────────────┐   ┌─────────────────────────────────┐
│  DdsMonitor.Avalonia.Core    │   │     /plugins/*.dll              │
│  (shared contracts)          │   │   StandardPlugin, Generator…    │
│  IMonitorPlugin              │◄──┤   each impl. IMonitorPlugin     │
│  IWindowManager / PanelAddr  │   │   referencing Core + Engine     │
│  IMenuRegistry, IToolbarReg  │   └──────────────┬──────────────────┘
│  IContextMenuRegistry        │                  │ ctor-injects
│  ITypeDrawerRegistry (Avlnia)│                  ▼
│  IUserSettings               │   ┌─────────────────────────────────┐
└──────────────┬───────────────┘   │      DdsMonitor.Engine          │
               │                   │  IDdsBridge • ITopicRegistry    │
               │ both reference    │  ISampleStore • SampleView      │
               └──────────────────►│  IFilterCompiler • TopicMetadata│
                                   │  IAssemblySourceService         │
                                   │  IEventBroker • IValueFormatter │
                                   │  HeadlessRunnerService          │
                                   └─────────────────────────────────┘
```

### Layering rules
- `DdsMonitor.Engine` — unchanged domain layer. Must be **purified** of remaining Blazor types (`RenderFragment` in `ISampleViewRegistry` and `ITypeDrawerRegistry`). Those become Blazor-free factory delegates.
- `DdsMonitor.Avalonia.Core` — UI-agnostic plugin contracts plus Avalonia-flavoured registries (`ITypeDrawerRegistry` Avalonia variant returning `Control` / `DataTemplate`). References the Engine for shared domain types.
- `DdsMonitor.Avalonia` — the shell executable. References Engine + Core. Implements registries, window manager, plugin loader. No DDS code.
- `DdsMonitor.Avalonia.StandardPlugin` (and any other plugin) — references Engine + Core only. Does **not** reference the shell executable.

---

## 3. Solution Layout

```
tools/DdsMonitor/
├── DdsMonitor.Engine/                       (existing, lightly purified)
├── DdsMonitor.Blazor/                       (existing, untouched)
├── DdsMonitor.Avalonia.Core/                NEW — shared contracts
├── DdsMonitor.Avalonia/                     NEW — shell exe
└── DdsMonitor.Avalonia.StandardPlugin/      NEW — V1 plugins live here
```

A single plugin assembly may host multiple `IMonitorPlugin` implementations; the loader iterates `assembly.ExportedTypes` looking for the interface, identical to the existing Blazor pattern.

---

## 4. Shell Responsibilities (`DdsMonitor.Avalonia`)

The shell is a `.NET 8` Avalonia desktop project that does five things and no more:

1. **Bootstrap the Generic Host.** `Host.CreateApplicationBuilder(args)` binds all existing `--AppSettings:*` and `--DdsSettings:*` CLI arguments to `IOptions<AppSettings>` / `IOptions<DdsSettings>` automatically — no custom parser.
2. **Register Engine and Core services** in DI. Engine exposes an `AddDdsEngineCore()` extension; the shell calls it and adds its own registry implementations.
3. **Discover and load plugins.** Iterate `./plugins/*.dll` with a collectible-aware `AssemblyLoadContext`, locate every `IMonitorPlugin`, call `ConfigureServices(IServiceCollection)` on each before building the container, then call `Initialize(IServiceProvider)` after.
4. **Dual-boot decision.** Read `DdsSettings.HeadlessMode`:
   - `None` → start background services AND boot Avalonia (`StartWithClassicDesktopLifetime`).
   - `Record` / `Replay` → `await host.RunAsync()`. Avalonia is never initialized; the existing `HeadlessRunnerService` handles the work.
5. **Provide the empty UI scaffold.** `ShellWindow` containing:
   - A `Menu` bound to `IMenuRegistry`.
   - A `ToolBar` (or `StackPanel`) bound to `IToolbarRegistry`.
   - A status bar (placeholder for V1).
   - **No panel area.** All panels are floating `Window` instances managed by `IWindowManager`.

### Why floating windows in V1
A docking framework is a large dependency and constrains the design of `IWindowManager`. By starting with plain `Window`s addressed by `PanelAddress`, every persistence and routing concern is forced into the contract, not the host. V2 swaps the implementation for `Dock.Avalonia` without changing plugins.

---

## 5. Core Contracts (`DdsMonitor.Avalonia.Core`)

### 5.1 Plugin lifecycle

```csharp
public interface IMonitorPlugin
{
    void ConfigureServices(IServiceCollection services);
    void Initialize(IServiceProvider provider);
}
```

The shell calls `ConfigureServices` on **all** plugins before `Build()`, then `Initialize` on **all** plugins afterwards. Plugins register their view-models and `IHostedService`s in `ConfigureServices`; they push UI registrations (menu items, toolbar buttons, context commands) in `Initialize`.

### 5.2 Panel addressing & window manager

The cornerstone of inter-plugin UI routing. A `PanelAddress` is an immutable, hierarchical identifier independent of physical placement.

```csharp
public sealed record PanelAddress(string Kind, string? Discriminator = null)
{
    public string Key => Discriminator is null ? Kind : $"{Kind}/{Discriminator}";
}
// Examples:
//   new("TopicExplorer")                      → singleton window
//   new("SamplesViewer", "RobotTelemetry")    → per-topic window, opens new instance per topic
```

```csharp
public interface IWindowManager
{
    /// Spawns the window if not already open; otherwise focuses it.
    /// PanelState (position, size, ComponentState dictionary) is restored from workspace.json.
    void Open(PanelAddress address, Func<PanelState, object> viewModelFactory, string title);

    void Close(PanelAddress address);

    bool IsOpen(PanelAddress address);
}
```

`PanelState` is the **existing Engine type** (`Engine/Workspace/PanelState.cs`) and includes window geometry plus the `Dictionary<string, object> ComponentState` slot used by Blazor today. The Avalonia `WindowManager` reuses this verbatim and writes through the existing `WorkspacePersistenceService` so `workspace.json` stays format-compatible with the Blazor version.

### 5.3 Stateful view-models (optional)

```csharp
public interface IStatefulViewModel
{
    void RestoreState(IReadOnlyDictionary<string, object> componentState);
    void CaptureState(IDictionary<string, object> componentState);
}
```

Implemented by view-models that need to persist filter strings, column widths, custom column definitions, etc. The `WindowManager` calls `CaptureState` on close (and debounced periodically), and `RestoreState` on open. The shell's persistence layer runs `FilterPersistableState` (existing Engine sanitizer) so plugins can only push JSON-safe primitives, arrays, and nested dictionaries.

### 5.4 Menu, toolbar, context menu

```csharp
public interface IMenuRegistry      { void Register(string path, ICommand command, string? icon = null); }
public interface IToolbarRegistry   { void Register(string id, ICommand command, string? icon, string tooltip); }

public interface IContextMenuRegistry
{
    /// Plugins register actions bound to a CLR type. The owning panel queries by type at open-time.
    void Register<T>(string title, Func<T, ICommand> commandFactory);
    IEnumerable<(string title, ICommand cmd)> Resolve<T>(T target);
}
```

The `IContextMenuRegistry` is the mechanism that lets, e.g., `SendSamplePlugin` inject a "Clone to Send" action into the `SamplesViewerPlugin`'s grid rows without either plugin referencing the other. The Engine already exposes an equivalent contract; the Core version simply wraps it for Avalonia commands.

### 5.5 Type drawers and value formatters

Two distinct registries — never combine them:

- **`IValueFormatterRegistry`** (already in Engine, UI-agnostic) — read-only display. Maps `Type` to a token sequence + plain-text representation for grid cells and inspector trees. Reused as-is.
- **`ITypeDrawerRegistry` Avalonia variant** (in `Avalonia.Core`) — two-way data authoring. Maps `Type` to a factory that produces an Avalonia `Control` bound to a `DrawerContext` (the existing Engine binding contract: `ValueGetter` / `OnChange` over the pre-compiled expression-tree setters).

```csharp
public interface IAvaloniaTypeDrawerRegistry
{
    void Register(Type type, Func<DrawerContext, Control> factory);
    Control Build(DrawerContext ctx);  // falls back to a generic StackPanel walker
}
```

The standard drawers for `int`, `double`, `string`, `bool`, `enum` ship in the shell or in a `StandardDrawersPlugin`. Plugins can register custom drawers for domain types (e.g., a `GeoCoord` map picker).

### 5.6 User settings & events

- **`IUserSettings`** — wraps a single `settings.json` in the OS AppData folder, namespaced by plugin id: `Get<T>(pluginId, key, default)`, `Set(pluginId, key, value)`, debounced `SaveAsync()`. This is **separate** from `workspace.json` (which holds per-panel state). User settings are global preferences ("show hidden topics", "theme").
- **`IEventBroker`** — reuse the Engine's existing broker. Strictly for **app state and user intent** — not for high-frequency DDS data. Pre-defined records reused from Engine (`SpawnPanelEvent`, `ParticipantsChangedEvent`, `SampleSelectedEvent`, `WorkspaceSaveRequestedEvent`).

---

## 6. The V1 Plugin Set

All five live in `DdsMonitor.Avalonia.StandardPlugin` (one assembly, multiple `IMonitorPlugin` classes). This proves multi-plugin-per-assembly loading from day one.

### 6.1 `DummyDataGeneratorPlugin` — backend prover
**Validates:** background services, headless mode, CLI parameter binding, context-menu cross-plugin injection.

- Registers an `IHostedService` that, when `--GeneratorPlugin:Enabled=true`, opens a writer via `IDdsBridge.GetWriter(...)` for a synthetic `Heartbeat` topic and publishes at `--GeneratorPlugin:PublishRateMs` (default 100ms; target 5000 Hz under stress to prove the firehose pipeline).
- Registers a "Toggle Dummy Generator" command in `IContextMenuRegistry` bound to topic names — `TopicExplorerPlugin` picks it up automatically.
- Adds an "Tools → Dummy Generator" menu item.
- Runs identically in headless mode (no UI dependency).

### 6.2 `WorkspaceManagerPlugin` — schema + network configuration
**Validates:** dynamic schema DLL hot-load via collectible `AssemblyLoadContext`, dynamic participant lifecycle, CLI override compatibility.

- Schema panel: Avalonia port of `TopicSourcesPanel.razor`. Uses the OS-native file picker → `IAssemblySourceService.Add(dllPath)` → existing `TopicDiscoveryService` does the rest. Listens for `IAssemblySourceService.Changed` and `ITopicRegistry.Changed`.
- Network panel: Avalonia port of `ParticipantEditorDialog`. Binds to `IDdsBridge.Participants`, calls `AddParticipant(domainId, partition)` / `RemoveParticipant(index)`, publishes `ParticipantsChangedEvent`.
- Honours startup CLI overrides (`--AppSettings:TopicSources:0=…`, `--DdsSettings:Participants:0:DomainId=0`) — they preload the same services this UI mutates.

### 6.3 `TopicExplorerPlugin` — frontend prover
**Validates:** Avalonia rendering, panel addressing, menu/toolbar/context-menu integration, user settings persistence, intent routing via `IEventBroker`.

- Opens `new PanelAddress("TopicExplorer")` — singleton floating window, position restored from `workspace.json`.
- `ListBox` bound to `ITopicRegistry` topics, refreshed on `ITopicRegistry.Changed`.
- Adds "View → Topic Explorer" menu item and a toolbar toggle.
- "Show Hidden Topics" checkbox — state read from / written to `IUserSettings` (proves global preference persistence).
- Right-click on a topic builds a context menu by calling `IContextMenuRegistry.Resolve<TopicName>(...)` — automatically includes "Toggle Dummy Generator" injected by 6.1.
- Double-click publishes `SpawnPanelEvent(new PanelAddress("SamplesViewer", topicName))`. The shell (or `SamplesViewerPlugin`) handles routing.

### 6.4 `SamplesViewerPlugin` — high-performance UI prover
**Validates:** per-instance addressed windows, virtualized 5 kHz+ data binding, background filtering, expression-tree payload inspection, per-instance state persistence.

- Subscribes to `SpawnPanelEvent` for `Kind == "SamplesViewer"`. Asks `IWindowManager.Open(...)` with a per-topic `Discriminator`. Two topics → two windows; the same topic twice → focus existing.
- View-model implements `IStatefulViewModel`: stores selected columns, column widths, sort direction, filter text in `ComponentState`.
- **Filtering pipeline:**
  1. View-model constructs an Engine `SampleView`, injecting the singleton `ISampleStore`. `SampleView` spins its own background worker.
  2. User filter string → `IFilterCompiler.Compile(expr)` → `Func<SampleData,bool>` → `_view.SetFilter(predicate)`.
  3. `SampleView.OnViewRebuilt` fires from the worker → view-model marshals to UI via `Dispatcher.UIThread.InvokeAsync` and tells the grid that `_view.CurrentFilteredCount` changed.
  4. Avalonia `TreeDataGrid` pulls visible rows by calling `_view.GetVirtualView(start, count)` → zero-allocation `ReadOnlyMemory<SampleData>` slice. No UI-thread filtering, no per-sample event, no polling timer.
- **Detail inspector** (lower half of the same window, or a child panel): on row click, iterates `sample.TopicMetadata.AllFields` (the pre-compiled `FieldMetadata`s) and builds a tree of view-models. Each leaf renders via `IValueFormatterRegistry` for display.

### 6.5 `SendSamplePlugin` — authoring prover
**Validates:** two-way binding through compiled expression-tree setters, the `ITypeDrawerRegistry`/`DrawerContext` round-trip, writer acquisition through `IDdsBridge.GetWriter`.

- Registers a "Clone to Send" context-menu action on `SampleData` rows (cross-plugin injection into 6.4).
- Spawns `new PanelAddress("SendSample", topicName)`.
- Instantiates an empty payload via `Activator.CreateInstance(TopicMetadata.TopicType)`, wraps each `FieldMetadata` in a `DrawerContext`, and queries `IAvaloniaTypeDrawerRegistry.Build(ctx)` for each field. The returned `Control` is strictly typed (e.g., `NumericUpDown` for `int`) — no `Convert.ChangeType` on submit.
- On "Send", calls `IDdsBridge.GetWriter(TopicMetadata)` then `writer.Write(payload)`.

---

## 7. Critical Architectural Patterns

These five concepts are the ones that, if mis-designed, require a rewrite later. The V1 MVP intentionally exercises each one:

| Concern | Pattern | Where it's proved in V1 |
| --- | --- | --- |
| Headless dual-boot | Generic Host branches on `HeadlessMode` before Avalonia init | 6.1 + shell `Program.cs` |
| Schema DLL hot-load | Collectible `AssemblyLoadContext` via `TopicDiscoveryService` | 6.2 |
| Dynamic DDS participants | `IDdsBridge.AddParticipant` / `RemoveParticipant` + `ParticipantsChangedEvent` | 6.2 |
| Firehose UI strategy | `SampleView` background worker + `OnViewRebuilt` + `GetVirtualView` zero-alloc slice | 6.4 |
| Inter-plugin UI injection | `IContextMenuRegistry` keyed by CLR type | 6.1 → 6.4, 6.5 → 6.4 |
| Addressed panel persistence | `PanelAddress` + `PanelState.ComponentState` dictionary | 6.4 |
| Expression-tree payload IO | Engine's `TopicMetadata.AllFields` Getter/Setter | 6.4 (read), 6.5 (write) |

---

## 8. Migration Path From `DdsMonitor.Blazor`

The Blazor app stays running and shipping during the migration. Avalonia is built alongside it.

### Phase 0 — Engine purification (small, mechanical)
Goal: remove the last Blazor leak so the Engine compiles without `Microsoft.AspNetCore.Components`.

- `ISampleViewRegistry` and `ITypeDrawerRegistry` currently use `RenderFragment`. Replace with `Type` + a UI-agnostic factory delegate (e.g., `Func<DrawerContext, object>`), where the `object` is whatever the UI shell understands. The Blazor app provides a Blazor adapter that casts back to `RenderFragment`; the Avalonia app provides an adapter producing `Control`.
- Verify the Engine builds and tests pass with no `Microsoft.AspNetCore.*` reference.

### Phase 1 — Empty shell
- Create `DdsMonitor.Avalonia.Core` with the contracts in §5.
- Create `DdsMonitor.Avalonia` referencing Engine + Core. Generic Host, plugin loader (port from Blazor's `PluginLoader`), `WindowManager`, registry implementations, `ShellWindow.axaml` with menu + toolbar bound to the registries.
- Smoke test: app launches, empty menu, no plugins, exits cleanly. Headless path: `--DdsSettings:HeadlessMode=Record` does not open a window.

### Phase 2 — First plugin pair
- Implement `WorkspaceManagerPlugin` (schema panel only) and `TopicExplorerPlugin`. Both alone validate plugin loading, registry wiring, `ITopicRegistry` consumption, and dynamic DLL hot-load.
- Acceptance: load a schema DLL via the schema panel → topic list updates in the explorer.

### Phase 3 — Backend prover
- Implement `DummyDataGeneratorPlugin`. Add `IContextMenuRegistry` integration so the explorer shows the injected command.
- Acceptance: `--HeadlessMode=Record --GeneratorPlugin:Enabled=true` writes a recording file with no UI. Interactive mode shows the cross-plugin context-menu item.

### Phase 4 — Firehose UI
- Implement `SamplesViewerPlugin` with `TreeDataGrid` + `SampleView`. This is the highest-risk slice; it should hit 5 kHz sustained from the dummy generator with the grid scrolling smoothly.
- Acceptance: 5 000 samples/s for 60 s, no dropped frames, RAM stable, filter text changes apply within 200 ms.

### Phase 5 — Authoring + Participants
- Implement `SendSamplePlugin` and finish `WorkspaceManagerPlugin` participant editor.
- Acceptance: send a hand-authored sample on a user-loaded schema, observe it round-trip back to the viewer.

### Phase 6 — Workspace polish
- Per-panel `IStatefulViewModel` round-trip for the samples viewer. Confirm `workspace.json` is byte-compatible with the Blazor version where panel kinds overlap.
- Acceptance: open three samples viewers on different topics, resize, move across monitors, close app, reopen — exact restore.

After Phase 6, V1 is complete. V2 work (Dock.Avalonia, hex viewer, replay UI, filter builder, sparklines, statistics overlay, export plugins, themes) becomes incremental plugin work against a stable contract.

---

## 9. Open Questions / Risks

1. **`TreeDataGrid` virtualization limits.** Avalonia's grid handles tens of thousands of rows well; the prover at Phase 4 must confirm it sustains 5 kHz append + filter without lag. Fallback: a custom `Canvas`-based virtualized list.
2. **Collectible `AssemblyLoadContext` for plugins.** The Engine already uses collectible contexts for schema DLLs. Plugins in V1 are non-collectible (loaded once). If V2 wants plugin hot-reload, the plugin loader will need the collectible variant — but every static cache in plugins becomes a leak. Defer to V2.
3. **Threading model around `IEventBroker`.** The Engine's broker delivers on the publisher's thread. UI subscribers must marshal via `Dispatcher.UIThread.InvokeAsync`. Make this explicit in plugin guidelines; consider adding a `Subscribe(handler, dispatch: UiThread)` overload to the Core wrapper.
4. **`workspace.json` schema compatibility.** Keeping byte-compatibility with the Blazor version is desirable but optional. The Avalonia `WindowManager` may write Avalonia-specific keys (`X`, `Y` of the floating window) inside `PanelState.ComponentState["__window"]` to keep the top-level schema unchanged.
5. **Standard drawers location.** `int`/`double`/`string`/`bool`/`enum` drawers could ship in the shell or in a tiny `DefaultDrawersPlugin`. Putting them in a plugin enforces the dogfood rule (the shell knows nothing). Recommendation: separate plugin.

---

## 10. V1 Acceptance Checklist

When all of the following pass, V1 is done:

- [ ] Engine builds with zero `Microsoft.AspNetCore.*` references.
- [ ] `DdsMonitor.Avalonia.exe` launches an empty shell on Windows, Linux, and macOS.
- [ ] `DdsMonitor.Avalonia.exe --DdsSettings:HeadlessMode=Record --DdsSettings:HeadlessFilePath=out.json --GeneratorPlugin:Enabled=true` records a file with no UI.
- [ ] Five plugins load from `./plugins/DdsMonitor.Avalonia.StandardPlugin.dll`, all from one assembly.
- [ ] User loads a schema DLL at runtime via the schema panel; topics appear in the explorer without app restart.
- [ ] User adds a second DDS participant on a different domain at runtime; both participants' topics merge into the registry.
- [ ] Double-click on a topic opens a samples viewer addressed to that topic. Double-click on a second topic opens a second viewer. Double-click on the first topic again focuses the existing window.
- [ ] Samples viewer sustains 5 kHz from the dummy generator with smooth scrolling and live filter response.
- [ ] Right-click on a topic in the explorer shows the "Toggle Dummy Generator" command injected by a different plugin.
- [ ] Right-click on a sample row in the viewer shows "Clone to Send" injected by yet another plugin, which opens the send panel pre-filled with the sample's payload.
- [ ] User edits a payload field in the send panel; submitting it round-trips back to the viewer.
- [ ] All existing `--AppSettings:*` and `--DdsSettings:*` CLI arguments from the Blazor version continue to work without modification.
- [ ] Window positions, column layouts, filter strings, and the "Show Hidden Topics" preference all survive an app restart.



------------------------------------

Reviewing the V1 design document above against our architectural refinements reveals several gaps and outdated concepts that diverge from the established capabilities of `DdsMonitor.Engine`. 

Here are the flaws that need to be corrected in the blueprint:

**1. Outdated State Persistence Pattern**
Section 5.3 and Section 6.4 still define an `IStatefulViewModel` interface requiring the window manager to invoke `CaptureState` and `RestoreState` lifecycle callbacks. This contradicts our refined approach. The existing engine already provides a `PanelState` model containing a `Dictionary<string, object> ComponentState`. The Avalonia shell should inject this specific dictionary directly into the plugin's ViewModel upon instantiation. The ViewModel then mutates its dictionary keys continuously and relies on the `IEventBroker` to publish a `WorkspaceSaveRequestedEvent`, triggering the shell's debounced persistence pipeline.

**2. Omission of `ISampleViewRegistry` in the Detail Inspector**
The document correctly segregates `IValueFormatterRegistry` (for read-only text formatting) and the Avalonia variant of `ITypeDrawerRegistry` (for two-way data authoring). However, Section 6.4 mandates that the Detail Inspector must always iterate `TopicMetadata.AllFields` to construct a generic tree of view-models. This completely ignores the `ISampleViewRegistry` extension point. To support complex domain types, the Detail Inspector must first query `ISampleViewRegistry` to determine if a plugin has registered a custom Avalonia view to entirely replace the default hierarchical tree for a specific CLR payload type.

**3. Unresolved EventBroker Threading Model**
Section 9 flags the threading model for `IEventBroker` as an "Open Question", suggesting we rely on plugin guidelines for UI dispatching. In a strict MVVM desktop shell, this is an architectural hazard. Because the Engine's broker delivers messages on the publisher's background thread, leaving thread marshaling up to individual plugin developers guarantees inevitable cross-thread UI crashes. The `DdsMonitor.Avalonia.Core` implementation of the broker must explicitly enforce `Dispatcher.UIThread` marshaling for UI-bound subscriptions.

**4. `PanelAddress` vs `PanelId` Divergence**
The design proposes a new immutable, hierarchical `PanelAddress` type for routing UI intent. This introduces unnecessary friction with the Engine's existing `PanelState` implementation, which relies on a simple string `PanelId` to maintain seamless `workspace.json` compatibility. To reuse the engine's `WorkspacePersistenceService` without modification, the shell should adhere to the established string-based `PanelId` convention (e.g., concatenating the panel type and discriminator, like `"SamplesViewer_RobotTelemetry"`) rather than inventing a new domain type.



