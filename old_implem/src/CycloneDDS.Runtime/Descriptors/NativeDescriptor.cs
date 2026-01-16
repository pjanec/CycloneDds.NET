using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CycloneDDS.Schema.Descriptors;

namespace CycloneDDS.Runtime.Descriptors;

public class NativeDescriptor : IDisposable
{
    private List<IntPtr> _allocations = new();
    private bool _disposed;

    public IntPtr Ptr { get; private set; }

    public NativeDescriptor(DescriptorData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        // Allocate arrays
        IntPtr ptrOps = AllocUInt32Array(data.Ops);
        IntPtr ptrTypeInfo = AllocBytes(data.TypeInfo);        
        IntPtr ptrTypeMap = AllocBytes(data.TypeMap);
        IntPtr ptrTypeName = AllocString(data.TypeName);
        IntPtr ptrMeta = AllocString(data.Meta);

        // Allocate main descriptor struct
        Ptr = AllocRaw(AbiOffsets.DescriptorSize);

        // Write fields by offset (ABI-safe)
        // Note: Casting uint to int for Marshal.WriteInt32 is safe for bit pattern
        
        WriteInt32(Ptr, AbiOffsets.Size, (int)data.Size);
        WriteInt32(Ptr, AbiOffsets.Align, (int)data.Align);
        if (AbiOffsets.Flagset >= 0) WriteInt32(Ptr, AbiOffsets.Flagset, (int)data.Flagset);
        WriteInt32(Ptr, AbiOffsets.NKeys, (int)data.NKeys);
        WriteIntPtr(Ptr, AbiOffsets.TypeName, ptrTypeName);
        
        // Keys handling
        IntPtr ptrKeys = AllocKeyDescriptors(data.Keys);
        WriteIntPtr(Ptr, AbiOffsets.Keys, ptrKeys); 

        WriteInt32(Ptr, AbiOffsets.NOps, (int)data.NOps); // Check if NOps exists in AbiOffsets. Yes.
        WriteIntPtr(Ptr, AbiOffsets.Ops, ptrOps);
        WriteIntPtr(Ptr, AbiOffsets.Meta, ptrMeta);

        // Nested struct: type_information { void* data; uint32_t sz; }
        // AbiOffsets generated TypeInfo_Data, TypeInfo_Size.
        if (AbiOffsets.TypeInfo_Data >= 0)
        {
            WriteIntPtr(Ptr, AbiOffsets.TypeInfo_Data, ptrTypeInfo);
            WriteInt32(Ptr, AbiOffsets.TypeInfo_Size, data.TypeInfo.Length);
        }
        
        if (AbiOffsets.TypeMap_Data >= 0)
        {
             WriteIntPtr(Ptr, AbiOffsets.TypeMap_Data, ptrTypeMap); 
             WriteInt32(Ptr, AbiOffsets.TypeMap_Size, data.TypeMap.Length);
        }
    }

    private IntPtr AllocKeyDescriptors(KeyDescriptor[]? keys)
    {
        if (keys == null || keys.Length == 0) return IntPtr.Zero;
        
        // dds_key_descriptor_t has: char* name, uint32_t flags, uint32_t index (Actually flags is m_offset in Cyclone DDS)
        // Size: IntPtr.Size + 4 + 4 (platform dependent)
        int keyDescSize = IntPtr.Size + 8;
        int totalSize = keys.Length * keyDescSize;
        
        var ptr = AllocRaw(totalSize);
        
        for (int i = 0; i < keys.Length; i++)
        {
            int offset = i * keyDescSize;
            IntPtr namePtr = AllocString(keys[i].Name);
            
            WriteIntPtr(ptr, offset, namePtr);
            // Write Flags/Offset
            Marshal.WriteInt32(ptr, offset + IntPtr.Size, (int)keys[i].Flags);
            // Write Index
            Marshal.WriteInt32(ptr, offset + IntPtr.Size + 4, (int)keys[i].Index);
        }
        
        return ptr;
    }

    private IntPtr AllocRaw(int size)
    {
        var ptr = Marshal.AllocHGlobal(size);
        // Zero memory
        // Marshal.ZeroFreeGlobalAllocAnsi(ptr); // Only for ANSI strings?
        // Manually zero
        byte[] zero = new byte[size];
        Marshal.Copy(zero, 0, ptr, size);
        
        _allocations.Add(ptr);
        return ptr;
    }

    private IntPtr AllocBytes(byte[]? data)
    {
        if (data == null || data.Length == 0) return IntPtr.Zero;
        var ptr = AllocRaw(data.Length);
        Marshal.Copy(data, 0, ptr, data.Length);
        return ptr;
    }

    private IntPtr AllocUInt32Array(uint[]? data)
    {
        if (data == null || data.Length == 0) return IntPtr.Zero;
        var ptr = AllocRaw(data.Length * 4);
        // Marshal.Copy used with int[], not uint[]. Cast or loop.
        // Block copy
        int[] signed = new int[data.Length];
        Buffer.BlockCopy(data, 0, signed, 0, data.Length * 4);
        Marshal.Copy(signed, 0, ptr, signed.Length);
        return ptr;
    }

    private IntPtr AllocString(string? str)
    {
        if (string.IsNullOrEmpty(str)) return IntPtr.Zero; // Or allow empty string?
        // Use ANSI for m_typename? Cyclone is usually UTF-8/ANSI.
        var ptr = Marshal.StringToHGlobalAnsi(str);
        _allocations.Add(ptr);
        return ptr;
    }

    private void WriteInt32(IntPtr basePtr, int offset, int value)
    {
        Marshal.WriteInt32(basePtr, offset, value);
    }

    private void WriteIntPtr(IntPtr basePtr, int offset, IntPtr value)
    {
        Marshal.WriteIntPtr(basePtr, offset, value);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        foreach (var ptr in _allocations)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _allocations.Clear();
        Ptr = IntPtr.Zero;
        _disposed = true;
    }

    ~NativeDescriptor()
    {
        Dispose(false);
    }
}
