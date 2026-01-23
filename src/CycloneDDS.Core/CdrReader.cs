using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace CycloneDDS.Core
{
    public ref struct CdrReader
    {
        private ReadOnlySpan<byte> _data;
        private int _position;
        private readonly CdrEncoding _encoding;

        public CdrEncoding Encoding => _encoding;
        public bool IsXcdr2 => _encoding == CdrEncoding.Xcdr2;

        public CdrReader(ReadOnlySpan<byte> data, CdrEncoding? encoding = null)
        {
            _data = data;
            _position = 0;
            
            if (encoding.HasValue)
            {
                _encoding = encoding.Value;
            }
            else
            {
                // Auto-detect
                _encoding = CdrEncoding.Xcdr1;
                if (data.Length >= 2)
                {
                    // Check byte 1. If >= 6, it's XCDR2. 
                    // This assumes standard CDR/XCDR header is present at start of buffer.
                    if (data[1] >= 6)
                    {
                        _encoding = CdrEncoding.Xcdr2;
                    }
                }
            }
        }

        public int Position => _position;
        public int Remaining => _data.Length - _position;

        public void Align(int alignment)
        {
            int currentPos = _position;
            int padding = (alignment - (currentPos % alignment)) & (alignment - 1);
            if (padding > 0)
            {
                if (_position + padding > _data.Length)
                    throw new IndexOutOfRangeException("Not enough data to align");
                _position += padding;
            }
        }

        public int ReadInt32()
        {
            if (_position + sizeof(int) > _data.Length)
                throw new IndexOutOfRangeException();
            
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position));
            _position += sizeof(int);
            return value;
        }

        public uint ReadUInt32()
        {
            if (_position + sizeof(uint) > _data.Length)
                throw new IndexOutOfRangeException();
            
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position));
            _position += sizeof(uint);
            return value;
        }

        public long ReadInt64()
        {
            if (_position + sizeof(long) > _data.Length)
                throw new IndexOutOfRangeException();
            long value = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_position));
            _position += sizeof(long);
            return value;
        }

        public ulong ReadUInt64()
        {
            if (_position + sizeof(ulong) > _data.Length)
                throw new IndexOutOfRangeException();
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_position));
            _position += sizeof(ulong);
            return value;
        }

        public float ReadFloat()
        {
            if (_position + sizeof(float) > _data.Length)
                throw new IndexOutOfRangeException();
            int val = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position));
            _position += sizeof(float);
            return BitConverter.Int32BitsToSingle(val);
        }

        public double ReadDouble()
        {
            if (_position + sizeof(double) > _data.Length)
                throw new IndexOutOfRangeException();
            long val = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(_position));
            _position += sizeof(double);
            return BitConverter.Int64BitsToDouble(val);
        }

        public byte ReadByte()
        {
            if (_position + 1 > _data.Length)
                throw new IndexOutOfRangeException();
            byte value = _data[_position];
            _position += 1;
            return value;
        }
        
        public byte ReadUInt8() => ReadByte();
        
        public sbyte ReadInt8() => (sbyte)ReadByte();
        
        public bool ReadBoolean() => ReadByte() != 0;

        public short ReadInt16()
        {
            if (_position + sizeof(short) > _data.Length)
                throw new IndexOutOfRangeException();
            short value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(_position));
            _position += sizeof(short);
            return value;
        }

        public ushort ReadUInt16()
        {
            if (_position + sizeof(ushort) > _data.Length)
                throw new IndexOutOfRangeException();
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position));
            _position += sizeof(ushort);
            return value;
        }

        public ReadOnlySpan<byte> ReadStringBytes(bool? isXcdr2 = null)
        {
            // Read length (4 bytes)
            int length = ReadInt32(); // count including NUL in XCDR1, excluding NUL in XCDR2
            
            if (_position + length > _data.Length)
                throw new IndexOutOfRangeException("Not enough data for string");
            
            bool useXcdr2 = isXcdr2 ?? (_encoding == CdrEncoding.Xcdr2);
            int bytesToReturn;
            if (useXcdr2)
            {
                // XCDR2: Length is exactly the number of bytes
                bytesToReturn = length;
            }
            else
            {
                // XCDR1: Length includes NUL terminator
                bytesToReturn = length > 0 ? length - 1 : 0;
            }
            
            var span = _data.Slice(_position, bytesToReturn);
            _position += length;
            return span;
        }

        public string ReadString(bool? isXcdr2 = null)
        {
            var span = ReadStringBytes(isXcdr2);
            return System.Text.Encoding.UTF8.GetString(span);
        }

        public ReadOnlySpan<byte> ReadFixedBytes(int count)
        {
            if (_position + count > _data.Length)
                throw new IndexOutOfRangeException();
            
            var span = _data.Slice(_position, count);
            _position += count;
            return span;
        }

        public Guid ReadGuid()
        {
            if (_position + 16 > _data.Length)
                throw new IndexOutOfRangeException();
            
            var value = new Guid(_data.Slice(_position, 16));
            _position += 16;
            return value;
        }

        public DateTime ReadDateTime()
        {
            return new DateTime(ReadInt64());
        }

        public DateTimeOffset ReadDateTimeOffset()
        {
             long ticks = ReadInt64();
             short offsetMin = ReadInt16();
             // padding 6 bytes
             if (_position + 6 > _data.Length) 
                 throw new IndexOutOfRangeException();
             _position += 6;
             
             return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMin));
        }

        public TimeSpan ReadTimeSpan()
        {
            return new TimeSpan(ReadInt64());
        }

        public System.Numerics.Vector2 ReadVector2()
        {
             if (_position + 8 > _data.Length) throw new IndexOutOfRangeException();
             var val = System.Runtime.InteropServices.MemoryMarshal.Read<System.Numerics.Vector2>(_data.Slice(_position));
             _position += 8;
             return val;
        }

        public System.Numerics.Vector3 ReadVector3()
        {
             if (_position + 12 > _data.Length) throw new IndexOutOfRangeException();
             var val = System.Runtime.InteropServices.MemoryMarshal.Read<System.Numerics.Vector3>(_data.Slice(_position));
             _position += 12;
             return val;
        }

        public System.Numerics.Vector4 ReadVector4()
        {
             if (_position + 16 > _data.Length) throw new IndexOutOfRangeException();
             var val = System.Runtime.InteropServices.MemoryMarshal.Read<System.Numerics.Vector4>(_data.Slice(_position));
             _position += 16;
             return val;
        }

        public System.Numerics.Quaternion ReadQuaternion()
        {
             if (_position + 16 > _data.Length) throw new IndexOutOfRangeException();
             var val = System.Runtime.InteropServices.MemoryMarshal.Read<System.Numerics.Quaternion>(_data.Slice(_position));
             _position += 16;
             return val;
        }

        public System.Numerics.Matrix4x4 ReadMatrix4x4()
        {
             if (_position + 64 > _data.Length) throw new IndexOutOfRangeException();
             var val = System.Runtime.InteropServices.MemoryMarshal.Read<System.Numerics.Matrix4x4>(_data.Slice(_position));
             _position += 64;
             return val;
        }

        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
                throw new IndexOutOfRangeException();
            _position = position;
        }
    }
}
