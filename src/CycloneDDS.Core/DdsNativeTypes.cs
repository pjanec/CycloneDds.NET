using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

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

        public unsafe Span<T> AsSpan<T>() where T : unmanaged
        {
            if (Length == 0) return Span<T>.Empty;
            return new Span<T>((void*)Buffer, (int)Length);
        }

        public unsafe T[] ToArray<T>() where T : unmanaged
        {
            if (Length == 0) return Array.Empty<T>();
            var span = new ReadOnlySpan<T>((void*)Buffer, (int)Length);
            return span.ToArray();
        }

        public unsafe List<T> ToList<T>() where T : unmanaged
        {
            if (Length == 0) return new List<T>();
            var list = new List<T>((int)Length);
            var span = new ReadOnlySpan<T>((void*)Buffer, (int)Length);
            foreach (var item in span)
            {
                list.Add(item);
            }
            return list;
        }
    }
}
