using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsKeyDescriptor
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string Name;
        public uint Offset;
        public uint Index;
    }
}
