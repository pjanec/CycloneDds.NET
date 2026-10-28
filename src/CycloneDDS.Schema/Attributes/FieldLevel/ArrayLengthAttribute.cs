using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the dimensions of a fixed-size array.
/// </summary>
/// <remarks>
/// On a C# <c>fixed</c> buffer it gives the IDL dimensions of a multi-dimensional array:
/// <c>[ArrayLength(2, 3)] public fixed double M[6];</c> is IDL <c>double M[2][3]</c> (row-major, so the
/// memory layout is the flat buffer's). The product of the dimensions must equal the buffer size.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class ArrayLengthAttribute : Attribute
{
    /// <summary>
    /// Gets the total number of elements (the product of <see cref="Dimensions"/>).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the dimensions, outermost first.
    /// </summary>
    public int[] Dimensions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The fixed length of the array.</param>
    public ArrayLengthAttribute(int length)
    {
        Length = length;
        Dimensions = new[] { length };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayLengthAttribute"/> class for a multi-dimensional array.
    /// </summary>
    /// <param name="dimensions">The dimensions, outermost first.</param>
    public ArrayLengthAttribute(params int[] dimensions)
    {
        Dimensions = dimensions ?? throw new ArgumentNullException(nameof(dimensions));
        int length = 1;
        foreach (var d in dimensions) length *= d;
        Length = length;
    }
}
