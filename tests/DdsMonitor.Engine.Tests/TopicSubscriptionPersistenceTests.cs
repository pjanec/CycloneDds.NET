using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DdsMonitor.Engine.Hosting;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for the topic subscription state persistence and CLI include/exclude filtering
/// introduced in the topic-subscription-persistence improvement.
/// Uses existing public test topic types: SampleTopic, MockTopic, DynamicReaderMessage.
/// </summary>
public sealed class TopicSubscriptionPersistenceTests
{
    // Type aliases for the three topics used across tests.
    private static readonly Type TypeA = typeof(SampleTopic);
    private static readonly Type TypeB = typeof(MockTopic);
    private static readonly Type TypeC = typeof(DynamicReaderMessage);

    private static IReadOnlyList<TopicMetadata> AllTestTopics() =>
        new[]
        {
            new TopicMetadata(TypeA),
            new TopicMetadata(TypeB),
            new TopicMetadata(TypeC),
        };

    // ─────────────────────────────────────────────────────────────────────────
    // TopicFilterService tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicFilterService_NoOptions_ReturnsEmptyExclusionSet()
    {
        var topics = AllTestTopics();
        var excluded = TopicFilterService.ComputeExcluded(topics, null, null, null);
        Assert.Empty(excluded);
    }

    [Fact]
    public void TopicFilterService_SavedExcludes_ResolvesToExcludedTypes()
    {
        var topics = AllTestTopics();
        var savedExcludes = new[] { TypeA.FullName! };

        var excluded = TopicFilterService.ComputeExcluded(topics, null, null, savedExcludes);

        Assert.Single(excluded);
        Assert.Contains(TypeA, excluded);
    }

    [Fact]
    public void TopicFilterService_CliExclude_ExcludesMatchingTopics()
    {
        var topics = AllTestTopics();
        var excludes = new[] { TypeB.FullName! };

        var excluded = TopicFilterService.ComputeExcluded(topics, null, excludes, null);

        Assert.Single(excluded);
        Assert.Contains(TypeB, excluded);
    }

    [Fact]
    public void TopicFilterService_CliInclude_ExcludesNonMatchingTopics()
    {
        var topics = AllTestTopics();
        var includes = new[] { TypeA.FullName! };

        var excluded = TopicFilterService.ComputeExcluded(topics, includes, null, null);

        // B and C are excluded because only A is explicitly included.
        Assert.Equal(2, excluded.Count);
        Assert.Contains(TypeB, excluded);
        Assert.Contains(TypeC, excluded);
        Assert.DoesNotContain(TypeA, excluded);
    }

    [Fact]
    public void TopicFilterService_CliIncludeAndExclude_AppliesCorrectly()
    {
        var topics = AllTestTopics();
        // Include A and B, then explicitly exclude A.
        var includes = new[] { TypeA.FullName!, TypeB.FullName! };
        var excludes = new[] { TypeA.FullName! };

        var excluded = TopicFilterService.ComputeExcluded(topics, includes, excludes, null);

        // Only B should remain subscribed; A excluded by CLI, C not included.
        Assert.Contains(TypeA, excluded);
        Assert.Contains(TypeC, excluded);
        Assert.DoesNotContain(TypeB, excluded);
    }

    [Fact]
    public void TopicFilterService_WildcardPattern_MatchesMultipleTopics()
    {
        var topics = AllTestTopics();
        // All three types are in the same namespace.
        var ns = TypeA.Namespace!;
        var wildcardExclude = new[] { $"{ns}.*" };

        var excluded = TopicFilterService.ComputeExcluded(topics, null, wildcardExclude, null);

        Assert.Equal(3, excluded.Count);
    }

    [Fact]
    public void TopicFilterService_GlobMatch_ExactMatch()
    {
        Assert.True(TopicFilterService.GlobMatch("My.Namespace.MyTopic", "My.Namespace.MyTopic"));
        Assert.False(TopicFilterService.GlobMatch("My.Namespace.MyTopic", "My.Namespace.OtherTopic"));
    }

    [Fact]
    public void TopicFilterService_GlobMatch_StarWildcard()
    {
        Assert.True(TopicFilterService.GlobMatch("FeatureDemo.Scenarios.StockTicker", "FeatureDemo.Scenarios.*"));
        Assert.True(TopicFilterService.GlobMatch("FeatureDemo.Scenarios.RobotState", "FeatureDemo.Scenarios.*"));
        Assert.False(TopicFilterService.GlobMatch("Other.Namespace.Something", "FeatureDemo.Scenarios.*"));
    }

