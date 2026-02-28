using System.Text.Json;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class HoverTooltipTests
{
    [Fact]
    public void HoverTooltip_ValidJson_ParsesWithoutError()
    {
        var result = JsonTooltipParser.TryFormatJson("{\"key\": 42}", out var formatted);

        Assert.True(result);

        using var document = JsonDocument.Parse(formatted);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void HoverTooltip_InvalidJson_ReturnsFalse()
    {
        var result = JsonTooltipParser.TryFormatJson("just a string", out var formatted);

        Assert.False(result);
        Assert.Equal(string.Empty, formatted);
    }
}
