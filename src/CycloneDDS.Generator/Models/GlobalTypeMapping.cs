using Microsoft.CodeAnalysis;
using CycloneDDS.Schema;

namespace CycloneDDS.Generator.Models
{
    internal sealed record GlobalTypeMapping
    {
        public required INamedTypeSymbol SourceType { get; init; }
        public required DdsWire WireKind { get; init; }
        
        public bool Equals(GlobalTypeMapping? other)
        {
            if (other is null) return false;
            return SymbolEqualityComparer.Default.Equals(SourceType, other.SourceType) && 
                   WireKind == other.WireKind;
        }
        
        public override int GetHashCode()
        {
             return SymbolEqualityComparer.Default.GetHashCode(SourceType) ^ WireKind.GetHashCode();
        }
    }
}
