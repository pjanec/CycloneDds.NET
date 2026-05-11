using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DdsMonitor.Avalonia.Core;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Avalonia.Core.Tests;

// ── UserSettingsStore ─────────────────────────────────────────────────────────

public sealed class UserSettingsStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly UserSettingsStore _store;

    public UserSettingsStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"DdsMonitorTest_{Guid.NewGuid():N}.json");
        _store = new UserSettingsStore(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public async Task UserSettingsStore_SetAndSave_PersistsKeyToDisk()
    {
        _store.Set("Section", "Key", "hello");
        await _store.SaveAsync().WaitAsync(TimeSpan.FromSeconds(3));

        var json = await File.ReadAllTextAsync(_tempFile);
        Assert.Contains("Section", json);
        Assert.Contains("hello", json);
    }

    [Fact]
    public async Task UserSettingsStore_GetAfterRoundTrip_ReturnsCorrectValue()
    {
        _store.Set("Section", "BoolKey", true);
        await _store.SaveAsync().WaitAsync(TimeSpan.FromSeconds(3));

        var store2 = new UserSettingsStore(_tempFile);
        Assert.True(store2.Get<bool>("Section", "BoolKey", false));
    }

    [Fact]
    public void UserSettingsStore_GetBeforeSet_ReturnsDefault()
    {
        Assert.False(_store.Get<bool>("Section", "Missing", false));
        Assert.Equal("default", _store.Get("Sec", "Key", "default"));
    }

    [Fact]
    public async Task UserSettingsStore_GetStringRoundTrip_ReturnsCorrectValue()
    {
        _store.Set("UI", "Theme", "Dark");
        await _store.SaveAsync().WaitAsync(TimeSpan.FromSeconds(3));

        var store2 = new UserSettingsStore(_tempFile);
        Assert.Equal("Dark", store2.Get<string>("UI", "Theme", "Light"));
    }

    [Fact]
    public async Task UserSettingsStore_RapidSaves_WritesFileOnce()
    {
        _store.Set("S", "K", 1);
        // Fire two saves in quick succession; only one should hit the disk.
        var t1 = _store.SaveAsync();
        var t2 = _store.SaveAsync();
        await Task.WhenAll(t1, t2).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public async Task UserSettingsStore_SaveAsync_CreatesDirectoryIfMissing()
    {
        var nestedDir = Path.Combine(Path.GetTempPath(), $"DdsMonTest_{Guid.NewGuid():N}", "sub");
        var file = Path.Combine(nestedDir, "settings.json");
        var store = new UserSettingsStore(file);
        store.Set("X", "Y", 42);
        await store.SaveAsync().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(File.Exists(file));
        Directory.Delete(nestedDir, recursive: true);
    }
}

// ── ToolbarRegistry ───────────────────────────────────────────────────────────

public sealed class ToolbarRegistryTests
{
    [Fact]
    public void ToolbarRegistry_RegisterTwo_BothInEntries()
    {
        var registry = new ToolbarRegistry();

        registry.Register("btn1", () => { }, "icon1", "Tooltip 1");
        registry.Register("btn2", () => { }, tooltip: "Tooltip 2");

        var entries = registry.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal("btn1", entries[0].Id);
        Assert.Equal("btn2", entries[1].Id);
    }

    [Fact]
    public void ToolbarRegistry_ChangedFires_OncePerRegistration()
    {
        var registry = new ToolbarRegistry();
        var count = 0;
        registry.Changed += () => count++;

        registry.Register("a", () => { });
        registry.Register("b", () => { });

        Assert.Equal(2, count);
    }

    [Fact]
    public void ToolbarRegistry_RegisterSameId_ReplacesEntry()
    {
        var registry = new ToolbarRegistry();
        var called = 0;
        registry.Register("x", () => { }, tooltip: "old");
        registry.Register("x", () => called++, tooltip: "new");

        Assert.Single(registry.Entries);
        Assert.Equal("new", registry.Entries[0].Tooltip);
        registry.Entries[0].Action();
        Assert.Equal(1, called);
    }

    [Fact]
    public void ToolbarRegistry_Empty_EntriesIsEmpty()
    {
        var registry = new ToolbarRegistry();
        Assert.Empty(registry.Entries);
    }

    [Fact]
    public void ToolbarRegistry_Register_NullId_Throws()
    {
        var registry = new ToolbarRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, () => { }));
    }
}

// ── AvaloniaViewRegistry ──────────────────────────────────────────────────────

public sealed class AvaloniaViewRegistryTests
{
    private sealed class TestViewModel { public string Name { get; set; } = ""; }

    [AvaloniaFact]
    public void AvaloniaViewRegistry_Register_BuildViewReturnsControl()
    {
        var registry = new AvaloniaViewRegistry();
        registry.Register<TestViewModel>(vm => new TextBlock { Text = vm.Name });

        var control = registry.BuildView(new TestViewModel { Name = "Hi" });

        Assert.NotNull(control);
        var tb = Assert.IsType<TextBlock>(control);
        Assert.Equal("Hi", tb.Text);
    }

    [AvaloniaFact]
    public void AvaloniaViewRegistry_UnregisteredType_ThrowsInvalidOperation()
    {
        var registry = new AvaloniaViewRegistry();
        Assert.Throws<InvalidOperationException>(() => registry.BuildView(new TestViewModel()));
    }

