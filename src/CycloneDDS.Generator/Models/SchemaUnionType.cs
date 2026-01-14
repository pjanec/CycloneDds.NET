using Microsoft.CodeAnalysis;

namespace CycloneDDS.Generator.Models
{
    internal sealed record SchemaUnionType
    {
        public required INamedTypeSymbol Symbol { get; init; }
        
        public bool Equals(SchemaUnionType? other)
        {
            if (other is null) return false;
            return SymbolEqualityComparer.Default.Equals(Symbol, other.Symbol);
        }
        
        public override int GetHashCode()
        {
            return SymbolEqualityComparer.Default.GetHashCode(Symbol);
        }
    }
}
