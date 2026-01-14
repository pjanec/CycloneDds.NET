using Microsoft.CodeAnalysis;

namespace CycloneDDS.Generator.Models
{
    internal sealed class SchemaField
    {
        public required IFieldSymbol Symbol { get; init; }
        public required string Name { get; init; }
        public required ITypeSymbol Type { get; init; }
        public bool IsKey { get; set; }
        public bool IsOptional { get; set; }
        public int? Bound { get; set; }
        public int? Id { get; set; }
    }
}
