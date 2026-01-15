using System;
using System.Collections.Immutable;
using System.Linq;

namespace CycloneDDS.Generator.Models
{
    internal sealed class GenerationInput : IEquatable<GenerationInput>
    {
        public ImmutableArray<SchemaTopicType> Topics { get; init; }
        public ImmutableArray<SchemaUnionType> Unions { get; init; }
        public ImmutableArray<GlobalTypeMapping> Mappings { get; init; }

        public bool Equals(GenerationInput? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return Topics.SequenceEqual(other.Topics) &&
                   Unions.SequenceEqual(other.Unions) &&
                   Mappings.SequenceEqual(other.Mappings);
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is GenerationInput other && Equals(other));
        }

        public override int GetHashCode()
        {
            int hash = 17;
            // Combined hash of all elements
            foreach(var t in Topics) hash = hash * 31 + t.GetHashCode();
            foreach(var u in Unions) hash = hash * 31 + u.GetHashCode();
            foreach(var m in Mappings) hash = hash * 31 + m.GetHashCode();
            return hash;
        }
    }
}
