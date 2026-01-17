using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Tests
{
    // Corresponds to dds_topic_descriptor_t in dds/dds_public_impl.h
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsTopicDescriptor
    {
        public uint Size;       // m_size
        public uint Align;      // m_align
        public uint Flagset;    // m_flagset
        public uint NKeys;      // m_nkeys
        public IntPtr TypeName; // m_typename (char*) - Missing in previous version
        public IntPtr Keys;     // m_keys (uint32_t*)
        public uint NOps;       // m_nops (uint32_t)  - Missing in previous version
        public IntPtr Ops;      // m_ops (uint32_t*)
        public IntPtr Meta;     // m_meta (char*)
    }

    public class DescriptorContainer : IDisposable
    {
        private IntPtr _descPtr;
        private GCHandle _opsHandle;
        private IntPtr _typeNamePtr;

        public IntPtr Ptr => _descPtr;

        public DescriptorContainer(uint[] ops, uint size, uint align, uint flagset = 0, string typeName = "TestType")
        {
            _opsHandle = GCHandle.Alloc(ops, GCHandleType.Pinned);
            _typeNamePtr = Marshal.StringToHGlobalAnsi(typeName);
            
            var desc = new DdsTopicDescriptor
            {
                Size = size,
                Align = align,
                Flagset = flagset,
                NKeys = 0,
                TypeName = _typeNamePtr,
                Keys = IntPtr.Zero,
                NOps = (uint)ops.Length,
                Ops = _opsHandle.AddrOfPinnedObject(),
                Meta = IntPtr.Zero
            };

            _descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DdsTopicDescriptor>());
            Marshal.StructureToPtr(desc, _descPtr, false);
        }

        public void Dispose()
        {
            if (_descPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_descPtr);
                _descPtr = IntPtr.Zero;
            }
            if (_typeNamePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_typeNamePtr);
                _typeNamePtr = IntPtr.Zero;
            }
            if (_opsHandle.IsAllocated)
            {
                _opsHandle.Free();
            }
        }
    }
}
