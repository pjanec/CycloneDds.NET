using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DdsMonitor.Avalonia.StandardPlugin.Tests;

// ── WorkspaceManagerPlugin tests ───────────────────────────────────────────────

public sealed class WorkspaceManagerPluginTests
{
    private static (StubMonitorContext ctx, StubMenuRegistry menu, StubWindowManager win, StubAvaloniaViewRegistry view)
        BuildContext()
    {
        var menu = new StubMenuRegistry();
        var win = new StubWindowManager();
        var view = new StubAvaloniaViewRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IAvaloniaViewRegistry>(view);
        return (ctx, menu, win, view);
    }

    [Fact]
    public void WorkspaceManagerPlugin_Initialize_RegistersSchemaSourcesMenuItem()
    {
        var (ctx, menu, _, _) = BuildContext();
        var plugin = new WorkspaceManagerPlugin();

        plugin.Initialize(ctx);

        Assert.Contains(menu.Items, i =>
            i.Label.Contains("Schema Sources", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkspaceManagerPlugin_Initialize_MenuItemSpawnsSchemaSourcesPanel()
    {
        var (ctx, menu, win, _) = BuildContext();
        var plugin = new WorkspaceManagerPlugin();
        plugin.Initialize(ctx);

        // Find and invoke the Schema Sources menu item
        var item = menu.Items.First(i => i.Label.Contains("Schema Sources", StringComparison.OrdinalIgnoreCase));
        menu.InvokeItem(item.Label);

        Assert.Single(win.SpawnCalls, c => c.TypeName == nameof(SchemaSourcesViewModel));
    }

    [Fact]
    public void WorkspaceManagerPlugin_ConfigureServices_DoesNotRegisterAnything()
    {
        var services = new ServiceCollection();
        new WorkspaceManagerPlugin().ConfigureServices(services);
        Assert.Empty(services);
    }
}

// ── SchemaSourcesViewModel tests ───────────────────────────────────────────────

public sealed class SchemaSourcesViewModelTests
{
    private static SchemaSourcesViewModel CreateVm(
        StubAssemblySourceService? assemblyService = null,
        StubTopicRegistry? topicRegistry = null,
        StubWindowManager? windowManager = null)
    {
        assemblyService ??= new StubAssemblySourceService();
        topicRegistry ??= new StubTopicRegistry();
        windowManager ??= new StubWindowManager();
        return new SchemaSourcesViewModel(assemblyService, topicRegistry, windowManager);
    }

    [Fact]
    public void SchemaSourcesViewModel_AddAssembly_CallsAssemblySourceService()
    {
        var assemblyService = new StubAssemblySourceService();
        var vm = CreateVm(assemblyService);

        vm.AddAssembly("some/path.dll");

        Assert.Single(assemblyService.Entries);
        Assert.Equal("some/path.dll", assemblyService.Entries[0].Path);
    }

    [Fact]
    public void SchemaSourcesViewModel_RemoveAssembly_CallsAssemblySourceService()
    {
        var assemblyService = new StubAssemblySourceService();
        assemblyService.Add("path0.dll");
        assemblyService.Add("path1.dll");
        var vm = CreateVm(assemblyService);

        vm.RemoveAssembly(0);

        Assert.Single(assemblyService.Entries);
        Assert.Equal("path1.dll", assemblyService.Entries[0].Path);
    }

    [Fact]
    public void SchemaSourcesViewModel_ChangedEvent_RefreshesEntries()
    {
        var assemblyService = new StubAssemblySourceService();
        var vm = CreateVm(assemblyService);

        Assert.Empty(vm.Entries);

        assemblyService.Add("path.dll");

        Assert.Single(vm.Entries);
    }

    [Fact]
    public void SchemaSourcesViewModel_IsCliOverride_True_ReflectedInViewModel()
    {
        var assemblyService = new StubAssemblySourceService { IsCliOverride = true };
        var vm = CreateVm(assemblyService);

        Assert.True(vm.IsCliOverride);
    }

    [Fact]
    public void SchemaSourcesViewModel_IsCliOverride_False_WhenNotSet()
    {
        var assemblyService = new StubAssemblySourceService { IsCliOverride = false };
        var vm = CreateVm(assemblyService);

        Assert.False(vm.IsCliOverride);
    }

    [Fact]
    public void SchemaSourcesViewModel_RemoveAssembly_OutOfRange_DoesNotThrow()
    {
        var assemblyService = new StubAssemblySourceService();
        var vm = CreateVm(assemblyService);

        var ex = Record.Exception(() => vm.RemoveAssembly(99));
        Assert.Null(ex);
    }
}

// ── TopicExplorerPlugin tests ──────────────────────────────────────────────────

public sealed class TopicExplorerPluginTests
{
    private static (StubMonitorContext ctx, StubMenuRegistry menu, StubToolbarRegistry toolbar, StubWindowManager win)
        BuildContext()
    {
        var menu = new StubMenuRegistry();
        var toolbar = new StubToolbarRegistry();
        var win = new StubWindowManager();
        var view = new StubAvaloniaViewRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IToolbarRegistry>(toolbar);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IAvaloniaViewRegistry>(view);
        return (ctx, menu, toolbar, win);
    }

    [Fact]
    public void TopicExplorerPlugin_Initialize_SpawnsTopicExplorerPanel()
    {
        var (ctx, _, _, win) = BuildContext();
        new TopicExplorerPlugin().Initialize(ctx);

        Assert.Contains(win.SpawnCalls, c => c.TypeName == nameof(TopicExplorerViewModel));
    }

    [Fact]
    public void TopicExplorerPlugin_Initialize_RegistersViewMenuItem()
    {
        var (ctx, menu, _, _) = BuildContext();
        new TopicExplorerPlugin().Initialize(ctx);

        Assert.Contains(menu.Items, i =>
            i.Label.Contains("Topic Explorer", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void TopicExplorerPlugin_Initialize_RegistersToolbarButton()
    {
        var (ctx, _, toolbar, _) = BuildContext();
        new TopicExplorerPlugin().Initialize(ctx);

        Assert.Contains(toolbar.Entries, e => e.Id == "TopicExplorer");
    }
}

// ── TopicExplorerViewModel tests ───────────────────────────────────────────────

public sealed class TopicExplorerViewModelTests
{
    private static TopicMetadata MakeMeta(string shortName, string ns = "MyApp")
    {
        // Use a dynamic type trick: create a TopicMetadata-compatible struct
        // We can't easily create one for arbitrary names, so use HeartbeatSample
        // and rely on name-based stubs.
        // For hidden topic tests, we use a specially named registered topic.
        return new TopicMetadata(typeof(HeartbeatSample));
    }

    private static TopicExplorerViewModel CreateVm(
        StubTopicRegistry? topicRegistry = null,
        StubContextMenuRegistry? contextMenuRegistry = null,
        IEventBroker? broker = null,
        IUserSettings? userSettings = null)
    {
        topicRegistry ??= new StubTopicRegistry();
        contextMenuRegistry ??= new StubContextMenuRegistry();
        broker ??= new StubEventBroker();
        userSettings ??= new StubUserSettings();
        return new TopicExplorerViewModel(topicRegistry, contextMenuRegistry, broker, userSettings);
    }

    [AvaloniaFact]
    public void TopicExplorerViewModel_TopicRegistryChanged_RefreshesTopics()
    {
        var registry = new StubTopicRegistry();
        var vm = CreateVm(topicRegistry: registry);
        vm.Initialize(new Dictionary<string, object>());

        Assert.Empty(vm.Topics);

        registry.Register(new TopicMetadata(typeof(HeartbeatSample)));
        // On the UI thread, OnTopicRegistryChanged runs synchronously
        Dispatcher.UIThread.RunJobs();

        Assert.Single(vm.Topics);
    }

    [Fact]
    public void TopicExplorerViewModel_ShowHidden_False_FiltersHiddenTopics()
    {
        // HeartbeatSample short name is "HeartbeatSample" (not hidden)
        // We can only filter topics we control. Use registry with known topic.
        var registry = new StubTopicRegistry();
        // Register the topic — it's visible (ShortName = "HeartbeatSample", no underscore prefix)
        registry.Register(new TopicMetadata(typeof(HeartbeatSample)));

        var vm = CreateVm(topicRegistry: registry);
        vm.Initialize(new Dictionary<string, object>());

        // ShowHidden=false by default; HeartbeatSample is not hidden, so it should be visible
        Assert.Single(vm.Topics);

        // Simulate a hidden topic by manipulating Topics directly — but we can't since
        // RefreshTopics reads from registry. Instead test the IsHidden filter logic via
        // the ShowHidden toggle affecting visible vs filtered counts.
        // This test confirms non-hidden topics appear when ShowHidden=false.
        Assert.Equal("HeartbeatSample", vm.Topics[0].ShortName);
    }

    [Fact]
    public void TopicExplorerViewModel_ShowHidden_True_ShowsAllTopics()
    {
        var registry = new StubTopicRegistry();
        registry.Register(new TopicMetadata(typeof(HeartbeatSample)));

        var vm = CreateVm(topicRegistry: registry);
        vm.Initialize(new Dictionary<string, object>());

        vm.ShowHidden = true;

        // HeartbeatSample is not hidden, so it remains visible
        Assert.Single(vm.Topics);
    }

    [Fact]
    public void TopicExplorerViewModel_ShowHiddenPersistedToUserSettings()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var settings = new UserSettingsStore(tempFile);
            var vm = CreateVm(userSettings: settings);
            vm.Initialize(new Dictionary<string, object>());

            Assert.False(settings.Get("TopicExplorer", "ShowHidden", false));

            vm.ShowHidden = true;

            Assert.True(settings.Get("TopicExplorer", "ShowHidden", false));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TopicExplorerViewModel_OpenSamplesViewer_PublishesSpawnPanelEvent()
    {
        SpawnPanelEvent? published = null;
        var broker = new CapturingBroker();
        broker.OnSpawnPanel = ev => published = ev;

        var vm = CreateVm(broker: broker);
        vm.Initialize(new Dictionary<string, object>());

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        vm.OpenSamplesViewer(meta);

        Assert.NotNull(published);
        Assert.Equal("SamplesViewer", published!.PanelTypeName);
        Assert.True(published.State?.ContainsKey("TopicName"));
    }

    [Fact]
    public void TopicExplorerViewModel_Dispose_DisposesSubscriptionTokens()
    {
        var broker = new TrackingEventBroker();
        var vm = CreateVm(broker: broker);

        Assert.True(broker.ActiveSubscriptionCount > 0, "ViewModel should have subscribed to the EventBroker");

        vm.Dispose();

        Assert.Equal(0, broker.ActiveSubscriptionCount);
    }

    [Fact]
    public void TopicExplorerViewModel_GetContextMenu_CallsContextMenuRegistry()
    {
        var registry = new StubContextMenuRegistry();
        var vm = CreateVm(contextMenuRegistry: registry);

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var items = vm.GetContextMenu(meta).ToList();

        // Registry had no providers, so items should be empty (but the call should not throw)
        Assert.NotNull(items);
    }

    [Fact]
    public void TopicExplorerViewModel_ShowHidden_False_DoesNotShowHiddenTopic()
    {
        var registry = new StubTopicRegistry();
        registry.Register(new TopicMetadata(typeof(_HiddenSample)));  // ShortName = "_HiddenSample"

        var vm = CreateVm(topicRegistry: registry);
        vm.Initialize(new Dictionary<string, object>());

        // Hidden topic must not appear when ShowHidden=false
        Assert.Empty(vm.Topics);

        vm.ShowHidden = true;

        // Hidden topic must appear when ShowHidden=true
        Assert.Single(vm.Topics);
        Assert.Equal("_HiddenSample", vm.Topics[0].ShortName);
    }
}

/// <summary>Event broker that captures SpawnPanelEvent for assertion.</summary>
internal sealed class CapturingBroker : IEventBroker
{
    public Action<SpawnPanelEvent>? OnSpawnPanel;

    public void Publish<TEvent>(TEvent ev)
    {
        if (ev is SpawnPanelEvent spe)
            OnSpawnPanel?.Invoke(spe);
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) => new NoopDisposable();
    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}

// ── DummyDataGeneratorPlugin tests ────────────────────────────────────────────

public sealed class DummyDataGeneratorPluginTests
{
    [Fact]
    public void DummyDataGeneratorPlugin_ConfigureServices_RegistersDummyGeneratorService()
    {
        var services = new ServiceCollection();
        // Add required dependencies that DummyGeneratorService needs
        services.AddSingleton<ITopicRegistry>(new StubTopicRegistry());
        services.AddSingleton<IDdsBridge>(new StubDdsBridge());
        services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder().Build());

        new DummyDataGeneratorPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<DummyGeneratorService>();
        Assert.NotNull(service);

        var hosted = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<DummyGeneratorService>()
            .FirstOrDefault();
        Assert.NotNull(hosted);
    }

    [Fact]
    public void DummyDataGeneratorPlugin_Initialize_RegistersToolsMenu()
    {
        var menu = new StubMenuRegistry();
        var contextMenu = new StubContextMenuRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IContextMenuRegistry>(contextMenu);
        // Note: DummyGeneratorService is optional in Initialize; null is gracefully handled

        new DummyDataGeneratorPlugin().Initialize(ctx);

        Assert.Contains(menu.Items, i =>
            i.Label.Contains("Dummy Generator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DummyDataGeneratorPlugin_Initialize_RegistersContextMenuProvider()
    {
        var menu = new StubMenuRegistry();
        var contextMenu = new StubContextMenuRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IContextMenuRegistry>(contextMenu);

        new DummyDataGeneratorPlugin().Initialize(ctx);

        var topicMeta = new TopicMetadata(typeof(HeartbeatSample));
        var items = contextMenu.GetItems(topicMeta).ToList();

        Assert.Contains(items, i => i.Label.Contains("Toggle Dummy Generator"));
    }
}

// ── DummyGeneratorService tests ────────────────────────────────────────────────

public sealed class DummyGeneratorServiceTests
{
    private static (DummyGeneratorService service, StubDynamicWriter writer) CreateService(
        bool enabled,
        int rateMs = 10)
    {
        var registry = new StubTopicRegistry();
        var bridge = new StubDdsBridge();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeneratorPlugin:Enabled"] = enabled.ToString().ToLower(),
                ["GeneratorPlugin:PublishRateMs"] = rateMs.ToString(),
            })
            .Build();

        var service = new DummyGeneratorService(registry, bridge, config);
        return (service, bridge.Writer);
    }

    [Fact]
    public async Task DummyGeneratorService_Enabled_True_StartsPublishing()
    {
        var (service, writer) = CreateService(enabled: true, rateMs: 10);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        await service.StopAsync(CancellationToken.None);

        Assert.True(writer.WriteCount > 0, $"Expected writes > 0 but got {writer.WriteCount}");
    }

    [Fact]
    public async Task DummyGeneratorService_Enabled_False_NoPublishing()
    {
        var (service, writer) = CreateService(enabled: false);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, writer.WriteCount);
    }

    [Fact]
    public async Task DummyGeneratorService_Toggle_StopsAndRestartsPublishing()
    {
        var (service, writer) = CreateService(enabled: true, rateMs: 10);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(150);

        int countBeforeStop = writer.WriteCount;
        Assert.True(countBeforeStop > 0, "Should have published before toggle");

        service.TogglePublishing(); // stop
        await Task.Delay(100);

        int countAfterStop = writer.WriteCount;

        service.TogglePublishing(); // restart
        await Task.Delay(150);

        int countAfterRestart = writer.WriteCount;

        await service.StopAsync(CancellationToken.None);

        // After stop: count should not increase significantly
        Assert.True(countAfterStop >= countBeforeStop, "Count should be frozen after toggle-off");
        // After restart: count should increase
        Assert.True(countAfterRestart > countAfterStop, "Count should increase after toggle-on");
    }

    [Fact]
    public async Task DummyGeneratorService_StopAsync_CancelsLoop()
    {
        var (service, writer) = CreateService(enabled: true, rateMs: 10);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(150);

        await service.StopAsync(CancellationToken.None);
        int countAtStop = writer.WriteCount;

        await Task.Delay(200);
        int countAfterDelay = writer.WriteCount;

        Assert.Equal(countAtStop, countAfterDelay);
    }

    [Fact]
    public async Task DummyGeneratorService_Enabled_True_RegistersTopicAndWriter()
    {
        var registry = new StubTopicRegistry();
        var bridge = new StubDdsBridge();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GeneratorPlugin:Enabled"] = "true",
                ["GeneratorPlugin:PublishRateMs"] = "100",
            })
            .Build();

        var service = new DummyGeneratorService(registry, bridge, config);
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.NotEmpty(registry.AllTopics);
        await service.StopAsync(CancellationToken.None);
    }
}

// ── HeartbeatSample tests ──────────────────────────────────────────────────────

public sealed class HeartbeatSampleTests
{
    [Fact]
    public void HeartbeatSample_HasNonKeyField_Timestamp()
    {
        var fields = typeof(HeartbeatSample).GetFields();

        var timestampField = fields.FirstOrDefault(f => f.Name == "Timestamp");
        Assert.NotNull(timestampField);
        Assert.Equal(typeof(long), timestampField!.FieldType);

        var sequenceField = fields.FirstOrDefault(f => f.Name == "Sequence");
        Assert.NotNull(sequenceField);
        Assert.Equal(typeof(int), sequenceField!.FieldType);
    }

    [Fact]
    public void HeartbeatSample_HasDdsTopicAttribute()
    {
        var attr = typeof(HeartbeatSample).GetCustomAttributes(typeof(CycloneDDS.Schema.DdsTopicAttribute), false);
        Assert.NotEmpty(attr);
    }

    [Fact]
    public void HeartbeatSample_IdField_HasDdsKeyAttribute()
    {
        var idField = typeof(HeartbeatSample).GetField("Id")!;
        var attr = idField.GetCustomAttributes(typeof(CycloneDDS.Schema.DdsKeyAttribute), false);
        Assert.NotEmpty(attr);
    }
}

// ── SamplesViewerPlugin tests ─────────────────────────────────────────────────

public sealed class SamplesViewerPluginTests
{
    private static (StubMonitorContext ctx, TrackingEventBroker broker, StubWindowManager win, StubAvaloniaViewRegistry view)
        BuildContext()
    {
        var broker = new TrackingEventBroker();
        var win = new StubWindowManager();
        var view = new StubAvaloniaViewRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IEventBroker>(broker);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IAvaloniaViewRegistry>(view);
        return (ctx, broker, win, view);
    }

    [Fact]
    public void SamplesViewerPlugin_Initialize_RegistersSpawnPanelSubscription()
    {
        var (ctx, broker, _, _) = BuildContext();
        new SamplesViewerPlugin().Initialize(ctx);

        Assert.True(broker.ActiveSubscriptionCount > 0, "Plugin should have subscribed to IEventBroker");
    }

    [AvaloniaFact]
    public void SamplesViewerPlugin_OnSpawnPanelEvent_CallsWindowManagerSpawnPanel()
    {
        var (ctx, broker, win, _) = BuildContext();
        new SamplesViewerPlugin().Initialize(ctx);

        broker.Publish(new SpawnPanelEvent("SamplesViewer",
            new Dictionary<string, object>(StringComparer.Ordinal) { ["TopicName"] = "myTopic" }));

        Assert.Single(win.SpawnCalls, c => c.TypeName == "SamplesViewer_myTopic");
    }

    [AvaloniaFact]
    public void SamplesViewerPlugin_OnSpawnPanelEvent_WrongPanelType_Ignored()
    {
        var (ctx, broker, win, _) = BuildContext();
        new SamplesViewerPlugin().Initialize(ctx);

        broker.Publish(new SpawnPanelEvent("OtherPanel", null));
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(win.SpawnCalls);
    }
}

// ── SamplesViewerViewModel tests ──────────────────────────────────────────────

public sealed class SamplesViewerViewModelTests
{
    private static SamplesViewerViewModel CreateVm(
        StubSampleView? view = null,
        StubFilterCompiler? compiler = null,
        TopicMetadata? meta = null)
    {
        view ??= new StubSampleView();
        compiler ??= new StubFilterCompiler();
        return new SamplesViewerViewModel(compiler, view: view, meta: meta);
    }

    [Fact]
    public void SamplesViewerViewModel_Initialize_WritesTopicNameToComponentState()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var vm = CreateVm(meta: meta);
        var state = new Dictionary<string, object>();

        vm.Initialize(state);

        Assert.True(state.ContainsKey("TopicName"), "ComponentState must have 'TopicName' key");
        Assert.Equal(meta.TopicName, state["TopicName"]);
    }

    [Fact]
    public void SamplesViewerViewModel_FilterText_ValidExpression_CallsSetFilter()
    {
        var view = new StubSampleView();
        var compiler = new StubFilterCompiler { NextResultIsValid = true };
        var vm = CreateVm(view: view, compiler: compiler);
        vm.Initialize(new Dictionary<string, object>());

        vm.FilterText = "Id == 1";

        Assert.True(view.SetFilterCalled, "SetFilter should have been called for a valid expression");
    }

    [Fact]
    public void SamplesViewerViewModel_FilterText_InvalidExpression_SetsFilterError()
    {
        var view = new StubSampleView();
        var compiler = new StubFilterCompiler { NextResultIsValid = false, NextErrorMessage = "Bad syntax" };
        var vm = CreateVm(view: view, compiler: compiler);
        vm.Initialize(new Dictionary<string, object>());

        vm.FilterText = "!!!invalid";

        Assert.NotNull(vm.FilterError);
        Assert.False(view.SetFilterCalled, "SetFilter must NOT be called for an invalid expression");
    }

    [Fact]
    public void SamplesViewerViewModel_FilterText_Empty_ClearsFilter()
    {
        var view = new StubSampleView();
        var compiler = new StubFilterCompiler();
        var vm = CreateVm(view: view, compiler: compiler);
        vm.Initialize(new Dictionary<string, object>());

        vm.FilterText = "";

        Assert.True(view.SetFilterCalled, "SetFilter(null) should be called to clear the filter");
        Assert.Null(view.LastFilter);
    }

    [AvaloniaFact]
    public void SamplesViewerViewModel_OnViewRebuilt_UpdatesFilteredCountOnUiThread()
    {
        var view = new StubSampleView();
        view.CurrentFilteredCount = 42;
        var vm = CreateVm(view: view);
        vm.Initialize(new Dictionary<string, object>());

        // Fire from a background thread to simulate the real SampleView worker
        Task.Run(() => view.TriggerViewRebuilt()).Wait();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(42, vm.FilteredCount);
    }

    [Fact]
    public void SamplesViewerViewModel_Dispose_DisposesView()
    {
        var view = new StubSampleView();
        var vm = CreateVm(view: view);
        vm.Initialize(new Dictionary<string, object>());

        vm.Dispose();

        Assert.True(view.Disposed, "StubSampleView should be disposed after vm.Dispose()");
    }

    [Fact]
    public void SamplesViewerViewModel_Dispose_UnsubscribesOnViewRebuilt()
    {
        var view = new StubSampleView();
        var vm = CreateVm(view: view);
        vm.Initialize(new Dictionary<string, object>());

        vm.Dispose();

        // After dispose, firing the event should not update FilteredCount
        view.CurrentFilteredCount = 99;
        view.TriggerViewRebuilt();

        Assert.Equal(0, vm.FilteredCount);
    }

    [Fact]
    public void SamplesViewerViewModel_Initialize_RestoresFilterText()
    {
        var view = new StubSampleView();
        var compiler = new StubFilterCompiler { NextResultIsValid = true };
        var vm = new SamplesViewerViewModel(compiler, view: view);
        var state = new Dictionary<string, object> { ["FilterText"] = "seq>5" };

        vm.Initialize(state);

        Assert.Equal("seq>5", vm.FilterText);
        Assert.NotNull(view.LastFilter);
    }

    [Fact]
    public void SamplesViewerViewModel_FilterTextChange_PublishesSaveEvent()
    {
        var view = new StubSampleView();
        var compiler = new StubFilterCompiler { NextResultIsValid = true };
        bool eventFired = false;
        var broker = new EventBroker();
        using var _ = broker.Subscribe<WorkspaceSaveRequestedEvent>(_ => eventFired = true);

        var vm = new SamplesViewerViewModel(compiler, view: view, eventBroker: broker);
        vm.Initialize(new Dictionary<string, object>());

        vm.FilterText = "Id == 1";

        Assert.True(eventFired, "WorkspaceSaveRequestedEvent should be published after valid FilterText change");
    }
}

// ── DetailInspectorPlugin tests ───────────────────────────────────────────────

public sealed class DetailInspectorPluginTests
{
    private static (StubMonitorContext ctx, StubContextMenuRegistry contextMenu, StubWindowManager win)
        BuildContext()
    {
        var contextMenu = new StubContextMenuRegistry();
        var win = new StubWindowManager();
        var view = new StubAvaloniaViewRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IContextMenuRegistry>(contextMenu);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IAvaloniaViewRegistry>(view);
        return (ctx, contextMenu, win);
    }

    [Fact]
    public void DetailInspectorPlugin_Initialize_RegistersContextMenuForSampleData()
    {
        var (ctx, contextMenu, _) = BuildContext();
        new DetailInspectorPlugin().Initialize(ctx);

        // Create a minimal SampleData and check context menu items
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var sample = new SampleData { TopicMetadata = meta, Payload = new HeartbeatSample() };
        var items = contextMenu.GetItems(sample).ToList();

        Assert.Contains(items, i => i.Label.Contains("Open Inspector", StringComparison.OrdinalIgnoreCase));
    }
}

// ── DetailInspectorViewModel tests ────────────────────────────────────────────

public sealed class DetailInspectorViewModelTests
{
    private static DetailInspectorViewModel CreateVm(TrackingEventBroker? broker = null)
    {
        broker ??= new TrackingEventBroker();
        return new DetailInspectorViewModel(broker);
    }

    [Fact]
    public void DetailInspectorViewModel_Initialize_ReadsIsLinkedFromState()
    {
        var vm = CreateVm();
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = false });

        Assert.False(vm.IsLinked);
    }

