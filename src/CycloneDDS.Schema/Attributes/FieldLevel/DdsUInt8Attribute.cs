using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a <c>byte</c> field (or a <c>List&lt;byte&gt;</c>, <c>byte[]</c> or <c>fixed byte</c> buffer) as IDL
/// <c>uint8</c> rather than <c>octet</c>. Both are one unsigned byte; they are different types to XTypes, so
/// this must match a peer whose IDL says <c>uint8</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsUInt8Attribute : Attribute
{
}
