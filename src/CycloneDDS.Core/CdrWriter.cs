using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace CycloneDDS.Core
{
    public ref struct CdrWriter
    {
        private IBufferWriter<byte> _output;
        private Span<byte> _span;
        private int _buffered;
        private int _totalWritten;

        public CdrWriter(IBufferWriter<byte> output)
        {
            _output = output;
            _span = output.GetSpan();
            _buffered = 0;
            _totalWritten = 0;
        }

        public int Position => _totalWritten + _buffered;

        public void Align(int alignment)
        {
            int currentPos = Position;
            int padding = (alignment - (currentPos % alignment)) & (alignment - 1);
            if (padding > 0)
            {
                EnsureSize(padding);
                _span.Slice(_buffered, padding).Clear();
                _buffered += padding;
            }
        }

        public void WriteInt32(int value)
        {
            Align(4);
            EnsureSize(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(int);
        }

        public void WriteUInt32(uint value)
        {
            Align(4);
            EnsureSize(sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(uint);
        }

        public void WriteInt64(long value)
        {
            Align(8);
            EnsureSize(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(long);
        }

        public void WriteUInt64(ulong value)
        {
            Align(8);
            EnsureSize(sizeof(ulong));
            BinaryPrimitives.WriteUInt64LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(ulong);
        }

        public void WriteFloat(float value)
        {
            Align(4);
            EnsureSize(sizeof(float));
            int val = BitConverter.SingleToInt32Bits(value);
            BinaryPrimitives.WriteInt32LittleEndian(_span.Slice(_buffered), val);
            _buffered += sizeof(float);
        }

        public void WriteDouble(double value)
        {
            Align(8);
            EnsureSize(sizeof(double));
            long val = BitConverter.DoubleToInt64Bits(value);
            BinaryPrimitives.WriteInt64LittleEndian(_span.Slice(_buffered), val);
            _buffered += sizeof(double);
        }

        public void WriteByte(byte value)
        {
            EnsureSize(sizeof(byte));
            _span[_buffered] = value;
            _buffered += sizeof(byte);
        }

        public void WriteUInt8(byte value) => WriteByte(value);

        public void WriteInt8(sbyte value)
        {
            EnsureSize(sizeof(sbyte));
            _span[_buffered] = (byte)value;
            _buffered += sizeof(sbyte);
        }

        public void WriteInt16(short value)
        {
            Align(2);
            EnsureSize(sizeof(short));
            BinaryPrimitives.WriteInt16LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(short);
        }

        public void WriteUInt16(ushort value)
        {
            Align(2);
            EnsureSize(sizeof(ushort));
            BinaryPrimitives.WriteUInt16LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(ushort);
        }

        public void WriteBool(bool value)
        {
            EnsureSize(sizeof(byte));
            _span[_buffered] = value ? (byte)1 : (byte)0;
            _buffered += sizeof(byte);
        }

        public void WriteString(ReadOnlySpan<char> value)
        {
            int utf8Length = Encoding.UTF8.GetByteCount(value);
            int totalLength = utf8Length + 1; // +1 for NUL
            
            WriteInt32(totalLength);
            
            EnsureSize(totalLength);
            int written = Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
            _buffered += written;
            _span[_buffered] = 0; // NUL terminator
            _buffered += 1;
        }

        public void WriteFixedString(ReadOnlySpan<byte> utf8Bytes, int fixedSize)
        {
            EnsureSize(fixedSize);
            
            int toCopy = Math.Min(utf8Bytes.Length, fixedSize);
            utf8Bytes.Slice(0, toCopy).CopyTo(_span.Slice(_buffered));
            
            if (toCopy < fixedSize)
            {
                _span.Slice(_buffered + toCopy, fixedSize - toCopy).Clear();
            }
            
            _buffered += fixedSize;
        }

        public void PatchUInt32(int position, uint value)
        {
            // Check if position is in the currently buffered span
            if (position >= _totalWritten)
            {
                int offset = position - _totalWritten;
                if (offset + sizeof(uint) <= _buffered)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(offset), value);
                    return;
                }
            }
            
            throw new NotSupportedException($"Cannot patch UInt32 at position {position}. Buffer might have been flushed or advanced.");
        }

        public void Complete()
        {
            if (_buffered > 0)
            {
                _output.Advance(_buffered);
                _totalWritten += _buffered;
                _buffered = 0;
                _span = default; // Make sure we don't use old span unless we get it again, though next call to GetSpan is needed. 
                // But this struct is likely disposed or done after Complete.
            }
        }

        private void EnsureSize(int size)
        {
            if (_buffered + size > _span.Length)
            {
                _output.Advance(_buffered);
                _totalWritten += _buffered;
                _buffered = 0;
                // Ask for at least 'size'. BufferWriter might give more.
                _span = _output.GetSpan(size); 
            }
        }
    }
}
