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

        public CdrReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
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

        public ReadOnlySpan<byte> ReadStringBytes()
        {
            // Read length (4 bytes)
            int length = ReadInt32(); // count including NUL
            
            if (_position + length > _data.Length)
                throw new IndexOutOfRangeException("Not enough data for string");
            
            // The bytes are at _position.
            // We want bytes excluding NUL terminator.
            // length includes NUL.
            int bytesToReturn = length > 0 ? length - 1 : 0;
            
            var span = _data.Slice(_position, bytesToReturn);
            _position += length;
            return span;
        }

        public string ReadString()
        {
            var span = ReadStringBytes();
            return Encoding.UTF8.GetString(span);
        }

        public ReadOnlySpan<byte> ReadFixedBytes(int count)
        {
            if (_position + count > _data.Length)
                throw new IndexOutOfRangeException();
            
            var span = _data.Slice(_position, count);
            _position += count;
            return span;
        }

        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
                throw new IndexOutOfRangeException();
            _position = position;
        }
    }
}
