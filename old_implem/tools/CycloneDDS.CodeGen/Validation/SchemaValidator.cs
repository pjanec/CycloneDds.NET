using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using CycloneDDS.CodeGen.Models;
using CycloneDDS.CodeGen.Diagnostics;
using Diagnostic = CycloneDDS.CodeGen.Diagnostics.Diagnostic;
using DiagnosticSeverity = CycloneDDS.CodeGen.Diagnostics.DiagnosticSeverity;

namespace CycloneDDS.CodeGen.Validation;

public class SchemaValidator
{
    private readonly List<Diagnostic> _diagnostics = new();
    
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public void AddDiagnostic(Diagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }
    
    public void ValidateTopicType(TypeDeclarationSyntax type, string sourceFile)
    {
        ValidateTopicAttribute(type, sourceFile);
        ValidateQosAttribute(type, sourceFile);
        ValidateFields(type, sourceFile);
    }
    
    public void ValidateUnionType(TypeDeclarationSyntax type, string sourceFile)
    {
        ValidateDiscriminator(type, sourceFile);
        ValidateCases(type, sourceFile);
    }
    
    private void ValidateTopicAttribute(TypeDeclarationSyntax type, string sourceFile)
    {
        var topicAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var name = attr.Name.ToString();
                return name is "DdsTopic" or "DdsTopicAttribute";
            });
            
        if (topicAttr == null)
        {
            AddError(DiagnosticCode.MissingTopicAttribute,
                $"Type '{type.Identifier}' must have [DdsTopic(...)] attribute",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
            return;
        }
        
        // Validate topic name
        if (topicAttr.ArgumentList?.Arguments.Count > 0)
        {
            var topicNameArg = topicAttr.ArgumentList.Arguments[0];
            var topicName = topicNameArg.Expression.ToString().Trim('"');
            
            if (string.IsNullOrWhiteSpace(topicName))
            {
                AddError(DiagnosticCode.InvalidTopicName,
                    "Topic name cannot be empty or whitespace",
                    sourceFile, GetLineNumber(type), type.Identifier.Text);
            }
            
            // Topic name validation: alphanumeric + underscore, no spaces
            if (!System.Text.RegularExpressions.Regex.IsMatch(topicName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            {
                AddError(DiagnosticCode.InvalidTopicName,
                    $"Topic name '{topicName}' invalid. Must start with letter/underscore, contain only alphanumerics",
                    sourceFile, GetLineNumber(type), type.Identifier.Text);
            }
        }
    }
    
    private void ValidateQosAttribute(TypeDeclarationSyntax type, string sourceFile)
    {
        var qosAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr =>
            {
                var name = attr.Name.ToString();
                return name is "DdsQos" or "DdsQosAttribute";
            });
            
        if (qosAttr == null)
        {
            AddWarning(DiagnosticCode.MissingQosAttribute,
                $"Type '{type.Identifier}' missing [DdsQos] attribute. Using defaults: Reliable, Volatile, KeepLast(1)",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void ValidateFields(TypeDeclarationSyntax type, string sourceFile)
    {
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        
        foreach (var field in fields)
        {
            ValidateFieldType(field, type, sourceFile);
        }
    }
    
    private void ValidateFieldType(FieldDeclarationSyntax field, TypeDeclarationSyntax parentType, string sourceFile)
    {
        var typeName = field.Declaration.Type.ToString();
        
        // Check for unsupported types
        var unsupportedTypes = new[] { "List<", "Dictionary<", "Hashtable", "ArrayList" };
        if (unsupportedTypes.Any(ut => typeName.Contains(ut)))
        {
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
            AddError(DiagnosticCode.UnsupportedFieldType,
                $"Field '{fieldName}' uses unsupported type '{typeName}'. Use T[] or BoundedSeq<T,N> instead",
                sourceFile, GetLineNumber(field), parentType.Identifier.Text, fieldName);
        }
    }
    
    private void ValidateDiscriminator(TypeDeclarationSyntax type, string sourceFile)
    {
        var discriminators = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsDiscriminator" or "DdsDiscriminatorAttribute";
                }))
            .ToList();
            
        if (discriminators.Count == 0)
        {
            AddError(DiagnosticCode.MissingDiscriminator,
                $"Union type '{type.Identifier}' must have exactly one [DdsDiscriminator] field",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
        else if (discriminators.Count > 1)
        {
            AddError(DiagnosticCode.MultipleDiscriminators,
                $"Union type '{type.Identifier}' has {discriminators.Count} discriminators, expected exactly 1",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void ValidateCases(TypeDeclarationSyntax type, string sourceFile)
    {
        var caseFields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsCase" or "DdsCaseAttribute";
                }))
            .ToList();
            
        var caseValues = new HashSet<string>();
        
        foreach (var caseField in caseFields)
        {
            var caseAttr = caseField.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsCase" or "DdsCaseAttribute";
                });
                
            if (caseAttr?.ArgumentList?.Arguments.Count > 0)
            {
                var caseValue = caseAttr.ArgumentList.Arguments[0].Expression.ToString();
                
                if (!caseValues.Add(caseValue))
                {
                    var fieldName = caseField.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "?";
                    AddError(DiagnosticCode.DuplicateCaseValue,
                        $"Union case value '{caseValue}' used multiple times in type '{type.Identifier}'",
                        sourceFile, GetLineNumber(caseField), type.Identifier.Text, fieldName);
                }
            }
        }
        
        // Check for multiple default cases
        var defaultCases = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr =>
                {
                    var name = attr.Name.ToString();
                    return name is "DdsDefaultCase" or "DdsDefaultCaseAttribute";
                }))
            .ToList();
            
        if (defaultCases.Count > 1)
        {
            AddError(DiagnosticCode.MultipleDefaultCases,
                $"Union type '{type.Identifier}' has {defaultCases.Count} default cases, expected at most 1",
                sourceFile, GetLineNumber(type), type.Identifier.Text);
        }
    }
    
    private void AddError(string code, string message, string? sourceFile = null, int? line = null, 
        string? typeName = null, string? fieldName = null)
    {
        _diagnostics.Add(new Diagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Error,
            Message = message,
            SourceFile = sourceFile,
            Line = line,
            TypeName = typeName,
            FieldName = fieldName
        });
    }
    
    private void AddWarning(string code, string message, string? sourceFile = null, int? line = null, 
        string? typeName = null, string? fieldName = null)
    {
        _diagnostics.Add(new Diagnostic
        {
            Code = code,
            Severity = DiagnosticSeverity.Warning,
            Message = message,
            SourceFile = sourceFile,
            Line = line,
            TypeName = typeName,
            FieldName = fieldName
        });
    }
    
    private int GetLineNumber(SyntaxNode node)
    {
        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
