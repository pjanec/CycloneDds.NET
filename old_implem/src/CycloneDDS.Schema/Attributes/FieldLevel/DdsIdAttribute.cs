using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Specifies an explicit member ID for a field or property.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsIdAttribute : Attribute
{
    /// <summary>
    /// Gets the member ID.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsIdAttribute"/> class.
    /// </summary>
    /// <param name="id">The explicit member ID.</param>
    public DdsIdAttribute(int id)
    {
        Id = id;
    }
}
