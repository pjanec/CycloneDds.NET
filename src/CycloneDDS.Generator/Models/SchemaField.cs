using System;

namespace CycloneDDS.Generator.Models
{
    internal sealed class SchemaField : IEquatable<SchemaField>
    {
        public required string Name { get; init; }
        public required string TypeName { get; init; } // Store type as string
        public required string DataType { get; init; } // e.g. "int", "string" - simplified for now
        
        public bool IsKey { get; set; }
        public bool IsOptional { get; set; }
        public int? Bound { get; set; }
        public int? Id { get; set; }

        public bool Equals(SchemaField? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && 
                   TypeName == other.TypeName && 
                   IsKey == other.IsKey && 
                   IsOptional == other.IsOptional && 
                   Bound == other.Bound && 
                   Id == other.Id;
        }

        public override bool Equals(object? obj) => 
            ReferenceEquals(this, obj) || (obj is SchemaField other && Equals(other));

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name.GetHashCode();
                hash = (hash * 397) ^ TypeName.GetHashCode();
                return hash;
            }
        }
    }
}
