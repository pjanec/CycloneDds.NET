namespace CycloneDDS.Schema.Formatting;

/// <summary>
/// Semantic classification for a <see cref="FormattedToken"/>, used by UI consumers
/// to map tokens to visual styles (e.g. CSS classes) without creating a dependency on
/// any specific UI framework.
/// </summary>
public enum TokenType
{
    /// <summary>Unclassified / default text.</summary>
    Default = 0,
    /// <summary>A numeric value (integer or floating-point).</summary>
    Number,
    /// <summary>A string / text value.</summary>
    String,
    /// <summary>A boolean value (<c>true</c> or <c>false</c>).</summary>
    Boolean,
    /// <summary>A named enumeration value.</summary>
    Enum,
    /// <summary>A language or domain keyword (e.g. a hex identifier).</summary>
    Keyword,
    /// <summary>Punctuation, delimiters, and structural characters.</summary>
    Punctuation,
    /// <summary>A null / absent value.</summary>
    Null,
}
