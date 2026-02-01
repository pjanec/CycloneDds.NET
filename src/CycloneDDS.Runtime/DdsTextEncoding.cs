using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CycloneDDS.Runtime
{
    /// <summary>
    /// Utilities for efficient UTF-8 string encoding and decoding.
    /// </summary>
    public static class DdsTextEncoding
    {
        /// <summary>
        /// Calculates the number of bytes required to store the string as UTF-8, including the null terminator.
        /// </summary>
        /// <param name="text">The string to measure.</param>
        /// <returns>The total byte size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetUtf8Size(string? text)
        {
            if (text == null)
                return 0;
            
            return Encoding.UTF8.GetByteCount(text) + 1;
        }

        /// <summary>
        /// Decodes a null-terminated UTF-8 string from a native pointer.
        /// </summary>
        /// <param name="ptr">Pointer to the null-terminated string.</param>
        /// <returns>The managed string, or null if the pointer is Zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? FromNativeUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            return Marshal.PtrToStringUTF8(ptr);
        }

        /// <summary>
        /// Creates a ReadOnlySpan from a null-terminated UTF-8 string pointer.
        /// This is a zero-copy operation.
        /// </summary>
        /// <param name="ptr">Pointer to the null-terminated string.</param>
        /// <returns>A span containing the UTF-8 bytes (excluding the null terminator).</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ReadOnlySpan<byte> GetSpanFromPtr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return ReadOnlySpan<byte>.Empty;

            // Find the length of the string (scan for null terminator)
            byte* p = (byte*)ptr;
            int length = 0;
            while (p[length] != 0)
            {
                length++;
            }

            return new ReadOnlySpan<byte>(p, length);
        }
    }
}
