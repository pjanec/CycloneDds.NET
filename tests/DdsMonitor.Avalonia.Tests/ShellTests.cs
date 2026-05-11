using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DdsMonitor.Avalonia.Tests;

// ── Helpers ───────────────────────────────────────────────────────────────────

/// <summary>Simple ViewModel registered for panel-spawn tests.</summary>
public sealed class TestPanelViewModel { }

/// <summary>
/// Stub IDdsBridge that tracks IsPaused and ResetAll calls without
/// starting any DDS infrastructure.
/// </summary>
internal sealed class StubDdsBridge : IDdsBridge
{
    public bool IsPaused { get; set; }
    public bool ResetAllCalled { get; private set; }

    public void ResetAll() => ResetAllCalled = true;

    // ── Minimal stubs ──────────────────────────────────────────────────────
    public CycloneDDS.Runtime.DdsParticipant Participant => throw new NotSupportedException();
    public IReadOnlyList<CycloneDDS.Runtime.DdsParticipant> Participants => [];
    public IReadOnlyList<ParticipantConfig> ParticipantConfigs => [];
    public string? CurrentPartition => null;
    public IReadOnlySet<Type> ExplicitlyUnsubscribedTopicTypes => new HashSet<Type>();
    public IReadOnlyDictionary<Type, IDynamicReader> ActiveReaders => new Dictionary<Type, IDynamicReader>();
    public event Action? ReadersChanged { add { } remove { } }

    public IDynamicReader Subscribe(TopicMetadata meta) => throw new NotSupportedException();
    public bool TrySubscribe(TopicMetadata meta, out IDynamicReader? reader, out string? error)
    { reader = null; error = "stub"; return false; }
    public void Unsubscribe(TopicMetadata meta) { }
    public IDynamicWriter GetWriter(TopicMetadata meta) => throw new NotSupportedException();
    public void ChangePartition(string? newPartition) { }
    public void InitializeExplicitlyUnsubscribed(IEnumerable<Type> types) { }
    public void AddParticipant(uint domainId, string partitionName) { }
    public void RemoveParticipant(int participantIndex) { }
    public void SetFilter(Func<SampleData, bool>? predicate) { }
    public void Dispose() { }
}

/// <summary>Stub IEventBroker that discards all events.</summary>
internal sealed class StubEventBroker : IEventBroker
{
    public void Publish<TEvent>(TEvent ev) { }
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) => new NoopDisposable();
    private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
}

/// <summary>Stub IMenuRegistry with no entries.</summary>
internal sealed class StubMenuRegistry : IMenuRegistry
{
    private readonly List<MenuNode> _menus = new();
    public event Action? Changed;

    public void AddMenuItem(string menuPath, string label, Action onClick) { }
    public void AddMenuItem(string menuPath, string label, Func<Task> onClickAsync) { }
    public IReadOnlyList<MenuNode> GetTopLevelMenus() => _menus;

    public void TriggerChanged() => Changed?.Invoke();
    public void AddTopLevel(MenuNode node) { _menus.Add(node); Changed?.Invoke(); }
}

/// <summary>Helper: build a headless ShellWindow with stub services.</summary>
internal static class ShellTestFactory
{
    public static (ShellWindow window, StubDdsBridge bridge, StubMenuRegistry menu, ToolbarRegistry toolbar)
        CreateShell()
    {
        var menu = new StubMenuRegistry();
        var toolbar = new ToolbarRegistry();
        var bridge = new StubDdsBridge();
        var window = new ShellWindow(menu, toolbar, bridge);
        return (window, bridge, menu, toolbar);
    }
}

// ── ShellWindow tests ─────────────────────────────────────────────────────────

public sealed class ShellWindowTests
{
    [AvaloniaFact]
    public void ShellWindow_InstantiatesWithoutException()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public void ShellWindow_HasFileMenuItem_AfterInitialization()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        var menu = window.FindControl<Menu>("TopMenu");
        Assert.NotNull(menu);
        Assert.True(menu!.Items.Count >= 1, "Expected at least one menu item (File)");

