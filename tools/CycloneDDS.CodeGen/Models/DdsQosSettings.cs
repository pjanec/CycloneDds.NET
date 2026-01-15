using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Models
{
    internal sealed record DdsQosSettings
    {
        public DdsReliability Reliability { get; init; }
        public DdsDurability Durability { get; init; }
        public DdsHistoryKind HistoryKind { get; init; }
        public int HistoryDepth { get; init; }
    }
}
