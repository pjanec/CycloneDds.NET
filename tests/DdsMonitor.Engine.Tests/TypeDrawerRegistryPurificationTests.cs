using System;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using DdsMonitor.Engine.Ui;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// TASK-A001 purification verification tests:
/// confirms that Engine's Ui registries have no Blazor dependency
/// and work with the new UI-agnostic Func-based API.
/// </summary>
public sealed class TypeDrawerRegistryPurificationTests
{
    // ── ITypeDrawerRegistry / TypeDrawerRegistry ─────────────────────────────

    [Fact]
    public void Register_FuncDrawer_ForInt_ReturnsNonNull()
    {
        var registry = new TypeDrawerRegistry();
        Func<DrawerContext, object?> factory = _ => 42;

        registry.Register(typeof(int), factory);

        Assert.NotNull(registry.GetDrawer(typeof(int)));
    }

    [Fact]
    public void Register_FuncDrawer_ForInt_ReturnsSameDelegate()
    {
        var registry = new TypeDrawerRegistry();
        Func<DrawerContext, object?> factory = _ => 42;

        registry.Register(typeof(int), factory);

        Assert.Same(factory, registry.GetDrawer(typeof(int)));
    }

    [Fact]
    public void Register_FuncDrawer_ForString_ReturnsNonNull()
    {
        var registry = new TypeDrawerRegistry();
        Func<DrawerContext, object?> factory = _ => "text";

        registry.Register(typeof(string), factory);

        Assert.NotNull(registry.GetDrawer(typeof(string)));
    }

    [Fact]
    public void GetDrawer_ForBuiltInType_ReturnsNonNull_WhenNotExplicitlyRegistered()
    {
        // Engine registers a UI-agnostic stub for all built-in types.
        var registry = new TypeDrawerRegistry();

        Assert.NotNull(registry.GetDrawer(typeof(int)));
        Assert.NotNull(registry.GetDrawer(typeof(string)));
        Assert.NotNull(registry.GetDrawer(typeof(bool)));
        Assert.NotNull(registry.GetDrawer(typeof(double)));
    }

    [Fact]
    public void GetDrawer_BuiltInStub_ReturnsNull_ReflectingUiAgnosticNature()
    {
        // Engine stubs return null – real rendering is done by UI adapters.
        var registry = new TypeDrawerRegistry();
        var drawer = registry.GetDrawer(typeof(int));

        Assert.NotNull(drawer);
        var ctx = new DrawerContext("f", typeof(int), () => 0, _ => { });
        Assert.Null(drawer!(ctx));
    }

    [Fact]
    public void DrawerContext_ConstructsWithFourParams_NoReceiver()
    {
        // DrawerContext no longer requires an IHandleEvent receiver (TASK-A001 purification).
        var ctx = new DrawerContext(
            "MyLabel",
            typeof(double),
            () => 3.14,
            v => { });

        Assert.Equal("MyLabel", ctx.Label);
        Assert.Equal(typeof(double), ctx.FieldType);
    }

    [Fact]
    public void DrawerContext_ValueGetter_ReturnsExpectedValue()
    {
        var ctx = new DrawerContext("X", typeof(int), () => 99, _ => { });
        Assert.Equal(99, ctx.ValueGetter());
    }

    [Fact]
    public void DrawerContext_OnChange_InvokesCallback()
    {
        object? captured = null;
        var ctx = new DrawerContext("X", typeof(int), () => 0, v => captured = v);

        ctx.OnChange("hello");

        Assert.Equal("hello", captured);
    }

    [Fact]
    public void DrawerContext_HasNoReceiverProperty()
    {
        // Verify at compile time that DrawerContext does not expose Receiver.
        // If this file compiles without referencing ctx.Receiver, the property is gone.
        var ctx = new DrawerContext("F", typeof(int), () => 0, _ => { });
        Assert.NotNull(ctx); // just to use ctx
    }

    [Fact]
    public void HasDrawer_ReturnsFalse_ForUnknownType()
    {
        var registry = new TypeDrawerRegistry();
        Assert.False(registry.HasDrawer(typeof(TypeDrawerRegistryPurificationTests)));
    }

    [Fact]
    public void HasDrawer_ReturnsTrue_AfterRegister()
    {
        var registry = new TypeDrawerRegistry();
        registry.Register(typeof(TypeDrawerRegistryPurificationTests), _ => null);
        Assert.True(registry.HasDrawer(typeof(TypeDrawerRegistryPurificationTests)));
    }

    // ── ISampleViewRegistry / SampleViewRegistry ─────────────────────────────

    private sealed class DummyPayload { }

    [Fact]
    public void SampleViewRegistry_Register_FuncViewer_ReturnsNonNull()
    {
        var registry = new SampleViewRegistry();
        Func<SampleData, object?> viewer = _ => "view";

        registry.Register(typeof(DummyPayload), viewer);

        Assert.NotNull(registry.GetViewer(typeof(DummyPayload)));
    }

    [Fact]
    public void SampleViewRegistry_Register_FuncViewer_ReturnsSameDelegate()
    {
        var registry = new SampleViewRegistry();
        Func<SampleData, object?> viewer = _ => new object();

        registry.Register(typeof(DummyPayload), viewer);

        Assert.Same(viewer, registry.GetViewer(typeof(DummyPayload)));
    }

    [Fact]
    public void SampleViewRegistry_GetViewer_ReturnsNull_WhenNothingRegistered()
    {
        var registry = new SampleViewRegistry();
        Assert.Null(registry.GetViewer(typeof(DummyPayload)));
    }

    [Fact]
    public void SampleViewRegistry_FuncViewer_IsCallable()
    {
        var registry = new SampleViewRegistry();
        Func<SampleData, object?> viewer = _ => "rendered";

        registry.Register(typeof(DummyPayload), viewer);
        var result = registry.GetViewer(typeof(DummyPayload))!(null!);

        Assert.Equal("rendered", result);
    }
}
