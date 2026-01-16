using System;
using System.Buffers;
using Xunit;
using CycloneDDS.Core;

namespace CycloneDDS.Core.Tests
{
    public class CdrSizerTests
    {
        [Fact]
        public void WriteByte_AdvancesBy1()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteByte(1);
            Assert.Equal(1, sizer.Position);
        }

        [Fact]
        public void WriteInt32_FromOffset0_Size4()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteInt32(123);
            Assert.Equal(4, sizer.Position);
        }

        [Fact]
        public void WriteInt32_FromOffset1_Size7()
        {
            var sizer = new CdrSizer(1);
            sizer.WriteInt32(123);
            // 1 -> align 4 -> 4. 4 + 4 = 8.
            // Wait, the instruction said: "WriteInt32 from offset 1 -> align to 4, then +4 = size 7 total"
            // Let's trace:
            // Start 1.
            // Align(1, 4) -> 4.
            // 4 + 4 = 8.
            // Delta = 8 - 1 = 7.
            // Position is 8.
            Assert.Equal(8, sizer.Position);
            Assert.Equal(7, sizer.GetSizeDelta(1));
        }

        [Fact]
        public void WriteDouble_FromOffset0_Size8()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteDouble(1.23);
            Assert.Equal(8, sizer.Position);
        }

        [Fact]
        public void WriteDouble_FromOffset5_Size11()
        {
            var sizer = new CdrSizer(5);
            sizer.WriteDouble(1.23);
            // 5 -> align 8 -> 8.
            // 8 + 8 = 16.
            // Delta = 16 - 5 = 11.
            Assert.Equal(16, sizer.Position);
            Assert.Equal(11, sizer.GetSizeDelta(5));
        }

        [Fact]
        public void WriteString_Hello_FromOffset0_Size10()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteString("Hello");
            // Align 4 -> 0.
            // +4 (len) -> 4.
            // +5 (bytes) -> 9.
            // +1 (null) -> 10.
            Assert.Equal(10, sizer.Position);
        }

        [Fact]
        public void WriteString_Empty_FromOffset0_Size5()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteString("");
            // Align 4 -> 0.
            // +4 (len) -> 4.
            // +0 (bytes) -> 4.
            // +1 (null) -> 5.
            Assert.Equal(5, sizer.Position);
        }

        [Fact]
        public void MultipleWrites_VerifyCumulativeSize()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteByte(1); // pos 1
            sizer.WriteInt32(2); // align 4 -> 4, +4 -> 8
            sizer.WriteDouble(3.0); // align 8 -> 8, +8 -> 16
            Assert.Equal(16, sizer.Position);
        }

        [Fact]
        public void GetSizeDelta_ReturnsCorrectDelta()
        {
            var sizer = new CdrSizer(10);
            sizer.WriteByte(1);
            Assert.Equal(1, sizer.GetSizeDelta(10));
        }

        [Fact]
        public void CdrSizer_Matches_CdrWriter_Output()
        {
            var sizer = new CdrSizer(0);
            sizer.WriteInt32(42);
            sizer.WriteString("Test");
            int expectedSize = sizer.GetSizeDelta(0);

            var writer = new ArrayBufferWriter<byte>();
            var cdr = new CdrWriter(writer);
            cdr.WriteInt32(42);
            cdr.WriteString("Test");
            cdr.Complete();
            
            // CRITICAL ASSERTION: Verify CdrSizer prediction matches actual output
            Assert.Equal(expectedSize, writer.WrittenCount);
        }
    }
}
