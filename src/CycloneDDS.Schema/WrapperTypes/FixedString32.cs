using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CycloneDDS.Schema;

/// <summary>
/// A fixed-size string with a maximum capacity of 32 bytes (UTF-8 encoded, NUL-terminated).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct FixedString32
{
    private fixed byte _buffer[32];
    private int _length;

    /// <summary>
    /// Gets the maximum capacity in bytes (32).
    /// </summary>
    public const int Capacity = 32;

    /// <summary>
    /// Gets the actual length of the string in bytes.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Creates a new FixedString32 from a string. Throws if too long or invalid UTF-8.
    /// </summary>
    /// <param name="value">The source string.</param>
    /// <exception cref="ArgumentException">Thrown if the UTF-8 representation exceeds capacity.</exception>
    /// <exception cref="ArgumentException">Thrown if the string contains invalid UTF-8 characters.</exception>
    public FixedString32(string value)
    {
        if (!TryFrom(value, out this))
        {
            // TryFrom returns false if too long. 
            // We need to differentiate for the exception message strictly speaking,
            // but effectively here we just say it failed. 
            // To be precise let's double check.
            int count;
            try
            {
                count = StrictUtf8.GetByteCount(value ?? string.Empty);
            }
            catch (EncoderFallbackException)
            {
                throw new ArgumentException("String contains invalid UTF-8 characters.", nameof(value));
            }

            if (count > Capacity)
                throw new ArgumentException($"String provided is too long ({count} bytes) for FixedString32.", nameof(value));
            
            throw new ArgumentException("String conversion failed.", nameof(value));
        }
    }

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// Tries to create a FixedString32 from a string.
    /// </summary>
    /// <param name="value">The source string.</param>
    /// <param name="result">The resulting FixedString32.</param>
    /// <returns>True if successful; otherwise false (e.g. if too long or invalid UTF-8).</returns>
    public static bool TryFrom(string value, out FixedString32 result)
    {
        result = default;
        if (value == null) return true; // Treat null as empty

        try
        {
            int byteCount = StrictUtf8.GetByteCount(value);
            if (byteCount > Capacity) return false;

            fixed (char* charPtr = value)
            {
                fixed (byte* destPtr = result._buffer)
                {
                    StrictUtf8.GetBytes(charPtr, value.Length, destPtr, Capacity);
                }
            }
            
            result._length = byteCount;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the string as a read-only span of bytes.
    /// </summary>
    /// <returns>A ReadOnlySpan of bytes representing the UTF-8 content.</returns>
    public ReadOnlySpan<byte> AsUtf8Span()
    {
         fixed (byte* ptr = _buffer)
         {
             return new ReadOnlySpan<byte>(ptr, _length);
         }
    }

    /// <summary>
    /// Allocates a new string from the buffer.
    /// </summary>
    /// <returns>The string representation.</returns>
    public string ToStringAllocated()
    {
#if NETSTANDARD2_0
        return Encoding.UTF8.GetString(AsUtf8Span().ToArray());
#else
        return Encoding.UTF8.GetString(AsUtf8Span());
#endif
    }

    /// <inheritdoc/>
    public override string ToString() => ToStringAllocated();
}
