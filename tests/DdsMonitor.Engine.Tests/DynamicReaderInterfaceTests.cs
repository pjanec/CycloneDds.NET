using System;
using CycloneDDS.Runtime.Interop;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DynamicReaderInterfaceTests
{
    [Fact]
    public void MockDynamicReader_FiresOnSampleReceived()
    {
        var metadata = new TopicMetadata(typeof(MockTopic));
        var reader = new MockDynamicReader(metadata);

        SampleData? received = null;
        reader.OnSampleReceived += sample => received = sample;

        var sampleData = new SampleData
        {
            Ordinal = 1,
            Payload = new MockTopic { Id = 7 },
            TopicMetadata = metadata,
            SampleInfo = new DdsApi.DdsSampleInfo { SourceTimestamp = 0 },
            Timestamp = DateTime.UtcNow,
            SizeBytes = 0
        };

        reader.Emit(sampleData);

        Assert.NotNull(received);
        Assert.Equal(7, ((MockTopic)received!.Payload).Id);
    }

    private sealed class MockDynamicReader : IDynamicReader
    {
        public MockDynamicReader(TopicMetadata metadata)
        {
            TopicMetadata = metadata;
        }

        public Type TopicType => TopicMetadata.TopicType;

        public TopicMetadata TopicMetadata { get; }

        public event Action<SampleData>? OnSampleReceived;

        public void Start(string? partition)
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }

        public void Emit(SampleData sample)
        {
            OnSampleReceived?.Invoke(sample);
        }
    }

}
