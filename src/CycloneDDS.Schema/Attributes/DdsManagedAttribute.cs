using System;

namespace CycloneDDS.Schema
{
    /// <summary>
    /// Explicitly marks a member or type as managed, allowing GC allocations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class DdsManagedAttribute : Attribute
    {
    }
}
