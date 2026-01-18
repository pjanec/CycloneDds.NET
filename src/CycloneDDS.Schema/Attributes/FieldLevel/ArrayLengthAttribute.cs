using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the fixed length of an array.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class ArrayLengthAttribute : Attribute
{
    /// <summary>
    /// Gets the fixed length of the array.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The fixed length of the array.</param>
    public ArrayLengthAttribute(int length)
    {
        Length = length;
    }
}
