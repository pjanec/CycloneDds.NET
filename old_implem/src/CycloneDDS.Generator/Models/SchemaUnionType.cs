namespace CycloneDDS.Generator.Models
{
    internal sealed record SchemaUnionType
    {
        public required string Namespace { get; init; }
        public required string TypeName { get; init; }
        public required string DefinitionName { get; init; }
    }
}
