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

                        SetExtensibility(typeSymbol, typeInfo);
                        PopulateEnumOrFields(typeSymbol, typeInfo, isEnum);
                        if (isTopic) ResolveTopicName(typeSymbol, typeInfo);
                        ExtractFormatTemplate(typeSymbol, typeInfo);

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

        // ─────────────────────────────────────────────────────────────────────────
        // D02: Extracted helper methods to reduce main discovery loop length
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Reads [DdsExtensibility] from the symbol and sets <see cref="TypeInfo.Extensibility"/>.</summary>
        private static void SetExtensibility(INamedTypeSymbol typeSymbol, TypeInfo typeInfo)
        {
            var extAttr = typeSymbol.GetAttributes().FirstOrDefault(
                a => a.AttributeClass?.Name == "DdsExtensibilityAttribute" ||
                     a.AttributeClass?.Name == "DdsExtensibility");

            if (extAttr != null && extAttr.ConstructorArguments.Length > 0)
            {
                var val = extAttr.ConstructorArguments[0].Value;
                if (val is int intVal)
                {
                    typeInfo.Extensibility = (DdsExtensibilityKind)intVal;
                }
                else
                {
                    try { typeInfo.Extensibility = (DdsExtensibilityKind)Convert.ToInt32(val); }
                    catch { /* leave at default */ }
                }
            }
            else
            {
                typeInfo.Extensibility = DdsExtensibilityKind.Appendable;
            }
        }

        /// <summary>
        /// Reads the <c>[DdsTypeFormat("template")]</c> attribute from the type symbol and
        /// stores the template string on <see cref="TypeInfo.FormatTemplate"/>.
        /// </summary>
        private static void ExtractFormatTemplate(INamedTypeSymbol typeSymbol, TypeInfo typeInfo)
        {
            var fmtAttr = typeSymbol.GetAttributes().FirstOrDefault(
                a => a.AttributeClass?.Name == "DdsTypeFormatAttribute" ||
                     a.AttributeClass?.Name == "DdsTypeFormat");

            if (fmtAttr != null && fmtAttr.ConstructorArguments.Length > 0)
            {
                typeInfo.FormatTemplate = fmtAttr.ConstructorArguments[0].Value as string;
            }
        }

        /// <summary>Populates enum members (bit-bound + values) or struct/union fields on <paramref name="typeInfo"/>.</summary>
        private void PopulateEnumOrFields(INamedTypeSymbol typeSymbol, TypeInfo typeInfo, bool isEnum)
        {
            if (isEnum)
            {
                // ME1-T01: Read underlying type and store bit bound
                if (typeSymbol.EnumUnderlyingType != null)
                {
                    typeInfo.EnumBitBound = typeSymbol.EnumUnderlyingType.SpecialType switch
                    {
                        SpecialType.System_Byte or SpecialType.System_SByte => 8,
                        SpecialType.System_Int16 or SpecialType.System_UInt16 => 16,
                        _ => 32
                    };
                }

                foreach (var member in typeSymbol.GetMembers().OfType<IFieldSymbol>())
                {
                    if (member.IsConst && member.HasConstantValue)
                    {
                        typeInfo.EnumMembers.Add(member.Name);
                        typeInfo.EnumMemberValues.Add(Convert.ToInt64(member.ConstantValue));
                    }
                }
            }
            else
            {
                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IFieldSymbol fieldSymbol && !fieldSymbol.IsImplicitlyDeclared)
                        typeInfo.Fields.Add(CreateFieldInfo(fieldSymbol));
                    else if (member is IPropertySymbol propSymbol && !propSymbol.IsImplicitlyDeclared)
                        typeInfo.Fields.Add(CreateFieldInfo(propSymbol));
                }
            }
        }

        /// <summary>
        /// ME1-T03: Resolves the DDS topic name from the <c>[DdsTopic]</c> attribute argument
        /// or falls back to the namespace-qualified type name with dots replaced by underscores.
        /// </summary>
        private static void ResolveTopicName(INamedTypeSymbol typeSymbol, TypeInfo typeInfo)
        {
            var topicAttr = typeSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "CycloneDDS.Schema.DdsTopicAttribute");

            string? explicitName = null;
            if (topicAttr != null && topicAttr.ConstructorArguments.Length > 0)
                explicitName = topicAttr.ConstructorArguments[0].Value as string;

            if (!string.IsNullOrWhiteSpace(explicitName))
            {
                typeInfo.TopicName = explicitName;
            }
            else
            {
                var fullName = string.IsNullOrEmpty(typeInfo.Namespace)
                    ? typeInfo.Name
                    : $"{typeInfo.Namespace}.{typeInfo.Name}";
                typeInfo.TopicName = fullName.Replace('.', '_');
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

            bool isFixedSizeBuffer = false;
            int fixedSize = 0;

            // Roslyn exposes C# fixed buffers (e.g. `public fixed byte Buf[64];`) as
            // IFieldSymbol.IsFixedSizeBuffer == true.  The field type is reported as the
            // *pointer* to the element (e.g. byte*), so we unwrap one level.
            if (member is IFieldSymbol fieldSym && fieldSym.IsFixedSizeBuffer)
            {
                isFixedSizeBuffer = true;
                fixedSize = fieldSym.FixedSize;
                if (type is IPointerTypeSymbol pointerType)
                {
                    type = pointerType.PointedAtType;
                }
            }

            // ME1-T02: Detect [InlineArray(N)] struct fields.
            // The attribute is on the FIELD'S TYPE (the InlineArray struct), not the field itself.
            bool isInlineArray = false;
            if (!isFixedSizeBuffer && type is INamedTypeSymbol namedFieldType)
            {
                var inlineAttr = namedFieldType.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() ==
                        "System.Runtime.CompilerServices.InlineArrayAttribute");
                if (inlineAttr != null && inlineAttr.ConstructorArguments.Length > 0
                    && inlineAttr.ConstructorArguments[0].Value is int inlineLen && inlineLen > 0)
                {
                    // Get the single user-defined field inside the InlineArray struct
                    var elemField = namedFieldType.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Where(f => !f.IsImplicitlyDeclared && !f.IsStatic)
                        .FirstOrDefault();
                    if (elemField != null)
                    {
                        isInlineArray = true;
                        isFixedSizeBuffer = true;
                        fixedSize = inlineLen;
                        type = elemField.Type; // element type
                    }
                }
            }

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

            // For fixed-size buffers the element type is extracted from a pointer, and Roslyn
            // may return fully-qualified names (e.g. "System.Byte" instead of "byte").
            // Normalise those to their C# keyword aliases.
            // The same normalization applies for [InlineArray] element types.
            if (isFixedSizeBuffer)
            {
                typeName = typeName switch
                {
                    "System.Byte"    => "byte",
                    "System.SByte"   => "sbyte",
                    "System.Int16"   => "short",
                    "System.UInt16"  => "ushort",
                    "System.Int32"   => "int",
                    "System.UInt32"  => "uint",
                    "System.Int64"   => "long",
                    "System.UInt64"  => "ulong",
                    "System.Single"  => "float",
                    "System.Double"  => "double",
                    "System.Boolean" => "bool",
                    "System.Char"    => "char",
                    _ => typeName
                };
            }
            
            return new FieldInfo
            {
                Name = member.Name,
                TypeName = typeName,
                Attributes = ExtractAttributes(member),
                IsFixedSizeBuffer = isFixedSizeBuffer,
                FixedSize = fixedSize,
                IsInlineArray = isInlineArray
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
