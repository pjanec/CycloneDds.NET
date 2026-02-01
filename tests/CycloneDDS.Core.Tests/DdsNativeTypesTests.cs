using System;
using System.Runtime.InteropServices;
using Xunit;
using CycloneDDS.Core;

namespace CycloneDDS.Core.Tests
{
    public class DdsNativeTypesTests
    {
        [Fact]
        public void DdsSequenceNative_LayoutIsSequential()
        {
            Assert.Equal(LayoutKind.Sequential, typeof(DdsSequenceNative).StructLayoutAttribute!.Value);
        }

        [Fact]
        public void DdsSequenceNative_SizeIsCorrect()
        {
            int size =  Marshal.SizeOf<DdsSequenceNative>();
            if (IntPtr.Size == 8) // x64
            {
                // 4 (Maximum) + 4 (Length) + 8 (Buffer) + 1 (Release) + 7 (padding) = 24
                Assert.Equal(24, size);
            }
            else // x86
            {
                // 4 (Maximum) + 4 (Length) + 4 (Buffer) + 1 (Release) + 3 (padding) = 16
                Assert.Equal(16, size);
            }
        }

        [Fact]
        public void DdsSequenceNative_OffsetsAreCorrect()
        {
            Assert.Equal(0, Marshal.OffsetOf<DdsSequenceNative>("Maximum").ToInt32());
            Assert.Equal(4, Marshal.OffsetOf<DdsSequenceNative>("Length").ToInt32());
            Assert.Equal(8, Marshal.OffsetOf<DdsSequenceNative>("Buffer").ToInt32());
            
            if (IntPtr.Size == 8) // x64
            {
                // Buffer is 8 bytes, so Offset(Buffer) + Size(Buffer) = 8 + 8 = 16
                Assert.Equal(16, Marshal.OffsetOf<DdsSequenceNative>("Release").ToInt32());
            }
            else // x86
            {
                // Buffer is 4 bytes, so Offset(Buffer) + Size(Buffer) = 8 + 4 = 12
                Assert.Equal(12, Marshal.OffsetOf<DdsSequenceNative>("Release").ToInt32());
            }
        }

        [Fact]
        public void DdsSequenceNative_ReleaseIsOneByte()
        {
            Assert.Equal(typeof(byte), typeof(DdsSequenceNative).GetField("Release")!.FieldType);
        }
    }
}
