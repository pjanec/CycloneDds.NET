using System;
using DdsMonitor.Blazor.Tests.Components;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace DdsMonitor.Blazor.Tests;

/// <summary>
/// DEBT-008 / DEBT-011: Regression tests verifying that the Detail Panel tree-view path
/// consults <see cref="ISampleViewRegistry.GetViewer"/> keyed by
/// <c>TopicMetadata.TopicType</c> (matching <c>DetailPanel.RenderTreeView()</c>)
/// and renders the custom <see cref="RenderFragment"/> when registered.
///
/// DEBT-011 fix: stub and test both use <c>TopicMetadata.TopicType</c> as the lookup
/// key; <see cref="SampleData.TopicMetadata"/> is now supplied with a real
/// <see cref="TopicMetadata"/> instance (no <c>null!</c>).
/// </summary>
public sealed class DetailPanelViewRegistryTests : TestContext
{
    // ── Topic types used as registry keys (must have [DdsTopic] for TopicMetadata ctor) ──
    // Defined in BlazorTestTypes.cs: FooTopicType, BarTopicType

    private static SampleData MakeSample(Type topicType) =>
        new()
        {
            Payload = Activator.CreateInstance(topicType),
            TopicMetadata = new TopicMetadata(topicType),
            Ordinal = 1
        };

    // ── Registry-based tests (pure unit, no Blazor rendering) ─────────────

    [Fact]
    public void GetViewer_ReturnsViewer_WhenTypeRegistered()
    {
        var registry = new SampleViewRegistry();

        registry.Register(typeof(FooTopicType),
            sd => builder => builder.AddContent(0, "custom-output"));

        var viewer = registry.GetViewer(typeof(FooTopicType));

        Assert.NotNull(viewer);
    }

    [Fact]
    public void GetViewer_ReturnsNull_WhenTypeNotRegistered()
    {
        var registry = new SampleViewRegistry();
        registry.Register(typeof(FooTopicType), sd => builder => builder.AddContent(0, "x"));

        var viewer = registry.GetViewer(typeof(BarTopicType));

        Assert.Null(viewer);
    }

    [Fact]
    public void Viewer_WhenInvoked_ProducesContent()
    {
        var registry = new SampleViewRegistry();
        var sample = MakeSample(typeof(FooTopicType));
        var invoked = false;

        registry.Register(typeof(FooTopicType),
            sd => builder => { invoked = true; builder.AddContent(0, "custom"); });

        var viewer = registry.GetViewer(typeof(FooTopicType));
        Assert.NotNull(viewer);

        var fragment = viewer!(sample);
        var renderedBuilder = new RenderTreeBuilder();
        fragment(renderedBuilder);

        Assert.True(invoked, "Custom RenderFragment was not invoked.");
    }

    // ── bUnit component tests ──────────────────────────────────────────────

    [Fact]
    public void StubDetailTreeView_RendersCustomViewer_WhenRegistered()
    {
        var registry = new SampleViewRegistry();
        var sample = MakeSample(typeof(FooTopicType));

        registry.Register(typeof(FooTopicType),
            sd => builder => builder.AddContent(0, "custom-viewer-output"));

        var cut = RenderComponent<StubDetailTreeView>(p => p
            .Add(c => c.Sample, sample)
            .Add(c => c.Registry, registry));

        Assert.Contains("custom-viewer-output", cut.Markup);
    }

    [Fact]
    public void StubDetailTreeView_RendersDefaultTree_WhenNoViewerRegistered()
    {
        var registry = new SampleViewRegistry();
        var sample = MakeSample(typeof(FooTopicType));
        // No viewer registered for FooTopicType.

        var cut = RenderComponent<StubDetailTreeView>(p => p
            .Add(c => c.Sample, sample)
            .Add(c => c.Registry, registry));

        Assert.Contains("default-tree", cut.Markup);
    }

    [Fact]
    public void StubDetailTreeView_HidesCustomViewer_WhenDifferentTypeRegistered()
    {
        var registry = new SampleViewRegistry();
        var sample = MakeSample(typeof(FooTopicType));

        // Register viewer for BarTopicType, not FooTopicType.
        registry.Register(typeof(BarTopicType),
            sd => builder => builder.AddContent(0, "wrong-viewer"));

        var cut = RenderComponent<StubDetailTreeView>(p => p
            .Add(c => c.Sample, sample)
            .Add(c => c.Registry, registry));

        // Must fall back to the default tree because FooTopicType has no registered viewer.
        Assert.Contains("default-tree", cut.Markup);
        Assert.DoesNotContain("wrong-viewer", cut.Markup);
    }
}
