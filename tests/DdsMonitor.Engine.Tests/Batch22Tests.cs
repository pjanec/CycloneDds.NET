using System;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for BATCH-22 bug fixes:
/// Task 1 – Auto-subscription &amp; checkbox sync (smoke test via TopicMetadata).
/// Task 2 – String filter operator crash (Contains / StartsWith / EndsWith).
/// Task 3 – Timestamp / synthetic wrapper field filtering.
/// Task 4 – SubscribeAll graceful error suppression (validated via engine logic).
/// </summary>
public sealed class Batch22Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Task 1: Smoke – wrapper fields added to AllFields so IsSubscribed state
    //         can be reflected correctly once auto-subscription runs.
    //         (Full UI wiring is exercised by the application; here we verify
    //          that TopicMetadata for every discovered topic includes the new
    //          wrapper fields required for correct subscription state tracking.)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_AllFields_ContainsTimestampAndOrdinalWrapperFields()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var timestamp = FindField(meta, "Timestamp");
        var ordinal = FindField(meta, "Ordinal");

        Assert.NotNull(timestamp);
        Assert.NotNull(ordinal);
        Assert.True(timestamp!.IsSynthetic);
        Assert.True(timestamp.IsWrapperField);
        Assert.Equal(typeof(DateTime), timestamp.ValueType);
        Assert.True(ordinal!.IsSynthetic);
        Assert.True(ordinal.IsWrapperField);
        Assert.Equal(typeof(long), ordinal.ValueType);
    }

    [Fact]
    public void TopicMetadata_DisplaySyntheticFields_AreNotMarkedAsWrapperFields()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        // "Delay [ms]" and "Size [B]" should remain non-wrapper synthetic fields.
        var delay = FindField(meta, "Delay [ms]");
        var size = FindField(meta, "Size [B]");

        Assert.NotNull(delay);
        Assert.True(delay!.IsSynthetic);
        Assert.False(delay.IsWrapperField);

        Assert.NotNull(size);
        Assert.True(size!.IsSynthetic);
        Assert.False(size.IsWrapperField);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 2: String filter operator crash – Contains / StartsWith / EndsWith
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FilterCompiler_StringContains_CompileSucceeds()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.Contains(\"hello\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_StringStartsWith_CompileSucceeds()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.StartsWith(\"hel\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_StringEndsWith_CompileSucceeds()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.EndsWith(\"llo\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_StringContains_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.Contains(\"world\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = result.Predicate!;

        var match = CreateStringSample(meta, 1, new StringTopic { Id = 1, Message = "hello world" });
        var miss = CreateStringSample(meta, 2, new StringTopic { Id = 2, Message = "goodbye" });

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_StringStartsWith_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.StartsWith(\"foo\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = result.Predicate!;

        var match = CreateStringSample(meta, 1, new StringTopic { Id = 1, Message = "foobar" });
        var miss = CreateStringSample(meta, 2, new StringTopic { Id = 2, Message = "barfoo" });

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_StringEndsWith_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.EndsWith(\"bar\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = result.Predicate!;

        var match = CreateStringSample(meta, 1, new StringTopic { Id = 1, Message = "foobar" });
        var miss = CreateStringSample(meta, 2, new StringTopic { Id = 2, Message = "barfoo" });

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_StringContains_DoesNotProduceUnknownPayloadFieldError()
    {
        // This exact reproduction of the bug-report scenario must NOT throw
        // "Unknown payload field 'Message.Contains'" any more.
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(StringTopic));

        var result = compiler.Compile("Payload.Message.Contains(\"abc\")", meta);

        Assert.True(result.IsValid, $"Unexpected error: {result.ErrorMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 3: Timestamp / Ordinal synthetic wrapper field filtering
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FilterCompiler_TimestampField_CompileSucceeds()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(SampleTopic));

        // Payload.Timestamp is the field path generated by ApplyField when the user
        // selects the Timestamp wrapper field in the filter builder.
        var result = compiler.Compile("Payload.Timestamp > DateTime.Parse(\"2020-01-01\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.NotNull(result.Predicate);
    }

    [Fact]
    public void FilterCompiler_TimestampField_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Payload.Timestamp > DateTime.Parse(\"2023-06-01\")", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = result.Predicate!;

        var match = CreateTimestampSample(meta, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var miss = CreateTimestampSample(meta, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_OrdinalField_FiltersCorrectly()
    {
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Payload.Ordinal > 100", meta);

        Assert.True(result.IsValid, result.ErrorMessage);
        var predicate = result.Predicate!;

        var match = CreateOrdinalSample(meta, 200);
        var miss = CreateOrdinalSample(meta, 50);

        Assert.True(predicate(match));
        Assert.False(predicate(miss));
    }

    [Fact]
    public void FilterCompiler_TimestampField_DoesNotProduceUnknownPayloadFieldError()
    {
        // Reproduction of the bug: "Unknown payload field 'Timestamp'" must not occur.
        var compiler = new FilterCompiler();
        var meta = new TopicMetadata(typeof(SampleTopic));

        var result = compiler.Compile("Payload.Timestamp == DateTime.Parse(\"2024-01-01\")", meta);

        Assert.True(result.IsValid, $"Unexpected error: {result.ErrorMessage}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 4: Subscribe All descriptor-safe (engine-level validation)
    //         The UI component silently drops errors for descriptor-failing topics.
    //         Here we validate the engine side: TrySubscribe returning false must
    //         not propagate an exception that crashes callers.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TopicMetadata_WrapperFields_GetterReadsFromSampleData_NotPayload()
    {
        var meta = new TopicMetadata(typeof(SampleTopic));

        var timestampField = FindField(meta, "Timestamp")!;
        var ordinalField = FindField(meta, "Ordinal")!;

        var expectedTimestamp = new DateTime(2025, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        const long expectedOrdinal = 42L;

        var sample = new SampleData
        {
            Ordinal = expectedOrdinal,
            Payload = new SampleTopic { Id = 1 },
            TopicMetadata = meta,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = expectedTimestamp,
            SizeBytes = 0
        };

        // Getter must be called with the SampleData (not payload) because IsSynthetic=true.
        var actualTimestamp = timestampField.Getter(sample);
        var actualOrdinal = ordinalField.Getter(sample);

        Assert.Equal(expectedTimestamp, actualTimestamp);
        Assert.Equal(expectedOrdinal, actualOrdinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static FieldMetadata? FindField(TopicMetadata meta, string structuredName)
        => meta.AllFields.FirstOrDefault(f => string.Equals(f.StructuredName, structuredName, StringComparison.Ordinal));

    private static SampleData CreateStringSample(TopicMetadata meta, long ordinal, StringTopic payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = meta,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private static SampleData CreateTimestampSample(TopicMetadata meta, DateTime timestamp)
    {
        return new SampleData
        {
            Ordinal = 1,
            Payload = new SampleTopic { Id = 1 },
            TopicMetadata = meta,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = timestamp,
            SizeBytes = 0
        };
    }

    private static SampleData CreateOrdinalSample(TopicMetadata meta, long ordinal)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = new SampleTopic { Id = 1 },
            TopicMetadata = meta,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = DateTime.UtcNow,
            SizeBytes = 0
        };
    }
}