    [Fact]
    public void TopicFilterService_CliExclude_OverridesSavedExcludes()
    {
        var topics = AllTestTopics();
        var savedExcludes = new[] { TypeA.FullName! };
        // CLI excludes only B — saved exclusion of A must be ignored.
        var cliExcludes = new[] { TypeB.FullName! };

        var excluded = TopicFilterService.ComputeExcluded(topics, null, cliExcludes, savedExcludes);

        // CLI overrides saved: A is NOT excluded, B IS excluded.
        Assert.DoesNotContain(TypeA, excluded);
        Assert.Contains(TypeB, excluded);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WorkspaceDocument round-trip tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WorkspaceDocument_SerializeDeserialize_RoundTrips()
    {
        var doc = new WorkspaceDocument
        {
            Panels = new List<PanelState>
            {
                new() { PanelId = "p1", Title = "Topics", ComponentTypeName = "TopicExplorer" }
            },
            ExcludedTopics = new List<string>
            {
                "My.Namespace.TopicA",
                "My.Namespace.TopicB"
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        var json = JsonSerializer.Serialize(doc, options);
        var loaded = JsonSerializer.Deserialize<WorkspaceDocument>(json, options);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Panels);
        Assert.Equal(2, loaded.ExcludedTopics!.Count);
        Assert.Contains("My.Namespace.TopicA", loaded.ExcludedTopics);
        Assert.Contains("My.Namespace.TopicB", loaded.ExcludedTopics);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // WindowManager ExcludedTopics persistence tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WindowManager_ExcludedTopics_StartedEmpty()
    {
        var manager = new WindowManager();
        Assert.Empty(manager.ExcludedTopics);
    }

    [Fact]
    public void WindowManager_SetExcludedTopics_PersistsInJson()
    {
        var manager = new WindowManager();
        manager.SetExcludedTopics(new[] { "Ns.TopicA", "Ns.TopicB" });

        var json = manager.SaveWorkspaceToJson();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.TryGetProperty("ExcludedTopics", out var et));
        Assert.Equal(2, et.GetArrayLength());
    }

    [Fact]
    public void WindowManager_LoadWorkspaceFromJson_RestoresExcludedTopics()
    {
        var manager = new WindowManager();
        manager.SetExcludedTopics(new[] { "Ns.TopicA" });

        var json = manager.SaveWorkspaceToJson();

        var manager2 = new WindowManager();
        manager2.LoadWorkspaceFromJson(json);

        Assert.Single(manager2.ExcludedTopics);
        Assert.Contains("Ns.TopicA", manager2.ExcludedTopics);
    }

    [Fact]
    public void WindowManager_LoadWorkspaceFromJson_LegacyArrayFormat_Works()
    {
        // Old workspace format was a plain JSON array of PanelState objects.
        var panels = new List<PanelState>
        {
            new() { PanelId = "p1", Title = "Topics", ComponentTypeName = "TopicExplorer" }
        };
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        var legacyJson = JsonSerializer.Serialize(panels, options);

        var manager = new WindowManager();
        manager.LoadWorkspaceFromJson(legacyJson);

        Assert.Single(manager.ActivePanels);
        Assert.Empty(manager.ExcludedTopics);
    }

    [Fact]
    public void WindowManager_ExcludedTopics_NullWhenEmpty_NotSerializedAsEmptyArray()
    {
        var manager = new WindowManager();
        manager.SpawnPanel("TestPanel");

        var json = manager.SaveWorkspaceToJson();
        using var doc = JsonDocument.Parse(json);

        // When ExcludedTopics is empty/null it should be omitted from the JSON.
        Assert.False(doc.RootElement.TryGetProperty("ExcludedTopics", out _));
    }

    [Fact]
    public void WindowManager_SaveAndLoad_RoundTripsExcludedTopicsAndPanels()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("SamplesPanel");
        panel.Title = "All Samples";
        manager.SetExcludedTopics(new[] { "Ns.ExcludedTopic" });

        var path = Path.GetTempFileName();
        try
        {
            manager.SaveWorkspace(path);

            var manager2 = new WindowManager();
            manager2.LoadWorkspace(path);

            Assert.Single(manager2.ActivePanels);
            Assert.Equal("All Samples", manager2.ActivePanels[0].Title);
            Assert.Single(manager2.ExcludedTopics);
            Assert.Equal("Ns.ExcludedTopic", manager2.ExcludedTopics[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BrowserLifecycleOptions.KeepAlive tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BrowserLifecycleOptions_KeepAlive_DefaultFalse()
    {
        var opts = new BrowserLifecycleOptions();
        Assert.False(opts.KeepAlive);
    }

    [Fact]
    public void BrowserLifecycleOptions_KeepAlive_CanBeSet()
    {
        var opts = new BrowserLifecycleOptions { KeepAlive = true };
        Assert.True(opts.KeepAlive);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AppSettings IncludeTopics/ExcludeTopics tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AppSettings_IncludeTopics_DefaultEmpty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.IncludeTopics);
    }

    [Fact]
    public void AppSettings_ExcludeTopics_DefaultEmpty()
    {
        var settings = new AppSettings();
        Assert.Empty(settings.ExcludeTopics);
    }
}
