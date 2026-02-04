using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using CycloneDDS.Schema;
using CycloneDDS.Compiler.Common;
using CycloneDDS.Compiler.Common.IdlJson;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen
{
    public class CodeGenerator
    {
        private readonly SchemaDiscovery _discovery = new SchemaDiscovery();
        private readonly IdlEmitter _idlEmitter = new IdlEmitter();
        private readonly SerializerEmitter _serializerEmitter = new SerializerEmitter();
        private readonly DeserializerEmitter _deserializerEmitter = new DeserializerEmitter();
        private readonly ViewEmitter _viewEmitter = new ViewEmitter();
        private readonly ViewExtensionsEmitter _viewExtensionsEmitter = new ViewExtensionsEmitter();

        public void Generate(string sourceDir, string outputDir, IEnumerable<string>? referencePaths = null)
        {
            //Console.WriteLine($"Discovering types in: {sourceDir}");
            var types = _discovery.DiscoverTopics(sourceDir, referencePaths);
            //Console.WriteLine($"Found {types.Count} type(s)");
            
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
                if (topic.IsTopic || topic.IsStruct || topic.IsUnion)
                {
                    Console.WriteLine($"CodeGen: topic: {topic.FullName}");
                    
                    var safeName = topic.FullName;
                    if (safeName.StartsWith("<global namespace>.")) safeName = safeName.Replace("<global namespace>.", "");
                    safeName = safeName.Replace("<", "_").Replace(">", "_");

                    // --- SAFETY CLEANUP START ---
                    // List of legacy file patterns to remove
                    var legacyFiles = new[]
                    {
                        $"{safeName}.Serializer.cs",
                        $"{safeName}.Deserializer.cs",
                        $"{safeName}.Descriptor.cs", 
                        $"{safeName}.View.cs",
                        $"{safeName}.ViewExtensions.cs" 
                    };

                    foreach (var legacyFile in legacyFiles)
                    {
                        string fullPath = Path.Combine(outputDir, legacyFile);
                        if (File.Exists(fullPath))
                        {
                            try 
                            { 
                                File.Delete(fullPath);
                            }
                            catch { /* Ignore */ }
                        }
                    }
                    // --- SAFETY CLEANUP END ---

                    var sb = new System.Text.StringBuilder();
                    
                    // 1. File Header & Usings
                    sb.AppendLine("// <auto-generated />");
                    sb.AppendLine("#pragma warning disable CS0162, CS0219, CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625");
                    sb.AppendLine("using System;");
                    sb.AppendLine("using System.Collections.Generic;");
                    sb.AppendLine("using System.Runtime.InteropServices;");
                    sb.AppendLine("using System.Runtime.CompilerServices;");
                    sb.AppendLine("using System.Text;");
                    sb.AppendLine("using CycloneDDS.Core;");
                    sb.AppendLine("using CycloneDDS.Runtime;");
                    sb.AppendLine();

                    // 2. Open Namespace
                    bool hasNamespace = !string.IsNullOrEmpty(topic.Namespace);
                    if (hasNamespace)
                    {
                        sb.AppendLine($"namespace {topic.Namespace}");
                        sb.AppendLine("{");
                    }

                    // 3. Inject Serializer Logic (Native Structs + Marshallers)
                    _serializerEmitter.EmitSerializerCode(sb, topic, registry);
                    sb.AppendLine();

                    // 4. Inject Reader Logic (Views + Extensions + ToManaged)
                    _deserializerEmitter.EmitDeserializerCode(sb, topic, registry);
                    sb.AppendLine();

                    // 5. Inject Descriptor (Using logic from GenerateDescriptors)
                    // We need to fetch the descriptor if available. 
                    // Note: original GenerateDescriptors iterates all local types. Here we are inside topic loop.
                    // We can reuse GenerateDescriptorCodeFromJson logic if we can get the descriptor.
                    // The original code called GenerateDescriptors separately.
                    // To consolidate "descriptor", we should move the generation logic here or call a method that appends it.
                    // However, GenerateDescriptors logic involves looking up JSON files based on discovery.
                    // Let's assume we can call a modified generator or just append the descriptor code here.
                    // The user listed "descriptor" in the list of what to consolidate.
                    
                    // For now, let's keep Descriptor separate as per original loop structure? 
                    // No, user specifically said "1. put all of these into a one single file per topic (Serializaer, deserializaer, descriptor, view, viewextensions)".
                    // So I must include descriptor here.
                    // I'll leave a placeholder or try to find the descriptor.
                    // The descriptor logic relies on `_discovery.GetIdlFileName` and JSON parsing.
                    // Since I don't want to duplicate logic, I will Extract `GenerateDescriptorCodeFromJson` related logic.
                    // But `GenerateDescriptors` does IO.
                    
                    AppendDescriptor(sb, topic, registry, outputDir);

                    // 5. Close Namespace
                    if (hasNamespace)
                    {
                        sb.AppendLine("}");
                    }

                    // 6. Write Single File
                    // If namespace is in name, strip it for filename if desired, or keep it to avoid collisions
                    if (topic.Namespace != null && safeName.StartsWith($"{topic.Namespace}.")) 
                        safeName = safeName.Substring(topic.Namespace.Length + 1);

                    string fileName = $"{safeName}.g.cs"; 
                    File.WriteAllText(Path.Combine(outputDir, fileName), sb.ToString());
                }
            }
            
            // Phase 3: Emit IDL (Grouped)
            _idlEmitter.EmitIdlFiles(registry, outputDir);
            
            // Emit Assembly Metadata
            EmitAssemblyMetadata(registry, outputDir);
            
            // Generate Descriptors (Runtime Support)
            //Console.WriteLine($"[DEBUG] LocalTypes count: {registry.LocalTypes.Count()}");
            //foreach(var t in registry.LocalTypes) Console.WriteLine($"[DEBUG] LocalType: {t.CSharpFullName} -> {t.TargetIdlFile}");

            GenerateDescriptors(registry, outputDir);

            //Console.WriteLine($"Output will go to: {outputDir}");
        }

        private void AppendDescriptor(StringBuilder sb, TypeInfo topic, GlobalTypeRegistry registry, string outputDir)
        {
            if (!registry.TryGetDefinition(topic.FullName, out var def)) return;
            
            string idlFileName = def.TargetIdlFile;
            string jsonFileName = System.IO.Path.GetFileName(idlFileName).Replace(".idl", ".json");
            string jsonPath = System.IO.Path.Combine(outputDir, jsonFileName);

            if (!System.IO.File.Exists(jsonPath)) return;

            try
            {
                var json = System.IO.File.ReadAllText(jsonPath);
                var descriptors = System.Text.Json.JsonSerializer.Deserialize<List<JsonTopicDescriptor>>(json);
                
                if (descriptors != null)
                {
                    // Try to match topic
                    // Use simple name matching or scoped logic
                    foreach (var desc in descriptors)
                    {
                        string csharpName = desc.TypeName.Replace("::", ".");
                        // Allow full match or suffix match
                        if (topic.FullName == csharpName || topic.FullName.EndsWith("." + csharpName))
                        {
                            EmitDescriptorCode(sb, topic, desc);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"    Descriptor append failed for {topic.Name}: {ex.Message}");
            }
        }

        private void EmitDescriptorCode(StringBuilder sb, TypeInfo topic, JsonTopicDescriptor descriptor)
        {
            sb.AppendLine($"    public partial struct {topic.Name}");
            sb.AppendLine("    {");
            
            // OPS
            sb.Append("        private static readonly uint[] _ops = new uint[] { ");
            if (descriptor.Ops != null && descriptor.Ops.Length > 0)
            {
                var opsString = string.Join(", ", descriptor.Ops.Select(op => (uint)op));
                sb.Append(opsString);
            }
            sb.AppendLine(" };");
            sb.AppendLine("        public static uint[] GetDescriptorOps() => _ops;");

            // KEYS
            if (descriptor.Keys != null && descriptor.Keys.Count > 0)
            {
                var keyIndices = CalculateKeyOpIndices(descriptor.Ops!, descriptor.Keys);

                sb.AppendLine();
                sb.AppendLine("        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]");
                sb.AppendLine("        {");
                foreach(var key in descriptor.Keys)
                {
                    var field = topic.Fields.FirstOrDefault(f => 
                        string.Equals(f.Name, key.Name, StringComparison.OrdinalIgnoreCase));
                    string fieldName = field != null ? field.Name : key.Name;
                    uint opIndex = keyIndices.ContainsKey(key.Name) ? keyIndices[key.Name] : 0;

                    sb.AppendLine($"            new DdsKeyDescriptor {{ Name = \"{fieldName}\", Offset = {opIndex}, Index = {key.Order} }},");
                }
                sb.AppendLine("        };");
                sb.AppendLine("        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;");
            }
            else
            {
                sb.AppendLine("        public static DdsKeyDescriptor[] GetKeyDescriptors() => null;");
            }

            // FLAGSET
            sb.AppendLine();
            sb.AppendLine($"        public static uint GetDescriptorFlagset() => {descriptor.FlagSet};");
            
            // SIZE & ALIGNMENT
            sb.AppendLine($"        public static uint GetDescriptorSize() => {descriptor.Size};");
            sb.AppendLine($"        public static uint GetDescriptorAlign() => {descriptor.Align};");

            sb.AppendLine("    }");
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
                            registry.RegisterExternal(extDef.CSharpFullName, extDef.TargetIdlFile, extDef.TargetModule, extDef.TypeInfo);
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
                         string? mappedType = attr.ConstructorArguments[0].Value as string;
                         if (mappedType != null && mappedType == fullTypeName)
                         {
                             string? idlFile = attr.ConstructorArguments[1].Value as string;
                             string? idlModule = attr.ConstructorArguments[2].Value as string;
                             
                             if (idlFile != null && idlModule != null)
                             {
                                 var typeInfo = new TypeInfo
                                 {
                                     Name = symbol.Name,
                                     Namespace = (symbol.ContainingNamespace?.ToDisplayString() ?? "").Replace("<global namespace>", "").Trim('.')
                                 };
                                 
                                 var symAttrs = symbol.GetAttributes();
                                 if (symAttrs.Any(a => a.AttributeClass?.Name == "DdsStructAttribute" || a.AttributeClass?.Name == "DdsStruct"))
                                     typeInfo.IsStruct = true;
                                 if (symAttrs.Any(a => a.AttributeClass?.Name == "DdsUnionAttribute" || a.AttributeClass?.Name == "DdsUnion"))
                                     typeInfo.IsUnion = true;
                                 if (symAttrs.Any(a => a.AttributeClass?.Name == "DdsTopicAttribute" || a.AttributeClass?.Name == "DdsTopic"))
                                     typeInfo.IsTopic = true;
                                 if (symbol.TypeKind == TypeKind.Enum)
                                     typeInfo.IsEnum = true;
                                 
                                 return new IdlTypeDefinition
                                 {
                                     CSharpFullName = fullTypeName,
                                     TargetIdlFile = idlFile,
                                     TargetModule = idlModule,
                                     IsExternal = true,
                                     TypeInfo = typeInfo
                                 };
                             }
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
                .Where(t => t.TypeInfo != null)
                .GroupBy(t => t.TargetIdlFile);

            var idlcRunner = new IdlcRunner();
            var jsonParser = new IdlJsonParser();
            var processedIdlFiles = new HashSet<string>();
            var localFileGroups = fileGroups.ToList();
            //Console.WriteLine($"[DEBUG] Found {localFileGroups.Count} file groups");
            //foreach(var g in localFileGroups) Console.WriteLine($"[DEBUG] Group: {g.Key}");

            // Phase 4a: Compile to JSON (ALL IDL files)
            string tempJsonDir = Path.Combine(outputDir, "temp_json");
            if (!Directory.Exists(tempJsonDir)) Directory.CreateDirectory(tempJsonDir);

            foreach (var group in localFileGroups)
            {
                string idlFileName = group.Key;
                string idlPath = Path.Combine(outputDir, $"{idlFileName}.idl");
                
                if (!processedIdlFiles.Contains(idlFileName))
                {
                    //Console.WriteLine($"[DEBUG] Running IDLC -l json for {idlFileName} at {idlPath}");
                    var result = idlcRunner.RunIdlc(idlPath, tempJsonDir, outputDir);
                    if (result.ExitCode != 0)
                    {
                         Console.Error.WriteLine($"    `idlc -l json {idlFileName}` failed for: {result.StandardError}");
                         continue; 
                    }
                    processedIdlFiles.Add(idlFileName);
                }
            }
            
            // Phase 4b: Parse JSON and Generate Descriptors
            foreach (var group in localFileGroups)
            {
                string idlFileName = group.Key;
                string jsonFile = Path.Combine(tempJsonDir, $"{idlFileName}.json");
                
                if (File.Exists(jsonFile))
                {
                    try
                    {
                        var jsonTypes = jsonParser.Parse(jsonFile);
                        //Console.WriteLine($"[DEBUG] Parsed {jsonTypes.Count} types from {jsonFile}");
                        
                        foreach(var topic in group)
                        {
                            if (topic.TypeInfo == null) continue;
                            if (topic.TypeInfo.IsEnum) continue;

                            try 
                            {
                                // Match C# type to JSON type
                                // C#: MyNamespace.MyTopic
                                // IDL/JSON: MyNamespace::MyTopic
                                string idlName = topic.CSharpFullName.Replace(".", "::");
                                
                                var jsonDef = jsonParser.FindType(jsonTypes, idlName);
                                
                                if (jsonDef != null && jsonDef.TopicDescriptor != null)
                                {
                                    // Generate descriptor code from JSON metadata
                                    var descCode = GenerateDescriptorCodeFromJson(topic.TypeInfo, jsonDef.TopicDescriptor);
                                    File.WriteAllText(Path.Combine(outputDir, $"{topic.TypeInfo.FullName}.Descriptor.cs"), descCode);
                                    //Console.WriteLine($"    Generated {topic.TypeInfo.Name}.Descriptor.cs");
                                }
                                else
                                {
                                    Console.WriteLine($"    Warning: No topic descriptor found for {topic.TypeInfo.Name} (IDL: {idlName})");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"    Descriptor generation failed for {topic.TypeInfo.Name}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"    JSON parsing failed for {idlFileName}: {ex.Message}");
                    }
                }
            }
        }

        private string GenerateDescriptorCodeFromJson(TypeInfo topic, JsonTopicDescriptor descriptor)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#pragma warning disable CS0162, CS0219, CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625");
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
            
            // OPS - Direct from JSON (no calculation needed!)
            sb.Append("        private static readonly uint[] _ops = new uint[] { ");
            if (descriptor.Ops != null && descriptor.Ops.Length > 0)
            {
                var opsString = string.Join(", ", descriptor.Ops.Select(op => (uint)op));
                sb.Append(opsString);
            }
            sb.AppendLine(" };");
            sb.AppendLine("        public static uint[] GetDescriptorOps() => _ops;");

            // KEYS - Calculate Op Indices based on KOF instructions
            if (descriptor.Keys != null && descriptor.Keys.Count > 0)
            {
                var keyIndices = CalculateKeyOpIndices(descriptor.Ops!, descriptor.Keys);

                sb.AppendLine();
                sb.AppendLine("        private static readonly DdsKeyDescriptor[] _keys = new DdsKeyDescriptor[]");
                sb.AppendLine("        {");
                foreach(var key in descriptor.Keys)
                {
                    // Match field name to C# casing (JSON might have different casing)
                    var field = topic.Fields.FirstOrDefault(f => 
                        string.Equals(f.Name, key.Name, StringComparison.OrdinalIgnoreCase));
                    string fieldName = field != null ? field.Name : key.Name;
                    
                    // Use calculated Op Index (keyIndices) instead of byte Offset
                    uint opIndex = keyIndices.ContainsKey(key.Name) ? keyIndices[key.Name] : 0;

                    sb.AppendLine($"            new DdsKeyDescriptor {{ Name = \"{fieldName}\", Offset = {opIndex}, Index = {key.Order} }},");
                }
                sb.AppendLine("        };");
                sb.AppendLine("        public static DdsKeyDescriptor[] GetKeyDescriptors() => _keys;");
            }
            else
            {
                sb.AppendLine("        public static DdsKeyDescriptor[] GetKeyDescriptors() => null;");
            }

            // FLAGSET
            sb.AppendLine();
            sb.AppendLine($"        public static uint GetDescriptorFlagset() => {descriptor.FlagSet};");
            
            // SIZE & ALIGNMENT (Critical for Arrays/Sequences)
            sb.AppendLine($"        public static uint GetDescriptorSize() => {descriptor.Size};");
            sb.AppendLine($"        public static uint GetDescriptorAlign() => {descriptor.Align};");

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

        private Dictionary<string, uint> CalculateKeyOpIndices(long[] ops, List<JsonKeyDescriptor> keys)
        {
            var result = new Dictionary<string, uint>();
            
            // Reconstruct the key order from JSON order to match KOF Instructions
            // Note: ops and keys are inputs.
            // Assumption: KOF instructions appear in the Ops stream in the same order as Keys.
            // But KOF instructions might be grouped (DDS_OP_KOF | n).
            
            if (ops == null || ops.Length == 0 || keys == null || keys.Count == 0) return result;
            
            // Sort keys by Order to match KOF structure expectation
            var sortedKeys = keys.OrderBy(k => k.Order).ToList();
            int currentKeyIndex = 0;

            // Scan Ops for KOF
            for (int i = 0; i < ops.Length; i++)
            {
                uint op = (uint)ops[i];
                uint opcode = (op & 0xFF000000); // Top 8 bits

                if (opcode == 0x07000000) // DDS_OP_KOF
                {
                    // Count of keys in this KOF block
                    // DDS_OP_KOF = 0x07 << 24
                    // Count = op & 0x00FFFFFF? No wait.
                    // Verification.c: DDS_OP_KOF | 1 -> 0x07000001
                    // Verification.c: DDS_OP_KOF | 2 -> 0x07000002
                    // Yes, low 24 bits are count.
                    
                    int count = (int)(op & 0x00FFFFFF);
                    
                    // The KOF instruction starts at 'i'. 
                    // But dds_key_descriptor.m_op_index MUST point to this index 'i'.
                    // Wait, if KOF covers multiple keys, do they all point to 'i'?
                    // Or do they point to specific offsets within the KOF block?
                    //
                    // DDS Spec regarding dds_key_descriptor_t:
                    // "m_op_index: index into m_ops for the key descriptor instruction"
                    //
                    // If multiple keys are grouped in one KOF (like nested struct keys), 
                    // does the naive descriptor point to the same KOF instruction?
                    //
                    // In verification.c (NestedKeyTopic):
                    //   /* key: location.building */
                    //   DDS_OP_KOF | 2, 1u, 0u
                    //
                    //   /* key: location.floor */
                    //   DDS_OP_KOF | 2, 1u, 2u
                    //
                    // It seems idlc emits a SEPARATE KOF block for EACH key, even if they look identical?
                    // 
                    // Wait. In verification.c for NestedKeyTopic:
                    // Lines 2348-2353:
                    //   /* key: location.building */   [Index 13]
                    //   DDS_OP_KOF | 2, 1u, 0u,  
                    //   /* key: location.floor */      [Index 16]
                    //   DDS_OP_KOF | 2, 1u, 2u
                    //
                    // Yes! It emits a separate KOF trio for EACH key.
                    // So we can assume 1 KOF instruction block = 1 Key.
                    
                    if (currentKeyIndex < sortedKeys.Count)
                    {
                        var key = sortedKeys[currentKeyIndex];
                        result[key.Name] = (uint)i;
                        currentKeyIndex++;
                    }
                    
                    // Skip arguments
                    i += count;
                }
            }
            
            return result;
        }

    }
}
