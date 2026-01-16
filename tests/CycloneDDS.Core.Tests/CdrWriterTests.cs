using System;
using System.Buffers;
using Xunit;

namespace CycloneDDS.Core.Tests
{
    public class CdrWriterTests
    {
        [Fact]
        public void WriteInt32_Aligned_NoPadding()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteInt32(0x12345678);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(4, data.Length);
            Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, data.ToArray());
        }

        [Fact]
        public void WriteInt32_Unaligned_AddsPadding()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteByte(0xAA);
            cdr.Align(4); 
            cdr.WriteInt32(0x12345678);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(1 + 3 + 4, data.Length);
            Assert.Equal(0xAA, data[0]);
            Assert.Equal(0x00, data[1]); // Pad
            Assert.Equal(0x00, data[2]); // Pad
            Assert.Equal(0x00, data[3]); // Pad
            Assert.Equal(0x78, data[4]);
        }

        [Fact]
        public void WriteDouble_Aligned_NoPadding()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteDouble(1.0);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(8, data.Length);
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0xF0, 0x3F }, data.ToArray());
        }

        [Fact]
        public void WriteDouble_Unaligned_AddsPadding()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteInt32(1); 
            cdr.Align(8);
            cdr.WriteDouble(1.0);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(4 + 4 + 8, data.Length);
            Assert.Equal(0, data[4]);
            Assert.Equal(0, data[5]);
            Assert.Equal(0, data[6]);
            Assert.Equal(0, data[7]);
        }

         [Fact]
        public void WriteString_IncludesLengthAndNull()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteString("Hello");
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(10, data.Length);
            
            Assert.Equal(0x06, data[0]);
            Assert.Equal(0x00, data[3]);

            Assert.Equal((byte)'H', data[4]);
            Assert.Equal((byte)'o', data[8]);
            
            Assert.Equal(0x00, data[9]);
        }

        [Fact]
        public void WriteString_Empty_IsJustNull()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            cdr.WriteString("");
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(5, data.Length);
            
            Assert.Equal(1, data[0]); 
            Assert.Equal(0, data[4]); 
        }

        [Fact]
        public void WriteFixedString_PaddedCorrectly()
        {
             var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            byte[] helloBytes = System.Text.Encoding.UTF8.GetBytes("Hello");
            cdr.WriteFixedString(helloBytes, 10);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(10, data.Length);
            Assert.Equal((byte)'H', data[0]);
            Assert.Equal((byte)'o', data[4]);
            Assert.Equal(0, data[5]); 
            Assert.Equal(0, data[9]); 
        }

         [Fact]
        public void WriteFixedString_TruncatesIfNeeded()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            byte[] helloBytes = System.Text.Encoding.UTF8.GetBytes("Hello");
            cdr.WriteFixedString(helloBytes, 3);
            cdr.Complete();

            var data = writer.WrittenSpan;
            Assert.Equal(3, data.Length);
            Assert.Equal((byte)'H', data[0]);
            Assert.Equal((byte)'e', data[1]);
            Assert.Equal((byte)'l', data[2]);
        }

        [Fact]
        public void BufferFlush_TracksPositionCorrectly()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            
            cdr.WriteFixedString(new byte[100], 100);
            Assert.Equal(100, cdr.Position);
            
            cdr.WriteFixedString(new byte[50], 50);
            Assert.Equal(150, cdr.Position);

            cdr.Align(8);
            Assert.Equal(152, cdr.Position);

            cdr.Complete();
            Assert.Equal(152, writer.WrittenCount);
        }

        [Fact]
        public void MultiplePrimitives_SequenceAlignment()
        {
             var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);

            // Offset 0
            cdr.WriteByte(1); 
            Assert.Equal(1, cdr.Position);

            cdr.Align(2);
            Assert.Equal(2, cdr.Position);

            cdr.WriteInt32(0x1234); 
            Assert.Equal(6, cdr.Position);
            
            cdr.Align(4);
            Assert.Equal(8, cdr.Position);

            cdr.WriteInt32(0x5678); 
            Assert.Equal(12, cdr.Position);
            
            cdr.Complete();
            
            var data = writer.WrittenSpan;
            Assert.Equal(12, data.Length);
            Assert.Equal(1, data[0]);
            Assert.Equal(0, data[1]); // Pad
            Assert.Equal(0x34, data[2]);
            Assert.Equal(0x00, data[5]);
            
            Assert.Equal(0, data[6]); // Pad
            Assert.Equal(0, data[7]); // Pad
            
            Assert.Equal(0x78, data[8]);
        }
    }
}
