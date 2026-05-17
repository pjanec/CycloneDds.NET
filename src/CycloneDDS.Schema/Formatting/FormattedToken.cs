namespace CycloneDDS.Schema.Formatting;

/// <summary>
/// A plain-text fragment carrying a semantic <see cref="TokenType"/> classification.
/// Sequences of <see cref="FormattedToken"/> are yielded by the duck-typed
/// <c>GetFormatTokens()</c> method emitted by <c>CycloneDDS.CodeGen</c> for types
/// annotated with <see cref="CycloneDDS.Schema.DdsTypeFormatAttribute"/>.
/// </summary>
public readonly struct FormattedToken
{
    /// <summary>The display text of this token.</summary>
    public string Text { get; }

    /// <summary>The semantic classification used by UI consumers for syntax highlighting.</summary>
    public TokenType Type { get; }

    public FormattedToken(string text, TokenType type)
    {
        Text = text;
        Type = type;
    }
}
