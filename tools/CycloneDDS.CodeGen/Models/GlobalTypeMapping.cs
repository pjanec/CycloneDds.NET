using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Models
{
    internal sealed record GlobalTypeMapping
    {
        public required string SourceTypeName { get; init; }
        public required DdsWire WireKind { get; init; }
        public required string DefinitionName { get; init; }
    }
}
