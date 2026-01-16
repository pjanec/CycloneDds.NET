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
            EnsureSize(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(int);
        }

        public void WriteUInt32(uint value)
        {
            EnsureSize(sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(uint);
        }

        public void WriteInt64(long value)
        {
            EnsureSize(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(long);
        }

        public void WriteUInt64(ulong value)
        {
            EnsureSize(sizeof(ulong));
            BinaryPrimitives.WriteUInt64LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(ulong);
        }

        public void WriteFloat(float value)
        {
            EnsureSize(sizeof(float));
            int val = BitConverter.SingleToInt32Bits(value);
            BinaryPrimitives.WriteInt32LittleEndian(_span.Slice(_buffered), val);
            _buffered += sizeof(float);
        }

        public void WriteDouble(double value)
        {
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
