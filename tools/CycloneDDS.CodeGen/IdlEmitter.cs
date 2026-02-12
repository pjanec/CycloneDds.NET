using CycloneDDS.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
                string guard = $"_CYCLONEDDS_GENERATED_{SanitizeToCSymbol(fileName.ToUpper())}_IDL_";
                
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
                    EmitModuleHierarchy(sb, moduleGroup.Key, moduleGroup, registry);
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

        private void EmitModuleHierarchy(StringBuilder sb, string modulePath, IEnumerable<IdlTypeDefinition> types, GlobalTypeRegistry registry)
        {
            if (modulePath == "<global namespace>") modulePath = "";
            var modules = modulePath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            
            // Open modules
            int indent = 0;
            foreach (var module in modules)
            {
                sb.AppendLine($"{GetIndent(indent)}module {module} {{");
                indent++;
            }
            
            // Emit types - Topologically Sorted
            var sortedTypes = TopologicalSort(types, registry);
            foreach (var type in sortedTypes)
            {
                if (type.TypeInfo == null) continue;

                if (type.TypeInfo.IsEnum)
                     EmitEnum(sb, type.TypeInfo, indent);
                else if (type.TypeInfo.HasAttribute("DdsUnion"))
                     EmitUnion(sb, type.TypeInfo, indent, registry);
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

        private IEnumerable<IdlTypeDefinition> TopologicalSort(IEnumerable<IdlTypeDefinition> types, GlobalTypeRegistry registry)
        {
            var typeList = types.ToList();
            var visited = new HashSet<IdlTypeDefinition>();
            var sorted = new List<IdlTypeDefinition>();
            var recursionStack = new HashSet<IdlTypeDefinition>();

            // Map TypeName -> Definition for fast lookup
            var lookUp = typeList.Where(t => t.TypeInfo != null).ToDictionary(t => t.TypeInfo!.Name, t => t);

            foreach (var type in typeList)
            {
                Visit(type, visited, sorted, recursionStack, lookUp, registry);
            }

            return sorted;
        }

        private void Visit(IdlTypeDefinition type, HashSet<IdlTypeDefinition> visited, List<IdlTypeDefinition> sorted, HashSet<IdlTypeDefinition> stack, Dictionary<string, IdlTypeDefinition> lookUp, GlobalTypeRegistry registry)
        {
            if (visited.Contains(type)) return;
            if (stack.Contains(type)) throw new Exception($"Circular dependency detected within module for type {type.TypeInfo?.Name}"); // Should be caught earlier by file cycle check, but intra-file cycles are possible

            stack.Add(type);

            if (type.TypeInfo != null)
            {
                foreach (var field in type.TypeInfo.Fields)
                {
                    string typeName = StripGenerics(field.TypeName);
                    // Check if this typeName maps to one of our local types in this collection
                    // We need to resolve fully qualified names or simple names.
                    // Assuming types in same module are referred by simple name or full name.
                    // registry keys are full names.
                    
                    if (registry.TryGetDefinition(typeName, out var depDef))
                    {
                        // Check if depDef is in our module grouping (i.e. in lookUp)
                        // If typeName was simple, registry might not find it if keys are full.
                        // But StripGenerics returns what was in the field.
                        // Assuming field types are fully qualified or we can match them.
                        
                        // Try matching by name in lookUp directly (if simple name)
                        // Or check if depDef is one of the types we are sorting.
                        
                        IdlTypeDefinition? dep = null;
                        if (depDef != null && lookUp.Values.Contains(depDef))
                        {
                            dep = depDef;
                        }
                        // Fallback: simple name match (e.g. "ProcessAddress" vs "CycloneDDS...ProcessAddress")
                        if (dep == null)
                        {
                             var simpleName = typeName.Split('.').Last();
                             if (lookUp.TryGetValue(simpleName, out var d)) dep = d;
                        }

                        if (dep != null)
                        {
                            Visit(dep, visited, sorted, stack, lookUp, registry);
                        }
                    }
                    else
                    {
                        // Try simple name lookup in current module list
                        var simpleName = typeName.Split('.').Last();
                        if (lookUp.TryGetValue(simpleName, out var dep))
                        {
                            Visit(dep, visited, sorted, stack, lookUp, registry);
                        }
                    }
                }
            }

            stack.Remove(type);
            visited.Add(type);
            sorted.Add(type);
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

            if (type.IsTopic)
            {
                sb.AppendLine($"{indent}@topic");
            }

            switch (type.Extensibility)
            {
                case DdsExtensibilityKind.Final:
                    sb.AppendLine($"{indent}@final");
                    break;
                case DdsExtensibilityKind.Appendable:
                    sb.AppendLine($"{indent}@appendable");
                    break;
                case DdsExtensibilityKind.Mutable:
                    sb.AppendLine($"{indent}@mutable");
                    break;
            }

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

        private void EmitUnion(StringBuilder sb, TypeInfo type, int indentLevel, GlobalTypeRegistry registry)
        {
            // Simplified port of existing logic with indentation
            string indent = GetIndent(indentLevel);
            string fieldIndent = GetIndent(indentLevel + 1);

            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) return; // Should throw

            var (switchType, _) = MapType(discriminator);

            switch (type.Extensibility)
            {
                case DdsExtensibilityKind.Final:
                    sb.AppendLine($"{indent}@final");
                    break;
                case DdsExtensibilityKind.Appendable:
                    sb.AppendLine($"{indent}@appendable");
                    break;
                case DdsExtensibilityKind.Mutable:
                    sb.AppendLine($"{indent}@mutable");
                    break;
            }

            sb.AppendLine($"{indent}union {type.Name} switch ({switchType}) {{");
            
            IdlTypeDefinition? enumDef = null;
            foreach(var t in registry.AllTypes)
            {
                 if (t.TypeInfo != null && t.TypeInfo.IsEnum)
                 {
                     string idlName = t.TypeInfo.Name;
                     if (!string.IsNullOrEmpty(t.TargetModule))
                         idlName = t.TargetModule.Replace(".", "::") + "::" + idlName;
                     
                     if (idlName == switchType) { enumDef = t; break; }
                     string csMapped = t.CSharpFullName.Replace(".", "::");
                     if (csMapped == switchType) { enumDef = t; break; }
                 }
            }

            foreach (var field in type.Fields)
            {
                if (field == discriminator) continue;
                // Simplified case generation
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr != null)
                {
                     foreach(var val in caseAttr.CaseValues)
                     {
                        string label = val!.ToString()!;
                        if (enumDef != null && val is int iVal)
                        {
                            if (iVal >= 0 && iVal < enumDef.TypeInfo!.EnumMembers.Count)
                                label = enumDef.TypeInfo.EnumMembers[iVal];
                        }
                        sb.AppendLine($"{fieldIndent}case {label}:");
                     }
                }
                else if (field.HasAttribute("DdsDefault") || field.HasAttribute("DdsDefaultCase"))
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
            
            // Handle Nullable
            if (typeName.EndsWith("?")) typeName = typeName.Substring(0, typeName.Length - 1);
            else if (typeName.StartsWith("Nullable<") || typeName.StartsWith("System.Nullable<"))
            {
                var s = typeName.IndexOf('<') + 1;
                var e = typeName.LastIndexOf('>');
                typeName = typeName.Substring(s, e - s);
            }

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

            // List<T>
            if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
            {
                var start = typeName.IndexOf('<') + 1;
                var end = typeName.LastIndexOf('>');
                var innerType = typeName.Substring(start, end - start);
                
                var innerField = new FieldInfo { TypeName = innerType };
                var (innerIdl, innerSuffix) = MapType(innerField);
                
                return ($"sequence<{innerIdl}>", "");
            }

            // Arrays
            if (typeName.EndsWith("[]"))
            {
                var elementTypeName = typeName.Substring(0, typeName.Length - 2);
                var innerField = new FieldInfo { TypeName = elementTypeName };
                
                // Propagate attributes if element is string, so we can detect string bound
                if (elementTypeName == "string" || elementTypeName == "System.String")
                {
                    innerField.Attributes = field.Attributes; 
                }

                var (innerIdl, innerSuffix) = MapType(innerField);
                
                // Check for ArrayLength (Fixed Array)
                var arrayLen = field.GetAttribute("ArrayLength");
                if (arrayLen != null && arrayLen.Arguments.Count > 0)
                {
                    string dims = "";
                    foreach(var arg in arrayLen.Arguments) dims += $"[{arg}]";
                    return (innerIdl, innerSuffix + dims);
                }

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
            if (typeName == "string" || typeName == "System.String")
            {
                var bound = field.GetAttribute("MaxLength")?.Arguments.FirstOrDefault() ?? 
                            field.GetAttribute("DdsString")?.Arguments.FirstOrDefault();
                
                if (bound != null) return ($"string<{bound}>", "");
                return ("string", "");
            }

            // Nested types
            if (field.Type != null)
            {
                // Use scoped name for cross-module reference
                return (field.Type.FullName.Replace(".", "::"), "");
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
            
            // Fallback to scoped name (e.g. Enums, Structs)
            return (typeName.Replace(".", "::"), "");
        }



        private string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }


        public static string SanitizeToCSymbol(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "_"; // Return a default safe symbol for empty inputs

            // 1. Replace anything that is NOT (^) alphanumeric or underscore with an underscore
            //    The '+' merges multiple bad chars into one underscore (optional, remove + if not wanted)
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");

            // 2. C symbols cannot start with a digit. 
            //    If the first char is a number, prepend an underscore.
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            return sanitized;
        }
    }


}
