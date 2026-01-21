using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    [DdsTopic("KeyLastMessage")]
    public partial struct KeyLastMessage
    {
        [DdsManaged]
        public string Data { get; set; }

        [DdsKey]
        public int Id { get; set; }
    }
}
