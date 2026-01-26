using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace CycloneDDS.Core
{
    public ref struct CdrWriter
    {
        private IBufferWriter<byte>? _output;
        private Span<byte> _span;
        private int _buffered;
        private int _totalWritten;
        private readonly CdrEncoding _encoding;

        public CdrEncoding Encoding => _encoding;
        public bool IsXcdr2 => _encoding == CdrEncoding.Xcdr2;

        // NEW: Zero-Alloc Constructor for Fixed Buffers
        public CdrWriter(Span<byte> buffer, CdrEncoding encoding = CdrEncoding.Xcdr1)
        {
            _output = null;  // Fixed buffer mode - no IBufferWriter
            _span = buffer;
            _buffered = 0;
            _totalWritten = 0;
            _encoding = encoding;
        }

        // EXISTING: Keep this for dynamic buffers
        public CdrWriter(IBufferWriter<byte> output, CdrEncoding encoding = CdrEncoding.Xcdr1)
        {
            _output = output;
            _span = output.GetSpan();
            _buffered = 0;
            _totalWritten = 0;
            _encoding = encoding;
        }

        public int Position => _totalWritten + _buffered;

        public void WriteBytes(ReadOnlySpan<byte> data)
        {
            EnsureSize(data.Length);
            data.CopyTo(_span.Slice(_buffered));
            _buffered += data.Length;
        }

        public void Align(int alignment)
        {
            int currentPos = Position - 4; // Adjust for Header
            int mask = alignment - 1;
            int padding = (alignment - (currentPos & mask)) & mask;
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

        public void WriteUInt32At(int offset, uint value)
        {
            // Only works if the offset refers to the current span
            // DdsWriter usage guarantees a single contiguous span for the whole message
            // or at least that we are patching something relatively recent.
            if (_output == null)
            {
                // Fixed buffer mode
                BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(offset), value);
            }
            else
            {
                // IBufferWriter mode - complicated. 
                // However, for this specific binding optimization work, we are focusing on the Span-based path.
                // If offset < _totalWritten, we can't patch.
                int relative = offset - _totalWritten;
                if (relative >= 0 && relative < _buffered)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(_span.Slice(relative), value);
                }
                else
                {
                    throw new NotSupportedException("Cannot patch memory that has been flushed or is out of range.");
                }
            }
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
            Console.WriteLine($"[CdrWriter] WriteDouble Val={value} @ {_buffered}");
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
            EnsureSize(sizeof(short));
            BinaryPrimitives.WriteInt16LittleEndian(_span.Slice(_buffered), value);
            _buffered += sizeof(short);
        }

        public void WriteUInt16(ushort value)
        {
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

        public void WriteString(ReadOnlySpan<char> value, bool? isXcdr2 = null)
        {
            int utf8Length = System.Text.Encoding.UTF8.GetByteCount(value);
            bool useXcdr2 = isXcdr2 ?? (_encoding == CdrEncoding.Xcdr2);
            Console.WriteLine($"[CdrWriter] WriteString Str='{value.ToString()}' Utf8Len={utf8Length} UseXcdr2={useXcdr2} @ {_buffered}");
            
            // EXPERIMENTAL FIX: CycloneDDS native seems to expect NUL-terminated strings even in XCDR2
            // causing "normalize_string: NUL check failed"
            // So we use XCDR1 style encoding (Len+1, NUL) for everything.
            if (false) // Disabled XCDR2 optimization (useXcdr2)
            {
                // XCDR2: Length is byte count. NO NUL terminator.
                WriteInt32(utf8Length);
                EnsureSize(utf8Length);
                int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
                _buffered += written;
            }
            else
            {
                // XCDR1 (Legacy) OR Patched XCDR2: Length is byte count + 1 (NUL). Includes NUL byte.
                int lengthToWrite = utf8Length + 1;
                WriteInt32(lengthToWrite);
                
                EnsureSize(utf8Length + 1);
                int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered));
                _buffered += written;
                
                _span[_buffered] = 0; // NUL terminator
                _buffered += 1;
            }
        }

        public void WriteFixedString(ReadOnlySpan<char> value, int internalLength)
        {
            // Writes string to a fixed-size byte buffer (char array in C)
            // No Length Header. Just bytes + NUL padding.
            
            // Calculate actual bytes needed for content
            int utf8Length = System.Text.Encoding.UTF8.GetByteCount(value);
            
            // Truncate if too long (should not happen if validated, but safe)
            // Actually verifying byte count vs length is hard without encoding, 
            // but we assume internalLength is the byte size of the array (e.g. 17).
            
            EnsureSize(internalLength);
            
            // Write available chars
            // Check if utf8Length > internalLength - 1 (must define NUL?)
            // If it's a fixed char array, usually it's "Zero terminated if shorter than Max, otherwise not"?
            // Or strictly always NUL terminated?
            // "string<16>" -> "char[17]" implies it strictly has room for NUL.
            int maxContent = internalLength - 1; 

            // Copy to a temp buffer first if needed? No, GetBytes writes to span.
            // But we can't write more than maxContent.
            // Simplified: Write, check length.
            
            // Optimization: If we trust byte count:
            if (utf8Length > maxContent)
            {
                 // Truncation needed logic (complex for UTF8), for now assume fit
                 // Or throw?
            }

            int written = System.Text.Encoding.UTF8.GetBytes(value, _span.Slice(_buffered, internalLength));
            
            // Pad remainder with 0
            if (written < internalLength)
            {
                 _span.Slice(_buffered + written, internalLength - written).Clear();
            }
            
            _buffered += internalLength;
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

        public void WriteFixedString(string value, int fixedSize)
        {
            EnsureSize(fixedSize);
            
            var dest = _span.Slice(_buffered, fixedSize);
            
            if (string.IsNullOrEmpty(value))
            {
                dest.Clear();
            }
            else
            {
                var chars = value.AsSpan();
                int byteCount = System.Text.Encoding.UTF8.GetByteCount(chars);
                
                if (byteCount <= fixedSize)
                {
                    int written = System.Text.Encoding.UTF8.GetBytes(chars, dest);
                    if (written < fixedSize)
                    {
                        dest.Slice(written).Clear();
                    }
                }
                else
                {
                    // Truncation required
                    var encoder = System.Text.Encoding.UTF8.GetEncoder();
                    encoder.Convert(chars, dest, true, out int charsUsed, out int bytesUsed, out bool completed);
                    
                    if (bytesUsed < fixedSize)
                    {
                        dest.Slice(bytesUsed).Clear();
                    }
                }
            }
            
            _buffered += fixedSize;
        }

        public void WriteGuid(Guid value)
        {
            EnsureSize(16);
            value.TryWriteBytes(_span.Slice(_buffered));
            _buffered += 16;
        }

        public void WriteDateTime(DateTime value)
        {
            WriteInt64(value.Ticks);
        }

        public void WriteDateTimeOffset(DateTimeOffset value)
        {
            // Serialize as 16 bytes: Ticks (8) + OffsetMinutes (2) + Padding (6)
            // Alignment required is 8.
            EnsureSize(16);
            BinaryPrimitives.WriteInt64LittleEndian(_span.Slice(_buffered), value.Ticks);
            BinaryPrimitives.WriteInt16LittleEndian(_span.Slice(_buffered + 8), (short)value.Offset.TotalMinutes);
            _span.Slice(_buffered + 10, 6).Clear(); // Padding
            _buffered += 16;
        }

        public void WriteTimeSpan(TimeSpan value)
        {
            WriteInt64(value.Ticks);
        }

        public void WriteVector2(System.Numerics.Vector2 value)
        {
            EnsureSize(8);
            System.Runtime.InteropServices.MemoryMarshal.Write(_span.Slice(_buffered), in value);
            _buffered += 8;
        }

        public void WriteVector3(System.Numerics.Vector3 value)
        {
            EnsureSize(12);
            System.Runtime.InteropServices.MemoryMarshal.Write(_span.Slice(_buffered), in value);
            _buffered += 12;
        }

        public void WriteVector4(System.Numerics.Vector4 value)
        {
            EnsureSize(16);
            System.Runtime.InteropServices.MemoryMarshal.Write(_span.Slice(_buffered), in value);
            _buffered += 16;
        }

        public void WriteQuaternion(System.Numerics.Quaternion value)
        {
            EnsureSize(16);
            System.Runtime.InteropServices.MemoryMarshal.Write(_span.Slice(_buffered), in value);
            _buffered += 16;
        }

        public void WriteMatrix4x4(System.Numerics.Matrix4x4 value)
        {
            EnsureSize(64);
            System.Runtime.InteropServices.MemoryMarshal.Write(_span.Slice(_buffered), in value);
            _buffered += 64;
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
            if (_output != null && _buffered > 0)
            {
                _output.Advance(_buffered);
                _totalWritten += _buffered;
                _buffered = 0;
            }
            // For fixed buffer, Complete() is no-op
        }

        private void EnsureSize(int size)
        {
            // If fixed buffer mode (_output == null)
            if (_output == null)
            {
                if (_buffered + size > _span.Length)
                    throw new InvalidOperationException(
                        $"CdrWriter buffer overflow. Needed {_buffered + size}, " +
                        $"Capacity {_span.Length}");
                return;
            }

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
