using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CycloneDDS.CodeGen.Validation;
using CycloneDDS.CodeGen.Diagnostics;
using Diagnostic = CycloneDDS.CodeGen.Diagnostics.Diagnostic;
using DiagnosticSeverity = CycloneDDS.CodeGen.Diagnostics.DiagnosticSeverity;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen;

public class CodeGenerator
{
    public int Generate(string sourceDirectory)
    {
        int filesGenerated = 0;
        var validator = new SchemaValidator();
        var fingerprintStore = new FingerprintStore(sourceDirectory);
        
        // Find all .cs files (exclude Generated/ folder)
        var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Generated") && !f.Contains("obj") && !f.Contains("bin"))
            .ToList();

        foreach (var file in csFiles)
        {
            try
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code, path: file);
                var root = tree.GetRoot();

                // Find types with [DdsTopic]
                var topicTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsTopicAttribute)
                    .ToList();

                foreach (var type in topicTypes)
                {
                    // Validate schema
                    validator.ValidateTopicType(type, file);
                    
                    // Check evolution
                    var currentFingerprint = SchemaFingerprint.Compute(type);
                    var previousFingerprint = fingerprintStore.GetPrevious(type.Identifier.Text);
                    
                    if (previousFingerprint != null)
                    {
                        var evolutionResult = SchemaFingerprint.CompareForEvolution(previousFingerprint, currentFingerprint);
                        
                        if (evolutionResult.HasBreakingChanges)
                        {
                            foreach (var error in evolutionResult.Errors)
                            {
                                validator.AddDiagnostic(new Diagnostic
                                {
                                    Code = DiagnosticCode.MemberTypeChanged,
                                    Severity = DiagnosticSeverity.Error,
                                    Message = error,
                                    SourceFile = file,
                                    TypeName = type.Identifier.Text
                                });
                            }
                        }
                    }
                    
                    fingerprintStore.Update(type.Identifier.Text, currentFingerprint);
                }

                if (topicTypes.Any())
                {
                    filesGenerated += GenerateForTopics(file, topicTypes);
                }

