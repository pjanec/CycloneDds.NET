using System;
using DdsMonitor.Engine.Ui;

namespace DdsMonitor.Engine.Tests.Ui;

/// <summary>
/// Unit tests for <see cref="TooltipProviderRegistry"/> (PLA1-P6-T06).
/// </summary>
public sealed class TooltipProviderRegistryTests
{
    [Fact]
    public void GetTooltipHtml_WhenNoProviders_ReturnsNull()
    {
        var registry = new TooltipProviderRegistry();

        var result = registry.GetTooltipHtml(typeof(string), "hello");

        Assert.Null(result);
    }

    [Fact]
    public void GetTooltipHtml_WhenProviderMatches_ReturnsHtml()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((type, value) =>
            type == typeof(int) ? "<b>int tooltip</b>" : null);

        var result = registry.GetTooltipHtml(typeof(int), 42);

        Assert.Equal("<b>int tooltip</b>", result);
    }

    [Fact]
    public void GetTooltipHtml_WhenFirstProviderReturnsNull_TriesNextProvider()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((_, __) => null);
        registry.RegisterProvider((type, _) => type == typeof(double) ? "<i>double</i>" : null);

        var result = registry.GetTooltipHtml(typeof(double), 3.14);

        Assert.Equal("<i>double</i>", result);
    }

    [Fact]
    public void GetTooltipHtml_WhenNoProviderMatches_ReturnsNull()
    {
        var registry = new TooltipProviderRegistry();
        registry.RegisterProvider((_, __) => null);

        var result = registry.GetTooltipHtml(typeof(string), "anything");

        Assert.Null(result);
    }
}
