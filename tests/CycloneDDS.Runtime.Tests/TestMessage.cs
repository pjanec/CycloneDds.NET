using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests
{
    [DdsTopic("TestMessageTopic")]
    public partial struct TestMessage
    {
        public int Id;
        public int Value;
    }
}
