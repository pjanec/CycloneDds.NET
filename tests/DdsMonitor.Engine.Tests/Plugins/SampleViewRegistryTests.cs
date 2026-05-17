using System.Threading.Tasks;
using DdsMonitor.Engine.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DdsMonitor.Engine.Tests.Plugins;

/// <summary>
/// Behaviour tests for SampleViewRegistry (PLA1-P3-T02).
/// Covers exact match, base-type resolution, interface resolution, and null fallback.
/// </summary>
public sealed class SampleViewRegistryTests
{
    // Tiny payload hierarchy used exclusively in these tests.
    private sealed class MyPayload { }
    private sealed class OtherPayload { }
    private class BasePayload { }
    private sealed class DerivedPayload : BasePayload { }
    private interface IMyInterface { }
    private sealed class InterfacePayload : IMyInterface { }

    private static RenderFragment<SampleData> MakeViewer(string tag) =>
        _ => builder => builder.AddContent(0, tag);

    // ── Null / not-registered ─────────────────────────────────────────────────

    [Fact]
    public void GetViewer_ReturnsNull_WhenNothingRegistered()
    {
        var registry = new SampleViewRegistry();

        var result = registry.GetViewer(typeof(object));

        Assert.Null(result);
    }

    [Fact]
    public void GetViewer_ReturnsNull_ForDifferentType()
    {
        var registry = new SampleViewRegistry();
        registry.Register(typeof(MyPayload), MakeViewer("mine"));

        var result = registry.GetViewer(typeof(OtherPayload));

        Assert.Null(result);
    }

    // ── Exact type match ──────────────────────────────────────────────────────

    [Fact]
    public void GetViewer_ReturnsViewer_ForExactType()
    {
        var registry = new SampleViewRegistry();
        var viewer = MakeViewer("exact");
        registry.Register(typeof(MyPayload), viewer);

        var result = registry.GetViewer(typeof(MyPayload));

        Assert.NotNull(result);
        Assert.Same(viewer, result);
    }

    [Fact]
    public void GetViewer_ExactMatch_TakesPrecedenceOverBaseType()
    {
        var registry = new SampleViewRegistry();
        var baseViewer = MakeViewer("base");
        var exactViewer = MakeViewer("exact");
        registry.Register(typeof(BasePayload), baseViewer);
        registry.Register(typeof(DerivedPayload), exactViewer);

        var result = registry.GetViewer(typeof(DerivedPayload));

        Assert.Same(exactViewer, result);
    }

    // ── Base-type resolution ──────────────────────────────────────────────────

    [Fact]
    public void GetViewer_ReturnsBaseTypeViewer_WhenExactNotRegistered()
    {
        var registry = new SampleViewRegistry();
        var baseViewer = MakeViewer("base");
        registry.Register(typeof(BasePayload), baseViewer);

        var result = registry.GetViewer(typeof(DerivedPayload));

        Assert.NotNull(result);
        Assert.Same(baseViewer, result);
    }

    // ── Interface resolution ──────────────────────────────────────────────────

    [Fact]
    public void GetViewer_ReturnsInterfaceViewer_WhenNeitherExactNorBaseRegistered()
    {
        var registry = new SampleViewRegistry();
        var ifaceViewer = MakeViewer("iface");
        registry.Register(typeof(IMyInterface), ifaceViewer);

        var result = registry.GetViewer(typeof(InterfacePayload));

        Assert.NotNull(result);
        Assert.Same(ifaceViewer, result);
    }

    // ── Register replaces previous ────────────────────────────────────────────

    [Fact]
    public void Register_OverwritesPreviousViewer_ForSameType()
    {
        var registry = new SampleViewRegistry();
        registry.Register(typeof(MyPayload), MakeViewer("first"));
        var second = MakeViewer("second");
        registry.Register(typeof(MyPayload), second);

        var result = registry.GetViewer(typeof(MyPayload));

        Assert.Same(second, result);
    }

    // ── Deterministic interface resolution (DEBT-007) ─────────────────────────

    // Two interfaces whose FullNames are known: IAaa sorts before IZzz.
    private interface IAaa { }
    private interface IZzz { }
    private sealed class MultiIfacePayload : IAaa, IZzz { }

    [Fact]
    public void GetViewer_WithMultipleMatchingInterfaces_ReturnsAlphabeticallyFirst()
    {
        var registry = new SampleViewRegistry();
        var viewerAaa = MakeViewer("aaa");
        var viewerZzz = MakeViewer("zzz");

        // Register both interfaces — Zzz first to ensure registration order doesn't win.
        registry.Register(typeof(IZzz), viewerZzz);
        registry.Register(typeof(IAaa), viewerAaa);

        // IAaa.FullName < IZzz.FullName alphabetically, so viewerAaa must win.
        var result = registry.GetViewer(typeof(MultiIfacePayload));

        Assert.Same(viewerAaa, result);
    }
}
