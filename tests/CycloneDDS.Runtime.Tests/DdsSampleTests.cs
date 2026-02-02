using System;
using System.Runtime.InteropServices;
using Xunit;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime.Tests
{
    // Dummy message type for testing lazy marshalling
    public struct ZeroCopyTestMsg
    {
        public int Id;
        public long Timestamp;

        // This method relies on the specific memory layout of the struct
        public static void MarshalFromNative(IntPtr ptr, out ZeroCopyTestMsg output)
        {
            output = Marshal.PtrToStructure<ZeroCopyTestMsg>(ptr);
        }
    }

    public unsafe class DdsSampleTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var ptr = new IntPtr(123456);
            var info = new DdsApi.DdsSampleInfo { 
                ValidData = 1,
                InstanceHandle = 99
            };

            var sample = new DdsSample<ZeroCopyTestMsg>(ptr, ref info);

            Assert.Equal(ptr, sample.NativePtr);
            Assert.Equal(1, sample.Info.ValidData);
            Assert.True(sample.IsValid);
            Assert.Equal(99, sample.Info.InstanceHandle);
        }

        [Fact]
        public void IsValid_ReturnsFalse_WhenValidDataIsZero()
        {
            var ptr = IntPtr.Zero;
            var info = new DdsApi.DdsSampleInfo { ValidData = 0 };
            var sample = new DdsSample<ZeroCopyTestMsg>(ptr, ref info);

            Assert.False(sample.IsValid);
        }

        [Fact]
        public void Data_LazilyMarshalls_FromNativePtr()
        {
            // Allocate native memory for the struct
            int size = Marshal.SizeOf<ZeroCopyTestMsg>();
            IntPtr nativeMem = Marshal.AllocHGlobal(size);

            try
            {
                // Write data to native memory
                var expected = new ZeroCopyTestMsg { Id = 42, Timestamp = 123456789 };
                Marshal.StructureToPtr(expected, nativeMem, false);

                var info = new DdsApi.DdsSampleInfo { ValidData = 1 };
                var sample = new DdsSample<ZeroCopyTestMsg>(nativeMem, ref info);

                // Access .Data triggers MarshalFromNative
                var result = sample.Data;

                Assert.Equal(expected.Id, result.Id);
                Assert.Equal(expected.Timestamp, result.Timestamp);
            }
            finally
            {
                Marshal.FreeHGlobal(nativeMem);
            }
        }
    }
}
