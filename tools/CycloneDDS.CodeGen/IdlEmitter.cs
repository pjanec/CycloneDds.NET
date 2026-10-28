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
                
                // 1. Generate #include directives.
                //    GetFileDependencies mirrors exactly what MapType emits as a scoped
                //    type reference, so every scoped name in the body gets a matching
                //    #include. Anything resolved only by file-name convention (i.e. the
                //    type registry had no entry, typically because the defining assembly
                //    was built without [assembly: DdsIdlMapping]) is reported in 'unresolved'
                //    so we can fail fast below instead of letting idlc emit a cryptic error.
                var unresolved = new List<UnresolvedTypeReference>();
                var dependencies = GetFileDependencies(fileGroup, registry, unresolved);
                ValidateDependencies(fileName, unresolved, outputDir);

                foreach (var depFile in dependencies.OrderBy(f => f))
                {
                    sb.AppendLine($"#include \"{depFile}.idl\"");
                }

                if (dependencies.Any())
                    sb.AppendLine();
                
                // 2. Emit the file's types in dependency order. A type may use a type of another module
                //    in the same file (Other::UsesOuter -> Outer::Holder), so the order is computed over
                //    the whole file and each run of same-module types gets its own (re)opened module block.
                var ordered = TopologicalSort(fileGroup, registry).ToList();
                for (int i = 0; i < ordered.Count; )
                {
                    var module = ordered[i].TargetModule;
                    var run = new List<IdlTypeDefinition>();
                    while (i < ordered.Count && ordered[i].TargetModule == module) run.Add(ordered[i++]);
                    EmitModuleHierarchy(sb, module, run, registry);
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

        /// <summary>
        /// Computes the set of IDL files that must be <c>#include</c>d by the given type group.
        ///
        /// This MUST stay consistent with <see cref="MapType"/>: every field type that MapType
        /// renders as a scoped (<c>Foo::Bar</c>) reference needs a matching include, otherwise
        /// idlc fails later with "Scoped name ... cannot be resolved". The include and the type
        /// name used to be derived independently (name from the Roslyn FQN, include only on a
        /// registry hit) — when those two paths disagreed the generator silently produced an
        /// un-includable IDL. They now share this single resolution path.
        /// </summary>
        /// <param name="unresolved">
        /// When provided, collects references that could only be resolved by the file-name
        /// convention (no registry / no <c>[assembly: DdsIdlMapping]</c>). The caller validates
        /// these so the failure is actionable rather than a cryptic idlc error.
        /// </param>
        private HashSet<string> GetFileDependencies(
            IEnumerable<IdlTypeDefinition> types,
            GlobalTypeRegistry registry,
            List<UnresolvedTypeReference>? unresolved = null)
        {
            var dependencies = new HashSet<string>();

            foreach (var type in types)
            {
                if (type.TypeInfo == null) continue;
                foreach (var field in type.TypeInfo.Fields)
                {
                    foreach (var referenced in ReferencedUserTypeNames(field.TypeName))
                    {
                        if (registry.TryGetDefinition(referenced, out var dep) && dep != null)
                        {
                            // Known type — use its authoritative target file. Skip self-references.
                            if (dep.TargetIdlFile != type.TargetIdlFile)
                                dependencies.Add(dep.TargetIdlFile);
                        }
                        else
                        {
                            // MapType WILL emit a scoped reference to this type, so an include is
                            // mandatory. The registry didn't know it; fall back to the file-name
                            // convention (<SimpleName>.idl) and flag it for validation.
                            string simple = SimpleName(referenced);
                            if (!string.Equals(simple, type.TypeInfo.Name, StringComparison.Ordinal))
                            {
                                dependencies.Add(simple);
                                unresolved?.Add(new UnresolvedTypeReference(
                                    referenced, simple, type.TypeInfo.Name, field.Name));
                            }
                        }
                    }
                }
            }
            return dependencies;
        }

        /// <summary>
        /// Fail-fast guard: every reference that could only be resolved by naming convention
        /// must have an actual <c>&lt;file&gt;.idl</c> in the output dir, or idlc will fail.
        /// If the file IS present we self-heal (emit the include) but warn, because the missing
        /// registry entry points at a real problem in the dependency's build.
        /// </summary>
        private void ValidateDependencies(string fileName, List<UnresolvedTypeReference> unresolved, string outputDir)
        {
            foreach (var u in unresolved)
            {
                string scoped = u.TypeFullName.Replace(".", "::");
                if (File.Exists(Path.Combine(outputDir, $"{u.IdlFile}.idl")))
                {
                    Console.Error.WriteLine(
                        $"WARNING [{fileName}.idl]: type '{u.TypeFullName}' (referenced by " +
                        $"{u.OwnerType}.{u.FieldName}) has no IDL mapping in the type registry. " +
                        $"Including '{u.IdlFile}.idl' by file-name convention. The assembly defining " +
                        $"'{u.TypeFullName}' was likely built without [assembly: DdsIdlMapping] " +
                        $"(its CycloneDDS codegen did not run — e.g. a stale obj/.../CycloneDdsGenerated). " +
                        $"Rebuild that assembly so this resolves deterministically.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot resolve IDL dependency for type '{u.TypeFullName}', referenced by " +
                        $"'{u.OwnerType}.{u.FieldName}'. The generated IDL emits this as '{scoped}', but " +
                        $"no '{u.IdlFile}.idl' exists in '{outputDir}' and the type is absent from the " +
                        $"registry. Likely causes: (1) the assembly defining '{u.TypeFullName}' was built " +
                        $"without [assembly: DdsIdlMapping] (CycloneDDS codegen didn't run there — check for " +
                        $"a stale obj/.../CycloneDdsGenerated folder), or (2) a missing reference to that " +
                        $"assembly. Without a fix, idlc fails with \"Scoped name '{scoped}' cannot be resolved\".");
                }
            }
        }

        /// <summary>
        /// Yields the fully-qualified names of user-defined types a field references, after
        /// unwrapping the same wrappers <see cref="MapType"/> unwraps (Nullable, List&lt;&gt;,
        /// BoundedSeq&lt;&gt;, arrays). Built-in/primitive types yield nothing — they never need
        /// an include. This is the include-side mirror of MapType's scoped-name emission.
        /// </summary>
        private IEnumerable<string> ReferencedUserTypeNames(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) yield break;
            typeName = typeName!.Trim().TrimEnd('?');

            // Nullable<T>
            if (typeName.StartsWith("Nullable<") || typeName.StartsWith("System.Nullable<"))
            {
                foreach (var inner in ReferencedUserTypeNames(ExtractGenericArg(typeName))) yield return inner;
                yield break;
            }

            // List<T> / BoundedSeq<T>
            if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.Contains("BoundedSeq<"))
            {
                foreach (var inner in ReferencedUserTypeNames(ExtractGenericArg(typeName))) yield return inner;
                yield break;
            }

            // T[]
            if (typeName.EndsWith("[]"))
            {
                foreach (var inner in ReferencedUserTypeNames(typeName.Substring(0, typeName.Length - 2))) yield return inner;
                yield break;
            }

            // Everything MapType maps to a primitive / inline IDL type needs no include.
            if (IsBuiltinIdlType(typeName)) yield break;

            // Anything left is a user-defined struct/enum/union → MapType emits a scoped name.
            yield return typeName;
        }

        private static string ExtractGenericArg(string typeName)
        {
            int start = typeName.IndexOf('<') + 1;
            int end = typeName.LastIndexOf('>');
            return (start > 0 && end > start) ? typeName.Substring(start, end - start).Trim() : typeName;
        }

        private static string SimpleName(string fullName)
        {
            int lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        /// <summary>
        /// True for every type <see cref="MapType"/> renders as a built-in/inline IDL type
        /// (primitive, FixedString, Guid/DateTime/TimeSpan, System.Numerics vectors, string).
        /// Keep this list in lock-step with MapType's special cases.
        /// </summary>
        private static bool IsBuiltinIdlType(string typeName)
        {
            if (typeName.Contains("FixedString32") || typeName.Contains("FixedString64") ||
                typeName.Contains("FixedString128") || typeName.Contains("FixedString256"))
                return true;

            switch (typeName)
            {
                // Primitives
                case "byte": case "System.Byte":
                case "sbyte": case "System.SByte":
                case "short": case "System.Int16":
                case "ushort": case "System.UInt16":
                case "int": case "System.Int32":
                case "uint": case "System.UInt32":
                case "long": case "System.Int64":
                case "ulong": case "System.UInt64":
                case "float": case "System.Single":
                case "double": case "System.Double":
                case "bool": case "System.Boolean":
                case "char": case "System.Char":
                // Standard value types
                case "Guid": case "System.Guid":
                case "DateTime": case "System.DateTime":
                case "DateTimeOffset": case "System.DateTimeOffset":
                case "TimeSpan": case "System.TimeSpan":
                // System.Numerics
                case "Vector2": case "System.Numerics.Vector2":
                case "Vector3": case "System.Numerics.Vector3":
                case "Vector4": case "System.Numerics.Vector4":
                case "Quaternion": case "System.Numerics.Quaternion":
                case "Matrix4x4": case "System.Numerics.Matrix4x4":
                // Managed string
                case "string": case "System.String":
                    return true;
                default:
                    return false;
            }
        }

        private void EmitModuleHierarchy(StringBuilder sb, string modulePath, IEnumerable<IdlTypeDefinition> types, GlobalTypeRegistry registry)
        {
            if (modulePath == "<global namespace>") modulePath = "";
            var modules = modulePath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
            
            // Open modules
            int indent = 0;
            foreach (var module in modules)
            {
                sb.AppendLine($"{GetIndent(indent)}module {IdlIdentifier.Escape(module)} {{");
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
            var members = new HashSet<IdlTypeDefinition>(typeList);
            // Simple names only as a last resort, and only when unique among the types being sorted.
            var bySimpleName = typeList.Where(t => t.TypeInfo != null)
                .GroupBy(t => t.TypeInfo!.Name)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First());
            var visited = new HashSet<IdlTypeDefinition>();
            var sorted = new List<IdlTypeDefinition>();
            var stack = new HashSet<IdlTypeDefinition>();

            foreach (var type in typeList)
                Visit(type, visited, sorted, stack, members, bySimpleName, registry);

            return sorted;
        }

        /// <summary>The type, among those being sorted, that a field of <paramref name="owner"/> refers to.</summary>
        private IdlTypeDefinition? ResolveDependency(string typeName, IdlTypeDefinition owner, HashSet<IdlTypeDefinition> members,
            Dictionary<string, IdlTypeDefinition> bySimpleName, GlobalTypeRegistry registry)
        {
            if (registry.TryGetDefinition(typeName, out var def) && def != null && members.Contains(def))
                return def;
            var ns = owner.TypeInfo?.Namespace;
            if (!typeName.Contains('.') && !string.IsNullOrEmpty(ns)
                && registry.TryGetDefinition(ns + "." + typeName, out def) && def != null && members.Contains(def))
                return def;
            return bySimpleName.TryGetValue(typeName.Split('.').Last(), out var bySimple) ? bySimple : null;
        }

        private void Visit(IdlTypeDefinition type, HashSet<IdlTypeDefinition> visited, List<IdlTypeDefinition> sorted, HashSet<IdlTypeDefinition> stack,
            HashSet<IdlTypeDefinition> members, Dictionary<string, IdlTypeDefinition> bySimpleName, GlobalTypeRegistry registry)
        {
            if (visited.Contains(type)) return;
            if (stack.Contains(type)) throw new Exception($"Circular dependency detected within module for type {type.TypeInfo?.Name}"); // Should be caught earlier by file cycle check, but intra-file cycles are possible

            stack.Add(type);

            if (type.TypeInfo != null)
            {
                foreach (var field in type.TypeInfo.Fields)
                {
                    var dep = ResolveDependency(StripGenerics(field.TypeName), type, members, bySimpleName, registry);
                    if (dep != null && dep != type)
                        Visit(dep, visited, sorted, stack, members, bySimpleName, registry);
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
            typeName = typeName.TrimEnd('?');
            return typeName.EndsWith("[]") ? typeName.Substring(0, typeName.Length - 2) : typeName;
        }

        // Updated helper methods to accept indent
        
        private void EmitStruct(StringBuilder sb, TypeInfo type, int indentLevel)
        {
            string indent = GetIndent(indentLevel);
            string fieldIndent = GetIndent(indentLevel + 1);

            if (type.IsTopic)
            {
                // ME1-C03 / D06: always emit plain @topic — idlc ignores and warns about name= parameter
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

            sb.AppendLine($"{indent}struct {IdlIdentifier.Escape(type.Name)} {{");
            
            foreach (var field in type.Fields)
            {
                var (idlType, suffix) = MapType(field);
                string annotations = "";
                
                if (field.HasAttribute("DdsKey"))
                    annotations = "@key ";
                
                if (field.HasAttribute("DdsOptional"))
                    annotations += "@optional ";

                annotations += MemberIdAnnotation(field);
                
                sb.AppendLine($"{fieldIndent}{annotations}{idlType} {IdlIdentifier.Escape(field.Name)}{suffix};");
            }
            
            sb.AppendLine($"{indent}}};");
        }

        /// <summary>
        /// <c>@id(N)</c> for a member with [DdsId(N)]: the member id is on the wire for mutable types
        /// (and part of the type identity), so it must reach the IDL idlc builds the serializer from.
        /// </summary>
        private static string MemberIdAnnotation(FieldInfo field)
        {
            var id = field.GetAttribute("DdsId");
            return id != null && id.Arguments.Count > 0 ? $"@id({id.Arguments[0]}) " : string.Empty;
        }

        private void EmitEnum(StringBuilder sb, TypeInfo type, int indentLevel)
        {
             string indent = GetIndent(indentLevel);
             string memberIndent = GetIndent(indentLevel + 1);

             // Cyclone DDS 11 idlc reads an enumerator as a SIGNED value of @bit_bound bits (a `byte` enum holds 0..127, a `uint` enum
             // 0..2147483647). Say so here, in C# terms, instead of letting idlc fail later with "Enumerator overflow".
             ValidateEnumValuesFitSignedRange(type);

             // ME1-T01: emit @bit_bound annotation for narrow enums (8-bit or 16-bit backing)
             if (type.EnumBitBound == 8)
                 sb.AppendLine($"{indent}@bit_bound(8)");
             else if (type.EnumBitBound == 16)
                 sb.AppendLine($"{indent}@bit_bound(16)");

             sb.AppendLine($"{indent}enum {IdlIdentifier.Escape(type.Name)} {{");
             
             for (int i = 0; i < type.EnumMembers.Count; i++)
             {
                 string comma = (i < type.EnumMembers.Count - 1) ? "," : "";
                 // Emit @value annotation when the C# integer value differs from the sequential IDL ordinal.
                 // Only emit for non-negative values: IDL enum ordinals are unsigned and @value(-N) is invalid.
                 long memberValue = i < type.EnumMemberValues.Count ? type.EnumMemberValues[i] : i;
                 bool hasExplicitValue = memberValue >= 0 && memberValue != i;
                 string valueAnnotation = hasExplicitValue ? $"@value({memberValue}) " : "";
                 sb.AppendLine($"{memberIndent}{valueAnnotation}{IdlIdentifier.Escape(type.EnumMembers[i])}{comma}");
             }
             
             sb.AppendLine($"{indent}}};");
        }

        /// <summary>
        /// An IDL enum with <c>@bit_bound(N)</c> holds the SIGNED range <c>-2^(N-1) .. 2^(N-1)-1</c> (Cyclone DDS 11's idlc). A C# enum whose
        /// underlying type is unsigned or wider than its values suggest can declare members outside it; fail here with a message that names the
        /// C# type, the member and what to change.
        /// </summary>
        private static void ValidateEnumValuesFitSignedRange(TypeInfo type)
        {
            int bits = type.EnumBitBound;
            long max = (1L << (bits - 1)) - 1;
            long min = -(1L << (bits - 1));

            var offenders = new List<string>();
            for (int i = 0; i < type.EnumMembers.Count && i < type.EnumMemberValues.Count; i++)
            {
                long v = type.EnumMemberValues[i];
                if (v < min || v > max) offenders.Add($"{type.EnumMembers[i]} = {v}");
            }
            if (offenders.Count == 0) return;

            string advice = bits switch
            {
                8 => "Lower the value to 127 or less to keep the enum one byte on the wire, or give it a wider underlying type (ushort/short, or int) - which changes its size on the wire.",
                16 => "Lower the value to 32767 or less to keep the enum two bytes on the wire, or give it a wider underlying type (int) - which changes its size on the wire.",
                _ => "An IDL enumerator cannot exceed 2147483647: use a smaller value, or keep the field as a plain integer instead of an enum (a 'uint' enum with the top bit set, such as a flags mask, cannot be an IDL enum)."
            };
            throw new InvalidOperationException(
                $"Enum '{type.FullName}' has member(s) outside the range its IDL type can hold: {string.Join(", ", offenders)}. " +
                $"It maps to IDL @bit_bound({bits}), and Cyclone DDS's IDL compiler (idlc) reads enumerator values as signed numbers, so the allowed range is {min}..{max} " +
                $"(a 'byte' enum holds 0..127, not 0..255). {advice}");
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

            sb.AppendLine($"{indent}union {IdlIdentifier.Escape(type.Name)} switch ({switchType}) {{");
            
            IdlTypeDefinition? enumDef = null;
            foreach(var t in registry.AllTypes)
            {
                 if (t.TypeInfo != null && t.TypeInfo.IsEnum)
                 {
                     string idlName = t.TypeInfo.Name;
                     if (!string.IsNullOrEmpty(t.TargetModule))
                         idlName = t.TargetModule.Replace(".", "::") + "::" + idlName;
                     idlName = IdlIdentifier.EscapeScoped(idlName);
                     
                     if (idlName == switchType) { enumDef = t; break; }
                     string csMapped = IdlIdentifier.EscapeScoped(t.CSharpFullName.Replace(".", "::"));
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
                        if (enumDef != null && val != null)
                        {
                            try
                            {
                                long iVal = Convert.ToInt64(val);
                                // Find the enum member whose actual numeric value matches iVal,
                                // rather than assuming value == index (which breaks for non-sequential enums).
                                int memberIndex = enumDef.TypeInfo!.EnumMemberValues.Count > 0
                                    ? enumDef.TypeInfo.EnumMemberValues.IndexOf(iVal)
                                    : (iVal >= 0 && iVal < enumDef.TypeInfo.EnumMembers.Count ? (int)iVal : -1);

                                if (memberIndex >= 0)
                                    label = IdlIdentifier.Escape(enumDef.TypeInfo.EnumMembers[memberIndex]);
                            }
                            catch (InvalidCastException) { }
                            catch (OverflowException) { }
                        }
                        
                        if (switchType == "boolean")
                        {
                            if (val is int i) label = (i != 0) ? "TRUE" : "FALSE";
                            else if (val is bool b) label = b ? "TRUE" : "FALSE";
                            else if (label == "1") label = "TRUE";
                            else if (label == "0") label = "FALSE";
                        }
                        
                        sb.AppendLine($"{fieldIndent}case {label}:");
                     }
                }
                else if (field.HasAttribute("DdsDefault") || field.HasAttribute("DdsDefaultCase"))
                {
                     sb.AppendLine($"{fieldIndent}default:");
                }

                var (idlType, suffix) = MapType(field);
                sb.AppendLine($"{fieldIndent}    {MemberIdAnnotation(field)}{idlType} {IdlIdentifier.Escape(field.Name)}{suffix};");
            }
            sb.AppendLine($"{indent}}};");
        }


        
        private (string Type, string Suffix) MapType(FieldInfo field)
        {
            // C# fixed-size buffers: `public fixed byte Buf[64];`
            // Treat as a fixed-length IDL array of the element type.
            if (field.IsFixedSizeBuffer)
            {
                var innerField = new FieldInfo { TypeName = field.TypeName };
                var (innerIdl, innerSuffix) = MapType(innerField);
                return (innerIdl, innerSuffix + $"[{field.FixedSize}]");
            }

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
                return (IdlIdentifier.EscapeScoped(field.Type.FullName.Replace(".", "::")), "");
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
            return (IdlIdentifier.EscapeScoped(typeName.Replace(".", "::")), "");
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

    /// <summary>
    /// A field's reference to a user-defined type that the IDL emitter could resolve only by
    /// file-name convention (it was absent from the type registry). Surfaced by
    /// <see cref="IdlEmitter.GetFileDependencies"/> for fail-fast validation.
    /// </summary>
    internal sealed class UnresolvedTypeReference
    {
        public UnresolvedTypeReference(string typeFullName, string idlFile, string ownerType, string fieldName)
        {
            TypeFullName = typeFullName;
            IdlFile = idlFile;
            OwnerType = ownerType;
            FieldName = fieldName;
        }

        /// <summary>Fully-qualified C# name of the referenced type (e.g. Fdp.Toolkit.Diagnostics.Gizmos.PipelineTarget).</summary>
        public string TypeFullName { get; }
        /// <summary>IDL file name (without extension) assumed by the &lt;SimpleName&gt;.idl convention.</summary>
        public string IdlFile { get; }
        /// <summary>Name of the type whose field made the reference.</summary>
        public string OwnerType { get; }
        /// <summary>Name of the referencing field.</summary>
        public string FieldName { get; }
    }

}
