using System;
using System.Text;

namespace CycloneDDS.Core
{
    /// <summary>
    /// Shadow writer that calculates sizes without writing bytes.
    /// MUST mirror CdrWriter API exactly for symmetric code generation.
    /// </summary>
    public ref struct CdrSizer
    {
        private int _cursor;
        
        public CdrSizer(int initialOffset)
        {
            _cursor = initialOffset;
        }
        
        public int Position => _cursor;

        public void Align(int alignment)
        {
            _cursor = AlignmentMath.Align(_cursor, alignment);
        }
        
        // Primitives (mirrors CdrWriter)
        public void WriteByte(byte value)
        {
            _cursor += 1;
        }
        
        public void WriteInt32(int value)
        {
            _cursor += 4;
        }
        
        public void WriteUInt32(uint value)
        {
            _cursor += 4;
        }
        
        public void WriteInt64(long value)
        {
            _cursor += 8;
        }
        
        public void WriteUInt64(ulong value)
        {
            _cursor += 8;
        }
        
        public void WriteFloat(float value)
        {
            _cursor += 4;
        }
        
        public void WriteDouble(double value)
        {
            _cursor += 8;
        }
        
        public void WriteString(ReadOnlySpan<char> value)
        {
            _cursor += 4; // Length (Int32)
            _cursor += Encoding.UTF8.GetByteCount(value);
            _cursor += 1; // NUL terminator
        }
        
        public void Skip(int bytes)
        {
            _cursor += bytes;
        }

        public void WriteFixedString(ReadOnlySpan<byte> utf8Bytes, int fixedSize)
        {
            _cursor += fixedSize;
        }

        public void WriteUInt8(byte value) => _cursor += 1;
        
        public void WriteInt8(sbyte value) => _cursor += 1;
        
        public void WriteBool(bool value) => _cursor += 1;

        public void WriteInt16(short value)
        {
            _cursor += 2;
        }

        public void WriteUInt16(ushort value)
        {
            _cursor += 2;
        }
        
        /// <summary>
        /// Returns size delta from initial offset.
        /// </summary>
        public int GetSizeDelta(int startOffset) => _cursor - startOffset;
    }
}
