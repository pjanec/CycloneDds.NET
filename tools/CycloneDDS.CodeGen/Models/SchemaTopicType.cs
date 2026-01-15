using CycloneDDS.CodeGen.Utilities;

namespace CycloneDDS.CodeGen.Models
{
    // "record" gives us value equality for free
    internal sealed record SchemaTopicType 
    {
        public required string Namespace { get; init; }
        public required string TypeName { get; init; }
        public required string TopicName { get; init; }
        public required string DefinitionName { get; init; }

        public DdsQosSettings? Qos { get; init; }

        // Use EquatableArray instead of ImmutableArray
        public EquatableArray<SchemaField> Fields { get; init; } = EquatableArray<SchemaField>.Empty;
        public EquatableArray<int> KeyFieldIndices { get; init; } = EquatableArray<int>.Empty;
    }
}