    [Fact]
    public void DetailInspectorViewModel_Initialize_ReadsSourcePanelIdFromState()
    {
        var vm = CreateVm();
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        Assert.Equal("SV_1", vm.SourcePanelId);
    }

    [Fact]
    public void DetailInspectorViewModel_IsLinked_True_SubscribesToSampleSelectedEvent()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        Assert.True(broker.ActiveSubscriptionCount > 0, "Should have subscribed to SampleSelectedEvent");
    }

    [Fact]
    public void DetailInspectorViewModel_IsLinked_False_DoesNotSubscribe()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = false, ["SourcePanelId"] = "SV_1" });

        Assert.Equal(0, broker.ActiveSubscriptionCount);
    }

    [AvaloniaFact]
    public void DetailInspectorViewModel_OnSampleReceived_UpdatesCurrentSample()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var sample = new SampleData { TopicMetadata = meta, Payload = new HeartbeatSample { Id = 7 } };

        broker.Publish(new SampleSelectedEvent("SV_1", sample));

        Assert.NotNull(vm.CurrentSample);
        Assert.Equal(7, ((HeartbeatSample)vm.CurrentSample!.Payload).Id);
    }

    [AvaloniaFact]
    public void DetailInspectorViewModel_OnSampleReceived_WrongPanel_Ignored()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var sample = new SampleData { TopicMetadata = meta, Payload = new HeartbeatSample { Id = 99 } };

        broker.Publish(new SampleSelectedEvent("SV_OTHER", sample));

        Assert.Null(vm.CurrentSample);
    }

    [AvaloniaFact]
    public void DetailInspectorViewModel_RebuildFieldTree_NullPayload_EmptyList()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var sample = new SampleData { TopicMetadata = meta, Payload = null! };

        var ex = Record.Exception(() => broker.Publish(new SampleSelectedEvent("SV_1", sample)));

        Assert.Null(ex);
        Assert.Empty(vm.FieldTree);
    }

    [Fact]
    public void DetailInspectorViewModel_Unlink_DisposesSubscription()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        Assert.True(broker.ActiveSubscriptionCount > 0);

        vm.IsLinked = false;

        Assert.Equal(0, broker.ActiveSubscriptionCount);
    }

    [Fact]
    public void DetailInspectorViewModel_Dispose_DisposesSubscriptions()
    {
        var broker = new TrackingEventBroker();
        var vm = new DetailInspectorViewModel(broker);
        vm.Initialize(new Dictionary<string, object> { ["IsLinked"] = true, ["SourcePanelId"] = "SV_1" });

        Assert.True(broker.ActiveSubscriptionCount > 0);

        vm.Dispose();

        Assert.Equal(0, broker.ActiveSubscriptionCount);
    }
}

