using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Overrides the default IDL type name for the decorated type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false)]
public sealed class DdsTypeNameAttribute : Attribute
{
    /// <summary>
    /// Gets the IDL type name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsTypeNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The IDL type name. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when name is null or whitespace.</exception>
    public DdsTypeNameAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Type name cannot be null or whitespace.", nameof(name));
        }
        Name = name;
    }
}
