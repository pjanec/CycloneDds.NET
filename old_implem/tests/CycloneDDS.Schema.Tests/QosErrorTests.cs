using System;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests;

public class QosErrorTests
{
    [Fact]
    public void DdsReliability_ValuesConnect()
    {
        Assert.Equal(0, (int)DdsReliability.BestEffort);
        Assert.Equal(1, (int)DdsReliability.Reliable);
    }
    
    [Fact]
    public void DdsDurability_ValuesCorrect()
    {
        Assert.Equal(0, (int)DdsDurability.Volatile);
        Assert.Equal(3, (int)DdsDurability.Persistent);
    }

    [Fact]
    public void DdsReturnCode_ValuesCorrect()
    {
        Assert.Equal(0, (int)DdsReturnCode.Ok);
        Assert.Equal(-1, (int)DdsReturnCode.Error);
        Assert.Equal(-11, (int)DdsReturnCode.NoData);
    }

    [Fact]
    public void DdsException_StoresCodeAndMessage()
    {
        var ex = new DdsException(DdsReturnCode.Timeout, "Operation timed out");
        
        Assert.Equal(DdsReturnCode.Timeout, ex.ErrorCode);
        Assert.Contains("DDS Error Timeout: Operation timed out", ex.Message);
    }

    [Fact]
    public void DdsSampleInfo_HasStubFields()
    {
        var info = new DdsSampleInfo { ValidData = true };
        Assert.True(info.ValidData);
    }
}
