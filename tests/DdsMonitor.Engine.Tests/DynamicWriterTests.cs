using CycloneDDS.Runtime;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class DynamicWriterTests
{
    [Fact]
    public void DynamicWriter_Write_DoesNotThrow()
    {
        var metadata = new TopicMetadata(typeof(DynamicWriterMessage));
        using var participant = new DdsParticipant();
        using var writer = new DynamicWriter<DynamicWriterMessage>(participant, metadata);

        var ex = Record.Exception(() => writer.Write(new DynamicWriterMessage { Id = 1, Value = 2 }));

        Assert.Null(ex);
    }

    [Fact]
    public void DynamicWriter_DisposeInstance_DoesNotThrow()
    {
        var metadata = new TopicMetadata(typeof(DynamicWriterKeyedMessage));
        using var participant = new DdsParticipant();
        using var writer = new DynamicWriter<DynamicWriterKeyedMessage>(participant, metadata);

        var ex = Record.Exception(() => writer.DisposeInstance(new DynamicWriterKeyedMessage { Id = 5, Value = 9 }));

        Assert.Null(ex);
    }
}
