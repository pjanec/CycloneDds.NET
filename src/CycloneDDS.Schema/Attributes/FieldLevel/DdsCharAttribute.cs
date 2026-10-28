using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a <c>byte</c> or <c>sbyte</c> field (or a <c>List&lt;byte&gt;</c>, <c>byte[]</c> or <c>fixed byte</c> buffer)
/// as IDL <c>char</c> rather than <c>octet</c>/<c>int8</c>. The memory layout is the same (one byte); the
/// difference is the type identity, which must match a peer whose IDL says <c>char</c>.
/// A C# <c>char</c> field cannot be used for this: it is two bytes.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsCharAttribute : Attribute
{
}