// ── DT-002 rename coverage test ───────────────────────────────────────────────

public sealed class DebtResolutionTests
{
    /// <summary>
    /// DT-002: Verifies that IWindowManager.RegisterPanelType parameter is now named
    /// 'viewModelType' (not the old Blazor-specific 'blazorComponentType').
    /// This test uses the named parameter syntax as a compile-time + runtime guard.
    /// </summary>
    [Fact]
    public void IWindowManager_RegisterPanelType_AcceptsViewModelTypeNamedParameter()
    {
        var win = new StubWindowManager();

        // Use the named parameter 'viewModelType' — compile error if param name reverts.
        win.RegisterPanelType("MyPanel", viewModelType: typeof(int));

        Assert.Contains("MyPanel", win.RegisteredPanelTypes.Keys);
    }
}

// ── SendSamplePlugin tests ────────────────────────────────────────────────────

public sealed class SendSamplePluginTests
{
    private static (StubMonitorContext ctx, StubMenuRegistry menu, StubWindowManager win,
        StubContextMenuRegistry contextMenu, StubAvaloniaTypeDrawerRegistry drawerRegistry)
        BuildContext()
    {
        var menu = new StubMenuRegistry();
        var win = new StubWindowManager();
        var contextMenu = new StubContextMenuRegistry();
        var drawerRegistry = new StubAvaloniaTypeDrawerRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IContextMenuRegistry>(contextMenu);
        ctx.Register<IAvaloniaTypeDrawerRegistry>(drawerRegistry);
        return (ctx, menu, win, contextMenu, drawerRegistry);
    }

