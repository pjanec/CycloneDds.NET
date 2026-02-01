using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.CompilerServices;

namespace CycloneDDS.Core
{
    /// <summary>
    /// Manages the "Tail" region of the memory block, allocating space for variable-length data.
    /// This struct guarantees that all allocations are within the fixed buffer provided.
    /// </summary>
    public ref struct NativeArena
    {
        private readonly Span<byte> _buffer;
        private readonly IntPtr _baseAddress;
        private int _tail;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeArena"/> struct.
        /// Zeros out the HEAD region [0..headSize).
        /// </summary>
        /// <param name="buffer">The full memory block (Head + Tail).</param>
        /// <param name="baseAddress">The pinned address of the buffer start.</param>
        /// <param name="headSize">The size of the fixed HEAD struct.</param>
        public NativeArena(Span<byte> buffer, IntPtr baseAddress, int headSize)
        {
            if (headSize > buffer.Length)
            {
                throw new IndexOutOfRangeException("Head size exceeds buffer length.");
            }

            _buffer = buffer;
            _baseAddress = baseAddress;
            _tail = headSize;

            // Zero out the HEAD region to ensure clean state
            buffer.Slice(0, headSize).Clear();
        }

        /// <summary>
        /// Allocates a UTF-8 string in the arena.
        /// </summary>
        /// <param name="text">The string to allocate.</param>
        /// <returns>Pointer to the null-terminated UTF-8 string, or IntPtr.Zero if text is null.</returns>
        public IntPtr CreateString(string? text)
        {
            if (text == null)
            {
                return IntPtr.Zero;
            }

            AlignCore(8); // Align string allocation to 8 bytes for consistency with GetNativeSize 

            int byteCount = Encoding.UTF8.GetByteCount(text);
            int size = byteCount + 1; // +1 for null terminator

            int offset = _tail;
            if (_tail + size > _buffer.Length)
            {
                throw new IndexOutOfRangeException("Insufficient buffer space for string.");
            }

            int written = Encoding.UTF8.GetBytes(text, _buffer.Slice(offset));
            _buffer[offset + written] = 0; // Null terminator

            _tail += size;
            
            IntPtr ptr = _baseAddress + offset;
            Console.WriteLine($"CreateString: '{text}' at {ptr} (Offset {offset})");
            return ptr;
        }

        private void AlignCore(int alignment)
        {
            int remainder = _tail % alignment;
            if (remainder != 0)
            {
                int padding = alignment - remainder;
                if (_tail + padding > _buffer.Length)
                {
                     throw new IndexOutOfRangeException("Insufficient buffer space for alignment.");
                }
                // Zero out padding? Not strictly necessary as we shouldn't read it, but good practice.
                _buffer.Slice(_tail, padding).Clear();
                _tail += padding;
            }
        }

        /// <summary>
        /// Creates a native DDS sequence from a span of primitives.
        /// Copying is done efficiently.
        /// </summary>
        public DdsSequenceNative CreateSequence<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            AlignCore(8); // Align the sequence data to 8 bytes (max primitive alignment)

            if (data.IsEmpty)
            {
                return new DdsSequenceNative
                {
                    Maximum = 0,
                    Length = 0,
                    Buffer = IntPtr.Zero
                };
            }

            int elementSize = Unsafe.SizeOf<T>();
            int totalSize = data.Length * elementSize;

            if (_tail + totalSize > _buffer.Length)
            {
                throw new IndexOutOfRangeException($"Insufficient buffer space for sequence of {typeof(T).Name}.");
            }

            int offset = _tail;
            
            // Copy data
            Span<byte> dest = _buffer.Slice(offset, totalSize);
            MemoryMarshal.Cast<T, byte>(data).CopyTo(dest);

            _tail += totalSize;

            return new DdsSequenceNative
            {
                Maximum = (uint)data.Length,
                Length = (uint)data.Length,
                Buffer = _baseAddress + offset
            };
        }

        /// <summary>
        /// Allocates an array of native structs in the arena.
        /// </summary>
        public Span<TNative> AllocateArray<TNative>(int count) where TNative : unmanaged
        {
            AlignCore(8); 

            int elementSize = Unsafe.SizeOf<TNative>();
            int totalSize = count * elementSize;

            if (_tail + totalSize > _buffer.Length)
            {
                throw new IndexOutOfRangeException($"Insufficient buffer space for array of {typeof(TNative).Name}.");
            }

            int offset = _tail;
            
            // Zero out memory
            Span<byte> memory = _buffer.Slice(offset, totalSize);
            memory.Clear();

            _tail += totalSize;

            return MemoryMarshal.Cast<byte, TNative>(memory);
        }
    }
}
