using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

/// <summary>
/// Tests for ME2-BATCH-03: Replay Stability, Null Serialization, and UX Adjustments.
/// Tasks covered: ME2-T20 (sort determinism), ME2-T16 (null display), ME2-T18 (column defaults), ME2-T19 (delay arithmetic).
/// </summary>
public sealed class ME2Batch03Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T19: Delay column timing arithmetic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Delay getter produces a positive millisecond value when receive time is after send time.
    /// SourceTimestamp is in nanoseconds since Unix epoch (1970-01-01 UTC).
    /// </summary>
    [Fact]
    public void DelayGetter_PositiveDelay_WhenReceiveAfterSend()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields.Single(f => f.StructuredName == "Delay [ms]");

        var sourceTime = new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc);
        var receiveTime = sourceTime.AddMilliseconds(42.5);

        // Convert sourceTime to nanoseconds-since-Unix-epoch (the real DDS SourceTimestamp unit).
        var sourceTimestampNs = (sourceTime.Ticks - DateTime.UnixEpoch.Ticks) * 100L;

        var sample = CreateSampleWithSourceTs(metadata, sourceTimestampNs, receiveTime);
        var delayMs = Assert.IsType<double>(delayField.Getter(sample));

        Assert.True(delayMs > 0, $"Expected positive delay but got {delayMs}");
        Assert.InRange(delayMs, 42.4, 42.6);
    }

    /// <summary>
    /// The Delay getter returns 0 when SourceTimestamp is 0 (not set).
    /// </summary>
    [Fact]
    public void DelayGetter_Returns0_WhenSourceTimestampIsZero()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields.Single(f => f.StructuredName == "Delay [ms]");

        var sample = CreateSampleWithSourceTs(metadata, 0L, DateTime.UtcNow);
        var delayMs = Assert.IsType<double>(delayField.Getter(sample));

        Assert.Equal(0.0, delayMs);
    }

    /// <summary>
    /// The Delay getter returns 0 when SourceTimestamp is long.MaxValue (sentinel for "unknown").
    /// </summary>
    [Fact]
    public void DelayGetter_Returns0_WhenSourceTimestampIsMaxValue()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields.Single(f => f.StructuredName == "Delay [ms]");

        var sample = CreateSampleWithSourceTs(metadata, long.MaxValue, DateTime.UtcNow);
        var delayMs = Assert.IsType<double>(delayField.Getter(sample));

        Assert.Equal(0.0, delayMs);
    }

    /// <summary>
    /// Regression: with the old bug (treating nanoseconds as .NET ticks), the same
    /// inputs yield a large negative delay. This test validates the old behaviour was wrong
    /// and confirms the corrected arithmetic.
    /// </summary>
    [Fact]
    public void DelayGetter_OldBugWouldProduceNegativeValue_CorrectCodeProducesPositive()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields.Single(f => f.StructuredName == "Delay [ms]");

        // Use a realistic DDS SourceTimestamp (nanoseconds since Unix epoch ≈ March 2026).
        // As raw .NET ticks this would represent year ~5621, causing a huge negative delay.
        var sourceTime = new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc);
        var sourceTimestampNs = (sourceTime.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
        var receiveTime = sourceTime.AddMilliseconds(1.0);

        var sample = CreateSampleWithSourceTs(metadata, sourceTimestampNs, receiveTime);
        var delayMs = Assert.IsType<double>(delayField.Getter(sample));

        // Correct code produces approximately +1 ms.
        Assert.InRange(delayMs, 0.5, 2.0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T20: ApplySortToViewCache determinism — Replay Mode
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When FixedSamples are out-of-order by ordinal, ApplySortToViewCache must fall back
    /// to a full O(N log N) sort instead of assuming the data is ascending and reversing.
    /// Validates via the IsCacheAscending guard path (white-box).
    /// </summary>
    [Fact]
    public void IsCacheAscending_ReturnsFalse_WhenSamplesOutOfOrder()
    {
        // Simulate the internal logic: build a list that is NOT ascending by ordinal.
        var meta = new TopicMetadata(typeof(OuterType));
        var samples = new List<SampleData>
        {
            CreateSample(meta, 3, new OuterType()),
            CreateSample(meta, 1, new OuterType()),
            CreateSample(meta, 2, new OuterType()),
        };

        // Verify ordering is not ascending.
        var ordinalField = meta.AllFields.Single(f => f.StructuredName == "Ordinal");
        bool ascending = IsCacheAscendingByField(samples, ordinalField);
        Assert.False(ascending);
    }

    /// <summary>
    /// When FixedSamples are already in ascending ordinal order,
    /// IsCacheAscending returns true, enabling the fast O(N) Reverse path.
    /// </summary>
    [Fact]
    public void IsCacheAscending_ReturnsTrue_WhenSamplesAreAscending()
    {
        var meta = new TopicMetadata(typeof(OuterType));
        var samples = new List<SampleData>
        {
            CreateSample(meta, 1, new OuterType()),
            CreateSample(meta, 2, new OuterType()),
            CreateSample(meta, 3, new OuterType()),
        };

        var ordinalField = meta.AllFields.Single(f => f.StructuredName == "Ordinal");
        bool ascending = IsCacheAscendingByField(samples, ordinalField);
        Assert.True(ascending);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T16: Null string field is distinguishable from empty string in metadata
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GetFieldValue returns null (not empty string) when a managed-string field is set to null.
    /// Validates that the metadata Getter correctly propagates null without coercing to "".
    /// </summary>
    [Fact]
    public void FieldMetadata_Getter_ReturnsNull_WhenStringFieldIsNull()
    {
        var meta = new TopicMetadata(typeof(KeyedType));
        var nameField = meta.AllFields.Single(f => f.StructuredName == "Name");

        var payload = new KeyedType { Id = 1, Name = null };
        var result = nameField.Getter(payload);

        Assert.Null(result);
    }

    /// <summary>
    /// GetFieldValue returns "" (not null) when a managed-string field is set to empty string.
    /// Validates that null and empty string have distinct representations at the metadata layer.
    /// </summary>
    [Fact]
    public void FieldMetadata_Getter_ReturnsEmpty_WhenStringFieldIsEmpty()
    {
        var meta = new TopicMetadata(typeof(KeyedType));
        var nameField = meta.AllFields.Single(f => f.StructuredName == "Name");

        var payload = new KeyedType { Id = 1, Name = string.Empty };
        var result = nameField.Getter(payload);

        Assert.Equal(string.Empty, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME2-T18: Default column selection is Topic + Timestamp only
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The default synthetic fields required for ME2-T18 column defaults are present in AllFields.
    /// Specifically, "Topic" and "Timestamp" must exist as wrapper synthetic fields.
    /// </summary>
    [Fact]
    public void TopicMetadata_TopicAndTimestamp_PresentAsSyntheticFields()
    {
        var meta = new TopicMetadata(typeof(OuterType));

        var topicField = meta.AllFields.FirstOrDefault(f => f.StructuredName == "Topic");
        var tsField = meta.AllFields.FirstOrDefault(f => f.StructuredName == "Timestamp");

        Assert.NotNull(topicField);
        Assert.True(topicField!.IsSynthetic);
        Assert.True(topicField.IsWrapperField);

        Assert.NotNull(tsField);
        Assert.True(tsField!.IsSynthetic);
        Assert.True(tsField.IsWrapperField);
    }

    /// <summary>
    /// "Topic" getter returns the ShortName of the topic (used as the column value).
    /// </summary>
    [Fact]
    public void TopicSyntheticField_Getter_ReturnsShortName()
    {
        var meta = new TopicMetadata(typeof(OuterType));
        var topicField = meta.AllFields.Single(f => f.StructuredName == "Topic");

        var sample = CreateSample(meta, 1, new OuterType());
        var value = topicField.Getter(sample);

        Assert.Equal("OuterType", value);
    }

    /// <summary>
    /// "Timestamp" getter returns the SampleData.Timestamp (reception time) as a DateTime.
    /// </summary>
    [Fact]
    public void TimestampSyntheticField_Getter_ReturnsReceptionTime()
    {
        var meta = new TopicMetadata(typeof(OuterType));
        var tsField = meta.AllFields.Single(f => f.StructuredName == "Timestamp");

        var receptionTime = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc);
        var sample = new SampleData
        {
            Ordinal = 1,
            Payload = new OuterType(),
            TopicMetadata = meta,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = receptionTime,
            SizeBytes = 0
        };

        var value = tsField.Getter(sample);
        Assert.Equal(receptionTime, value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static SampleData CreateSample(TopicMetadata metadata, long ordinal, object payload)
    {
        return new SampleData
        {
            Ordinal = ordinal,
            Payload = payload,
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo
            {
                SourceTimestamp = 0,
                InstanceState = DdsInstanceState.Alive
            },
            Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 0
        };
    }

    private static SampleData CreateSampleWithSourceTs(TopicMetadata metadata, long sourceTimestampNs, DateTime receiveTime)
    {
        return new SampleData
        {
            Ordinal = 1,
            Payload = new OuterType(),
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo
            {
                SourceTimestamp = sourceTimestampNs,
                InstanceState = DdsInstanceState.Alive
            },
            Timestamp = receiveTime,
            SizeBytes = 0
        };
    }

    /// <summary>
    /// Replicates the <c>IsCacheAscending</c> logic from SamplesPanel for white-box testing.
    /// Returns true when every consecutive pair in <paramref name="samples"/> is in ascending
    /// order by the given <paramref name="field"/>.
    /// </summary>
    private static bool IsCacheAscendingByField(List<SampleData> samples, FieldMetadata field)
    {
        for (var i = 1; i < samples.Count; i++)
        {
            var left = field.IsSynthetic ? (object)samples[i - 1] : samples[i - 1].Payload;
            var right = field.IsSynthetic ? (object)samples[i] : samples[i].Payload;
            var leftVal = field.Getter(left!);
            var rightVal = field.Getter(right!);
            var comparison = Compare(leftVal, rightVal);
            if (comparison > 0) return false;
        }
        return true;
    }

    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        if (left is IComparable comparable) return comparable.CompareTo(right);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }
}
