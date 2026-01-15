namespace CycloneDDS.Schema
{
    public enum DdsReliability { Reliable, BestEffort }
    public enum DdsDurability { Volatile, TransientLocal, Transient, Persistent }
    public enum DdsHistoryKind { KeepLast, KeepAll }
    public enum DdsWire { Cdr, CdrLe, CdrBe }
}
