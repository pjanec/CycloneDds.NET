using System;
using System.Runtime.InteropServices;
using Xunit;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Tests
{
    public class DdsTextEncodingTests
    {
        [Fact]
        public void GetUtf8Size_HandlesNull()
        {
            Assert.Equal(0, DdsTextEncoding.GetUtf8Size(null));
        }

        [Fact]
        public void GetUtf8Size_IncludesNulTerminator()
        {
            Assert.Equal(3, DdsTextEncoding.GetUtf8Size("Hi"));
        }

        [Fact]
        public void GetUtf8Size_HandlesUnicode()
        {
            // "©" is 0xC2 0xA9 in UTF-8 (2 bytes). So size should be 2 + 1 = 3.
            Assert.Equal(3, DdsTextEncoding.GetUtf8Size("©"));
        }

        [Fact]
        public void FromNativeUtf8_HandlesNull()
        {
            Assert.Null(DdsTextEncoding.FromNativeUtf8(IntPtr.Zero));
        }

        [Fact]
        public void FromNativeUtf8_DecodesCorrectly()
        {
            byte[] buffer = new byte[] { 0x48, 0x69, 0x00 }; // "Hi\0"
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    string? result = DdsTextEncoding.FromNativeUtf8((IntPtr)ptr);
                    Assert.Equal("Hi", result);
                }
            }
        }

        [Fact]
        public void GetSpanFromPtr_HandlesNull()
        {
            Assert.True(DdsTextEncoding.GetSpanFromPtr(IntPtr.Zero).IsEmpty);
        }

        [Fact]
        public void GetSpanFromPtr_ReturnsCorrectSpan()
        {
            byte[] buffer = new byte[] { 0x48, 0x69, 0x00 }; // "Hi\0"
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    ReadOnlySpan<byte> span = DdsTextEncoding.GetSpanFromPtr((IntPtr)ptr);
                    Assert.Equal(2, span.Length);
                    Assert.Equal(0x48, span[0]);
                    Assert.Equal(0x69, span[1]);
                }
            }
        }
    }
}
