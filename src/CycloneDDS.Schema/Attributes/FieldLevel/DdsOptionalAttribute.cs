using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a field or property as optional.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsOptionalAttribute : Attribute
{
}
