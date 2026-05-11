using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Xunit;

namespace DdsMonitor.Avalonia.Tests;

// ── Test helpers ──────────────────────────────────────────────────────────────

/// <summary>ViewModel that implements IStatefulViewModel and records Initialize calls.</summary>
public sealed class StatefulTestPanelViewModel : IStatefulViewModel
{
    [ThreadStatic]
    private static StatefulTestPanelViewModel? _lastCreated;
    public static StatefulTestPanelViewModel? LastCreated => _lastCreated;

    public bool WasInitialized { get; private set; }
    public IDictionary<string, object>? ReceivedState { get; private set; }

    public StatefulTestPanelViewModel() { _lastCreated = this; }

    public void Initialize(IDictionary<string, object> componentState)
    {
        WasInitialized = true;
        ReceivedState = componentState;
        componentState["TopicName"] = "TestTopic";
    }
}

/// <summary>A second panel type so two panels can be open simultaneously.</summary>
public sealed class AnotherTestPanelViewModel { }

/// <summary>Event broker that counts active subscriptions.</summary>
internal sealed class TrackingEventBroker : IEventBroker
{
    private int _subscriptionCount;
    public int SubscriptionCount => _subscriptionCount;

    public void Publish<TEvent>(TEvent ev) { }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        Interlocked.Increment(ref _subscriptionCount);
        return new Token(this);
    }

    private sealed class Token : IDisposable
    {
        private readonly TrackingEventBroker _owner;
        private bool _disposed;
        public Token(TrackingEventBroker owner) => _owner = owner;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Interlocked.Decrement(ref _owner._subscriptionCount);
        }
    }
}

/// <summary>Minimal IWorkspaceState stub.</summary>
internal sealed class StubWorkspaceState : IWorkspaceState
{
    public StubWorkspaceState(string path = "") => WorkspaceFilePath = path;
    public string WorkspaceFilePath { get; }
}

/// <summary>IHostApplicationLifetime stub.</summary>
internal sealed class StubHostApplicationLifetime : IHostApplicationLifetime
{
    private readonly CancellationTokenSource _stopping = new();
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => _stopping.Token;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() => _stopping.Cancel();
    public void TriggerStopping() => _stopping.Cancel();
}

/// <summary>IWindowManager stub that counts SaveWorkspace calls.</summary>
internal sealed class CountingWindowManager : IWindowManager
{
    public int SaveCallCount { get; private set; }

    public event Action<PanelState>? PanelClosed { add { } remove { } }
    public event Action? PanelsChanged { add { } remove { } }
    public IReadOnlyList<PanelState> ActivePanels => [];
    public IReadOnlyList<string> ExcludedTopics => [];
    public IReadOnlyDictionary<string, Type> RegisteredPanelTypes => new Dictionary<string, Type>();

    public void SetExcludedTopics(IEnumerable<string> topicTypeNames) { }
    public PanelState SpawnPanel(string componentTypeName, Dictionary<string, object>? initialState = null)
        => new() { PanelId = componentTypeName, ComponentTypeName = componentTypeName };
    public void ClosePanel(string panelId) { }
    public void BringToFront(string panelId) { }
    public void ShowPanel(string panelId) { }
    public void ClearPanels() { }
    public void RegisterPanelType(string typeName, Type viewModelType) { }
    public void SaveWorkspace(string filePath) { SaveCallCount++; }
    public string SaveWorkspaceToJson() => "[]";
    public void LoadWorkspace(string filePath) { }
    public void LoadWorkspaceFromJson(string json) { }
}

// ── AvaloniaWorkspacePersistenceService tests (no Avalonia UI required) ────────

public sealed class PersistenceServiceTests
{
    private static AvaloniaWorkspacePersistenceService CreateService(
        IEventBroker? broker = null,
        IWindowManager? windowManager = null,
        IWorkspaceState? workspaceState = null,
        IHostApplicationLifetime? lifetime = null,
        TimeSpan? debounceDelay = null)
    {
        return new AvaloniaWorkspacePersistenceService(
            broker ?? new StubEventBroker(),
            windowManager ?? new CountingWindowManager(),
            workspaceState ?? new StubWorkspaceState("test.json"),
            lifetime ?? new StubHostApplicationLifetime(),
            debounceDelay);
    }

    [Fact]
    public void AvaloniaWorkspacePersistenceService_SubscribesToSaveEvent()
    {
        var broker = new TrackingEventBroker();

        using var svc = CreateService(broker: broker);

        Assert.True(broker.SubscriptionCount > 0,
            "Service should subscribe to WorkspaceSaveRequestedEvent on construction");
    }

