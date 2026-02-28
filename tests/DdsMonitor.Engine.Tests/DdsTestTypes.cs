using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Tests;

[DdsTopic("Sample")]
public partial struct SampleTopic
{
    public int Id;
}

[DdsTopic("MockTopic")]
public partial struct MockTopic
{
    public int Id;
}

[DdsTopic("DynamicReaderTopic")]
public partial struct DynamicReaderMessage
{
    public int Id;
    public int Value;
}

[DdsTopic("DynamicWriterTopic")]
public partial struct DynamicWriterMessage
{
    public int Id;
    public int Value;
}

[DdsTopic("DynamicWriterKeyed")]
public partial struct DynamicWriterKeyedMessage
{
    [DdsKey]
    public int Id;

    public int Value;
}

[DdsTopic("Simple")]
public partial struct SimpleType
{
    public int Count;
}

[DdsTopic("InstanceKeyed")]
public partial struct InstanceKeyedMessage
{
    [DdsKey]
    public int Id;

    public int Value;
}

[DdsTopic("InstanceComposite")]
public partial struct InstanceCompositeKeyMessage
{
    [DdsKey]
    public int EntityId;

    [DdsKey]
    public int PartId;

    public int Value;
}

[DdsTopic("RobotTopic")]
public partial struct RobotTopic
{
    public int Id;
}

[DdsTopic("OtherTopic")]
public partial struct OtherTopic
{
    public int Id;
}
