using CycloneDDS.Schema;

namespace CycloneDDS.Runtime.Tests.KeyedMessages
{
    /// <summary>
    /// Composite key (multiple key fields).
    /// Example: Sensor in a specific location - uniquely identified by both SensorId and LocationId.
    /// </summary>
    [DdsTopic("CompositeKeyTopic")]
    public partial struct CompositeKeyMessage
    {
        [DdsKey, DdsId(0)]
        public int SensorId;    // KEY FIELD 1
        
        [DdsKey, DdsId(1)]
        public int LocationId;  // KEY FIELD 2
        
        [DdsId(2)]
        public double Temperature; // Data field
    }
}
