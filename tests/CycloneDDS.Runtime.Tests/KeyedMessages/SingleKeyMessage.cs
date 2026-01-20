using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// Single primitive key - most common DDS keyed topic pattern.
    /// Example: Vehicle tracking by VehicleId.
    /// </summary>
    [DdsTopic("SingleKeyTopic")]
    public partial struct SingleKeyMessage
    {
        [DdsKey, DdsId(0)]
        public int DeviceId;   // KEY FIELD
        
        [DdsId(1)]
        public int Value;      // Data field
        
        [DdsId(2)]
        public long Timestamp; // Data field
    }
}
