using CycloneDDS.Schema;

namespace CycloneDDS.Generator.Models
{
    internal sealed class DdsQosSettings
    {
        public DdsReliability Reliability { get; set; }
        public DdsDurability Durability { get; set; }
        public DdsHistoryKind HistoryKind { get; set; }
        public int HistoryDepth { get; set; }
    }
}
