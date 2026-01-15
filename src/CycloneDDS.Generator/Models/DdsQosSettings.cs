using CycloneDDS.Schema;

namespace CycloneDDS.Generator.Models
{
    // Changed to record for automatic value equality
    internal sealed record DdsQosSettings
    {
        public DdsReliability Reliability { get; init; }
        public DdsDurability Durability { get; init; }
        public DdsHistoryKind HistoryKind { get; init; }
        public int HistoryDepth { get; init; }
    }
}
