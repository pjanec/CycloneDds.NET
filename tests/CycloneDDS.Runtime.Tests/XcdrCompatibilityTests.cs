using System;
using System.Runtime.InteropServices;
using Xunit;
using CycloneDDS.Core;
using CycloneDDS.Schema;
using CycloneDDS.Runtime;
using System.Text;

namespace CycloneDDS.Runtime.Tests
{
    public class XcdrCompatibilityTests
    {
        [Fact]
        public void Xcdr1String_Roundtrip()
        {
            var buffer = new byte[100];
            var writer = new CdrWriter(buffer, CdrEncoding.Xcdr1);
            
            string testVal = "Hello";
            writer.WriteString(testVal);
            
            // Verify: Length (4) + "Hello" (5) + NUL (1) = 10 bytes
            // Length field should be 6
            var length = MemoryMarshal.Read<int>(buffer);
            Assert.Equal(6, length);
            
            // Check NUL
            int nulOffset = 4 + 5;
            Assert.Equal(0, buffer[nulOffset]);
            
            // Deserialize
            var reader = new CdrReader(buffer, CdrEncoding.Xcdr1);
            Assert.Equal(CdrEncoding.Xcdr1, reader.Encoding);
            
            var readVal = reader.ReadString();
            Assert.Equal(testVal, readVal);
        }

        [Fact]
        public void Xcdr2String_Roundtrip()
        {
            var buffer = new byte[100];
            var writer = new CdrWriter(buffer, CdrEncoding.Xcdr2);
            
            string testVal = "World";
            writer.WriteString(testVal);
            
            // Verify: Length (4) + "World" (5) = 9 bytes
            // Length should be 5
            var length = MemoryMarshal.Read<int>(buffer);
            Assert.Equal(5, length);
            
            // Deserialize
            var reader = new CdrReader(buffer, CdrEncoding.Xcdr2);
            Assert.Equal(CdrEncoding.Xcdr2, reader.Encoding);
            
            var readVal = reader.ReadString();
            Assert.Equal(testVal, readVal);
        }

        [Fact]
        public void Xcdr1String_Empty()
        {
            var buffer = new byte[100];
            var writer = new CdrWriter(buffer, CdrEncoding.Xcdr1);
            
            writer.WriteString("");
            
            // XCDR1 Empty: Length 1 (NUL), 1 byte NUL
            var length = MemoryMarshal.Read<int>(buffer);
            Assert.Equal(1, length);
            Assert.Equal(0, buffer[4]); // The NUL byte
            
            Assert.Equal(5, writer.Position); // 4 + 1
            
             // Deserialize
            var reader = new CdrReader(buffer, CdrEncoding.Xcdr1);
            var readVal = reader.ReadString();
            Assert.Equal("", readVal);
        }

        [Fact]
        public void Xcdr2String_Empty()
        {
            var buffer = new byte[100];
            var writer = new CdrWriter(buffer, CdrEncoding.Xcdr2);
            
            writer.WriteString("");
            
            // XCDR2 Empty: Length 0
            var length = MemoryMarshal.Read<int>(buffer);
            Assert.Equal(0, length);
            
            Assert.Equal(4, writer.Position); // 4 + 0
            
            // Deserialize
            var reader = new CdrReader(buffer, CdrEncoding.Xcdr2);
            var readVal = reader.ReadString();
            Assert.Equal("", readVal);
        }
        
        [Fact]
        public void AutoDetection_Xcdr1_FromHeader()
        {
            var buffer = new byte[16];
            buffer[0] = 0x00;
            buffer[1] = 0x01; // CDR LE
            
            var reader = new CdrReader(buffer); // Auto-detect
            Assert.Equal(CdrEncoding.Xcdr1, reader.Encoding);
        }

        [Fact]
        public void AutoDetection_Xcdr2_FromHeader()
        {
            var buffer = new byte[16];
            buffer[0] = 0x00;
            buffer[1] = 0x09; // D_CDR2 LE
            
            var reader = new CdrReader(buffer); // Auto-detect
            Assert.Equal(CdrEncoding.Xcdr2, reader.Encoding);
        }

        [Fact]
        public void CdrSizer_Xcdr1_String()
        {
            var sizer = new CdrSizer(0, CdrEncoding.Xcdr1);
            sizer.WriteString("123"); 
            // 4 len + 3 bytes + 1 nul = 8
            Assert.Equal(8, sizer.Position);
        }

        [Fact]
        public void CdrSizer_Xcdr2_String()
        {
            var sizer = new CdrSizer(0, CdrEncoding.Xcdr2);
            sizer.WriteString("123"); 
            // 4 len + 3 bytes = 7
            Assert.Equal(7, sizer.Position);
        }
    }
}
