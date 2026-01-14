using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a field or property as a key for the DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsKeyAttribute : Attribute
{
}
