using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Memory;

/// <summary>
/// Arena allocator for efficient, GC-free memory allocation.
/// Uses bump-pointer allocation with geometric growth.
/// </summary>
public sealed class Arena : IDisposable
{
    private const int DefaultInitialCapacity = 4096;
    private const int MaxRetainedCapacity = 1024 * 1024; // 1MB default
    
    private IntPtr _buffer = IntPtr.Zero;
    private int _capacity = 0;
    private int _position = 0;
    private bool _disposed = false;
    
    public Arena() : this(DefaultInitialCapacity) { }
    
    public Arena(int initialCapacity)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        
        _capacity = initialCapacity;
        _buffer = Marshal.AllocHGlobal(initialCapacity);
    }
    
    /// <summary>
    /// Allocate bytes. Returns IntPtr to allocated memory.
    /// </summary>
    public IntPtr Allocate(int size)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Arena));
        
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        
        // Align to 8 bytes
        size = (size + 7) & ~7;
        
        // Check if we need to grow
        if (_position + size > _capacity)
        {
            Grow(size);
        }
        
        var ptr = IntPtr.Add(_buffer, _position);
        _position += size;
        return ptr;
    }
    
    /// <summary>
    /// Allocate typed array. Returns pointer to T[count].
    /// </summary>
    public unsafe IntPtr Allocate<T>(int count) where T : unmanaged
    {
        return Allocate(sizeof(T) * count);
    }
    
    /// <summary>
    /// Reset arena position to 0, reusing buffer.
    /// </summary>
    public void Reset()
    {
        _position = 0;
    }
    
    /// <summary>
    /// Get current mark for rewind.
    /// </summary>
    public int GetMark() => _position;
    
    /// <summary>
    /// Rewind to previous mark.
    /// </summary>
    public void Rewind(int mark)
    {
        if (mark < 0 || mark > _position)
            throw new ArgumentOutOfRangeException(nameof(mark));
        _position = mark;
    }
    
    /// <summary>
    /// Trim buffer if over MaxRetainedCapacity.
    /// </summary>
    public void Trim()
    {
        if (_capacity > MaxRetainedCapacity)
        {
            Marshal.FreeHGlobal(_buffer);
            _capacity = MaxRetainedCapacity;
            _buffer = Marshal.AllocHGlobal(_capacity);
            _position = 0;
        }
    }
    
    private void Grow(int requiredSize)
    {
        // Geometric growth: 2x capacity or required size, whichever is larger
        var newCapacity = Math.Max(_capacity * 2, _capacity + requiredSize);
        
        var newBuffer = Marshal.AllocHGlobal(newCapacity);
        
        // Copy existing data
        if (_position > 0)
        {
            unsafe
            {
                Buffer.MemoryCopy(_buffer.ToPointer(), newBuffer.ToPointer(), newCapacity, _position);
            }
        }
        
        Marshal.FreeHGlobal(_buffer);
        _buffer = newBuffer;
        _capacity = newCapacity;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_buffer);
                _buffer = IntPtr.Zero;
            }
            _disposed = true;
        }
    }
    
    // Properties for testing
    public int Capacity => _capacity;
    public int Position => _position;
    public int Available => _capacity - _position;
}
