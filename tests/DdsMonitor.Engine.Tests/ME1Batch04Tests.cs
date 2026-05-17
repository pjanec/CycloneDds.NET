using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Channels;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME1-BATCH-04:
///   ME1-C02 — InlineArray union arms receive discriminator metadata from TopicMetadata.
///   ME1-C04 — D02 (SchemaDiscovery helpers), D03 (SerializerEmitter helper), D04 (AddParticipant hot-wire).
///   ME1-C05 — JSON enum serialization as string names.
///   ME1-C07 — IsLinkedDetailPanel correctly parses both bool and JsonElement values.
///   ME1-C08 — JSON export strips inactive union arms.
/// </summary>
public sealed class ME1Batch04Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME1-C02 (D05) — InlineArray union arms receive discriminator metadata
    // ─────────────────────────────────────────────────────────────────────────
    // EightFloatsInline is [InlineArray(8)] FloatBuf8 — the D05 fix specifically
    // moves union-arm detection BEFORE the InlineArray early-exit so it gets metadata.
    // OkMessage is FixedString32 (not InlineArray) and was already handled correctly.

    [Fact]
    public void TopicMetadata_InlineArrayUnionArm_EightFloats_IsFixedSizeArray()
    {
        var meta = new TopicMetadata(typeof(SelfTestPose));
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "UnionValue.EightFloatsInline");

        Assert.NotNull(field);
        Assert.True(field.IsFixedSizeArray, "InlineArray arm must be IsFixedSizeArray=true.");
        Assert.Equal(8, field.FixedArrayLength);
        Assert.Equal(typeof(float), field.ElementType);
    }

    [Fact]
    public void TopicMetadata_InlineArrayUnionArm_EightFloats_HasDiscriminatorMetadata()
    {
        // Key D05 regression: before fix, union-arm detection was after InlineArray exit
        // so EightFloatsInline got no DependentDiscriminatorPath.
        var meta = new TopicMetadata(typeof(SelfTestPose));
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "UnionValue.EightFloatsInline");

        Assert.NotNull(field);
        Assert.False(field.IsDiscriminatorField);
        Assert.Equal("UnionValue.level", field.DependentDiscriminatorPath);
        Assert.NotNull(field.ActiveWhenDiscriminatorValue);
        Assert.Equal((long)(int)StatusLevel.Error, Convert.ToInt64(field.ActiveWhenDiscriminatorValue));
        Assert.False(field.IsDefaultUnionCase);
    }

    [Fact]
    public void TopicMetadata_NonInlineArray_UnionArm_OkMessage_HasDiscriminatorMetadata()
    {
        // OkMessage is FixedString32 (not InlineArray), but is a union arm.
        // Verify it also has correct discriminator metadata (sanity check).
        var meta = new TopicMetadata(typeof(SelfTestPose));
        var field = meta.AllFields.SingleOrDefault(f => f.StructuredName == "UnionValue.OkMessage");

        Assert.NotNull(field);
        Assert.False(field.IsDiscriminatorField);
        Assert.Equal("UnionValue.level", field.DependentDiscriminatorPath);
        Assert.NotNull(field.ActiveWhenDiscriminatorValue);
        Assert.Equal((long)(int)StatusLevel.Ok, Convert.ToInt64(field.ActiveWhenDiscriminatorValue));
        Assert.False(field.IsDefaultUnionCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-C04 (D04) — DdsBridge.AddParticipant registers a new participant
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsBridge_InitialState_HasOneParticipant()
    {
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);
        Assert.Single(bridge.ParticipantConfigs);
        Assert.Single(bridge.Participants);
    }

    [Fact]
    public void DdsBridge_AddParticipant_IncreasesParticipantCount()
    {
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);
        Assert.Single(bridge.ParticipantConfigs);
        bridge.AddParticipant(0, string.Empty);
        Assert.Equal(2, bridge.ParticipantConfigs.Count);
        Assert.Equal(2, bridge.Participants.Count);
    }

    [Fact]
    public void DdsBridge_RemoveParticipant_DecreasesParticipantCount()
    {
        using var bridge = new DdsBridge(Channel.CreateUnbounded<SampleData>().Writer);
        bridge.AddParticipant(0, string.Empty);
        bridge.RemoveParticipant(1);
        Assert.Single(bridge.ParticipantConfigs);
        Assert.Single(bridge.Participants);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-C05 — JSON enum serialization as string names
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsJsonOptions_Export_SerializesEnumAsString()
    {
        var payload = new MockEnumTopic { Id = 1, Status = MockTopicStatus.Warning };
        var json = JsonSerializer.Serialize(payload, DdsJsonOptions.Export);
        Assert.Contains("\"Warning\"", json);
        // Enum value must be a string name — "Status" must NOT be followed by an integer literal.
        Assert.DoesNotContain("\"Status\":1", json);
        Assert.DoesNotContain("\"Status\":0", json);
    }

    [Fact]
    public void DdsJsonOptions_Display_SerializesEnumAsString()
    {
        var payload = new MockEnumTopic { Id = 2, Status = MockTopicStatus.Error };
        var json = JsonSerializer.Serialize(payload, DdsJsonOptions.Display);
        Assert.Contains("\"Error\"", json);
    }

    [Fact]
    public void DdsJsonOptions_Import_DeserializesEnumFromString()
    {
        const string json = "{\"Id\":3,\"Status\":\"Ok\"}";
        var payload = JsonSerializer.Deserialize<MockEnumTopic>(json, DdsJsonOptions.Import);
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.Id);
        Assert.Equal(MockTopicStatus.Ok, payload.Status);
    }

    [Fact]
    public void DdsJsonOptions_Export_EnumRoundTripsViaImport()
    {
        var original = new MockEnumTopic { Id = 5, Status = MockTopicStatus.Error };
        var json = JsonSerializer.Serialize(original, DdsJsonOptions.Export);
        var restored = JsonSerializer.Deserialize<MockEnumTopic>(json, DdsJsonOptions.Import);
        Assert.NotNull(restored);
        Assert.Equal(original.Status, restored!.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-C07 — PanelState.ComponentState deserialises IsLinked as JsonElement
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PanelState_ComponentState_IsLinked_True_DeserializesAsJsonElement()
    {
        // When workspace JSON is loaded, Dictionary<string,object> values come back
        // as JsonElement. SamplesPanel.IsLinkedDetailPanel must handle both bool and JsonElement.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var panels = new List<PanelState>
        {
            new() { PanelId = "Detail.1", ComponentState = new(StringComparer.Ordinal) { ["IsLinked"] = true } }
        };
        var json = JsonSerializer.Serialize(panels, options);
        var loaded = JsonSerializer.Deserialize<List<PanelState>>(json, options);

        Assert.NotNull(loaded);
        var panel = Assert.Single(loaded!);
        Assert.True(panel.ComponentState.TryGetValue("IsLinked", out var raw));
        var element = Assert.IsType<JsonElement>(raw);
        Assert.Equal(JsonValueKind.True, element.ValueKind);
        Assert.True(element.GetBoolean());
    }

    [Fact]
    public void PanelState_ComponentState_IsLinked_False_DeserializesAsJsonElement()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var panels = new List<PanelState>
        {
            new() { PanelId = "Detail.2", ComponentState = new(StringComparer.Ordinal) { ["IsLinked"] = false } }
        };
        var json = JsonSerializer.Serialize(panels, options);
        var loaded = JsonSerializer.Deserialize<List<PanelState>>(json, options);

        var panel = Assert.Single(loaded!);
        Assert.True(panel.ComponentState.TryGetValue("IsLinked", out var raw));
        var element = Assert.IsType<JsonElement>(raw);
        Assert.Equal(JsonValueKind.False, element.ValueKind);
        Assert.False(element.GetBoolean());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-C08 — JSON export strips inactive union arms via DdsUnionJsonConverterFactory
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DdsUnionJsonConverter_CanConvert_ReturnsTrueForDdsUnion()
    {
        Assert.True(DdsUnionJsonConverterFactory.Instance.CanConvert(typeof(TestingUnion)));
    }

    [Fact]
    public void DdsUnionJsonConverter_CanConvert_ReturnsFalseForNonUnion()
    {
        Assert.False(DdsUnionJsonConverterFactory.Instance.CanConvert(typeof(SelfTestPose)));
        Assert.False(DdsUnionJsonConverterFactory.Instance.CanConvert(typeof(MockEnumTopic)));
    }

    [Fact]
    public void DdsJsonOptions_Export_UnionArm_OkCase_OnlyOkMessagePresent()
    {
        var union = new TestingUnion { level = StatusLevel.Ok };
        var json = JsonSerializer.Serialize(union, DdsJsonOptions.Export);

        Assert.Contains("\"level\"", json);
        Assert.Contains("\"OkMessage\"", json);
        Assert.DoesNotContain("\"EightFloatsInline\"", json);
        Assert.DoesNotContain("\"DefaultMessage\"", json);
    }

    [Fact]
    public void DdsJsonOptions_Export_UnionArm_ErrorCase_OnlyEightFloatsPresent()
    {
        var union = new TestingUnion { level = StatusLevel.Error };
        var json = JsonSerializer.Serialize(union, DdsJsonOptions.Export);

        Assert.Contains("\"level\"", json);
        Assert.Contains("\"EightFloatsInline\"", json);
        Assert.DoesNotContain("\"OkMessage\"", json);
        Assert.DoesNotContain("\"DefaultMessage\"", json);
    }

    [Fact]
    public void DdsJsonOptions_Export_UnionArm_DefaultCase_WhenWarning()
    {
        var union = new TestingUnion { level = StatusLevel.Warning, DefaultMessage = "fallback" };
        var json = JsonSerializer.Serialize(union, DdsJsonOptions.Export);

        Assert.Contains("\"level\"", json);
        Assert.Contains("\"DefaultMessage\"", json);
        Assert.DoesNotContain("\"OkMessage\"", json);
        Assert.DoesNotContain("\"EightFloatsInline\"", json);
    }

    [Fact]
    public void DdsJsonOptions_Export_Union_DiscriminatorSerializedAsStringName()
    {
        // JsonStringEnumConverter (ME1-C05) + DdsUnionJsonConverterFactory (ME1-C08) combined:
        // the discriminator value must be its string name, not an integer.
        var union = new TestingUnion { level = StatusLevel.Ok };
        var json = JsonSerializer.Serialize(union, DdsJsonOptions.Export);
        Assert.Contains("\"Ok\"", json);
    }
}


/// <summary>Minimal topic with an enum field used for ME1-C05 JSON enum tests.</summary>
[DdsTopic("MockEnumTopic")]
public partial struct MockEnumTopic
{
    public int Id;
    public MockTopicStatus Status;
}

/// <summary>Helper enum for ME1-C05 tests.</summary>
public enum MockTopicStatus : byte
{
    Ok,
    Warning,
    Error
}
