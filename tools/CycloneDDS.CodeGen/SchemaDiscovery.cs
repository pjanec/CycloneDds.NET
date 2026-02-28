using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen
{
    public class SchemaDiscovery
    {
        public Compilation? Compilation { get; private set; }
        public HashSet<string> ValidExternalTypes { get; } = new HashSet<string>();

        public List<TypeInfo> DiscoverTopics(string sourceDirectory, IEnumerable<string>? referencePaths = null)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
            }

            // 1. Find all .cs files
            var files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "Generated" + Path.DirectorySeparatorChar) &&
                            !f.EndsWith(".Descriptor.cs") && 
                            !f.EndsWith(".Serializer.cs") &&
                            !f.EndsWith(".Deserializer.cs"))
                .ToArray();
            
            if (files.Length == 0)
            {
                 return new List<TypeInfo>();
            }

            // 2. Parse into syntax trees
            var syntaxTrees = files.Select(f => 
                CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();
            
            // 3. Create compilation
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Schema.DdsTopicAttribute).Assembly.Location)
            };

            if (referencePaths != null)
            {
                foreach (var refPath in referencePaths)
                {
                    if (File.Exists(refPath)) 
                    {
                        try { references.Add(MetadataReference.CreateFromFile(refPath)); } catch {}
                    }
                }
            }
            
            // Add System.Runtime for net8.0 if needed, but object might be enough for basic types
            // If running on .NET Core, we might need more refs.
            // For now, let's trust the environment or add basic refs.
            var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (trustedAssemblies != null)
            {
                foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
            }

            Compilation = CSharpCompilation.Create("Discovery")
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTrees);

            var typeDisplayFormat = new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

            CollectExternalTypes(Compilation, typeDisplayFormat);
            
            var topics = new List<TypeInfo>();
            
            foreach (var tree in syntaxTrees)
            {
                var semanticModel = Compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                var typeDecls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
                
                foreach (var typeDecl in typeDecls)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
                    if (typeSymbol == null) continue;

                    bool isTopic = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsTopicAttribute");
                    bool isStruct = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsStructAttribute");
                    bool isUnion = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsUnionAttribute");
                    bool isEnum = typeSymbol.TypeKind == TypeKind.Enum;
                    bool isClass = typeSymbol.TypeKind == TypeKind.Class;

                    if (isTopic || isStruct || isUnion || isEnum)
                    {
                        var typeInfo = new TypeInfo 
                        { 
                            Name = typeSymbol.Name,
                            Namespace = (typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty).Replace("<global namespace>", "").Trim('.'),
                            SourceFile = tree.FilePath,
                            IsTopic = isTopic,
                            IsStruct = isStruct,
                            IsUnion = isUnion,
                            IsEnum = isEnum,
                            IsClass = isClass,
                            Attributes = ExtractAttributes(typeSymbol)
                        };

                        // Check for DdsExtensibility attribute
                        var extAttr = typeSymbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "DdsExtensibilityAttribute" || a.AttributeClass?.Name == "DdsExtensibility");
                        if (extAttr != null && extAttr.ConstructorArguments.Length > 0)
                        {
                            var val = extAttr.ConstructorArguments[0].Value;
                            //Console.WriteLine($"[DEBUG-SCHEMA] Type {typeSymbol.Name} has Extensibility Attr. ValType={val?.GetType().Name} Val={val}");
                            if (val is int intVal)
                            {
                                typeInfo.Extensibility = (DdsExtensibilityKind)intVal;
                                //Console.WriteLine($"[DEBUG-SCHEMA]   -> Set to {typeInfo.Extensibility}");
                            }
                            else
                            {
                                Console.WriteLine($"[DEBUG-SCHEMA]   -> Value is not int! Fallback to Appendable?");
                                // Force correct cast if it's not int (e.g. underlying type issue)
                                try {
                                    typeInfo.Extensibility = (DdsExtensibilityKind)Convert.ToInt32(val);
                                    Console.WriteLine($"[DEBUG-SCHEMA]   -> Converted to {typeInfo.Extensibility}");
                                } catch (Exception ex) {
                                    Console.WriteLine($"[DEBUG-SCHEMA]   -> Convention failed: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            // Default to Appendable
                            typeInfo.Extensibility = DdsExtensibilityKind.Appendable;
                            //Console.WriteLine($"[DEBUG-SCHEMA] Type {typeSymbol.Name} default Extensibility Appendable (No Attr)");

                        }

                        if (isEnum)
                        {
                            foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
                            {
                                if (member.IsConst && member.HasConstantValue)
                                {
                                    typeInfo.EnumMembers.Add(member.Name);
                                }
                            }
                        }
                        else
                        {
                            // Extract fields and properties
                            foreach (var member in typeSymbol.GetMembers())
                            {
                                if (member is IFieldSymbol fieldSymbol && !fieldSymbol.IsImplicitlyDeclared)
                                {
                                    typeInfo.Fields.Add(CreateFieldInfo(fieldSymbol));
                                }
                                else if (member is IPropertySymbol propSymbol && !propSymbol.IsImplicitlyDeclared)
                                {
                                    typeInfo.Fields.Add(CreateFieldInfo(propSymbol));
                                }
                            }
                        }

                        topics.Add(typeInfo);
                    }
                }
            }

            // Second pass: Resolve nested types
            // We can do this by matching FullName
            var topicMap = topics.ToDictionary(t => t.FullName);
            foreach (var topic in topics)
            {
                foreach (var field in topic.Fields)
                {
                    // Remove nullable ? for lookup
                    var lookupName = field.TypeName.TrimEnd('?');
                    if (topicMap.TryGetValue(lookupName, out var resolvedType))
                    {
                        field.Type = resolvedType;
                    }
                    else if (lookupName.Contains("<") && lookupName.Contains(">"))
                    {
                         var start = lookupName.IndexOf('<') + 1;
                         var end = lookupName.LastIndexOf('>');
                         var innerName = lookupName.Substring(start, end - start).Trim().TrimEnd('?');
                         
                         if (topicMap.TryGetValue(innerName, out var resolvedInner))
                         {
                             field.GenericType = resolvedInner;
                         }
                    }
                }
            }
            
            return topics;
        }

        public string GetIdlFileName(TypeInfo type, string sourceFileName)
        {
            // Check for [DdsIdlFile] attribute
            var attr = type.GetAttribute("DdsIdlFile");
            
            if (attr != null && attr.Arguments.Count > 0)
            {
                string? fileName = attr.Arguments[0] as string;
                if (fileName != null)
                {
                    ValidateIdlFileName(fileName, type.Name);
                    return fileName;
                }
            }
            
            // Default: Use C# source filename without extension
            return Path.GetFileNameWithoutExtension(sourceFileName);
        }

        public string GetIdlModule(TypeInfo type)
        {
            // Check for [DdsIdlModule] attribute
            var attr = type.GetAttribute("DdsIdlModule");
            
            if (attr != null && attr.Arguments.Count > 0)
            {
                string? modulePath = attr.Arguments[0] as string;
                if (modulePath != null)
                {
                    ValidateIdlModule(modulePath, type.Name);
                    return modulePath;
                }
            }
            
            // Default: Convert C# namespace to IDL modules
            // "Corp.Common.Geo" -> "Corp::Common::Geo"
            return type.Namespace.Replace(".", "::");
        }

        private void ValidateIdlFileName(string fileName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException($"[DdsIdlFile] on '{typeName}' cannot be empty.");

            if (fileName.Contains(".") || fileName.Contains("/") || fileName.Contains("\\"))
                throw new ArgumentException($"[DdsIdlFile(\"{fileName}\")] on '{typeName}' contains extension or path separators. Use the name without extension.");
            
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                 throw new ArgumentException($"[DdsIdlFile(\"{fileName}\")] on '{typeName}' contains invalid characters.");
        }

        private void ValidateIdlModule(string modulePath, string typeName)
        {
            if (string.IsNullOrWhiteSpace(modulePath))
                 throw new ArgumentException($"[DdsIdlModule] on '{typeName}' cannot be empty.");

            if (modulePath.Contains("."))
                throw new ArgumentException($"[DdsIdlModule(\"{modulePath}\")] on '{typeName}' contains '.' (C# syntax). Use '::' for IDL modules.");
            
            var parts = modulePath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            foreach(var part in parts)
            {
                 if (!System.Text.RegularExpressions.Regex.IsMatch(part, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                     throw new ArgumentException($"[DdsIdlModule(\"{modulePath}\")] on '{typeName}' contains invalid identifier segment '{part}'.");
            }
        }

        private bool HasAttribute(ISymbol symbol, string attributeFullName)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attributeFullName);
        }

        private List<AttributeInfo> ExtractAttributes(ISymbol symbol)
        {
            var attributes = new List<AttributeInfo>();
            foreach (var attr in symbol.GetAttributes())
            {
                var attrInfo = new AttributeInfo
                {
                    Name = attr.AttributeClass?.Name ?? "",
                };

                foreach (var arg in attr.ConstructorArguments)
                {
                    if (arg.Value != null)
                    {
                        attrInfo.Arguments.Add(arg.Value);
                    }
                }
                attributes.Add(attrInfo);
            }
            return attributes;
        }

        private FieldInfo CreateFieldInfo(ISymbol member)
        {
            ITypeSymbol type = member switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                _ => throw new ArgumentException("Member must be field or property")
            };

            // Use a format that ensures fully qualified names (Namespace.Type)
            // We want "Namespace.Type", not "global::Namespace.Type"
            var format = new SymbolDisplayFormat(
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

            // Capture valid DDS types (even external ones) for validation
            if (HasAttribute(type, "CycloneDDS.Schema.DdsStructAttribute") || 
                HasAttribute(type, "CycloneDDS.Schema.DdsTopicAttribute") ||
                HasAttribute(type, "CycloneDDS.Schema.DdsUnionAttribute") ||
                type.TypeKind == TypeKind.Enum)
            {
                // Unclear if ToDisplayString() matches TypeName format exactly (nullable?)
                // TypeName handles nullable? 
                // ToDisplayString with defaults usually includes ?
                ValidExternalTypes.Add(type.ToDisplayString(format).TrimEnd('?'));
            }

            // Normalize common types to C# aliases for consistency with SerializerEmitter
            string typeName = type.ToDisplayString(format);
            if (typeName == "System.String") typeName = "string";
            
            return new FieldInfo
            {
                Name = member.Name,
                TypeName = typeName,
                Attributes = ExtractAttributes(member)
            };
        }

        private void CollectExternalTypes(Compilation compilation, SymbolDisplayFormat format)
        {
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                CollectExternalTypes(assembly.GlobalNamespace, format);
            }
        }

        private void CollectExternalTypes(INamespaceSymbol ns, SymbolDisplayFormat format)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (ShouldIncludeExternalType(type))
                {
                    ValidExternalTypes.Add(type.ToDisplayString(format).TrimEnd('?'));
                }

                foreach (var nested in type.GetTypeMembers())
                {
                    if (ShouldIncludeExternalType(nested))
                    {
                        ValidExternalTypes.Add(nested.ToDisplayString(format).TrimEnd('?'));
                    }
                }
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                CollectExternalTypes(child, format);
            }
        }

        private bool ShouldIncludeExternalType(INamedTypeSymbol type)
        {
            return type.TypeKind == TypeKind.Enum ||
                   HasAttribute(type, "CycloneDDS.Schema.DdsStructAttribute") ||
                   HasAttribute(type, "CycloneDDS.Schema.DdsTopicAttribute") ||
                   HasAttribute(type, "CycloneDDS.Schema.DdsUnionAttribute");
        }
    }
}
