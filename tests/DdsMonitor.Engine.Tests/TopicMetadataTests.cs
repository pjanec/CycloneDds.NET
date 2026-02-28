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

        var sample = new SampleData
        {
            Ordinal = 1,
            Payload = new OuterType(),
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = sourceTime.Ticks },
            Timestamp = receiveTime,
            SizeBytes = 10
        };

        var delayMs = Assert.IsType<double>(delayField.Getter(sample));
        Assert.InRange(delayMs, 123.3, 123.5);
    }

}
