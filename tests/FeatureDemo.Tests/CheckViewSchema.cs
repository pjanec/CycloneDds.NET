using NUnit.Framework;

namespace FeatureDemo.Tests;

public class CheckViewSchema
{
    [Test]
    public void FixedString32View_Exists()
    {
        var type = typeof(CycloneDDS.Schema.FixedString32View);
        Assert.That(type, Is.Not.Null);
    }
}