    [Fact]
    public void AvaloniaWorkspacePersistenceService_FlushSync_CallsSaveWorkspace()
    {
        var wm = new CountingWindowManager();

        using var svc = CreateService(windowManager: wm);
        svc.FlushSync();

        Assert.Equal(1, wm.SaveCallCount);
    }

    [Fact]
    public void AvaloniaWorkspacePersistenceService_FlushSync_EmptyPath_DoesNotThrow()
    {
        var wm = new CountingWindowManager();

        using var svc = CreateService(windowManager: wm, workspaceState: new StubWorkspaceState(""));

        var ex = Record.Exception(() => svc.FlushSync());

        Assert.Null(ex);
        Assert.Equal(0, wm.SaveCallCount);
    }

    [Fact]
    public async Task AvaloniaWorkspacePersistenceService_Dispose_CancelsDebounce()
    {
        var wm = new CountingWindowManager();

        var svc = CreateService(windowManager: wm, debounceDelay: TimeSpan.FromSeconds(60));
        svc.RequestSave(); // schedule a save in 60 seconds
        svc.Dispose();     // should cancel the pending save

        await Task.Delay(50); // give any still-running continuation a moment

        Assert.Equal(0, wm.SaveCallCount);
    }

    [Fact]
    public void AvaloniaWorkspacePersistenceService_Dispose_ReleasesSubscription()
    {
        var broker = new TrackingEventBroker();

        var svc = CreateService(broker: broker);
        Assert.Equal(1, broker.SubscriptionCount);

        svc.Dispose();

        Assert.Equal(0, broker.SubscriptionCount);
    }
}

// ── AvaloniaWindowManager persistence integration tests ───────────────────────

public sealed class WindowManagerPersistenceTests
{
    private static AvaloniaWindowManager CreateManager()
    {
        var viewRegistry = new AvaloniaViewRegistry();
        viewRegistry.Register<TestPanelViewModel>(_ => new TextBlock { Text = "Test" });
        viewRegistry.Register<AnotherTestPanelViewModel>(_ => new TextBlock { Text = "Another" });
        viewRegistry.Register<StatefulTestPanelViewModel>(_ => new TextBlock { Text = "Stateful" });

        var services = new ServiceCollection()
            .AddTransient<TestPanelViewModel>()
            .AddTransient<AnotherTestPanelViewModel>()
            .AddTransient<StatefulTestPanelViewModel>()
            .BuildServiceProvider();

        return new AvaloniaWindowManager(viewRegistry, services, new StubEventBroker());
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_CallsInitializeOnStatefulViewModel()
    {
        var manager = CreateManager();
        var typeName = typeof(StatefulTestPanelViewModel).FullName!;

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(StatefulTestPanelViewModel.LastCreated);
        Assert.True(StatefulTestPanelViewModel.LastCreated!.WasInitialized,
            "Initialize should be called on IStatefulViewModel after SpawnPanel");
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_WritesTopicNameToComponentState()
    {
        var manager = CreateManager();
        var typeName = typeof(StatefulTestPanelViewModel).FullName!;

        var ps = manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(ps.ComponentState.ContainsKey("TopicName"),
            "ComponentState should have 'TopicName' written by Initialize");
        Assert.Equal("TestTopic", ps.ComponentState["TopicName"]);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_RestoresGeometryFromComponentState()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        var initialState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["__window"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["Width"] = 900.0, ["Height"] = 600.0, ["X"] = 50.0, ["Y"] = 75.0,
            }
        };

        var ps = manager.SpawnPanel(typeName, initialState);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(900.0, ps.Width);
        Assert.Equal(600.0, ps.Height);
        Assert.Equal(50.0, ps.X);
        Assert.Equal(75.0, ps.Y);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_RestoresGeometryFromJsonElement()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        // Simulate JSON-deserialized state where __window is a JsonElement
        var stateJson = """{"__window": {"Width": 850.0, "Height": 550.0, "X": 20.0, "Y": 30.0}}""";
        var state = JsonSerializer.Deserialize<Dictionary<string, object>>(stateJson)!;

        var ps = manager.SpawnPanel(typeName, state);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(850.0, ps.Width, precision: 1);
        Assert.Equal(550.0, ps.Height, precision: 1);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_ActivePanels_ReturnsOpenPanels()
    {
        var manager = CreateManager();
        var typeName1 = typeof(TestPanelViewModel).FullName!;
        var typeName2 = typeof(AnotherTestPanelViewModel).FullName!;

        manager.SpawnPanel(typeName1, null);
        manager.SpawnPanel(typeName2, null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(manager.ActivePanels.Count == 2, "Both panels should be active");

        manager.ClosePanel(typeName1);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(manager.ActivePanels);
        Assert.Equal(typeName2, manager.ActivePanels[0].PanelId);
    }
}
