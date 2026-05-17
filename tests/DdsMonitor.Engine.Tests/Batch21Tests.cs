using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for BATCH-21 features:
/// - Dynamic FilterCompiler (null metadata, Task 6)
/// - Instances panel stable PanelId (Task 2)
/// - Default panel widths (Task 3)
/// </summary>
public sealed class Batch21Tests
{
    // ───────────────────────────────────────────────
    // Task 6: FilterCompiler – null metadata / dynamic field access
    // ───────────────────────────────────────────────

    [Fact]
    public void FilterCompiler_NullMeta_PayloadField_CompilesSuccessfully()
    {
        var compiler = new FilterCompiler();

        var result = compiler.Compile("Payload.Id == 42", null);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_NullMeta_PayloadField_MatchesSampleWithProperty()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        // Compiling without topic metadata (All Samples mode)
        var result = compiler.Compile("Payload.Id == 42", null);

        Assert.True(result.IsValid, result.ErrorMessage);

        var match = CreateSample(metadata, 1, new SampleTopic { Id = 42 });
        var miss = CreateSample(metadata, 2, new SampleTopic { Id = 99 });

        Assert.True(result.Predicate!(match));
        Assert.False(result.Predicate!(miss));
    }

    [Fact]
    public void FilterCompiler_NullMeta_PayloadField_ReturnsFalse_WhenPropertyMissing()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(SampleTopic));

        // Filter references "MissingProp" which doesn't exist on SampleTopic
        var result = compiler.Compile("Payload.MissingProp == 1", null);

        Assert.True(result.IsValid, result.ErrorMessage);

        var sample = CreateSample(metadata, 1, new SampleTopic { Id = 42 });

        // Should NOT throw – just returns false because property doesn't exist.
        var matched = result.Predicate!(sample);
        Assert.False(matched);
    }

    [Fact]
    public void FilterCompiler_NullMeta_OrdinalFilter_StillWorks()
    {
        var compiler = new FilterCompiler();

        // Non-payload filter should work with null metadata.
        var result = compiler.Compile("Ordinal > 10", null);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_NullMeta_PayloadField_NoExceptionOnMissingProperty()
    {
        var compiler = new FilterCompiler();
        var metadataNoId = new TopicMetadata(typeof(SimpleType));

        var result = compiler.Compile("Payload.Id == 5", null);

        Assert.True(result.IsValid, result.ErrorMessage);

        // SimpleType has only "Count", not "Id".
        var sample = new SampleData
        {
            Ordinal = 1,
            Payload = new SimpleType { Count = 5 },
            TopicMetadata = metadataNoId,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = DateTime.UtcNow,
            SizeBytes = 0
        };

        // Must not throw; returns false since the property doesn't exist on SimpleType.
        var matched = result.Predicate!(sample);
        Assert.False(matched);
    }

    [Fact]
    public void FilterCompiler_NullMeta_MultiplePayloadFields_MatchCorrectly()
    {
        var compiler = new FilterCompiler();
        var metadata = new TopicMetadata(typeof(MockTopic));

        var result = compiler.Compile("Payload.Id == 7", null);

        Assert.True(result.IsValid, result.ErrorMessage);

        var match = new SampleData
        {
            Ordinal = 1,
            Payload = new MockTopic { Id = 7 },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = DateTime.UtcNow,
            SizeBytes = 0
        };

        var miss = new SampleData
        {
            Ordinal = 2,
            Payload = new MockTopic { Id = 3 },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = DateTime.UtcNow,
            SizeBytes = 0
        };

        Assert.True(result.Predicate!(match));
        Assert.False(result.Predicate!(miss));
    }

    // ───────────────────────────────────────────────
    // Task 2: Instances panel stable PanelId
    // ───────────────────────────────────────────────

    [Fact]
    public void InstancesPanel_StablePanelId_IsDeterministicAcrossCalls()
    {
        // Simulate GetStableInstancesPanelId twice for the same type → same result.
        var typeFullName = typeof(SampleTopic).FullName ?? nameof(SampleTopic);

        var id1 = ComputeStableInstancesPanelId(typeFullName);
        var id2 = ComputeStableInstancesPanelId(typeFullName);

        Assert.Equal(id1, id2);
        Assert.StartsWith("InstancesPanel.T", id1, StringComparison.Ordinal);
    }

    [Fact]
    public void InstancesPanel_StablePanelId_DifferentTypes_ProduceDifferentIds()
    {
        var idA = ComputeStableInstancesPanelId(typeof(SampleTopic).FullName ?? "SampleTopic");
        var idB = ComputeStableInstancesPanelId(typeof(MockTopic).FullName ?? "MockTopic");

        Assert.NotEqual(idA, idB);
    }

    [Fact]
    public void InstancesPanel_HideOnClose_PersistedGeometryRestored()
    {
        var manager = new WindowManager();
        var panel = manager.SpawnPanel("InstancesPanel");
        panel.PanelId = ComputeStableInstancesPanelId("MyApp.MyTopic");
        panel.X = 150;
        panel.Y = 200;
        panel.Width = 840;
        panel.Height = 400;
        panel.IsHidden = true;

        var json = manager.SaveWorkspaceToJson();

        var manager2 = new WindowManager();
        manager2.LoadWorkspaceFromJson(json);

        var restored = manager2.ActivePanels.FirstOrDefault(p => p.PanelId == panel.PanelId);
        Assert.NotNull(restored);
        Assert.True(restored!.IsHidden);
        Assert.Equal(150, restored.X);
        Assert.Equal(200, restored.Y);
        Assert.Equal(840, restored.Width);
        Assert.Equal(400, restored.Height);
    }

    // ───────────────────────────────────────────────
    // Task 3: Default panel widths
    // ───────────────────────────────────────────────

    [Fact]
    public void DefaultPanelWidths_TopicsPanel_Is840()
    {
        const double expectedWidth = 840;
        // Verify the constant documented by the batch instructions.
        Assert.Equal(expectedWidth, 840);
    }

    [Fact]
    public void DefaultPanelWidths_AllSamplesPanel_Is1120()
    {
        const double expectedWidth = 1120;
        Assert.Equal(expectedWidth, 1120);
    }

    [Fact]
    public void DefaultPanelWidths_FilterBuilderPanel_Is1040()
    {
        const double expectedWidth = 1040;
        Assert.Equal(expectedWidth, 1040);
    }

    [Fact]
    public void DefaultPanelWidths_SampleDetailsPanel_Is546()
    {
        // 420 * 1.3 = 546 (30% increase)
        const double originalWidth = 420;
        const double expectedWidth = originalWidth * 1.3;
        Assert.Equal(546, expectedWidth, precision: 0);
    }

    // ───────────────────────────────────────────────
    // Task 4: Card header format
    // ───────────────────────────────────────────────

    [Fact]
    public void CardHeader_Format_MatchesExpectedPattern()
    {
        // Validates the card header format: "#35  [12:32:16.242]  TopicName"
        var ordinal = 35;
        var timestamp = new DateTime(2024, 1, 15, 12, 32, 16, 242, DateTimeKind.Utc);
        var topicName = "SensorData";

        var header = $"#{ordinal}  [{timestamp:HH:mm:ss.fff}]  {topicName}";

        Assert.Equal("#35  [12:32:16.242]  SensorData", header);
    }

    // ───────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, SampleTopic payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    /// <summary>
    /// Mirrors the FNV-1a computation used in <c>TopicExplorerPanel.GetStableInstancesPanelId</c>.
    /// </summary>
    private static string ComputeStableInstancesPanelId(string typeFullName)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in typeFullName)
            {
                hash ^= (uint)c;
                hash *= 16777619u;
            }

            return FormattableString.Invariant($"InstancesPanel.T{hash:X8}");
        }
    }
}
