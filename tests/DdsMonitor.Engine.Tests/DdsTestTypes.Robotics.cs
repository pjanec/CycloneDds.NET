using CycloneDDS.Schema;

namespace DdsMonitor.Engine.Tests.Robotics;

[DdsTopic("Navigation")]
public partial struct NavigationTopic
{
    public int Id;
}
