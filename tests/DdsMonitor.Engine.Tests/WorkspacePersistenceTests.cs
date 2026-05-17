using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class WorkspacePersistenceTests
{
    [Fact]
    public void WorkspacePersistence_SerializeDeserialize_RoundTrips()
    {
        var panels = new List<PanelState>
        {
            new()
            {
                PanelId = "SamplesPanel.1",
                Title = "Samples [RobotState]",
                ComponentTypeName = "SamplesPanel",
                X = 42,
                Y = 84,
                Width = 520,
                Height = 360,
                ZIndex = 7,
                IsMinimized = false,
                IsHidden = false,
                ComponentState = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["SelectedColumns"] = new[] { "Payload.Id", "Payload.Status" },
                    ["Threshold"] = 3.5,
                    ["IsPinned"] = true
                }
            },
            new()
            {
                PanelId = "DetailPanel.2",
                Title = "Detail",
                ComponentTypeName = "DetailPanel",
                X = 120,
                Y = 140,
                Width = 440,
                Height = 300,
                ZIndex = 8,
                IsMinimized = true,
                IsHidden = false,
                ComponentState = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["Notes"] = "Pinned"
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        var json = JsonSerializer.Serialize(panels, options);
        var roundTrip = JsonSerializer.Deserialize<List<PanelState>>(json, options);

        Assert.NotNull(roundTrip);
        Assert.Equal(panels.Count, roundTrip!.Count);

        for (var i = 0; i < panels.Count; i++)
        {
            var original = panels[i];
            var loaded = roundTrip[i];

            Assert.Equal(original.PanelId, loaded.PanelId);
            Assert.Equal(original.Title, loaded.Title);
            Assert.Equal(original.ComponentTypeName, loaded.ComponentTypeName);
            Assert.Equal(original.X, loaded.X);
            Assert.Equal(original.Y, loaded.Y);
            Assert.Equal(original.Width, loaded.Width);
            Assert.Equal(original.Height, loaded.Height);
            Assert.Equal(original.ZIndex, loaded.ZIndex);
            Assert.Equal(original.IsMinimized, loaded.IsMinimized);
            Assert.Equal(original.IsHidden, loaded.IsHidden);

            Assert.Equal(original.ComponentState.Count, loaded.ComponentState.Count);
            foreach (var entry in original.ComponentState)
            {
                Assert.True(loaded.ComponentState.TryGetValue(entry.Key, out var loadedValue));
                Assert.Equal(NormalizeValue(entry.Value), NormalizeValue(loadedValue));
            }
        }
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeValue(item)).ToArray(),
                _ => element.ToString()
            };
        }

        if (value is string[] array)
        {
            return array.ToArray();
        }

        return value;
    }
}
