using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the maximum length of a string or sequence.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class MaxLengthAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum length of the string or sequence.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaxLengthAttribute"/> class.
    /// </summary>
    /// <param name="length">The maximum length.</param>
    public MaxLengthAttribute(int length)
    {
        Length = length;
    }
}
