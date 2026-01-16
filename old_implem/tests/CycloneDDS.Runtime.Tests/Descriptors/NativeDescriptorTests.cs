using Xunit;
using CycloneDDS.Runtime.Descriptors;
using CycloneDDS.Schema.Descriptors;
using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Tests.Descriptors;

public class NativeDescriptorTests
{
    [Fact]
    public void NativeDescriptor_Build_WritesCorrectOffsets()
    {
        var data = new DescriptorData
        {
            TypeName = "Test::Message",
            Size = 24,
            Align = 8,
            NOps = 5,
            Ops = new uint[] { 0x01, 0x02, 0x03 }
        };
        
        using var descriptor = new NativeDescriptor(data);
        
        Assert.Equal(24, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.Size));
        Assert.Equal(8, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.Align));
        Assert.Equal(5, Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.NOps));
    }
    
    [Fact]
    public void NativeDescriptor_TypeName_AllocatedCorrectly()
    {
        var data = new DescriptorData { TypeName = "Test::Type" };
        using var descriptor = new NativeDescriptor(data);
        
        var namePtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.TypeName);
        var name = Marshal.PtrToStringAnsi(namePtr);
        
        Assert.Equal("Test::Type", name);
    }
    
    [Fact]
    public void NativeDescriptor_OpsArray_CopiedCorrectly()
    {
        var ops = new uint[] { 0xDEADBEEF, 0xCAFEBABE, 0x12345678 };
        var data = new DescriptorData { Ops = ops, NOps = 3 };
        
        using var descriptor = new NativeDescriptor(data);
        
        var opsPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.Ops);
        var readOps = new int[3];
        Marshal.Copy(opsPtr, readOps, 0, 3);
        
        Assert.Equal(unchecked((int)0xDEADBEEF), readOps[0]);
    }
    
    [Fact]
    public void NativeDescriptor_WithKeys_AllocatesKeyArray()
    {
        var data = new DescriptorData
        {
            TypeName = "Test",
            NKeys = 2,
            Keys = new[]
            {
                new KeyDescriptor { Name = "Id", Flags = 1, Index = 0 },
                new KeyDescriptor { Name = "Name", Flags = 2, Index = 1 }
            }
        };
        
        using var descriptor = new NativeDescriptor(data);
        
        var keysPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.Keys);
        Assert.NotEqual(IntPtr.Zero, keysPtr);
        
        // Size per key = IntPtr + 4 + 4
        int keySize = IntPtr.Size + 8;
        
        // Verify first key
        var key0NamePtr = Marshal.ReadIntPtr(keysPtr, 0);
        var key0Name = Marshal.PtrToStringAnsi(key0NamePtr);
        var key0Flags = Marshal.ReadInt32(keysPtr, IntPtr.Size);
        var key0Index = Marshal.ReadInt32(keysPtr, IntPtr.Size + 4);
        
        Assert.Equal("Id", key0Name);
        Assert.Equal(1, key0Flags);
        Assert.Equal(0, key0Index);

         // Verify second key
        var key1Offset = keySize;
        var key1NamePtr = Marshal.ReadIntPtr(keysPtr, key1Offset);
        var key1Name = Marshal.PtrToStringAnsi(key1NamePtr);
        var key1Flags = Marshal.ReadInt32(keysPtr, key1Offset + IntPtr.Size);
        var key1Index = Marshal.ReadInt32(keysPtr, key1Offset + IntPtr.Size + 4);
        
        Assert.Equal("Name", key1Name);
        Assert.Equal(2, key1Flags);
        Assert.Equal(1, key1Index);
    }
    
    [Fact]
    public void NativeDescriptor_Dispose_FreesAllMemory()
    {
        var data = new DescriptorData
        {
            TypeName = "Test",
            Ops = new uint[] { 1, 2, 3 },
            TypeInfo = new byte[] { 0x60, 0x00 }
        };
        
        var descriptor = new NativeDescriptor(data);
        var ptr = descriptor.Ptr;
        
        descriptor.Dispose();
        
        // Ptr property should be Zero after dispose
        Assert.Equal(IntPtr.Zero, descriptor.Ptr);
    }
    
    [Fact]
    public void NativeDescriptor_TypeInfoBlob_CopiedCorrectly()
    {
        var typeInfo = new byte[] { 0x60, 0x01, 0x02, 0x03 };
        var data = new DescriptorData { TypeInfo = typeInfo };
        
        using var descriptor = new NativeDescriptor(data);
        
        var infoPtr = Marshal.ReadIntPtr(descriptor.Ptr, AbiOffsets.TypeInfo_Data);
        var size = Marshal.ReadInt32(descriptor.Ptr, AbiOffsets.TypeInfo_Size);
        
        Assert.NotEqual(IntPtr.Zero, infoPtr);
        Assert.Equal(4, size);
        
        var readBytes = new byte[4];
        Marshal.Copy(infoPtr, readBytes, 0, 4);
        Assert.Equal(typeInfo, readBytes);
    }
}
