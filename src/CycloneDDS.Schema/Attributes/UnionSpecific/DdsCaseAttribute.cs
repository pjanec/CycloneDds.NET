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
    /// Gets all discriminator values of this case (an IDL case with several labels:
    /// <c>case 2: case 3:</c> is <c>[DdsCase(2, 3)]</c>).
    /// </summary>
    public object[] Values { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsCaseAttribute"/> class.
    /// </summary>
    /// <param name="value">The discriminator value. Must be compatible with the discriminator type.</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public DdsCaseAttribute(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Values = new[] { value };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsCaseAttribute"/> class for a case with several labels.
    /// </summary>
    /// <param name="values">The discriminator values. Each must be compatible with the discriminator type.</param>
    /// <exception cref="ArgumentException">Thrown when no value is given or a value is null.</exception>
    public DdsCaseAttribute(params object[] values)
    {
        if (values == null || values.Length == 0) throw new ArgumentException("At least one case value is required.", nameof(values));
        foreach (var v in values)
            if (v == null) throw new ArgumentException("Case values must not be null.", nameof(values));
        Values = values;
        Value = values[0];
    }
}