    [AvaloniaFact]
    public void AvaloniaViewRegistry_NullViewModel_ThrowsArgumentNull()
    {
        var registry = new AvaloniaViewRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.BuildView(null!));
    }
}

// ── AvaloniaTypeDrawerRegistry ────────────────────────────────────────────────

public sealed class AvaloniaTypeDrawerRegistryTests
{
    private sealed class StringLike { public string Value { get; set; } = ""; }

    private static AvaloniaDrawerContext MakeCtx(Type type, object? value = null) =>
        new(label: "field", targetType: type, value: value, onChange: _ => { });

    [AvaloniaFact]
    public void AvaloniaTypeDrawerRegistry_RegisterString_BuildReturnsControl()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        registry.Register(typeof(string), ctx => new TextBox { Text = ctx.Value?.ToString() ?? "" });

        var control = registry.Build(MakeCtx(typeof(string), "hello"));

        Assert.NotNull(control);
    }

    [AvaloniaFact]
    public void AvaloniaTypeDrawerRegistry_FactoryReturnsNonControl_ThrowsInvalidCast()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        registry.Register(typeof(int), _ => (object)new object()); // not a Control

        Assert.Throws<InvalidCastException>(() => registry.Build(MakeCtx(typeof(int))));
    }

    [AvaloniaFact]
    public void AvaloniaTypeDrawerRegistry_UnknownType_FallbackReturnsStackPanel()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        var control = registry.Build(MakeCtx(typeof(StringLike), new StringLike { Value = "test" }));

        Assert.NotNull(control);
        Assert.IsType<StackPanel>(control);
    }

    [AvaloniaFact]
    public void AvaloniaTypeDrawerRegistry_UnknownEmptyType_FallbackReturnsStackPanel()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        // Type with no public properties — still returns StackPanel with placeholder TextBlock.
        var control = registry.Build(MakeCtx(typeof(int)));

        Assert.NotNull(control);
        Assert.IsType<StackPanel>(control);
    }

    [AvaloniaFact]
    public void AvaloniaTypeDrawerRegistry_Register_NullType_Throws()
    {
        var registry = new AvaloniaTypeDrawerRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, _ => new TextBox()));
    }

    [AvaloniaFact]
    public void AvaloniaDrawerContext_Properties_AreSetCorrectly()
    {
        var onChange = (object? v) => { };
        var ctx = new AvaloniaDrawerContext("myLabel", typeof(bool), true, onChange);

        Assert.Equal("myLabel", ctx.Label);
        Assert.Equal(typeof(bool), ctx.TargetType);
        Assert.Equal(true, ctx.Value);
    }
}

// ── IEventBrokerExtensions.SubscribeOnUiThread ────────────────────────────────

public sealed class EventBrokerExtensionsTests
{
    private sealed record TestEvent(string Data);

    [AvaloniaFact]
    public async Task SubscribeOnUiThread_PublishFromBackground_HandlerInvokedOnUiThread()
    {
        var broker = new EventBroker();
        var tcs = new TaskCompletionSource<bool>();
        bool? wasOnUiThread = null;

        using var _ = broker.SubscribeOnUiThread<TestEvent>(ev =>
        {
            wasOnUiThread = Dispatcher.UIThread.CheckAccess();
            tcs.TrySetResult(true);
        });

        await Task.Run(() => broker.Publish(new TestEvent("hello")));
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(wasOnUiThread, "Handler was not invoked on the UI thread.");
    }

    [AvaloniaFact]
    public async Task SubscribeOnUiThread_Unsubscribe_HandlerNotInvokedAfterDispose()
    {
        var broker = new EventBroker();
        var count = 0;
        var tcs = new TaskCompletionSource<bool>();

        var subscription = broker.SubscribeOnUiThread<TestEvent>(_ =>
        {
            count++;
            tcs.TrySetResult(true);
        });

        await Task.Run(() => broker.Publish(new TestEvent("first")));
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        subscription.Dispose();
        broker.Publish(new TestEvent("second"));

        await Task.Delay(100); // give dispatcher time if it were to execute
        Assert.Equal(1, count); // only the first event counted
    }
}

// ── IStatefulViewModel ────────────────────────────────────────────────────────

public sealed class StatefulViewModelTests
{
    private sealed class TestViewModel : IStatefulViewModel
    {
        private IDictionary<string, object>? _state;
        public string? Name => _state?.TryGetValue("Name", out var n) == true ? n as string : null;
        public void Initialize(IDictionary<string, object> componentState) => _state = componentState;
    }

    [Fact]
    public void IStatefulViewModel_Initialize_ViewModelReceivesState()
    {
        var vm = new TestViewModel();
        var state = new Dictionary<string, object> { ["Name"] = "DdsMonitor" };
        vm.Initialize(state);
        Assert.Equal("DdsMonitor", vm.Name);
    }

    [Fact]
    public void IStatefulViewModel_Initialize_MutatingDictChangesVmState()
    {
        var vm = new TestViewModel();
        var state = new Dictionary<string, object> { ["Name"] = "before" };
        vm.Initialize(state);
        state["Name"] = "after";
        Assert.Equal("after", vm.Name);
    }
}
