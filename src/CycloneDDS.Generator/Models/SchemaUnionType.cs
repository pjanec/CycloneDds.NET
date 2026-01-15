using System;

namespace CycloneDDS.Generator.Models
{
    internal sealed class SchemaUnionType : IEquatable<SchemaUnionType>
    {
        public required string Namespace { get; init; }
        public required string TypeName { get; init; }
        public required string DefinitionName { get; init; }

        public bool Equals(SchemaUnionType? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return DefinitionName == other.DefinitionName;
        }

        public override bool Equals(object? obj) => 
            ReferenceEquals(this, obj) || (obj is SchemaUnionType other && Equals(other));

        public override int GetHashCode() => DefinitionName.GetHashCode();
    }
}
