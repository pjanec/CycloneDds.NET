using CycloneDDS.Generator.Utilities;

namespace CycloneDDS.Generator.Models
{
    internal sealed record GenerationInput
    {
        public EquatableArray<SchemaTopicType> Topics { get; init; }
        public EquatableArray<SchemaUnionType> Unions { get; init; }
        public EquatableArray<GlobalTypeMapping> Mappings { get; init; }
    }
}
