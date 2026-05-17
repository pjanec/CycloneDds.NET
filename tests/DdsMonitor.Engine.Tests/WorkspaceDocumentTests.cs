using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Unit tests for <see cref="WorkspaceDocument.PluginSettings"/> serialisation behaviour
/// (PLA1-P4-T02).
/// </summary>
public sealed class WorkspaceDocumentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Serialize_OmitsPluginSettings_WhenNull()
    {
        var doc = new WorkspaceDocument { PluginSettings = null };

        var json = JsonSerializer.Serialize(doc, JsonOptions);

        Assert.DoesNotContain("PluginSettings", json, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_IncludesPluginSettings_WhenPopulated()
    {
        var doc = new WorkspaceDocument
        {
            PluginSettings = new Dictionary<string, object>
            {
                ["ECS"] = new Dictionary<string, object> { ["Key"] = "Value" }
            }
        };

        var json = JsonSerializer.Serialize(doc, JsonOptions);

        Assert.Contains("\"PluginSettings\"", json, System.StringComparison.Ordinal);
        Assert.Contains("\"ECS\"", json, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_OldFormat_DoesNotThrow()
    {
        const string oldJson = "{\"Panels\":[],\"ExcludedTopics\":null}";

        var doc = JsonSerializer.Deserialize<WorkspaceDocument>(oldJson, JsonOptions);

        Assert.NotNull(doc);
        Assert.Null(doc!.PluginSettings);
    }
}
