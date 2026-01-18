using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CycloneDDS.CodeGen
{
    public class SchemaDiscovery
    {
        public List<TypeInfo> DiscoverTopics(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
            }

            // 1. Find all .cs files
            var files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
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

            var compilation = CSharpCompilation.Create("Discovery")
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTrees);
            
            var topics = new List<TypeInfo>();
            
            foreach (var tree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                var typeDecls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
                
                foreach (var typeDecl in typeDecls)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
                    if (typeSymbol == null) continue;

                    bool isTopic = HasAttribute(typeSymbol, "CycloneDDS.Schema.DdsTopicAttribute");
                    bool isEnum = typeSymbol.TypeKind == TypeKind.Enum;

                    if (isTopic || isEnum)
                    {
                        var typeInfo = new TypeInfo 
                        { 
                            Name = typeSymbol.Name,
                            Namespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                            IsEnum = isEnum,
                            Attributes = ExtractAttributes(typeSymbol)
                        };

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
                }
            }
            
            return topics;
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

            return new FieldInfo
            {
                Name = member.Name,
                TypeName = type.ToDisplayString(),
                Attributes = ExtractAttributes(member)
            };
        }
    }
}
