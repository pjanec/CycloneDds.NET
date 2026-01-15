using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen.Diagnostics;

public record Diagnostic
{
    public required string Code { get; init; }
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? SourceFile { get; init; }
    public int? Line { get; init; }
    public string? TypeName { get; init; }
    public string? FieldName { get; init; }
    
    public override string ToString()
    {
        var location = SourceFile != null && Line.HasValue 
            ? $"{SourceFile}({Line}): " 
            : TypeName != null 
                ? $"{TypeName}: " 
                : "";
                
        var severity = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info"
        };
        
        return $"{location}{severity} {Code}: {Message}";
    }
}
