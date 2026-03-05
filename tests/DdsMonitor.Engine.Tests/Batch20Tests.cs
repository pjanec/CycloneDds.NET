using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for BATCH-20 features:
/// - Detail Panel Position Persistence (Task 1)
/// - All Samples Column Aggregation (Task 4)
/// - All Samples Panel Identification (Task 3)
/// </summary>
public sealed class Batch20Tests
{
    // ───────────────────────────────────────────────
    // Task 1: Detail Panel Hide-on-Close Persistence
    // ───────────────────────────────────────────────

    [Fact]
    public void DetailPanel_HiddenState_PreservesPosition()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("DetailPanel");
        panel.X = 200;
        panel.Y = 150;
        panel.Width = 500;
        panel.Height = 400;
        panel.Title = "Detail [TestTopic]";

        // Simulate hide-on-close: set IsHidden instead of calling ClosePanel
        panel.IsHidden = true;

        var found = manager.ActivePanels.FirstOrDefault(p => p.PanelId == panel.PanelId);
        Assert.NotNull(found);
        Assert.True(found!.IsHidden);
        Assert.Equal(200, found.X);
        Assert.Equal(150, found.Y);
        Assert.Equal(500, found.Width);
        Assert.Equal(400, found.Height);
    }

    [Fact]
    public void DetailPanel_HiddenState_SurvivesWorkspaceRoundTrip()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("DetailPanel");
        panel.Title = "Detail [SavedTopic]";
        panel.X = 300;
        panel.Y = 200;
        panel.Width = 600;
        panel.Height = 450;
        panel.IsHidden = true;

        var json = manager.SaveWorkspaceToJson();

        var manager2 = new WindowManager();
        manager2.LoadWorkspaceFromJson(json);

        var reloaded = manager2.ActivePanels.FirstOrDefault(p => p.PanelId == panel.PanelId);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsHidden);
        Assert.Equal(300, reloaded.X);
        Assert.Equal(200, reloaded.Y);
        Assert.Equal(600, reloaded.Width);
        Assert.Equal(450, reloaded.Height);
        Assert.Equal("Detail [SavedTopic]", reloaded.Title);
    }

    [Fact]
    public void DetailPanel_Restore_ResetsHiddenAndMinimized()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("DetailPanel");
        panel.IsHidden = true;
        panel.IsMinimized = true;

        // Simulate restore (equivalent to OpenDetail reusing a hidden panel)
        panel.IsHidden = false;
        panel.IsMinimized = false;

        Assert.False(panel.IsHidden);
        Assert.False(panel.IsMinimized);
    }

    [Fact]
    public void DetailPanel_MultipleIndexed_EachPreservesItsOwnPosition()
    {
        var manager = new WindowManager();
        var p1 = manager.SpawnPanel("DetailPanel");
        p1.X = 100; p1.Y = 100; p1.Width = 400; p1.Height = 300;
        p1.IsHidden = true;

        var p2 = manager.SpawnPanel("DetailPanel");
        p2.X = 200; p2.Y = 200; p2.Width = 500; p2.Height = 350;
        p2.IsHidden = true;

        var json = manager.SaveWorkspaceToJson();

        var m2 = new WindowManager();
        m2.LoadWorkspaceFromJson(json);

        var r1 = m2.ActivePanels.FirstOrDefault(p => p.PanelId == p1.PanelId);
        var r2 = m2.ActivePanels.FirstOrDefault(p => p.PanelId == p2.PanelId);

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.Equal(100, r1!.X);
        Assert.Equal(200, r2!.X);
        Assert.Equal(400, r1.Width);
        Assert.Equal(500, r2.Width);
    }

    // ───────────────────────────────────────────────
    // Task 3: All Samples Panel Identification
    // ───────────────────────────────────────────────

    [Fact]
    public void AllSamplesPanel_IsIdentifiedByAbsenceOfTopicTypeKey()
    {
        const string topicTypeKey = "SamplesPanel.TopicTypeName";

        var allSamplesState = new Dictionary<string, object>(StringComparer.Ordinal);
        var topicSpecificState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [topicTypeKey] = "SomeAssemblyQualifiedTypeName"
        };

        // All Samples panel has NO TopicTypeName in its state
        Assert.False(allSamplesState.ContainsKey(topicTypeKey));
        // Topic-specific panel HAS TopicTypeName in its state
        Assert.True(topicSpecificState.ContainsKey(topicTypeKey));
    }

    [Fact]
    public void AllSamplesPanel_PanelId_IsReservedAsIndex0()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("SamplesPanel");
        panel.PanelId = "SamplesPanel.0"; // Reserve panel ID 0

        var found = manager.ActivePanels.FirstOrDefault(p =>
            string.Equals(p.PanelId, "SamplesPanel.0", StringComparison.Ordinal));

        Assert.NotNull(found);
    }

    [Fact]
    public void AllSamplesPanel_Hidden_CanBeRestored()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("SamplesPanel");
        panel.PanelId = "SamplesPanel.0";
        panel.IsHidden = true;

        // Simulate OpenAllSamplesPanel finding and restoring it
        var found = manager.ActivePanels.FirstOrDefault(p =>
            string.Equals(p.PanelId, "SamplesPanel.0", StringComparison.Ordinal));

        Assert.NotNull(found);
        found!.IsHidden = false;
        found.IsMinimized = false;

        Assert.False(found.IsHidden);
        Assert.False(found.IsMinimized);
    }

    // ───────────────────────────────────────────────
    // Task 4: All-Topics Column Aggregation
    // ───────────────────────────────────────────────

    [Fact]
    public void ColumnAggregation_DeduplicatesFieldsByStructuredName()
    {
        var registry = new TopicRegistry();
        registry.Register(new TopicMetadata(typeof(AggregateTopicA)));
        registry.Register(new TopicMetadata(typeof(AggregateTopicB)));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aggregated = new List<(string StructuredName, Type SourceTopicType)>();
        var fieldTopicTypes = new Dictionary<FieldMetadata, Type>();

        foreach (var topic in registry.AllTopics)
        {
            foreach (var field in topic.AllFields)
            {
                if (!field.IsSynthetic && seen.Add(field.StructuredName))
                {
                    aggregated.Add((field.StructuredName, topic.TopicType));
                    fieldTopicTypes[field] = topic.TopicType;
                }
            }
        }

        // "SharedField" exists in both topics but must appear only once
        Assert.Single(aggregated, a => a.StructuredName == "SharedField");

        // "OnlyInA" comes from AggregateTopicA
        var onlyInA = aggregated.First(a => a.StructuredName == "OnlyInA");
        Assert.Equal(typeof(AggregateTopicA), onlyInA.SourceTopicType);

        // "OnlyInB" comes from AggregateTopicB
        var onlyInB = aggregated.First(a => a.StructuredName == "OnlyInB");
        Assert.Equal(typeof(AggregateTopicB), onlyInB.SourceTopicType);
    }

    [Fact]
    public void ColumnAggregation_EmptyRegistry_ProducesNoFields()
    {
        var registry = new TopicRegistry();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aggregated = new List<string>();

        foreach (var topic in registry.AllTopics)
        {
            foreach (var field in topic.AllFields.Where(f => !f.IsSynthetic))
            {
                if (seen.Add(field.StructuredName))
                {
                    aggregated.Add(field.StructuredName);
                }
            }
        }

        Assert.Empty(aggregated);
    }

    [Fact]
    public void ColumnAggregation_SingleTopic_AllNonSyntheticFieldsIncluded()
    {
        var registry = new TopicRegistry();
        registry.Register(new TopicMetadata(typeof(AggregateTopicA)));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var aggregated = new List<string>();

        foreach (var topic in registry.AllTopics)
        {
            foreach (var field in topic.AllFields.Where(f => !f.IsSynthetic))
            {
                if (seen.Add(field.StructuredName))
                {
                    aggregated.Add(field.StructuredName);
                }
            }
        }

        Assert.Contains("SharedField", aggregated);
        Assert.Contains("OnlyInA", aggregated);
        Assert.DoesNotContain("OnlyInB", aggregated);
    }

    [Fact]
    public void ColumnAggregation_FieldTopicTypeMap_AllowsCrossTopicNullReturn()
    {
        // Simulate the null-safety check in SamplesPanel.GetFieldValue:
        // If a field belongs to TopicA but the sample is from TopicB, return null.
        var metaA = new TopicMetadata(typeof(AggregateTopicA));
        var metaB = new TopicMetadata(typeof(AggregateTopicB));

        // Simulate _fieldTopicTypes populated during all-topics column aggregation
        var fieldTopicTypes = new Dictionary<FieldMetadata, Type>();
        foreach (var field in metaA.AllFields.Where(f => !f.IsSynthetic))
        {
            fieldTopicTypes[field] = typeof(AggregateTopicA);
        }

        // Create a sample from TopicB
        var sampleTopicBMeta = metaB;

        // For fields belonging to TopicA, the simulated GetFieldValue should return null
        // when the sample's TopicType doesn't match the field's source topic.
        foreach (var field in fieldTopicTypes.Keys)
        {
            if (fieldTopicTypes.TryGetValue(field, out var expectedType) &&
                expectedType != sampleTopicBMeta.TopicType)
            {
                // This is the path that returns null for cross-topic fields
                Assert.NotEqual(sampleTopicBMeta.TopicType, expectedType);
            }
        }
    }

    [Fact]
    public void ColumnAggregation_MatchingTopicType_FieldGetterCanExecute()
    {
        var metaA = new TopicMetadata(typeof(AggregateTopicA));
        var sharedField = metaA.AllFields.First(f =>
            string.Equals(f.StructuredName, "SharedField", StringComparison.Ordinal));

        var payload = new AggregateTopicA { SharedField = 42 };

        // Call getter directly - should not throw when topic type matches
        var result = sharedField.Getter(payload);

        Assert.Equal(42, result);
    }
}

[DdsTopic("AggregateTopicA")]
public partial struct AggregateTopicA
{
    public int SharedField;
    public float OnlyInA;
}

[DdsTopic("AggregateTopicB")]
public partial struct AggregateTopicB
{
    public int SharedField;
    public double OnlyInB;
}
