using System;
using System.Linq; // Required for SequenceEqual
using System.Collections.Immutable;
using CycloneDDS.Schema;

namespace CycloneDDS.Generator.Models
{
    internal sealed class SchemaTopicType : IEquatable<SchemaTopicType>
    {
        public required string Namespace { get; init; }
        public required string TypeName { get; init; }
        public required string TopicName { get; init; }
        public required string DefinitionName { get; init; }

        public DdsQosSettings? Qos { get; set; }
        public ImmutableArray<SchemaField> Fields { get; set; }
        public ImmutableArray<int> KeyFieldIndices { get; set; }

        public bool Equals(SchemaTopicType? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            // Check basic strings
            if (Namespace != other.Namespace)
            {
               // throw new Exception($"Namespace diff: '{Namespace}' vs '{other.Namespace}'");
               return false;
            }
            if (TypeName != other.TypeName ||
                TopicName != other.TopicName ||
                DefinitionName != other.DefinitionName)
            {
                return false;
            }

            // Check QoS object equality (record handles nulls and value comparison)
            if (!Equals(Qos, other.Qos)) return false;

            // CRITICAL: Use SequenceEqual for ImmutableArrays
            // Default equality for ImmutableArray is Reference Equality, which fails here.
            if (!Fields.SequenceEqual(other.Fields)) return false;
            if (!KeyFieldIndices.SequenceEqual(other.KeyFieldIndices)) return false;

            return true;
        }

        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj) || (obj is SchemaTopicType other && Equals(other));

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Namespace.GetHashCode();
                hashCode = (hashCode * 397) ^ TypeName.GetHashCode();
                hashCode = (hashCode * 397) ^ TopicName.GetHashCode();
                hashCode = (hashCode * 397) ^ DefinitionName.GetHashCode();
                
                // Add QoS hash
                if (Qos != null) hashCode = (hashCode * 397) ^ Qos.GetHashCode();
                
                // We typically don't hash arrays for performance, but if needed, hash the length
                hashCode = (hashCode * 397) ^ Fields.Length;
                
                return hashCode;
            }
        }
    }
}