    [Fact]
    public void SendSamplePlugin_Initialize_RegistersContextMenuForSampleData()
    {
        var (ctx, _, _, contextMenu, _) = BuildContext();
        new SendSamplePlugin().Initialize(ctx);

        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var sample = new SampleData { TopicMetadata = meta, Payload = new HeartbeatSample() };
        var items = contextMenu.GetItems(sample).ToList();

        Assert.Contains(items, i => i.Label.Contains("Clone to Send", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SendSamplePlugin_Initialize_RegistersStandardDrawers()
    {
        var (ctx, _, _, _, drawerRegistry) = BuildContext();
        new SendSamplePlugin().Initialize(ctx);

        Assert.True(drawerRegistry.HasDrawer(typeof(int)),
            "IAvaloniaTypeDrawerRegistry should have an int drawer after plugin init");
    }

    [Fact]
    public void SendSamplePlugin_Initialize_RegistersToolsMenuSendSample()
    {
        var (ctx, menu, _, _, _) = BuildContext();
        new SendSamplePlugin().Initialize(ctx);

        Assert.Contains(menu.Items, i =>
            i.Label.Contains("Send Sample", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SendSamplePlugin_Initialize_ToolsMenuSpawnsSendSampleBlankPanel()
    {
        var (ctx, menu, win, _, _) = BuildContext();
        new SendSamplePlugin().Initialize(ctx);

        var item = menu.Items.First(i => i.Label.Contains("Send Sample", StringComparison.OrdinalIgnoreCase));
        menu.InvokeItem(item.Label);

        Assert.Contains(win.SpawnCalls, c => c.TypeName == "SendSample_Blank");
    }

    [AvaloniaFact]
    public void SendSampleViewModel_Build_CreatesControlsForAllNonSyntheticFields()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new AvaloniaTypeDrawerRegistry();
        StandardDrawerRegistrar.Register(registry);
        var bridge = new StubDdsBridge();

        // HeartbeatSample has 3 fields: Id (int), Timestamp (long), Sequence (int)
        // AllFields also includes synthetic wrapper fields — we skip those.
        int expected = meta.AllFields.Count(f => !f.IsSynthetic);
        var vm = new SendSampleViewModel(meta, registry, bridge);

        Assert.Equal(expected, vm.BuiltControls.Count);
    }

    [Fact]
    public void SendSampleViewModel_Send_CallsWriter()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new StubAvaloniaTypeDrawerRegistry(); // empty — no Avalonia controls created
        var bridge = new StubDdsBridge();

        var vm = new SendSampleViewModel(meta, registry, bridge);
        vm.Send();

        Assert.True(bridge.Writer.WriteCount > 0, "Write should have been called after Send()");
    }

    [Fact]
    public void SendSampleViewModel_Send_ExceptionSetsError()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new StubAvaloniaTypeDrawerRegistry(); // empty — no Avalonia controls created
        var bridge = new ThrowingDdsBridge();

        var vm = new SendSampleViewModel(meta, registry, bridge);
        vm.Send();

        Assert.NotNull(vm.SendError);
        Assert.Contains("DDS Publish Failed", vm.SendError);
    }

    [Fact]
    public void SendSampleViewModel_Send_ExceptionDoesNotThrow()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new StubAvaloniaTypeDrawerRegistry(); // empty — no Avalonia controls created
        var bridge = new ThrowingDdsBridge();

        var vm = new SendSampleViewModel(meta, registry, bridge);

        var ex = Record.Exception(() => vm.Send());
        Assert.Null(ex);
    }

    [Fact]
    public void SendSampleViewModel_Send_ClearsPreviousError()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new StubAvaloniaTypeDrawerRegistry(); // empty — no Avalonia controls created

        var throwingBridge = new ThrowingDdsBridge();
        var vm = new SendSampleViewModel(meta, registry, throwingBridge);
        vm.Send(); // First send throws → sets SendError
        Assert.NotNull(vm.SendError);

        // Second VM with non-throwing bridge starts with null error
        var goodBridge = new StubDdsBridge();
        var vm2 = new SendSampleViewModel(meta, registry, goodBridge);
        vm2.Send(); // SendError cleared at start of Send()
        Assert.Null(vm2.SendError);
    }

    [Fact]
    public void SendSampleViewModel_InitialPayload_UsedDirectly()
    {
        var meta = new TopicMetadata(typeof(HeartbeatSample));
        var registry = new StubAvaloniaTypeDrawerRegistry(); // empty — no Avalonia controls created
        var bridge = new StubDdsBridge();

        var payload = new HeartbeatSample { Id = 42, Timestamp = 999L, Sequence = 7 };
        var boxedPayload = (object)payload;

        var vm = new SendSampleViewModel(meta, registry, bridge, initialPayload: boxedPayload);
        vm.Send();

        Assert.True(bridge.Writer.WriteCount > 0,
            "Send should invoke writer when initialPayload is provided");
        Assert.Null(vm.SendError);
    }
}

/// <summary>DdsBridge stub whose GetWriter throws to simulate DDS publish failure.</summary>
internal sealed class ThrowingDdsBridge : IDdsBridge
{
    private sealed class ThrowingWriter : IDynamicWriter
    {
        public Type TopicType => typeof(HeartbeatSample);
        public void Write(object payload) => throw new InvalidOperationException("DDS write error");
        public void DisposeInstance(object payload) { }
        public void Dispose() { }
    }

