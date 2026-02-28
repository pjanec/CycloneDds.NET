using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Tests;

[DdsTopic("TestTopic")]
public partial struct OuterType
{
    public int Id;
    public InnerType Position;
}

[DdsStruct]
public partial struct InnerType
{
    public double X;
    public double Y;
}

[DdsTopic("Keyed")]
public partial struct KeyedType
{
    [DdsKey]
    public int Id;

    [DdsManaged]
    public string Name;
}
