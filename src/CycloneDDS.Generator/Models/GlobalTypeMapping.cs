using System;
using CycloneDDS.Schema;

namespace CycloneDDS.Generator.Models
{
    internal sealed class GlobalTypeMapping : IEquatable<GlobalTypeMapping>
    {
        public required string SourceTypeName { get; init; }
        public required DdsWire WireKind { get; init; }
        public required string DefinitionName { get; init; }

        public bool Equals(GlobalTypeMapping? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return DefinitionName == other.DefinitionName && WireKind == other.WireKind;
        }

        public override bool Equals(object? obj) => 
            ReferenceEquals(this, obj) || (obj is GlobalTypeMapping other && Equals(other));

        public override int GetHashCode() => 
            (DefinitionName.GetHashCode() * 397) ^ WireKind.GetHashCode();
    }
}
