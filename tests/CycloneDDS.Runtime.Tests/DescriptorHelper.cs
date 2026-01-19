using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Tests
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsTypeMetaSer
    {
        public IntPtr data;
        public uint sz;
    }

    // Corresponds to dds_topic_descriptor_t in dds/dds_public_impl.h
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsTopicDescriptor
    {
        public uint Size;       // m_size
        public uint Align;      // m_align
        public uint Flagset;    // m_flagset
        public uint NKeys;      // m_nkeys
        public IntPtr TypeName; // m_typename (char*) 
        public IntPtr Keys;     // m_keys (dds_key_descriptor_t*)
        public uint NOps;       // m_nops (uint32_t)
        // C# auto-padding for IntPtr alignment should match C compiler (usually)
        public IntPtr Ops;      // m_ops (uint32_t*)
        public IntPtr Meta;     // m_meta (char*)
        public DdsTypeMetaSer type_information;
        public DdsTypeMetaSer type_mapping;
        public uint restrict_data_representation;
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
                Meta = IntPtr.Zero,
                type_information = new DdsTypeMetaSer { data = IntPtr.Zero, sz = 0 },
                type_mapping = new DdsTypeMetaSer { data = IntPtr.Zero, sz = 0 },
                restrict_data_representation = 0
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
