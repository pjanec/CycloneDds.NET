using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    // Flattened to avoid CodeGen/Runtime StackOverflow with nested struct keys
    [DdsTopic("NestedKeyMessage")]
    public partial struct NestedKeyMessage
    {
        [DdsKey]
        [DdsId(0)]
        public int InnerId;

        [DdsId(1)]
        [DdsManaged]
        public string Data;
    }
}
