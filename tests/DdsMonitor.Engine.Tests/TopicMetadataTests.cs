using System;
using System.Linq;
using CycloneDDS.Schema;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public class TopicMetadataTests
{
    [Fact]
    public void TopicMetadata_FlattensNestedProperties()
    {
        var metadata = new TopicMetadata(typeof(OuterType));

        Assert.Contains(metadata.AllFields, field => field.StructuredName == "Id");
        Assert.Contains(metadata.AllFields, field => field.StructuredName == "Position.X");
        Assert.Contains(metadata.AllFields, field => field.StructuredName == "Position.Y");
    }

    [Fact]
    public void TopicMetadata_IdentifiesKeyFields()
    {
        var metadata = new TopicMetadata(typeof(KeyedType));

        var keyField = Assert.Single(metadata.KeyFields);
        Assert.Equal("Id", keyField.StructuredName);
    }

    [Fact]
    public void SyntheticFields_AppearInAllFields()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields[^2];
        var sizeField = metadata.AllFields[^1];

        Assert.Equal("Delay [ms]", delayField.StructuredName);
        Assert.True(delayField.IsSynthetic);
        Assert.Equal("Size [B]", sizeField.StructuredName);
        Assert.True(sizeField.IsSynthetic);
    }

    [Fact]
    public void SyntheticField_DelayGetter_ComputesCorrectly()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        var delayField = metadata.AllFields.Single(field => field.StructuredName == "Delay [ms]");

        var sourceTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var receiveTime = sourceTime.AddMilliseconds(123.4);

        // SampleInfo.SourceTimestamp is nanoseconds since Unix epoch (1970-01-01 UTC).
        var sourceTimestampNs = (sourceTime.Ticks - DateTime.UnixEpoch.Ticks) * 100L;

        var sample = new SampleData
        {
            Ordinal = 1,
            Payload = new OuterType(),
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = sourceTimestampNs },
            Timestamp = receiveTime,
            SizeBytes = 10
        };

        var delayMs = Assert.IsType<double>(delayField.Getter(sample));
        Assert.InRange(delayMs, 123.3, 123.5);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ME1-T03: Default topic name from namespace
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Success condition 2: TopicMetadata for a type with [DdsTopic] (no name) derives
    /// the name from the fully-qualified type name with dots replaced by underscores.
    /// </summary>
    [Fact]
    public void TopicMetadata_DefaultTopicName_DerivedFromFullName()
    {
        // DefaultNameTopic is in namespace DdsMonitor.Engine.Tests,
        // so TopicName must be "DdsMonitor_Engine_Tests_DefaultNameTopic".
        var metadata = new TopicMetadata(typeof(DefaultNameTopic));

        Assert.Equal("DdsMonitor_Engine_Tests_DefaultNameTopic", metadata.TopicName);
    }

    /// <summary>
    /// Success condition 3: TopicMetadata for a type with [DdsTopic("ExplicitNameTopic")]
    /// keeps the explicit name.
    /// </summary>
    [Fact]
    public void TopicMetadata_ExplicitTopicName_Preserved()
    {
        var metadata = new TopicMetadata(typeof(ExplicitNamedTopic));
        Assert.Equal("ExplicitNameTopic", metadata.TopicName);
    }

    /// <summary>
    /// Backward compatibility: existing types with explicit names continue to work.
    /// </summary>
    [Fact]
    public void TopicMetadata_OldExplicitName_StillWorks()
    {
        var metadata = new TopicMetadata(typeof(OuterType));
        Assert.Equal("TestTopic", metadata.TopicName);
    }
}
