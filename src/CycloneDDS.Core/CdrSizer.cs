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
        private readonly CdrEncoding _encoding;
        
        public CdrSizer(int initialOffset, CdrEncoding encoding = CdrEncoding.Xcdr1)
        {
            _cursor = initialOffset;
            _encoding = encoding;
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
        
        public void WriteString(ReadOnlySpan<char> value, bool? isXcdr2 = null)
        {
            // Note: CdrWriter.WriteInt32 does NOT align internally.
            // SerializerEmitter generates explicit Align(4) calls before writing strings.
            _cursor += 4; // Length (Int32)
            _cursor += Encoding.UTF8.GetByteCount(value);
            
            bool useXcdr2 = isXcdr2 ?? (_encoding == CdrEncoding.Xcdr2);
            // EXPERIMENTAL FIX: Always add NUL terminator space (matching CdrWriter patch)
            if (true) // (!useXcdr2)
            {
                _cursor += 1; // NUL terminator
            }
        }
        
        public void Skip(int bytes)
        {
            _cursor += bytes;
        }

        public void WriteFixedString(ReadOnlySpan<byte> utf8Bytes, int fixedSize)
        {
            _cursor += fixedSize;
        }

        public void WriteFixedString(string value, int fixedSize)
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

        public void WriteGuid(Guid value) => _cursor += 16;
        public void WriteDateTime(DateTime value) => _cursor += 8;
        public void WriteDateTimeOffset(DateTimeOffset value) => _cursor += 16;
        public void WriteTimeSpan(TimeSpan value) => _cursor += 8;
        public void WriteVector2(System.Numerics.Vector2 value) => _cursor += 8;
        public void WriteVector3(System.Numerics.Vector3 value) => _cursor += 12;
        public void WriteVector4(System.Numerics.Vector4 value) => _cursor += 16;
        public void WriteQuaternion(System.Numerics.Quaternion value) => _cursor += 16;
        public void WriteMatrix4x4(System.Numerics.Matrix4x4 value) => _cursor += 64;
        
        /// <summary>
        /// Returns size delta from initial offset.
        /// </summary>
        public int GetSizeDelta(int startOffset) => _cursor - startOffset;
    }
}
