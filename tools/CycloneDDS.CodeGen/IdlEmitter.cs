using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen
{
    public class IdlEmitter
    {
        public void EmitIdlFiles(GlobalTypeRegistry registry, string outputDir)
        {
            // Group local types by target IDL file
            var fileGroups = registry.LocalTypes.GroupBy(t => t.TargetIdlFile).ToList();
            
            // 0. Detect Circular Dependencies
            DetectCircularDependencies(fileGroups, registry);

            foreach (var fileGroup in fileGroups)
            {
                string fileName = fileGroup.Key;
                var sb = new StringBuilder();
                string guard = $"_CYCLONEDDS_GENERATED_{fileName.ToUpper()}_IDL_";
                
                // Header comment
                sb.AppendLine($"// Auto-generated IDL for {fileName} by CycloneDDS C# Bindings");
                sb.AppendLine($"// Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"#ifndef {guard}");
                sb.AppendLine($"#define {guard}");
                sb.AppendLine();
                
                // 1. Generate #include directives
                var dependencies = GetFileDependencies(fileGroup, registry);
                foreach (var depFile in dependencies.OrderBy(f => f))
                {
                    sb.AppendLine($"#include \"{depFile}.idl\"");
                }
                
                if (dependencies.Any())
                    sb.AppendLine();
                
                // 2. Group by module and emit
                var moduleGroups = fileGroup.GroupBy(t => t.TargetModule);
                foreach (var moduleGroup in moduleGroups.OrderBy(g => g.Key))
                {
                    EmitModuleHierarchy(sb, moduleGroup.Key, moduleGroup);
                }
                
                sb.AppendLine($"#endif // {guard}");
                
                // 3. Write to file
                string outputPath = Path.Combine(outputDir, $"{fileName}.idl");
                File.WriteAllText(outputPath, sb.ToString());
            }
        }

        private void DetectCircularDependencies(IEnumerable<IGrouping<string, IdlTypeDefinition>> fileGroups, GlobalTypeRegistry registry)
        {
            // Build dependency graph: File -> Dependencies
            var graph = new Dictionary<string, HashSet<string>>();
            
            foreach (var group in fileGroups)
            {
                var file = group.Key;
                var deps = GetFileDependencies(group, registry);
                graph[file] = deps;
            }

            // DFS for cycle detection
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var file in graph.Keys)
            {
                if (DetectCycle(file, graph, visited, recursionStack, out var cyclePath))
                {
                    throw new InvalidOperationException(
                        $"Circular dependency detected in IDL files: {string.Join(" -> ", cyclePath)} -> {file}");
                }
            }
        }

        private bool DetectCycle(string current, Dictionary<string, HashSet<string>> graph, 
                                 HashSet<string> visited, HashSet<string> recursionStack, out List<string> path)
        {
            path = new List<string>();
            
            if (recursionStack.Contains(current))
            {
                return true; // Cycle detected
            }
            
            if (visited.Contains(current))
            {
                return false; // Already checked
            }

            visited.Add(current);
            recursionStack.Add(current);
            path.Add(current);

            if (graph.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    // Only check neighbors that are in our generation set (local files)
                    if (graph.ContainsKey(neighbor))
                    {
                        if (DetectCycle(neighbor, graph, visited, recursionStack, out var subPath))
                        {
                            path.AddRange(subPath);
                            return true;
                        }
                    }
                }
            }

            recursionStack.Remove(current);
            path.RemoveAt(path.Count - 1);
            return false;
        }

        private HashSet<string> GetFileDependencies(IEnumerable<IdlTypeDefinition> types, GlobalTypeRegistry registry)
        {
            var dependencies = new HashSet<string>();
            
            foreach (var type in types)
            {
                if (type.TypeInfo == null) continue;
                foreach (var field in type.TypeInfo.Fields)
                {
                    string fieldType = StripGenerics(field.TypeName);
                    
                    if (registry.TryGetDefinition(fieldType, out var dep) && dep != null)
                    {
                        // Don't include self-references
                        if (dep.TargetIdlFile != type.TargetIdlFile)
                        {
                            dependencies.Add(dep.TargetIdlFile);
                        }
                    }
                }
            }
            return dependencies;
        }

        private void EmitModuleHierarchy(StringBuilder sb, string modulePath, IEnumerable<IdlTypeDefinition> types)
        {
            var modules = modulePath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            
            // Open modules
            int indent = 0;
            foreach (var module in modules)
            {
                sb.AppendLine($"{GetIndent(indent)}module {module} {{");
                indent++;
            }
            
            // Emit types - Using name sorting as per design doc
            foreach (var type in types.OrderBy(t => t.TypeInfo?.Name ?? ""))
            {
                if (type.TypeInfo == null) continue;

                if (type.TypeInfo.IsEnum)
                     EmitEnum(sb, type.TypeInfo, indent);
                else if (type.TypeInfo.HasAttribute("DdsUnion"))
                     EmitUnion(sb, type.TypeInfo, indent);
                else
                     EmitStruct(sb, type.TypeInfo, indent);
                
                sb.AppendLine();
            }
            
            // Close modules
            for (int i = modules.Length - 1; i >= 0; i--)
            {
                indent--;
                sb.AppendLine($"{GetIndent(indent)}}};");
            }
            
            sb.AppendLine();
        }

        private string GetIndent(int count) => new string(' ', count * 4);

        private string StripGenerics(string typeName)
        {
            int idx = typeName.IndexOf('<');
            if (idx > 0)
            {
                // Handle List<T> -> T
                if (typeName.StartsWith("System.Collections.Generic.List") || typeName.StartsWith("List"))
                {
                    int end = typeName.LastIndexOf('>');
                    return typeName.Substring(idx + 1, end - idx - 1).Trim();
                }
            }
            return typeName.TrimEnd('?');
        }

        // Updated helper methods to accept indent
        
        private void EmitStruct(StringBuilder sb, TypeInfo type, int indentLevel)
        {
            string indent = GetIndent(indentLevel);
            string fieldIndent = GetIndent(indentLevel + 1);

            sb.AppendLine($"{indent}@appendable");
            sb.AppendLine($"{indent}struct {type.Name} {{");
            
            foreach (var field in type.Fields)
            {
                var (idlType, suffix) = MapType(field);
                string annotations = "";
                
                if (field.HasAttribute("DdsKey"))
                    annotations = "@key ";
                
                if (field.HasAttribute("DdsOptional"))
                    annotations += "@optional ";
                
                sb.AppendLine($"{fieldIndent}{annotations}{idlType} {ToCamelCase(field.Name)}{suffix};");
            }
            
            sb.AppendLine($"{indent}}};");
        }

        private void EmitEnum(StringBuilder sb, TypeInfo type, int indentLevel)
        {
             string indent = GetIndent(indentLevel);
             string memberIndent = GetIndent(indentLevel + 1);

             sb.AppendLine($"{indent}enum {type.Name} {{");
             
             for (int i = 0; i < type.EnumMembers.Count; i++)
             {
                 string comma = (i < type.EnumMembers.Count - 1) ? "," : "";
                 sb.AppendLine($"{memberIndent}{type.EnumMembers[i]}{comma}");
             }
             
             sb.AppendLine($"{indent}}};");
        }

        private void EmitUnion(StringBuilder sb, TypeInfo type, int indentLevel)
        {
            // Simplified port of existing logic with indentation
            string indent = GetIndent(indentLevel);
            string fieldIndent = GetIndent(indentLevel + 1);

            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) return; // Should throw

            var (switchType, _) = MapType(discriminator);

            sb.AppendLine($"{indent}@appendable");
            sb.AppendLine($"{indent}union {type.Name} switch ({switchType}) {{");

            foreach (var field in type.Fields)
            {
                if (field == discriminator) continue;
                // Simplified case generation
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr != null)
                {
                     foreach(var val in caseAttr.CaseValues)
                        sb.AppendLine($"{fieldIndent}case {val}:");
                }
                else if (field.HasAttribute("DdsDefault"))
                {
                     sb.AppendLine($"{fieldIndent}default:");
                }

                var (idlType, suffix) = MapType(field);
                sb.AppendLine($"{fieldIndent}    {idlType} {ToCamelCase(field.Name)}{suffix};");
            }
            sb.AppendLine($"{indent}}};");
        }


        
        private (string Type, string Suffix) MapType(FieldInfo field)
        {
            var typeName = field.TypeName;

            // Fixed Strings
            if (typeName.Contains("FixedString32")) return ("char", "[32]");
            if (typeName.Contains("FixedString64")) return ("char", "[64]");
            if (typeName.Contains("FixedString128")) return ("char", "[128]");
            if (typeName.Contains("FixedString256")) return ("char", "[256]");

            // Primitives
            if (typeName == "byte" || typeName == "System.Byte") return ("octet", "");
            if (typeName == "sbyte" || typeName == "System.SByte") return ("int8", "");
            if (typeName == "short" || typeName == "System.Int16") return ("int16", "");
            if (typeName == "ushort" || typeName == "System.UInt16") return ("uint16", "");
            if (typeName == "int" || typeName == "System.Int32") return ("int32", "");
            if (typeName == "uint" || typeName == "System.UInt32") return ("uint32", "");
            if (typeName == "long" || typeName == "System.Int64") return ("int64", "");
            if (typeName == "ulong" || typeName == "System.UInt64") return ("uint64", "");
            if (typeName == "float" || typeName == "System.Single") return ("float", "");
            if (typeName == "double" || typeName == "System.Double") return ("double", "");
            if (typeName == "bool" || typeName == "System.Boolean") return ("boolean", "");
            if (typeName == "char" || typeName == "System.Char") return ("char", "");
            
            // New Standard Types
            if (typeName == "Guid" || typeName == "System.Guid") return ("octet", "[16]");
            if (typeName == "DateTime" || typeName == "System.DateTime") return ("int64", "");
            if (typeName == "DateTimeOffset" || typeName == "System.DateTimeOffset") return ("octet", "[16]");
            if (typeName == "TimeSpan" || typeName == "System.TimeSpan") return ("int64", ""); // Ticks
            
            // System.Numerics
            if (typeName == "Vector2" || typeName == "System.Numerics.Vector2") return ("float", "[2]");
            if (typeName == "Vector3" || typeName == "System.Numerics.Vector3") return ("float", "[3]");
            if (typeName == "Vector4" || typeName == "System.Numerics.Vector4") return ("float", "[4]");
            if (typeName == "Quaternion" || typeName == "System.Numerics.Quaternion") return ("float", "[4]");
            if (typeName == "Matrix4x4" || typeName == "System.Numerics.Matrix4x4") return ("float", "[16]");

            // Arrays
            if (typeName.EndsWith("[]"))
            {
                var elementTypeName = typeName.Substring(0, typeName.Length - 2);
                var innerField = new FieldInfo { TypeName = elementTypeName };
                var (innerIdl, innerSuffix) = MapType(innerField);
                return ($"sequence<{innerIdl}>", "");
            }

            // BoundedSeq
            if (typeName.Contains("BoundedSeq<"))
            {
                // Extract T
                var start = typeName.IndexOf('<') + 1;
                var end = typeName.LastIndexOf('>');
                var innerType = typeName.Substring(start, end - start);
                
                // Recursively map inner type
                // We create a dummy FieldInfo for the inner type
                var innerField = new FieldInfo { TypeName = innerType };
                // Pass resolved type if available? 
                // We don't have resolved type for inner generic arg easily here without more parsing.
                // But MapType handles simple names too.
                
                var (innerIdl, innerSuffix) = MapType(innerField);
                // Sequence of array? sequence<char[32]> is not valid IDL?
                // IDL: sequence<type, bound>
                // If inner type has suffix (array), we might need a typedef.
                // But for now let's assume simple sequences.
                
                return ($"sequence<{innerIdl}>", "");
            }

            // Managed String
            if (typeName == "string" || typeName == "System.String") return ("string", "");

            // Nested types
            if (field.Type != null)
            {
                return (field.Type.Name, "");
            }

            // Generic inner
             if (field.GenericType != null)
            {
                // When we fall through from BoundedSeq above, MapType recursively calls itself with a new dummy FieldInfo.
                // That dummy FieldInfo does NOT have GenericType set because we created it just with TypeName.
                // So this branch is only reached if standard SchemaDiscovery populated field.GenericType.
                // But MapType recursion creates a NEW FieldInfo.
                // So line 215 above (MapType call) creates FieldInfo with TypeName only.
                // So field.Type is null, field.GenericType is null.
                // It falls through to Fallback.
            }
            
            // Fallback to simple name (e.g. Enums)
            var parts = typeName.Split('.');
            return (parts.Last(), "");
        }



        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }


}
