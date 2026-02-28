using System;
using CycloneDDS.Schema;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public class SampleDataTests
{
    [Fact]
    public void SampleData_WithInitSyntax_SetsAllProperties()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var original = new SampleData
        {
            Ordinal = 1,
            Payload = "initial",
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Sender = new SenderIdentity { ProcessId = 1, MachineName = "a", IpAddress = "1.1.1.1" },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 5
        };

        var updated = original with
        {
            Ordinal = 2,
            Payload = "updated",
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 10 },
            Sender = new SenderIdentity { ProcessId = 2, MachineName = "b", IpAddress = "2.2.2.2" },
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc),
            SizeBytes = 8
        };

        Assert.Equal(2, updated.Ordinal);
        Assert.Equal("updated", updated.Payload);
        Assert.Same(metadata, updated.TopicMetadata);
        Assert.Equal(10, updated.SampleInfo.SourceTimestamp);
        Assert.Equal(2u, updated.Sender?.ProcessId);
        Assert.Equal("b", updated.Sender?.MachineName);
        Assert.Equal("2.2.2.2", updated.Sender?.IpAddress);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc), updated.Timestamp);
        Assert.Equal(8, updated.SizeBytes);
    }

    [Fact]
    public void SampleData_RecordEquality_WorksByValue()
    {
        var metadata = new TopicMetadata(typeof(SampleTopic));
        var sender = new SenderIdentity { ProcessId = 3, MachineName = "host", IpAddress = "10.0.0.1" };
        var info = new DdsApi.DdsSampleInfo { SourceTimestamp = 100 };

        var left = new SampleData
        {
            Ordinal = 3,
            Payload = "payload",
            TopicMetadata = metadata,
            SampleInfo = info,
            Sender = sender,
            Timestamp = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 20
        };

        var right = new SampleData
        {
            Ordinal = 3,
            Payload = "payload",
            TopicMetadata = metadata,
            SampleInfo = info,
            Sender = sender,
            Timestamp = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            SizeBytes = 20
        };

        Assert.Equal(left, right);
    }

#pragma warning disable CS0649
    [DdsTopic("Sample")]
    private struct SampleTopic
    {
        public int Id;
    }
#pragma warning restore CS0649
}