                // Find types with [DdsUnion]
                var unionTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsUnionAttribute)
                    .ToList();

                foreach (var type in unionTypes)
                {
                    validator.ValidateUnionType(type, file);
                }

                if (unionTypes.Any())
                {
                    filesGenerated += GenerateForUnions(file, unionTypes);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"[CodeGen] ERROR: Failed to read {file}: {ex.Message}");
                // Continue processing other files
                continue;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CodeGen] ERROR: Unexpected error processing {file}: {ex.Message}");
                throw;
            }
        }

        // Report all diagnostics
        foreach (var diagnostic in validator.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                Console.Error.WriteLine(diagnostic);
            else
                Console.WriteLine(diagnostic);
        }
        
        // Save fingerprints
        fingerprintStore.Save();
        
        // Return error code if validation failed
        if (validator.HasErrors)
        {
            Console.Error.WriteLine($"\n[CodeGen] Validation failed with {validator.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s)");
            return -1;  // Signal error to build system
        }

        // Generate IDL if validation succeeded
        var idlEmitter = new IdlEmitter();
        
        foreach (var file in csFiles)
        {
            try
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code, path: file);
                var root = tree.GetRoot();
                var sourceDir = Path.GetDirectoryName(file)!;
                var generatedDir = Path.Combine(sourceDir, "Generated");
                Directory.CreateDirectory(generatedDir);

                // Topics
                var topicTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsTopicAttribute)
                    .ToList();

                foreach (var type in topicTypes)
                {
                    var topicName = ExtractTopicName(type);
                    var idlCode = idlEmitter.GenerateIdl(type, topicName);
                    var idlFile = Path.Combine(generatedDir, $"{type.Identifier.Text}.idl");
                    File.WriteAllText(idlFile, idlCode);
                    Console.WriteLine($"[CodeGen]   Generated IDL: {idlFile}");
                }

                // Unions
                var unionTypes = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Where(HasDdsUnionAttribute)
                    .ToList();

                foreach (var type in unionTypes)
                {
                    var idlCode = idlEmitter.GenerateUnionIdl(type);
                    var idlFile = Path.Combine(generatedDir, $"{type.Identifier.Text}.idl");
                    File.WriteAllText(idlFile, idlCode);
                    Console.WriteLine($"[CodeGen]   Generated Union IDL: {idlFile}");
                }

                // Enums (Generate IDL for all public enums found)
                var enumTypes = root.DescendantNodes()
                    .OfType<EnumDeclarationSyntax>()
                    .Where(e => e.Modifiers.Any(m => m.Text == "public"))
                    .ToList();

                foreach (var enumDecl in enumTypes)
                {
                    var idlCode = idlEmitter.GenerateEnumIdl(enumDecl);
                    var idlFile = Path.Combine(generatedDir, $"{enumDecl.Identifier.Text}.idl");
                    File.WriteAllText(idlFile, idlCode);
                    Console.WriteLine($"[CodeGen]   Generated Enum IDL: {idlFile}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CodeGen] ERROR: Failed to generate IDL for {file}: {ex.Message}");
            }
        }

        return filesGenerated;
    }

    private bool HasDdsTopicAttribute(TypeDeclarationSyntax type)
    {
        return type.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => 
            {
                var name = attr.Name.ToString();
                return name is "DdsTopic" or "DdsTopicAttribute";
            });
    }

    private bool HasDdsUnionAttribute(TypeDeclarationSyntax type)
    {
        return type.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => 
            {
                var name = attr.Name.ToString();
                return name is "DdsUnion" or "DdsUnionAttribute";
            });
    }

    private int GenerateForTopics(string sourceFile, List<TypeDeclarationSyntax> types)
    {
        int count = 0;
        var sourceDir = Path.GetDirectoryName(sourceFile)!;
        var generatedDir = Path.Combine(sourceDir, "Generated");
        Directory.CreateDirectory(generatedDir);

        foreach (var type in types)
        {
            try
            {
                var typeName = type.Identifier.Text;
                var namespaceName = GetNamespace(type);
                var topicName = ExtractTopicName(type);

                var generatedCode = $@"// <auto-generated/>
// Generated from: {Path.GetFileName(sourceFile)}
// Topic: {topicName}

namespace {namespaceName}
{{
    partial class {typeName}
    {{
        // FCDC-005: Discovery placeholder (CLI tool)
        // TODO: Generate native types, managed views, marshallers in FCDC-009+
    }}
}}
";

                var outputFile = Path.Combine(generatedDir, $"{typeName}.Discovery.g.cs");
                File.WriteAllText(outputFile, generatedCode);
                Console.WriteLine($"[CodeGen]   Generated: {outputFile}");
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"[CodeGen] ERROR: Failed to write generated file: {ex.Message}");
                // Continue with other types
            }
        }

        return count;
    }

    private int GenerateForUnions(string sourceFile, List<TypeDeclarationSyntax> types)
    {
        int count = 0;
        var sourceDir = Path.GetDirectoryName(sourceFile)!;
        var generatedDir = Path.Combine(sourceDir, "Generated");
        Directory.CreateDirectory(generatedDir);

        foreach (var type in types)
        {
            try
            {
                var typeName = type.Identifier.Text;
                var namespaceName = GetNamespace(type);

                var generatedCode = $@"// <auto-generated/>
// Generated from: {Path.GetFileName(sourceFile)}
// Union type

namespace {namespaceName}
{{
    partial class {typeName}
    {{
        // FCDC-005: Union discovery placeholder (CLI tool)
        // TODO: Generate union discriminator, case handling in FCDC-027
    }}
}}
";

                var outputFile = Path.Combine(generatedDir, $"{typeName}.Discovery.g.cs");
                File.WriteAllText(outputFile, generatedCode);
                Console.WriteLine($"[CodeGen]   Generated: {outputFile}");
                count++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"[CodeGen] ERROR: Failed to write generated file: {ex.Message}");
                // Continue with other types
            }
        }

        return count;
    }

    private string GetNamespace(TypeDeclarationSyntax type)
    {
        var namespaceDecl = type.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDecl != null)
            return namespaceDecl.Name.ToString();

        var fileScopedNs = type.Ancestors().OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
        if (fileScopedNs != null)
            return fileScopedNs.Name.ToString();

        return "Global";
    }

    private string ExtractTopicName(TypeDeclarationSyntax type)
    {
        var topicAttr = type.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => 
            {
                var name = attr.Name.ToString();
                return name is "DdsTopic" or "DdsTopicAttribute";
            });

        if (topicAttr?.ArgumentList?.Arguments.Count > 0)
        {
            var arg = topicAttr.ArgumentList.Arguments[0];
            return arg.Expression.ToString().Trim('"');
        }

        return type.Identifier.Text;
    }
}
