using System;
using System.Reflection;
using Xunit;
using CycloneDDS.Schema;

[assembly: DdsTypeMap(typeof(Guid), DdsWire.Guid16)]
[assembly: DdsTypeMap(typeof(DateTime), DdsWire.Int64TicksUtc)]

namespace CycloneDDS.Schema.Tests;

public class TypeMapTests
{
    [Fact]
    public void CanRetrieveAssemblyAttributes()
    {
        var asm = Assembly.GetExecutingAssembly();
        var attrs = asm.GetCustomAttributes<DdsTypeMapAttribute>();
        
        Assert.Contains(attrs, a => a.SourceType == typeof(Guid) && a.WireKind == DdsWire.Guid16);
        Assert.Contains(attrs, a => a.SourceType == typeof(DateTime) && a.WireKind == DdsWire.Int64TicksUtc);
    }

    [Fact]
    public void DdsTypeMap_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DdsTypeMapAttribute(null!, DdsWire.Guid16));
    }

    [Fact]
    public void DdsWire_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(DdsWire), DdsWire.Guid16));
        Assert.True(Enum.IsDefined(typeof(DdsWire), DdsWire.FixedUtf8Bytes32));
        Assert.True(Enum.IsDefined(typeof(DdsWire), DdsWire.FixedUtf8Bytes128));
    }
}
