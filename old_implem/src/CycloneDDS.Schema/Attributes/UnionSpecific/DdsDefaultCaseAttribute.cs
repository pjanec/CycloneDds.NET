using System;

namespace CycloneDDS.Schema;

/// <summary>
/// Marks a union arm as the default case to match any unmatched discriminator values.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class DdsDefaultCaseAttribute : Attribute
{
}
