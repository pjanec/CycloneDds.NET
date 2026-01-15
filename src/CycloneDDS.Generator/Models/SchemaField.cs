namespace CycloneDDS.Generator.Models
{
    internal sealed record SchemaField
    {
        public required string Name { get; init; }
        public required string TypeName { get; init; }
        public required string DataType { get; init; }
        public bool IsKey { get; init; }
        public bool IsOptional { get; init; }
        public int? Bound { get; init; }
        public int? Id { get; init; }
    }
}
