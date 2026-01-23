namespace CycloneDDS.Core
{
    /// <summary>
    /// CDR encoding format selector.
    /// Determines wire format conventions for strings, DHEADERs, and encapsulation.
    /// </summary>
    public enum CdrEncoding : byte
    {
        /// <summary>
        /// Legacy CDR (DDS v1.2 / DCPS 2.1).
        /// - Strings include NUL terminator (Length = ByteCount + 1).
        /// - No DHEADERs (even for Appendable types - degrades to Final behavior).
        /// - Encapsulation header: 0x0000 (BE) or 0x0001 (LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR1 (value = 0).
        /// </summary>
        Xcdr1 = 0,

        /// <summary>
        /// Extended CDR (DDS X-Types 1.3).
        /// - Strings do NOT include NUL terminator (Length = ByteCount exactly).
        /// - DHEADERs used for Appendable/Mutable types (4-byte length prefix).
        /// - Encapsulation header: 0x0008 (D_CDR2 BE) or 0x0009 (D_CDR2 LE).
        /// - QoS: DDS_DATA_REPRESENTATION_XCDR2 (value = 2).
        /// </summary>
        Xcdr2 = 2
    }
}