        var fileItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "File");
        Assert.NotNull(fileItem);
    }

    [AvaloniaFact]
    public void ShellWindow_FileMenu_HasExitItem()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        var menu = window.FindControl<Menu>("TopMenu");
        var fileMenu = menu!.Items.OfType<MenuItem>().First(m => m.Header?.ToString() == "File");
        var exitItem = fileMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "Exit");
        Assert.NotNull(exitItem);
    }

    [AvaloniaFact]
    public void ShellWindow_HasPlayButton()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        var playBtn = window.FindControl<Button>("PlayButton");
        Assert.NotNull(playBtn);
        Assert.Contains("Play", playBtn!.Content?.ToString() ?? string.Empty);
    }

    [AvaloniaFact]
    public void ShellWindow_HasPauseButton()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        var pauseBtn = window.FindControl<Button>("PauseButton");
        Assert.NotNull(pauseBtn);
        Assert.Contains("Pause", pauseBtn!.Content?.ToString() ?? string.Empty);
    }

    [AvaloniaFact]
    public void ShellWindow_HasResetButton()
    {
        var (window, _, _, _) = ShellTestFactory.CreateShell();
        var resetBtn = window.FindControl<Button>("ResetButton");
        Assert.NotNull(resetBtn);
        Assert.Contains("Reset", resetBtn!.Content?.ToString() ?? string.Empty);
    }

    [AvaloniaFact]
    public void ShellWindow_PlayButtonClick_SetsBridgeIsPausedFalse()
    {
        var (window, bridge, _, _) = ShellTestFactory.CreateShell();
        bridge.IsPaused = true;

        var playBtn = window.FindControl<Button>("PlayButton")!;
        playBtn.Command?.Execute(null);
        // Trigger via raising click event
        playBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(bridge.IsPaused);
    }

    [AvaloniaFact]
    public void ShellWindow_PauseButtonClick_SetsBridgeIsPausedTrue()
    {
        var (window, bridge, _, _) = ShellTestFactory.CreateShell();
        bridge.IsPaused = false;

        var pauseBtn = window.FindControl<Button>("PauseButton")!;
        pauseBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(bridge.IsPaused);
    }

    [AvaloniaFact]
    public void ShellWindow_ResetButtonClick_CallsBridgeResetAll()
    {
        var (window, bridge, _, _) = ShellTestFactory.CreateShell();

        var resetBtn = window.FindControl<Button>("ResetButton")!;
        resetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(bridge.ResetAllCalled);
    }

    [AvaloniaFact]
    public void ShellWindow_MenuReacts_ToMenuRegistryChanged()
    {
        var (window, _, menuRegistry, _) = ShellTestFactory.CreateShell();
        var menu = window.FindControl<Menu>("TopMenu")!;
        int countBefore = menu.Items.Count;

        // Add a new top-level menu item via the registry
        menuRegistry.AddTopLevel(new MenuNode("Plugins"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(menu.Items.Count > countBefore, "Menu should have grown after registry change");
    }

    [AvaloniaFact]
    public void ShellWindow_ToolbarReacts_ToToolbarRegistryChanged()
    {
        var (window, _, _, toolbarRegistry) = ShellTestFactory.CreateShell();
        var toolbar = window.FindControl<StackPanel>("Toolbar")!;
        int countBefore = toolbar.Children.Count;

        toolbarRegistry.Register("tool1", () => { }, tooltip: "My Tool");
        Dispatcher.UIThread.RunJobs();

        Assert.True(toolbar.Children.Count > countBefore, "Toolbar should have grown after registration");
    }
}

// ── AvaloniaWindowManager tests ───────────────────────────────────────────────

public sealed class AvaloniaWindowManagerTests
{
    private static AvaloniaWindowManager CreateManager()
    {
        var viewRegistry = new AvaloniaViewRegistry();
        viewRegistry.Register<TestPanelViewModel>(_ => new TextBlock { Text = "Test Panel" });

        var services = new ServiceCollection()
            .AddSingleton<TestPanelViewModel>()
            .BuildServiceProvider();

        var eventBroker = new StubEventBroker();
        return new AvaloniaWindowManager(viewRegistry, services, eventBroker);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_OpensWindow()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(manager.ActivePanels);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_SpawnPanel_SamePanelIdTwice_WindowCountStaysOne()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(manager.ActivePanels);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_ClosePanel_RemovesPanelFromActive()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Single(manager.ActivePanels);

        manager.ClosePanel(typeName);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(manager.ActivePanels);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_WindowClose_SavesGeometryToComponentState()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;
        PanelState? closedState = null;
        manager.PanelClosed += ps => closedState = ps;

        manager.SpawnPanel(typeName, null);
        Dispatcher.UIThread.RunJobs();

        manager.ClosePanel(typeName);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(closedState);
        Assert.True(closedState!.ComponentState.ContainsKey("__window"),
            "ComponentState should contain '__window' geometry key after close");

        var geo = closedState.ComponentState["__window"] as Dictionary<string, object>;
        Assert.NotNull(geo);
        Assert.True(geo!.ContainsKey("X"));
        Assert.True(geo.ContainsKey("Y"));
        Assert.True(geo.ContainsKey("Width"));
        Assert.True(geo.ContainsKey("Height"));
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_RespawnAfterClose_RestoresGeometry()
    {
        var manager = CreateManager();
        var typeName = typeof(TestPanelViewModel).FullName!;

        var initialState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["__window"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["X"] = 100.0, ["Y"] = 200.0, ["Width"] = 800.0, ["Height"] = 600.0,
            }
        };

        var ps = manager.SpawnPanel(typeName, initialState);
        Dispatcher.UIThread.RunJobs();

        // Panel state X/Y restored from component state
        Assert.Equal(100.0, ps.X);
        Assert.Equal(200.0, ps.Y);
    }

    [AvaloniaFact]
    public void AvaloniaWindowManager_RegisterPanelType_StoresMapping()
    {
        var manager = CreateManager();
        manager.RegisterPanelType("TestPanel", typeof(TestPanelViewModel));

        Assert.True(manager.RegisteredPanelTypes.ContainsKey("TestPanel"));
        Assert.Equal(typeof(TestPanelViewModel), manager.RegisteredPanelTypes["TestPanel"]);
    }
}

// ── ViewLocator tests ─────────────────────────────────────────────────────────

public sealed class ViewLocatorTests
{
    [AvaloniaFact]
    public void ViewLocator_RegisteredViewModel_ReturnsControl()
    {
        var registry = new AvaloniaViewRegistry();
        registry.Register<TestPanelViewModel>(_ => new TextBlock { Text = "OK" });

        var locator = new ViewLocator(registry);
        var result = locator.Build(new TestPanelViewModel());

        Assert.NotNull(result);
    }

    [AvaloniaFact]
    public void ViewLocator_UnregisteredViewModel_ReturnsErrorTextBlock()
    {
        var registry = new AvaloniaViewRegistry();
        var locator = new ViewLocator(registry);

        var result = locator.Build(new TestPanelViewModel());

        Assert.NotNull(result);
        Assert.IsType<TextBlock>(result);
    }

    [Fact]
    public void ViewLocator_Null_ReturnsNull()
    {
        var registry = new AvaloniaViewRegistry();
        var locator = new ViewLocator(registry);

        Assert.Null(locator.Build(null));
    }

    [Fact]
    public void ViewLocator_Match_TrueForNonNull()
    {
        var locator = new ViewLocator(new AvaloniaViewRegistry());
        Assert.True(locator.Match(new object()));
        Assert.False(locator.Match(null));
    }
}