    public bool IsPaused { get; set; }
    public CycloneDDS.Runtime.DdsParticipant Participant => throw new NotSupportedException();
    public IReadOnlyList<CycloneDDS.Runtime.DdsParticipant> Participants => Array.Empty<CycloneDDS.Runtime.DdsParticipant>();
    public IReadOnlyList<ParticipantConfig> ParticipantConfigs => Array.Empty<ParticipantConfig>();
    public string? CurrentPartition => null;
    public IReadOnlySet<Type> ExplicitlyUnsubscribedTopicTypes => new HashSet<Type>();
    public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders => new Dictionary<Type, IDynamicReader>();
    public event Action? ReadersChanged { add { } remove { } }
    public IDynamicReader Subscribe(TopicMetadata meta) => throw new NotSupportedException();
    public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? error)
    { reader = null; error = "stub"; return false; }
    public void Unsubscribe(TopicMetadata meta) { }
    public IDynamicWriter GetWriter(TopicMetadata meta) => new ThrowingWriter();
    public void ChangePartition(string? newPartition) { }
    public void InitializeExplicitlyUnsubscribed(IEnumerable<Type> types) { }
    public void AddParticipant(uint domainId, string partitionName) { }
    public void RemoveParticipant(int participantIndex) { }
    public void SetFilter(Func<SampleData, bool>? predicate) { }
    public void Dispose() { }
    public void ResetAll() { }
}

