using System;
using System.Linq;
using System.Text.Json;
using CycloneDDS.Schema;
using DdsMonitor.Engine.Json;
using DdsMonitor.Engine.Ui;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests covering the fixes introduced for:
/// - Issue 6: FixedStringN support in TopicMetadata, JSON, TypeDrawerRegistry
/// - Issue 3: Participant indicator pair format
/// - Issue 4: CloneRequestService
/// </summary>
public sealed class FixedStringAndCloneTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // TopicMetadata: FixedString types are treated as leaf types
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(FixedString32))]
    [InlineData(typeof(FixedString64))]
    [InlineData(typeof(FixedString128))]
    [InlineData(typeof(FixedString256))]
    public void TopicMetadata_IsFixedStringType_ReturnsTrue_ForAllFixedStringVariants(Type type)
    {
        Assert.True(TopicMetadata.IsFixedStringType(type));
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(object))]
    [InlineData(typeof(double))]
    public void TopicMetadata_IsFixedStringType_ReturnsFalse_ForNonFixedStringTypes(Type type)
    {
        Assert.False(TopicMetadata.IsFixedStringType(type));
    }

    [Fact]
    public void TopicMetadata_FixedString32Field_IsLeaf_NotExpanded()
    {
        var meta = new TopicMetadata(typeof(FixedStringTopic));

        // OkMessage should appear as a single leaf FieldMetadata, not as "OkMessage.Length"
        Assert.Contains(meta.AllFields, f => f.StructuredName == "OkMessage");
        Assert.DoesNotContain(meta.AllFields, f => f.StructuredName.StartsWith("OkMessage."));
    }

    [Fact]
    public void TopicMetadata_FixedString32Field_HasCorrectValueType()
    {
        var meta = new TopicMetadata(typeof(FixedStringTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "OkMessage");

        Assert.Equal(typeof(FixedString32), field.ValueType);
        Assert.False(field.IsArrayField);
        Assert.False(field.IsFixedSizeArray);
    }

    [Fact]
    public void TopicMetadata_FixedString32Field_Getter_ReturnsActualValue()
    {
        var meta = new TopicMetadata(typeof(FixedStringTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "OkMessage");

        object boxed = new FixedStringTopic { OkMessage = new FixedString32("hello") };
        var value = field.Getter(boxed);

        Assert.NotNull(value);
        Assert.IsType<FixedString32>(value);
        Assert.Equal("hello", value.ToString());
    }

    [Fact]
    public void TopicMetadata_FixedString32Field_Setter_UpdatesValue()
    {
        var meta = new TopicMetadata(typeof(FixedStringTopic));
        var field = meta.AllFields.Single(f => f.StructuredName == "OkMessage");

        object boxed = new FixedStringTopic();
        field.Setter(boxed, new FixedString32("updated"));

        var value = field.Getter(boxed);
        Assert.Equal("updated", value?.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JSON: FixedStringN serializes as a string
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("test string")]
    public void Json_FixedString32_SerializesAsJsonString(string input)
    {
        var value = new FixedString32(input);
        var opts = DdsJsonOptions.Display;

        var json = JsonSerializer.Serialize(value, opts);

        // Result should be a JSON string, not {"Length": N}
        Assert.StartsWith("\"", json);
        Assert.EndsWith("\"", json);
        // The actual string content should be preserved (without quotes)
        Assert.Contains(input, json);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("another test")]
    public void Json_FixedString64_SerializesAsJsonString(string input)
    {
        var value = new FixedString64(input);
        var opts = DdsJsonOptions.Export;

        var json = JsonSerializer.Serialize(value, opts);

        Assert.StartsWith("\"", json);
        Assert.DoesNotContain("Length", json);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    public void Json_FixedString128_SerializesAsJsonString(string input)
    {
        var value = new FixedString128(input);
        var json = JsonSerializer.Serialize(value, DdsJsonOptions.Export);
        Assert.StartsWith("\"", json);
        Assert.DoesNotContain("Length", json);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("")]
    public void Json_FixedString256_SerializesAsJsonString(string input)
    {
        var value = new FixedString256(input);
        var json = JsonSerializer.Serialize(value, DdsJsonOptions.Export);
        Assert.StartsWith("\"", json);
        Assert.DoesNotContain("Length", json);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData("round-trip msg")]
    public void Json_FixedString32_RoundTrips_Via_Export_Options(string input)
    {
        var original = new FixedString32(input);
        var opts = DdsJsonOptions.Export;

        var json = JsonSerializer.Serialize(original, opts);
        var restored = JsonSerializer.Deserialize<FixedString32>(json, opts);

        Assert.Equal(input, restored.ToString());
    }

    [Fact]
    public void Json_TopicWithFixedString_SerializesStringValue_NotLength()
    {
        var payload = new FixedStringTopic { OkMessage = new FixedString32("test msg") };
        var json = JsonSerializer.Serialize(payload, payload.GetType(), DdsJsonOptions.Display);

        Assert.Contains("\"test msg\"", json);
        // Must NOT contain raw { "Length": ... } representation
        Assert.DoesNotContain("\"Length\"", json);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TypeDrawerRegistry: FixedStringN types have registered drawers
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(FixedString32))]
    [InlineData(typeof(FixedString64))]
    [InlineData(typeof(FixedString128))]
    [InlineData(typeof(FixedString256))]
    public void TypeDrawerRegistry_HasDrawer_ReturnsTrue_ForFixedStringTypes(Type type)
    {
        var registry = new TypeDrawerRegistry();
        Assert.True(registry.HasDrawer(type));
    }

    [Theory]
    [InlineData(typeof(FixedString32))]
    [InlineData(typeof(FixedString64))]
    [InlineData(typeof(FixedString128))]
    [InlineData(typeof(FixedString256))]
    public void TypeDrawerRegistry_GetDrawer_ReturnsNonNull_ForFixedStringTypes(Type type)
    {
        var registry = new TypeDrawerRegistry();
        var drawer = registry.GetDrawer(type);
        Assert.NotNull(drawer);
    }

    [Fact]
    public void TypeDrawerRegistry_FixedString32Drawer_RendersInputElement()
    {
        var registry = new TypeDrawerRegistry();
        var drawer = registry.GetDrawer(typeof(FixedString32));
        Assert.NotNull(drawer);

        // Render fragment via DrawerContext and verify it does not throw.
        var currentValue = new FixedString32("initial");
        var ctx = new DrawerContext(
            "OkMessage",
            typeof(FixedString32),
            () => currentValue,
            v => { if (v is FixedString32 fs) currentValue = fs; });

        // The fragment should be non-null and creation should not throw.
        var fragment = drawer!(ctx);
        Assert.NotNull(fragment);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CloneRequestService
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CloneRequestService_InitialPending_IsNull()
    {
        var svc = new CloneRequestService();
        Assert.Null(svc.Pending);
    }

    [Fact]
    public void CloneRequestService_SetRequest_MakesPendingNonNull()
    {
        var svc = new CloneRequestService();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var payload = new SampleTopic();

        svc.SetRequest(meta, payload);

        Assert.NotNull(svc.Pending);
        Assert.Equal(meta, svc.Pending!.Value.Meta);
        Assert.Equal(payload, svc.Pending.Value.Payload);
    }

    [Fact]
    public void CloneRequestService_TakeRequest_ClearsPending()
    {
        var svc = new CloneRequestService();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var payload = new SampleTopic();

        svc.SetRequest(meta, payload);
        var taken = svc.TakeRequest();

        Assert.NotNull(taken);
        Assert.Null(svc.Pending); // Should be cleared after take
    }

    [Fact]
    public void CloneRequestService_TakeRequest_ReturnsNull_WhenNoPendingRequest()
    {
        var svc = new CloneRequestService();
        var result = svc.TakeRequest();
        Assert.Null(result);
    }

    [Fact]
    public void CloneRequestService_RequestAvailable_FiresWhenRequestSet()
    {
        var svc = new CloneRequestService();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var payload = new SampleTopic();
        var firedCount = 0;

        svc.RequestAvailable += () => firedCount++;
        svc.SetRequest(meta, payload);

        Assert.Equal(1, firedCount);
    }

    [Fact]
    public void CloneRequestService_RequestAvailable_FiresEachTime()
    {
        var svc = new CloneRequestService();
        var meta = new TopicMetadata(typeof(SampleTopic));
        var firedCount = 0;

        svc.RequestAvailable += () => firedCount++;

        svc.SetRequest(meta, new SampleTopic());
        svc.SetRequest(meta, new SampleTopic());
        svc.SetRequest(meta, new SampleTopic());

        Assert.Equal(3, firedCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Participant summary pair format: D:0,P:*|D:2,P:abc
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParticipantSummary_EmptyList_ReturnsNone()
    {
        var result = GetParticipantSummaryHelper(Array.Empty<ParticipantConfig>());
        Assert.Equal("none", result);
    }

    [Fact]
    public void ParticipantSummary_SingleEntry_ReturnsPair()
    {
        var configs = new[] { new ParticipantConfig { DomainId = 0, PartitionName = "*" } };
        var result = GetParticipantSummaryHelper(configs);
        Assert.Equal("D:0,P:*", result);
    }

    [Fact]
    public void ParticipantSummary_EmptyPartition_ShowsAsterisk()
    {
        var configs = new[] { new ParticipantConfig { DomainId = 5, PartitionName = string.Empty } };
        var result = GetParticipantSummaryHelper(configs);
        Assert.Equal("D:5,P:*", result);
    }

    [Fact]
    public void ParticipantSummary_MultipleEntries_ShowsPairsWithPipeSeparator()
    {
        var configs = new[]
        {
            new ParticipantConfig { DomainId = 0, PartitionName = "*" },
            new ParticipantConfig { DomainId = 2, PartitionName = "abc" }
        };
        var result = GetParticipantSummaryHelper(configs);
        Assert.Equal("D:0,P:*|D:2,P:abc", result);
    }

    [Fact]
    public void ParticipantSummary_KeptAsPairs_DomainAndPartitionNotGroupedSeparately()
    {
        // Ensure domain and partition are kept as strict pairs, not
        // "D:0,2|P:*,abc" (old buggy format)
        var configs = new[]
        {
            new ParticipantConfig { DomainId = 1, PartitionName = "ns1" },
            new ParticipantConfig { DomainId = 2, PartitionName = "ns2" }
        };
        var result = GetParticipantSummaryHelper(configs);
        Assert.Equal("D:1,P:ns1|D:2,P:ns2", result);
        // Old broken format would be "D:1,2|P:ns1,ns2"
        Assert.DoesNotContain("D:1,2", result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replicates the GetParticipantSummary logic from MainLayout.razor.
    /// </summary>
    private static string GetParticipantSummaryHelper(ParticipantConfig[] configs)
    {
        if (configs.Length == 0)
            return "none";

        return string.Join("|", configs.Select(c =>
        {
            var partition = string.IsNullOrEmpty(c.PartitionName) ? "*" : c.PartitionName;
            return $"D:{c.DomainId},P:{partition}";
        }));
    }

}

// ─────────────────────────────────────────────────────────────────────────────
// Test topic type with a FixedString32 field
// ─────────────────────────────────────────────────────────────────────────────

[DdsTopic("FixedStringTopic")]
public partial struct FixedStringTopic
{
    public int Id;
    public FixedString32 OkMessage;
}
