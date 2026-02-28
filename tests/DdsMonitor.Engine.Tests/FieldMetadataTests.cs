using System.Linq;
using CycloneDDS.Schema;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public class FieldMetadataTests
{
    [Fact]
    public void FieldMetadata_Getter_ReturnsCorrectValue()
    {
        var metadata = new TopicMetadata(typeof(SimpleType));
        var field = metadata.AllFields.Single(f => f.StructuredName == "Count");
        var instance = new SimpleType { Count = 7 };

        var value = field.Getter(instance);

        Assert.Equal(7, value);
    }

    [Fact]
    public void FieldMetadata_Setter_SetsCorrectValue()
    {
        var metadata = new TopicMetadata(typeof(SimpleType));
        var field = metadata.AllFields.Single(f => f.StructuredName == "Count");
        var instance = new SimpleType();

        field.Setter(instance, 42);

        Assert.Equal(42, instance.Count);
    }

    [DdsTopic("Simple")]
    private class SimpleType
    {
        public int Count { get; set; }
    }
}