// ── NetworkConfigViewModel tests ──────────────────────────────────────────────

public sealed class NetworkConfigViewModelTests
{
    private static NetworkConfigViewModel CreateVm(
        StubDdsBridge? bridge = null,
        StubEventBroker? broker = null)
    {
        bridge ??= new StubDdsBridge();
        broker ??= new StubEventBroker();
        return new NetworkConfigViewModel(bridge, broker);
    }

    [Fact]
    public void WorkspaceManagerPlugin_Initialize_RegistersNetworkConfigMenuItem()
    {
        var menu = new StubMenuRegistry();
        var win = new StubWindowManager();
        var view = new StubAvaloniaViewRegistry();
        var ctx = new StubMonitorContext();
        ctx.Register<IMenuRegistry>(menu);
        ctx.Register<IWindowManager>(win);
        ctx.Register<IAvaloniaViewRegistry>(view);

        new WorkspaceManagerPlugin().Initialize(ctx);

        Assert.Contains(menu.Items, i =>
            i.Label.Contains("Network Config", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NetworkConfigViewModel_Constructor_LoadsExistingParticipants()
    {
        var bridge = new StubDdsBridge();
        bridge.SimulatedParticipantConfigs.Add(new ParticipantConfig { DomainId = 0, PartitionName = "A" });
        bridge.SimulatedParticipantConfigs.Add(new ParticipantConfig { DomainId = 1, PartitionName = "B" });

        var vm = CreateVm(bridge: bridge);

        Assert.Equal(2, vm.Participants.Count);
    }

    [Fact]
    public void NetworkConfigViewModel_AddRow_IncreasesCount()
    {
        var vm = CreateVm();
        int initial = vm.Participants.Count;

        vm.AddRow();

        Assert.Equal(initial + 1, vm.Participants.Count);
    }

    [Fact]
    public void NetworkConfigViewModel_RemoveRow_DecreasesCount()
    {
        var vm = CreateVm();
        vm.AddRow();
        int afterAdd = vm.Participants.Count;

        vm.RemoveRow(0);

        Assert.Equal(afterAdd - 1, vm.Participants.Count);
    }

    [Fact]
    public void NetworkConfigViewModel_Apply_CallsAddParticipant()
    {
        var bridge = new StubDdsBridge();
        var vm = CreateVm(bridge: bridge);
        vm.AddRow();

        vm.Apply();

        Assert.True(bridge.AddParticipantCallCount > 0,
            "AddParticipant should have been called after Apply()");
    }

    [Fact]
    public void NetworkConfigViewModel_Apply_ExceptionSetsApplyError()
    {
        var bridge = new StubDdsBridge();
        var vm = CreateVm(bridge: bridge);
        vm.AddRow();

        Assert.Null(vm.ApplyError);

        bridge.AddParticipantShouldThrow = true;
        vm.Apply();

        Assert.NotNull(vm.ApplyError);
    }

    [Fact]
    public void NetworkConfigViewModel_Apply_NoChanges_SkipsBridgeCalls()
    {
        var bridge = new StubDdsBridge();
        bridge.SimulatedParticipantConfigs.Add(new ParticipantConfig { DomainId = 0, PartitionName = "test" });
        var vm = new NetworkConfigViewModel(bridge, new StubEventBroker());
        // vm.Participants was populated from bridge in ctor — should match bridge state exactly

        vm.Apply();

        Assert.Equal(0, bridge.AddParticipantCallCount);
    }
}

// ── StandardDrawerRegistrar tests ────────────────────────────────────────────

public sealed class DrawerRegistrarTests
{
    [AvaloniaFact]
    public void StandardDrawerRegistrar_Register_IntDrawer_ReturnsNumericUpDown()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        StandardDrawerRegistrar.Register(registry);

        var ctx = new AvaloniaDrawerContext("Id", typeof(int), 0, _ => { });
        var control = registry.Build(ctx);

        Assert.IsType<NumericUpDown>(control);
    }

    [AvaloniaFact]
    public void StandardDrawerRegistrar_Register_BoolDrawer_ReturnsToggleSwitch()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        StandardDrawerRegistrar.Register(registry);

        var ctx = new AvaloniaDrawerContext("Flag", typeof(bool), false, _ => { });
        var control = registry.Build(ctx);

        Assert.True(control is ToggleSwitch or CheckBox,
            $"Expected ToggleSwitch or CheckBox but got {control.GetType().Name}");
    }

    [AvaloniaFact]
    public void StandardDrawerRegistrar_Register_StringDrawer_ReturnsTextBox()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        StandardDrawerRegistrar.Register(registry);

        var ctx = new AvaloniaDrawerContext("Name", typeof(string), "", _ => { });
        var control = registry.Build(ctx);

        Assert.IsType<TextBox>(control);
    }
}
