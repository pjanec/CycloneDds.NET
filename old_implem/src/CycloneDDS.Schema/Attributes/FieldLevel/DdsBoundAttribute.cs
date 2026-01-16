using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies the maximum bound for strings or sequences.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsBoundAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum bound.
    /// </summary>
    public int Max { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsBoundAttribute"/> class.
    /// </summary>
    /// <param name="max">The maximum bound. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when max is less than or equal to 0.</exception>
    public DdsBoundAttribute(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max bound must be greater than 0.");
        }
        Max = max;
    }
}
