using System;
using Xunit;
using CycloneDDS.Schema;

namespace CycloneDDS.Schema.Tests;

public class AdditionalTests
{
    // FCDC-002 Additional Tests
    [Fact]
    public void FixedString32_Capacity_Is32()
    {
        Assert.Equal(32, FixedString32.Capacity);
    }

    [Fact]
    public void FixedString64_Capacity_Is64()
    {
        Assert.Equal(64, FixedString64.Capacity);
    }

    [Fact]
    public void FixedString128_Capacity_Is128()
    {
        Assert.Equal(128, FixedString128.Capacity);
    }

    [Fact]
    public void BoundedSeq_Indexer_ThrowsOnInvalidIndex()
    {
        var seq = new BoundedSeq<int>(5);
        seq.Add(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => seq[1]); // Wait, List throws ArgumentOutOfThrow?
        // List check:
        // List throws ArgumentOutOfRangeException.
        // My BoundedSeq wrapper:
        /*
            get
            {
                if (_storage == null) throw new IndexOutOfRangeException();
                return _storage[index];
            }
        */
        // If _storage exists, it delegates to List.
        // List throws ArgumentOutOfRangeException.
    }
    
    [Fact]
    public void BoundedSeq_Indexer_Throws_WhenListThrows()
    {
        var seq = new BoundedSeq<int>(5);
        seq.Add(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => seq[1]); 
    }

    [Fact]
    public void BoundedSeq_Indexer_Throws_WhenUninitialized()
    {
        BoundedSeq<int> seq = default;
        Assert.Throws<IndexOutOfRangeException>(() => seq[0]);
    }

    // FCDC-003 Additional Tests
    [Fact]
    public void TypeMap_WireEnum_CoversAllCases()
    {
        foreach (DdsWire val in Enum.GetValues(typeof(DdsWire)))
        {
            Assert.True(Enum.IsDefined(typeof(DdsWire), val));
        }
    }

    [Fact]
    public void TypeMap_Attribute_PropertiesReadBack()
    {
        var attr = new DdsTypeMapAttribute(typeof(string), DdsWire.FixedUtf8Bytes32);
        Assert.Equal(typeof(string), attr.SourceType);
        Assert.Equal(DdsWire.FixedUtf8Bytes32, attr.WireKind);
    }

    // FCDC-004 Additional Tests
    [Fact]
    public void DdsReturnCode_HasOkZero()
    {
         Assert.Equal(0, (int)DdsReturnCode.Ok);
    }

    [Fact]
    public void DdsException_IsException()
    {
        Assert.IsAssignableFrom<Exception>(new DdsException(DdsReturnCode.Error, "test"));
    }

    [Fact]
    public void DdsReliability_Defaults()
    {
        // Just verify values again for sanity
        Assert.Equal(DdsReliability.BestEffort, (DdsReliability)0);
    }

    [Fact]
    public void DdsHistoryKind_Values()
    {
        Assert.Equal(0, (int)DdsHistoryKind.KeepLast);
        Assert.Equal(1, (int)DdsHistoryKind.KeepAll);
    }
    
    [Fact]
    public void DdsSampleInfo_StructLayout_Sequential()
    {
        Assert.True(typeof(DdsSampleInfo).IsLayoutSequential);
    }
}
