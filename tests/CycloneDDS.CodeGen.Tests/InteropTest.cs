using System;
using System.Buffers;
using CycloneDDS.Core;
using CycloneDDS.Schema;
using Xunit;
using System.Runtime.InteropServices;

namespace CycloneDDS.CodeGen.Tests
{
    public class InteropTest
    {
        [Fact]
        public void CSharp_Matches_C_ByteForByte()
        {
            var testUnion = new TestUnion();
            testUnion._d = 1; 
            testUnion.valueA = unchecked((int)0xDEADBEEF);

            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            testUnion.Serialize(ref cdr);
            cdr.Complete();
            
            byte[] csharpBytes = writer.WrittenSpan.ToArray();
            string csharpHex = BitConverter.ToString(csharpBytes);
            
            Console.WriteLine($"C# Hex: {csharpHex}");
            Console.WriteLine($"C# Size: {csharpBytes.Length} bytes");
            
            string expectedHex = "08-00-00-00-01-00-00-00-EF-BE-AD-DE";
            
            Assert.Equal(expectedHex, csharpHex);
        }
    }

    [DdsUnion]
    public partial struct TestUnion
    {
        [DdsDiscriminator]
        public int _d;

        [DdsCase(1)]
        public int valueA;

        [DdsCase(2)]
        public double valueB;
        
        [DdsCase(3)]
        public string valueC;

        public void Serialize(ref CdrWriter writer)
        {
            writer.Align(4);
            int dheaderPos = writer.Position;
            writer.WriteUInt32(0);

            int bodyStart = writer.Position;

            writer.Align(4);
            writer.WriteInt32(_d);

            switch (_d)
            {
                case 1:
                    writer.Align(4);
                    writer.WriteInt32(valueA);
                    break;
                case 2:
                    writer.Align(8);
                    writer.WriteDouble(valueB);
                    break;
                case 3:
                    writer.Align(4);
                    writer.WriteString(valueC);
                    break;
            }

            // Patch DHEADER
            int bodySize = writer.Position - bodyStart;
            writer.PatchUInt32(dheaderPos, (uint)bodySize);
        }
    }
}

