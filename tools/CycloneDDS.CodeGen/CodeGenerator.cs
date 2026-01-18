using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen
{
    public class CodeGenerator
    {
        private readonly SchemaDiscovery _discovery = new SchemaDiscovery();
        private readonly IdlEmitter _idlEmitter = new IdlEmitter();
        private readonly SerializerEmitter _serializerEmitter = new SerializerEmitter();
        private readonly DeserializerEmitter _deserializerEmitter = new DeserializerEmitter();

        public void Generate(string sourceDir, string outputDir, IEnumerable<string>? referencePaths = null)
        {
            Console.WriteLine($"Discovering types in: {sourceDir}");
            var types = _discovery.DiscoverTopics(sourceDir, referencePaths);
            Console.WriteLine($"Found {types.Count} type(s)");
            
            // Validate ALL types with strict checking
            var validator = new SchemaValidator(types, _discovery.ValidExternalTypes);
            var managedValidator = new ManagedTypeValidator();
            
            bool hasErrors = false;
            foreach (var type in types)
            {
                var result = validator.Validate(type);
                if (!result.IsValid)
                {
                    hasErrors = true;
                    foreach (var err in result.Errors)
                    {
                        Console.Error.WriteLine($"ERROR: {err}");
                    }
                }

                var managedErrors = managedValidator.Validate(type);
                if (managedErrors.Any(d => d.Severity == ValidationSeverity.Error))
                {
                    hasErrors = true;
                     foreach (var d in managedErrors.Where(d => d.Severity == ValidationSeverity.Error))
                         Console.Error.WriteLine($"ERROR: {d.Message}");
                }
            }
            
            if (hasErrors)
            {
                var errorMsg = "Schema validation failed. Fix errors above.";
                // Collect errors for exception message to help debugging
                var allErrors = new List<string>();
                foreach (var type in types)
                {
                    var result = validator.Validate(type);
                    allErrors.AddRange(result.Errors);
                    var managedErrors = managedValidator.Validate(type);
                    allErrors.AddRange(managedErrors.Where(d => d.Severity == ValidationSeverity.Error).Select(d => d.Message));
                }
                if (allErrors.Any())
                {
                    errorMsg += "\nErrors:\n" + string.Join("\n", allErrors);
                }
                throw new InvalidOperationException(errorMsg);
            }
            
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Phase 1: Registry Population
            var registry = new GlobalTypeRegistry();
            foreach (var type in types)
            {
                var idlFile = _discovery.GetIdlFileName(type, type.SourceFile);
                var idlModule = _discovery.GetIdlModule(type);
                registry.RegisterLocal(type, type.SourceFile, idlFile, idlModule);
            }

            // Phase 2: Dependency Resolution
            ResolveExternalDependencies(registry, types);
            
            // Emit Serializers (Per Type, C# code)
            foreach (var topic in types)
            {
                if (topic.IsTopic || topic.IsStruct)
                {
                    var serializerCode = _serializerEmitter.EmitSerializer(topic);
                    File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Serializer.cs"), serializerCode);

                    var deserializerCode = _deserializerEmitter.EmitDeserializer(topic);
                    File.WriteAllText(Path.Combine(outputDir, $"{topic.Name}.Deserializer.cs"), deserializerCode);
                    Console.WriteLine($"    Generated Serializers for {topic.Name}");
                }
            }
            
            // Phase 3: Emit IDL (Grouped)
            _idlEmitter.EmitIdlFiles(registry, outputDir);
            
            // Emit Assembly Metadata
            EmitAssemblyMetadata(registry, outputDir);
            
            // Generate Descriptors (Runtime Support)
            GenerateDescriptors(registry, outputDir);

            Console.WriteLine($"Output will go to: {outputDir}");
        }

        private void ResolveExternalDependencies(GlobalTypeRegistry registry, List<TypeInfo> types)
        {
            var resolvedCache = new HashSet<string>();

            foreach(var type in types)
            {
                foreach(var field in type.Fields)
                {
                    var fieldTypeName = StripGenerics(field.TypeName);
                    
                    if (registry.TryGetDefinition(fieldTypeName, out _)) continue;
                    if (resolvedCache.Contains(fieldTypeName)) continue;
                    
                    var extDef = ResolveExternalType(_discovery.Compilation, fieldTypeName);
                    if (extDef != null)
                    {
                        if (!registry.TryGetDefinition(extDef.CSharpFullName, out _))
                        {
                            registry.RegisterExternal(extDef.CSharpFullName, extDef.TargetIdlFile, extDef.TargetModule);
                            resolvedCache.Add(fieldTypeName);
                        }
                    }
                }
            }
        }
        
        private IdlTypeDefinition? ResolveExternalType(Compilation? compilation, string fullTypeName)
        {
            if (compilation == null) return null;
            
            var symbol = compilation.GetTypeByMetadataName(fullTypeName);
            if (symbol == null) return null;
            
            if (symbol.Locations.Any(loc => loc.IsInSource)) return null; 
            
            var assembly = symbol.ContainingAssembly;
            if (assembly == null) return null;
            
            var attributes = assembly.GetAttributes();
            foreach (var attr in attributes)
            {
                if (attr.AttributeClass?.Name == "DdsIdlMappingAttribute" || attr.AttributeClass?.Name == "DdsIdlMapping")
                {
                     if (attr.ConstructorArguments.Length >= 3)
                     {
                         string mappedType = attr.ConstructorArguments[0].Value as string;
                         if (mappedType == fullTypeName)
                         {
                             string idlFile = attr.ConstructorArguments[1].Value as string;
                             string idlModule = attr.ConstructorArguments[2].Value as string;
                             
                             return new IdlTypeDefinition
                             {
                                 CSharpFullName = fullTypeName,
                                 TargetIdlFile = idlFile,
                                 TargetModule = idlModule,
                                 IsExternal = true
                             };
                         }
                     }
                }
            }
            
            return null;
        }

        private void EmitAssemblyMetadata(GlobalTypeRegistry registry, string outputDir)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("using CycloneDDS.Schema;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine();
            
            foreach (var type in registry.LocalTypes)
            {
                sb.AppendLine($"[assembly: DdsIdlMapping(\"{type.CSharpFullName}\", \"{type.TargetIdlFile}\", \"{type.TargetModule}\")]");
            }
            
            File.WriteAllText(Path.Combine(outputDir, "CycloneDDS.IdlMap.g.cs"), sb.ToString());
        }

        private void GenerateDescriptors(GlobalTypeRegistry registry, string outputDir)
        {
            var fileGroups = registry.LocalTypes
                .Where(t => t.TypeInfo.IsTopic)
                .GroupBy(t => t.TargetIdlFile);

            var idlcRunner = new IdlcRunner();
            var processedIdlFiles = new HashSet<string>();

            foreach (var group in fileGroups)
            {
                string idlFileName = group.Key;
                string idlPath = Path.Combine(outputDir, $"{idlFileName}.idl");
                
                if (!processedIdlFiles.Contains(idlFileName))
                {
                    string tempCGroup = Path.Combine(outputDir, "temp_c");
                    if (!Directory.Exists(tempCGroup)) Directory.CreateDirectory(tempCGroup);

                    var result = idlcRunner.RunIdlc(idlPath, tempCGroup);
                    if (result.ExitCode != 0)
                    {
                         Console.Error.WriteLine($"    idlc failed for {idlFileName}: {result.StandardError}");
                         continue; 
                    }
                    
                    processedIdlFiles.Add(idlFileName);
                    
                    string cFile = Path.Combine(tempCGroup, $"{idlFileName}.c");
                    if (File.Exists(cFile))
                    {
                        var parser = new DescriptorParser(); 
                        
                        foreach(var topic in group)
                        {
                             try 
                             {
                                 var metadata = parser.ParseDescriptor(cFile, topic.TypeInfo.Name);
                                 var descCode = GenerateDescriptorCode(topic.TypeInfo, metadata);
                                 File.WriteAllText(Path.Combine(outputDir, $"{topic.TypeInfo.Name}.Descriptor.cs"), descCode);
                                 Console.WriteLine($"    Generated {topic.TypeInfo.Name}.Descriptor.cs");
                             }
                             catch (Exception ex)
                             {
                                 Console.Error.WriteLine($"    Descriptor parsing failed for {topic.TypeInfo.Name}: {ex.Message}");
                             }
                        }
                    }
                }
            }
        }

        private string GenerateDescriptorCode(TypeInfo topic, DescriptorMetadata metadata)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using CycloneDDS.Runtime;");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(topic.Namespace))
            {
                sb.AppendLine($"namespace {topic.Namespace}");
                sb.AppendLine("{");
            }
            
            sb.AppendLine($"    public partial struct {topic.Name}");
            sb.AppendLine("    {");
            
            sb.Append("        private static readonly uint[] _ops = new uint[] {");
            if (metadata.OpsValues != null && metadata.OpsValues.Length > 0)
            {
                 sb.Append(string.Join(", ", metadata.OpsValues));
            }
            sb.AppendLine("};");

            sb.AppendLine();
            sb.AppendLine("        public static uint[] GetDescriptorOps() => _ops;");
            
            sb.AppendLine("    }");
            
            if (!string.IsNullOrEmpty(topic.Namespace))
            {
                sb.AppendLine("}");
            }
            return sb.ToString(); 
        }

        private string StripGenerics(string typeName)
        {
            int idx = typeName.IndexOf('<');
            if (idx > 0)
            {
                if (typeName.StartsWith("System.Collections.Generic.List") || typeName.StartsWith("List"))
                {
                    int end = typeName.LastIndexOf('>');
                    return typeName.Substring(idx + 1, end - idx - 1).Trim();
                }
            }
            return typeName.TrimEnd('?');
}

    }
}
