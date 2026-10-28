using System.Linq;
using System.Text.Json;
using CycloneDDS.Schema;
using DdsMonitor.Engine;
using DdsMonitor.Engine.Json;
using Xunit;

namespace DdsMonitor.Engine.Tests;

// case 1: case 2: long A;  default: short C;
[DdsUnion]
public partial struct MonitorMultiLabelUnion
{
    [DdsDiscriminator] public int Kind;
    [DdsCase(1, 2)] public int A;
    [DdsDefaultCase] public short C;
}

[DdsTopic("MonitorMultiLabelTopic")]
public partial struct MonitorMultiLabelTopic
{
    [DdsKey] public int Id;
    public MonitorMultiLabelUnion U;
}

/// <summary>An arm with several case labels is active for each of them, not just the first.</summary>
public sealed class MultiLabelUnionTests
{
    [Fact]
    public void Metadata_CarriesEveryLabelOfTheArm()
    {
        var meta = new TopicMetadata(typeof(MonitorMultiLabelTopic));
        var arm = meta.AllFields.Single(f => f.StructuredName == "U.A");
        Assert.Equal(new object[] { 1, 2 }, arm.ActiveWhenDiscriminatorValues);
        Assert.Equal(1, arm.ActiveWhenDiscriminatorValue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Json_WritesTheArm_ForEachLabel(int kind)
    {
        var json = JsonSerializer.Serialize(new MonitorMultiLabelUnion { Kind = kind, A = 5 }, DdsJsonOptions.Export);
        Assert.Contains("\"A\":5", json);
        Assert.DoesNotContain("\"C\"", json);
    }

    [Fact]
    public void Json_WritesTheDefaultArm_ForAnUnlistedLabel()
    {
        var json = JsonSerializer.Serialize(new MonitorMultiLabelUnion { Kind = 7, C = 3 }, DdsJsonOptions.Export);
        Assert.Contains("\"C\":3", json);
        Assert.DoesNotContain("\"A\"", json);
    }
}
