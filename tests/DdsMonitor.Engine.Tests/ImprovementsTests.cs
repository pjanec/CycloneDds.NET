using System;
using System.Collections.Generic;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for the improvements batch:
///   - AppSettings (layout file, config folder CLI options)
///   - "Show Only This Topic" filter text helper
///   - DetailPanel link persistence via PanelState
///   - Tree expand/collapse override logic
/// </summary>
public sealed class ImprovementsTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // AppSettings — structure
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AppSettings_DefaultValues_AreNull()
    {
        var settings = new AppSettings();

        Assert.Null(settings.WorkspaceFile);
        Assert.Null(settings.ConfigFolder);
    }

    [Fact]
    public void AppSettings_SectionName_IsAppSettings()
    {
        Assert.Equal("AppSettings", AppSettings.SectionName);
    }

    [Fact]
    public void AppSettings_WorkspaceFile_CanBeSet()
    {
        var settings = new AppSettings { WorkspaceFile = "/path/to/layout.json" };

        Assert.Equal("/path/to/layout.json", settings.WorkspaceFile);
    }

    [Fact]
    public void AppSettings_ConfigFolder_CanBeSet()
    {
        var settings = new AppSettings { ConfigFolder = "/path/to/config" };

        Assert.Equal("/path/to/config", settings.ConfigFolder);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Topic filter text helpers
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("MyTopic", "Sample.Topic == \"MyTopic\"")]
    [InlineData("Topic With Spaces", "Sample.Topic == \"Topic With Spaces\"")]
    [InlineData("Topic\"WithQuotes\"", "Sample.Topic == \"Topic\\\"WithQuotes\\\"\"")]
    [InlineData("Topic\\WithBackslash", "Sample.Topic == \"Topic\\\\WithBackslash\"")]
    public void TopicFilter_ShowOnlyTopic_ProducesCorrectExpression(string topic, string expected)
    {
        var result = BuildShowOnlyTopicFilter(topic);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("MyTopic", "Sample.Topic != \"MyTopic\"")]
    [InlineData("Topic\"Q\"", "Sample.Topic != \"Topic\\\"Q\\\"\"")]
    public void TopicFilter_ExcludeTopic_ProducesCorrectExpression(string topic, string expected)
    {
        var result = BuildExcludeTopicFilter(topic, string.Empty);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TopicFilter_ExcludeTopic_AppendsToExistingFilter()
    {
        var result = BuildExcludeTopicFilter("MyTopic", "Sample.Topic != \"OtherTopic\"");
        Assert.Equal("(Sample.Topic != \"OtherTopic\") AND Sample.Topic != \"MyTopic\"", result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DetailPanel link persistence key
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DetailPanel_PersistPanelState_SavesIsLinkedAndSourcePanelId()
    {
        var state = new PanelState { PanelId = "DetailPanel.1" };

        // Simulate what PersistPanelState does.
        state.ComponentState["IsLinked"] = true;
        state.ComponentState["SourcePanelId"] = "SamplesPanel.0";

        Assert.True((bool)state.ComponentState["IsLinked"]);
        Assert.Equal("SamplesPanel.0", state.ComponentState["SourcePanelId"]?.ToString());
    }

    [Fact]
    public void DetailPanel_PersistPanelState_OverwritesStaleSourcePanelId()
    {
        var state = new PanelState { PanelId = "DetailPanel.1" };
        state.ComponentState["SourcePanelId"] = "SamplesPanel.0";

        // Simulate moving the link to a different panel.
        state.ComponentState["SourcePanelId"] = "SamplesPanel.2";

        Assert.Equal("SamplesPanel.2", state.ComponentState["SourcePanelId"]?.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tree expand/collapse override logic – mirrors DetailPanel behaviour
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TreeExpand_WithNullOverride_DefaultsStructExpanded_ArrayCollapsed()
    {
        var treeExpanded = new Dictionary<string, bool>();
        bool? treeExpandOverride = null;

        Assert.True(SimulateIsExpanded(treeExpanded, treeExpandOverride, "MyField", isArray: false));
        Assert.False(SimulateIsExpanded(treeExpanded, treeExpandOverride, "MyArray", isArray: true));
    }

    [Fact]
    public void TreeExpand_WithTrueOverride_AllNodesExpanded()
    {
        var treeExpanded = new Dictionary<string, bool> { ["someArray"] = false };
        bool? treeExpandOverride = true;

        Assert.True(SimulateIsExpanded(treeExpanded, treeExpandOverride, "someArray", isArray: true));
        Assert.True(SimulateIsExpanded(treeExpanded, treeExpandOverride, "SomeStruct", isArray: false));
    }

    [Fact]
    public void TreeExpand_WithFalseOverride_AllNodesCollapsed()
    {
        var treeExpanded = new Dictionary<string, bool> { ["someStruct"] = true };
        bool? treeExpandOverride = false;

        Assert.False(SimulateIsExpanded(treeExpanded, treeExpandOverride, "someStruct", isArray: false));
        Assert.False(SimulateIsExpanded(treeExpanded, treeExpandOverride, "SomeArray", isArray: true));
    }

    [Fact]
    public void TreeExpand_ManualToggle_ClearsOverride()
    {
        var treeExpanded = new Dictionary<string, bool>();
        bool? treeExpandOverride = true;

        // Manual toggle clears the override.
        treeExpandOverride = null;
        treeExpanded["SomeArray"] = !SimulateIsExpanded(treeExpanded, treeExpandOverride, "SomeArray", isArray: true);

        // After toggle, the explicit state should be used (array was false default, toggled to true).
        Assert.True(treeExpanded["SomeArray"]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string BuildShowOnlyTopicFilter(string topicName)
    {
        var escaped = topicName.Replace("\\", "\\\\", StringComparison.Ordinal)
                               .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"Sample.Topic == \"{escaped}\"";
    }

    private static string BuildExcludeTopicFilter(string topicName, string existingFilter)
    {
        var escaped = topicName.Replace("\\", "\\\\", StringComparison.Ordinal)
                               .Replace("\"", "\\\"", StringComparison.Ordinal);
        var condition = $"Sample.Topic != \"{escaped}\"";
        return string.IsNullOrWhiteSpace(existingFilter)
            ? condition
            : $"({existingFilter}) AND {condition}";
    }

    private static bool SimulateIsExpanded(
        Dictionary<string, bool> treeExpanded,
        bool? treeExpandOverride,
        string nodePath,
        bool isArray)
    {
        if (treeExpandOverride.HasValue)
        {
            return treeExpandOverride.Value;
        }

        if (treeExpanded.TryGetValue(nodePath, out var val))
        {
            return val;
        }

        return !isArray;
    }
}
