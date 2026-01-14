using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Associates a union arm with a specific discriminator value.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsCaseAttribute : Attribute
{
    /// <summary>
    /// Gets the discriminator value associated with this case.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsCaseAttribute"/> class.
    /// </summary>
    /// <param name="value">The discriminator value. Must be compatible with the discriminator type.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public DdsCaseAttribute(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
