using System;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Tests.Fixtures;
using CycloneDDS.Schema.TestData;
using Xunit;

namespace CycloneDDS.Runtime.Tests.IntegrationTests;

[Collection("DDS Integration")]
public class DataTypeTests : IClassFixture<DdsIntegrationFixture>
{
    private readonly DdsIntegrationFixture _fixture;

    public DataTypeTests(DdsIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test_SimplePrimitives_Registration()
    {
        var participant = _fixture.Participant;
        // Verify we can create a writer for simple types using the Native struct
        using var writer = new DdsWriter<SimpleMessageNative>(participant);
        Assert.NotNull(writer);
    }
    
    [Fact]
    public void Test_AllPrimitives_Registration()
    {
        var participant = _fixture.Participant;
        using var writer = new DdsWriter<AllPrimitivesMessageNative>(participant);
        Assert.NotNull(writer);
    }

    [Fact]
    public void Test_ArrayMessage_Registration()
    {
        var participant = _fixture.Participant;
        using var writer = new DdsWriter<ArrayMessageNative>(participant);
        Assert.NotNull(writer);
    }
}
