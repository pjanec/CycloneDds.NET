using System;
using System.Buffers;
using Xunit;

namespace CycloneDDS.Core.Tests
{
    public class MoreTests
    {
        [Fact]
        public void CdrWriter_WriteUInt32_And_Reader_ReadUInt32()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteUInt32(0xFFFFFFFF);
            cdr.Complete();

            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(0xFFFFFFFF, reader.ReadUInt32());
        }

        [Fact]
        public void CdrWriter_WriteInt64_And_Reader_ReadInt64()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteInt64(-1234567890123L);
            cdr.Complete();

            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(-1234567890123L, reader.ReadInt64());
        }

        [Fact]
        public void CdrWriter_WriteUInt64_And_Reader_ReadUInt64()
        {
             var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteUInt64(0xFFFFFFFFFFFFFFFF);
            cdr.Complete();

            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, reader.ReadUInt64());
        }

        [Fact]
        public void CdrWriter_WriteFloat_And_Reader_ReadFloat()
        {
             var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteFloat(1.234f);
            cdr.Complete();

            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(1.234f, reader.ReadFloat());
        }

        [Fact]
        public void CdrWriter_WriteByte_And_Reader_ReadByte()
        {
             var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteByte(127);
            cdr.Complete();

            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(127, reader.ReadByte());
        }
        
        [Fact]
        public void CdrReader_Remaining_IsCorrect()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteInt32(1);
            cdr.WriteInt32(2);
            cdr.Complete();
            
            var reader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(8, reader.Remaining);
            reader.ReadInt32();
            Assert.Equal(4, reader.Remaining);
            reader.ReadInt32();
            Assert.Equal(0, reader.Remaining);
        }
    }
}
