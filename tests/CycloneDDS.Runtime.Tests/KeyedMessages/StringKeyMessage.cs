using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    [DdsTopic("StringKeyMessage")]
    public partial struct StringKeyMessage
    {
        [DdsKey]
        [DdsManaged]
        public string KeyId { get; set; }
        
        [DdsManaged]
        public string Message { get; set; }
    }
}
