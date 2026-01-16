using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace CycloneDDS.Core.Tests
{
    public class CdrReaderTests
    {
        [Fact]
        public void ReadInt32_Aligned_ReadsCorrectly()
        {
            var data = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(data, 0x12345678);
            
            var reader = new CdrReader(data);
            int val = reader.ReadInt32();
            
            Assert.Equal(0x12345678, val);
            Assert.Equal(4, reader.Position);
        }

        [Fact]
        public void ReadInt32_WithAlignment_SkipsPadding()
        {
            var data = new byte[8];
            data[0] = 1;
            // Pad 1, 2, 3 -> Pos 4
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 0x12345678);
            
            var reader = new CdrReader(data);
            reader.ReadByte();
            reader.Align(4);
            int val = reader.ReadInt32();
            
            Assert.Equal(0x12345678, val);
            Assert.Equal(8, reader.Position);
        }

        [Fact]
        public void ReadString_ReadsLengthAndBytes()
        {
            // Length=6 (Hello + Nul), 'H','e','l','l','o', Nul
            var data = new byte[] { 
                0x06, 0x00, 0x00, 0x00, 
                (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o', 0x00 
            };
            
            var reader = new CdrReader(data);
            var span = reader.ReadStringBytes();
            
            Assert.Equal(5, span.Length);
            Assert.Equal("Hello", Encoding.UTF8.GetString(span));
            Assert.Equal(10, reader.Position);
        }

        [Fact]
        public void ReadString_Empty_ReturnsEmpty()
        {
            // Length=1 (Nul only), Nul
            var data = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00 };
            
            var reader = new CdrReader(data);
            var span = reader.ReadStringBytes();
            
            Assert.Equal(0, span.Length);
            Assert.Equal(5, reader.Position);
        }

        [Fact]
        public void ReadFixedBytes_ReadsExactCount()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var reader = new CdrReader(data);
            var span = reader.ReadFixedBytes(3);
            
            Assert.Equal(3, span.Length);
            Assert.Equal(1, span[0]);
            Assert.Equal(3, span[2]);
            Assert.Equal(3, reader.Position);
        }

        [Fact]
        public void Seek_MovesPosition()
        {
            var data = new byte[10];
            var reader = new CdrReader(data);
            
            reader.Seek(5);
            Assert.Equal(5, reader.Position);
            
            reader.ReadByte();
            Assert.Equal(6, reader.Position);
        }

        [Fact]
        public void Read_PastEnd_Throws()
        {
            var data = new byte[2];
            var reader = new CdrReader(data);
            
            bool threw = false;
            try
            {
                reader.ReadInt32();
            }
            catch (IndexOutOfRangeException)
            {
                threw = true;
            }
            Assert.True(threw);
        }

        [Fact]
        public void Align_WithNotEnoughData_Throws()
        {
            var data = new byte[3];
            var reader = new CdrReader(data);
            reader.ReadByte(); 
            
            bool threw = false;
            try
            {
                reader.Align(4);
            }
            catch (IndexOutOfRangeException)
            {
                threw = true;
            }
            Assert.True(threw);
        }

        [Fact]
        public void ReadString_MalformedLength_Throws()
        {
            var data = new byte[] { 100, 0, 0, 0, 1, 2 };
            var reader = new CdrReader(data);
            
            bool threw = false;
            try
            {
                reader.ReadStringBytes();
            }
            catch (IndexOutOfRangeException)
            {
                threw = true;
            }
            Assert.True(threw);
        }
    }
}
