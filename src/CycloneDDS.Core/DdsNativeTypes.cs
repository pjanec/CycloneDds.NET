using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Core
{
    /// <summary>
    /// Native representation of a DDS sequence matching the dds_sequence_t ABI.
    /// This struct guarantees compatibility with the C-level representation of sequences
    /// in Cyclone DDS.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsSequenceNative
    {
        /// <summary>
        /// The maximum number of elements the buffer can hold.
        /// </summary>
        public uint Maximum;

        /// <summary>
        /// The current number of elements in the sequence.
        /// </summary>
        public uint Length;

        /// <summary>
        /// Pointer to the data buffer.
        /// </summary>
        public IntPtr Buffer;

        /// <summary>
        /// Indicates whether the buffer is owned by the sequence (true) or external (false).
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool Release;


    }
}
