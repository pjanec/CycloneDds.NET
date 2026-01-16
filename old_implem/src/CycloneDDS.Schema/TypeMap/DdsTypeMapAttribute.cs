using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Registers a global type mapping for the assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class DdsTypeMapAttribute : Attribute
{
    /// <summary>
    /// Gets the source .NET type to map.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the wire representation kind.
    /// </summary>
    public DdsWire WireKind { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsTypeMapAttribute"/> class.
    /// </summary>
    /// <param name="sourceType">The source .NET type.</param>
    /// <param name="wireKind">The target wire representation.</param>
    /// <exception cref="ArgumentNullException">Thrown when sourceType is null.</exception>
    public DdsTypeMapAttribute(Type sourceType, DdsWire wireKind)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        WireKind = wireKind;
    }
}
