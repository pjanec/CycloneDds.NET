using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a class or struct as a DDS union.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class DdsUnionAttribute : Attribute
{
}
