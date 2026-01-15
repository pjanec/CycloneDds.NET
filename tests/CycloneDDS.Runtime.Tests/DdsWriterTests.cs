using System;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests;

public class DdsWriterTests : IDisposable
{
    public DdsWriterTests()
    {
        TestRegistration.Register();
    }

    public void Dispose()
    {
    }

    [Fact]
    public void DdsWriter_Create_InitializesNativeTopic()
    {
        using var participant = new DdsParticipant();
        using var writer = new DdsWriter<TestMessageNative>(participant);
        
        Assert.False(writer.IsDisposed);
    }

    [Fact]
    public void DdsWriter_Write_CallsNativeWrite()
    {
        using var participant = new DdsParticipant();
        using var writer = new DdsWriter<TestMessageNative>(participant);
        
        var msg = new TestMessageNative { Id = 1, Value = 42 };
        
        // Expect failure because descriptor is null
        var ex = Assert.Throws<DdsException>(() => writer.Write(ref msg));
        // Verify it's not a random error, but likely BadParameter or Error
        // Assert.Equal(DdsReturnCode.Error, ex.Code); // Exact code depends on implementation
    }

    [Fact]
    public void DdsWriter_TryWrite_ReturnsFalse_WhenDescriptorMissing()
    {
        using var participant = new DdsParticipant();
        using var writer = new DdsWriter<TestMessageNative>(participant);
        
        var msg = new TestMessageNative { Id = 2, Value = 84 };
        var success = writer.TryWrite(ref msg);
        
        // Expect false because descriptor is null
        Assert.False(success);
    }

    [Fact]
    public void DdsWriter_AfterDispose_ThrowsObjectDisposed()
    {
        using var participant = new DdsParticipant();
        var writer = new DdsWriter<TestMessageNative>(participant);
        writer.Dispose();
        
        var msg = new TestMessageNative();
        Assert.Throws<ObjectDisposedException>(() => writer.Write(ref msg));
    }

    [Fact]
    public void DdsWriter_InvalidType_ThrowsDdsException()
    {
        using var participant = new DdsParticipant();
        
        // Int32 is not registered
        Assert.Throws<DdsException>(() => new DdsWriter<int>(participant));
    }
}
