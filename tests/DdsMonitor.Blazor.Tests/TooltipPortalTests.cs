using System;
using DdsMonitor.Blazor.Tests.Components;
using DdsMonitor.Engine.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace DdsMonitor.Blazor.Tests;

/// <summary>
/// bUnit tests for tooltip provider registry rendering (PLA1-DEBT-017).
///
/// Uses <see cref="StubTooltipPortal"/> — a minimal stub that mirrors the
/// <c>GetOverrideHtml</c> / fallback logic of <c>TooltipPortal.razor</c> without
/// requiring a reference to <c>DdsMonitor.Blazor</c> (which carries native binaries).
/// </summary>
public sealed class TooltipPortalTests : TestContext
{
    // ── Provider returns markup ────────────────────────────────────────────

    [Fact]
    public void StubTooltipPortal_RendersProviderHtml_WhenRegistryReturnsMarkup()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((t, v) =>
            t == typeof(FooTopicType) ? "<b>sensor-gauge</b>" : null);

        Services.AddSingleton<ITooltipProviderRegistry>(registry);

        var cut = RenderComponent<StubTooltipPortal>(p => p
            .Add(c => c.ContextType, typeof(FooTopicType))
            .Add(c => c.ContextValue, null)
            .Add(c => c.DefaultHtml, "default-json-content"));

        // Provider HTML rendered directly — NOT wrapped in <pre>
        Assert.Contains("sensor-gauge", cut.Markup);
        Assert.DoesNotContain("<pre", cut.Markup);
    }

    // ── Provider returns null → default JSON in <pre> ─────────────────────

    [Fact]
    public void StubTooltipPortal_RendersDefaultJson_WhenProviderReturnsNull()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((t, v) => null); // always defers

        Services.AddSingleton<ITooltipProviderRegistry>(registry);

        var cut = RenderComponent<StubTooltipPortal>(p => p
            .Add(c => c.ContextType, typeof(FooTopicType))
            .Add(c => c.ContextValue, null)
            .Add(c => c.DefaultHtml, "default-json-content"));

        Assert.Contains("default-json-content", cut.Markup);
        Assert.Contains("tooltip-content", cut.Markup); // in <pre class="tooltip-content">
        Assert.DoesNotContain("sensor-gauge", cut.Markup);
    }

    // ── No provider registered → default JSON ─────────────────────────────

    [Fact]
    public void StubTooltipPortal_RendersDefaultJson_WhenNoProviderRegistered()
    {
        var registry = new TooltipProviderRegistry(); // empty registry
        Services.AddSingleton<ITooltipProviderRegistry>(registry);

        var cut = RenderComponent<StubTooltipPortal>(p => p
            .Add(c => c.ContextType, typeof(FooTopicType))
            .Add(c => c.ContextValue, null)
            .Add(c => c.DefaultHtml, "fallback-content"));

        Assert.Contains("fallback-content", cut.Markup);
        Assert.Contains("tooltip-content", cut.Markup);
    }

    // ── Provider for different type → default JSON ─────────────────────────

    [Fact]
    public void StubTooltipPortal_RendersDefaultJson_WhenProviderDoesNotMatchType()
    {
        var registry = new TooltipProviderRegistry();
        // Provider only handles BarTopicType
        registry.RegisterProvider((t, v) =>
            t == typeof(BarTopicType) ? "<em>bar-tooltip</em>" : null);

        Services.AddSingleton<ITooltipProviderRegistry>(registry);

        var cut = RenderComponent<StubTooltipPortal>(p => p
            .Add(c => c.ContextType, typeof(FooTopicType))
            .Add(c => c.ContextValue, null)
            .Add(c => c.DefaultHtml, "foo-default"));

        Assert.Contains("foo-default", cut.Markup);
        Assert.Contains("tooltip-content", cut.Markup);
        Assert.DoesNotContain("bar-tooltip", cut.Markup);
    }

    // ── Null ContextType → always shows default ────────────────────────────

    [Fact]
    public void StubTooltipPortal_RendersDefaultJson_WhenContextTypeIsNull()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((t, v) => "<b>should-not-appear</b>");

        Services.AddSingleton<ITooltipProviderRegistry>(registry);

        var cut = RenderComponent<StubTooltipPortal>(p => p
            .Add(c => c.ContextType, (Type?)null)
            .Add(c => c.ContextValue, null)
            .Add(c => c.DefaultHtml, "no-context-default"));

        Assert.Contains("no-context-default", cut.Markup);
        Assert.DoesNotContain("should-not-appear", cut.Markup);
    }
}
