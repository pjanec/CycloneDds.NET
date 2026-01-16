using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the fixed length of an array.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class ArrayLengthAttribute : Attribute
{
    public int Length { get; }

    public ArrayLengthAttribute(int length)
    {
        Length = length;
    }
}
