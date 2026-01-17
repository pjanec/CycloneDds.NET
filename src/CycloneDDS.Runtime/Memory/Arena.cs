using System;
using System.Buffers;

namespace CycloneDDS.Runtime.Memory
{
    public static class Arena
    {
        public static byte[] Rent(int minimumLength)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        public static void Return(byte[] buffer, bool clearArray = false)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray);
        }
    }
}
