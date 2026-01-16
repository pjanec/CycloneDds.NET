using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a field or property as the discriminator for a union.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsDiscriminatorAttribute : Attribute
{
}
