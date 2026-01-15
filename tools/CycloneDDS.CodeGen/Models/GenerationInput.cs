using CycloneDDS.CodeGen.Utilities;

namespace CycloneDDS.CodeGen.Models
{
    internal sealed record GenerationInput
    {
        public EquatableArray<SchemaTopicType> Topics { get; init; }
        public EquatableArray<SchemaUnionType> Unions { get; init; }
        public EquatableArray<GlobalTypeMapping> Mappings { get; init; }
    }
}
