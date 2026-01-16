using System;
using System.Buffers;
using System.Text;
using Xunit;

namespace CycloneDDS.Core.Tests
{
    public class CdrRoundTripTests
    {
        [Fact]
        public void RoundTrip_Int32()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            cdrWriter.WriteInt32(123456);
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(123456, cdrReader.ReadInt32());
        }

        [Fact]
        public void RoundTrip_Double()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            cdrWriter.WriteDouble(3.14159);
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(3.14159, cdrReader.ReadDouble());
        }

        [Fact]
        public void RoundTrip_String()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            cdrWriter.WriteString("Hello World");
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            var span = cdrReader.ReadStringBytes();
            Assert.Equal("Hello World", Encoding.UTF8.GetString(span));
        }

        [Fact]
        public void RoundTrip_EmptyString()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            cdrWriter.WriteString("");
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            var span = cdrReader.ReadStringBytes();
            Assert.Equal("", Encoding.UTF8.GetString(span));
        }

        [Fact]
        public void RoundTrip_MultipleAlignedFields()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            
            cdrWriter.WriteByte(1); // Pos 1
            cdrWriter.Align(4);     // Pos 4
            cdrWriter.WriteInt32(0xABCDEF); // Pos 8
            cdrWriter.Align(8);     // Pos 8
            cdrWriter.WriteDouble(123.456); // Pos 16
            
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            Assert.Equal(1, cdrReader.ReadByte());
            
            cdrReader.Align(4);
            Assert.Equal(0xABCDEF, cdrReader.ReadInt32());
            
            cdrReader.Align(8);
            Assert.Equal(123.456, cdrReader.ReadDouble());
        }

        [Fact]
        public void RoundTrip_ComplexStruct()
        {
            var writer = new ArrayBufferWriter<byte>();
            var cdrWriter = new CdrWriter(writer);
            
            // struct Inner { uint id; string name; }
            // struct Outer { byte tag; Inner inner; double value; }
            
            // tag
            cdrWriter.WriteByte(0xFF); 
            
            // Inner misalignment
            cdrWriter.Align(4); 
            // id
            cdrWriter.WriteUInt32(100);
            // name (string starts with int length, so align 4 usually)
            // But WriteString writes Length (int), which doesn't auto align in my impl, 
            // but previous WriteUInt32 left us at 8 (aligned to 4).
            cdrWriter.WriteString("Test");
            
            // value (double) needs 8 align
            cdrWriter.Align(8);
            cdrWriter.WriteDouble(99.99);
            
            cdrWriter.Complete();

            var cdrReader = new CdrReader(writer.WrittenSpan);
            
            Assert.Equal(0xFF, cdrReader.ReadByte());
            
            cdrReader.Align(4);
            Assert.Equal(100u, cdrReader.ReadUInt32());
            
            // Writestring didn't align, but we are at 8 + 4 + 4 + 1(null) + padding?
            // Wait.
            // Pos 0: Byte -> 1.
            // Align 4 -> 4.
            // UInt32 -> 8.
            // WriteString("Test"):
            //   Length = 5. WriteInt32(5) -> 12.
            //   Bytes "Test\0" -> 12 + 5 = 17.
            // Align 8 -> 17%8=1. Pad=7 -> 24.
            // Double -> 32.
            
            var strSpan = cdrReader.ReadStringBytes(); // Reads int length then bytes
            Assert.Equal("Test", Encoding.UTF8.GetString(strSpan));
            
            cdrReader.Align(8);
            Assert.Equal(99.99, cdrReader.ReadDouble());
        }
    }
}
