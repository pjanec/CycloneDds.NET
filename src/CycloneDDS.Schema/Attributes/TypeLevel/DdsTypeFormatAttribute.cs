namespace CycloneDDS.Schema;

/// <summary>
/// Specifies a custom inline-preview format template for a DDS struct, topic, or union.
/// When present, the <c>CycloneDDS.CodeGen</c> tool parses the template at compile time
/// and emits two methods into the generated partial type:
/// <list type="bullet">
///   <item><description>
///     <c>public override string ToString()</c> — a native C# string-interpolation
///     expression providing a fast, allocation-efficient plain-text representation.
///   </description></item>
///   <item><description>
///     <c>public IEnumerable&lt;FormattedToken&gt; GetFormatTokens()</c> — a duck-typed
///     (no interface dependency) method that yields syntax-highlighted token sequences for
///     rich UI rendering.
///   </description></item>
/// </list>
/// </summary>
/// <remarks>
/// Template syntax: combine literal text with <c>{FieldName:FormatString:TokenType}</c>
/// placeholders.  All three parts follow the colon-separated convention:
/// <list type="bullet">
///   <item><description><b>FieldName</b> (required): the exact C# field or property name.</description></item>
///   <item><description><b>FormatString</b> (optional): a standard .NET format string passed to <c>ToString()</c> (e.g. <c>D</c>, <c>X8</c>, <c>0.00</c>).</description></item>
///   <item><description><b>TokenType</b> (optional): one of the <c>CycloneDDS.Schema.Formatting.TokenType</c> enum members (e.g. <c>Number</c>, <c>Keyword</c>, <c>String</c>).</description></item>
/// </list>
/// Literal text outside braces is emitted with <c>TokenType.Punctuation</c>.
/// </remarks>
/// <example>
/// <code>
/// [DdsStruct]
/// [DdsTypeFormat("{Site:D:Number}:{App:D:Number}:{Entity:X8:Keyword}")]
/// public partial struct DisEntityId
/// {
///     public ushort Site;
///     public ushort App;
///     public uint Entity;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
public sealed class DdsTypeFormatAttribute : Attribute
{
    /// <summary>
    /// The format template string.  See <see cref="DdsTypeFormatAttribute"/> for syntax details.
    /// </summary>
    public string Template { get; }

    /// <param name="template">
    /// The format template (e.g. <c>"{X:0.00:Number}, {Y:0.00:Number}, {Z:0.00:Number}"</c>).
    /// </param>
    public DdsTypeFormatAttribute(string template)
    {
        Template = template;
    }
}
