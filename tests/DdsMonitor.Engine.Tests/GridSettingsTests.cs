using System.Collections.Generic;
using DdsMonitor.Engine.Ui;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class GridSettingsTests
{
    [Fact]
    public void GridSettings_SerializeDeserialize_RoundTrips()
    {
        var original = new GridSettings
        {
            FilterText = "Payload.Id > 10",
            ColumnKeys = new List<string> { "Payload.Id", "Payload.Value" },
            ColumnWeights = new Dictionary<string, double>
            {
                ["Payload.Id"] = 0.3,
                ["Payload.Value"] = 0.7
            },
            SortFieldKey = "Payload.Id",
            SortDirection = SortDirection.Descending
        };

        var json = original.ToJson();
        var restored = GridSettings.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal(original.FilterText, restored!.FilterText);
        Assert.Equal(original.ColumnKeys, restored.ColumnKeys);
        Assert.Equal(original.ColumnWeights, restored.ColumnWeights);
        Assert.Equal(original.SortFieldKey, restored.SortFieldKey);
        Assert.Equal(original.SortDirection, restored.SortDirection);
    }

    [Fact]
    public void GridSettings_FromJson_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(GridSettings.FromJson(string.Empty));
        Assert.Null(GridSettings.FromJson("   "));
    }

    [Fact]
    public void GridSettings_FromJson_InvalidJson_ReturnsNull()
    {
        Assert.Null(GridSettings.FromJson("not valid json {{{{"));
    }

    [Fact]
    public void GridSettings_ToJson_ProducesValidJson()
    {
        var settings = new GridSettings
        {
            FilterText = "Ordinal == 1",
            SortDirection = SortDirection.Ascending
        };

        var json = settings.ToJson();

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("SortDirection", json);
    }
}
