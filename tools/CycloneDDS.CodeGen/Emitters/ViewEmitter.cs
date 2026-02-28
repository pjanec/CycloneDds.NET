using System;
using System.Text;
using System.Linq;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Emitters
{
    public class ViewEmitter
    {
        public string EmitViewStruct(TypeInfo type, GlobalTypeRegistry? registry, bool generateUsings = true)
        {
            var sb = new StringBuilder();
            
            if (generateUsings)
            {
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine("using CycloneDDS.Core;");
                sb.AppendLine("using CycloneDDS.Runtime;");
                sb.AppendLine();
                sb.AppendLine("#pragma warning disable CS8600");
                sb.AppendLine("#pragma warning disable CS8601");
                sb.AppendLine("#pragma warning disable CS8603");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }

            var indent = !string.IsNullOrEmpty(type.Namespace) ? "    " : "";
            
            // Generate View Struct
            sb.AppendLine($"{indent}public ref struct {type.Name}View");
            sb.AppendLine($"{indent}{{");
            
            // Native Pointer
            sb.AppendLine($"{indent}    private unsafe readonly {type.Name}_Native* _ptr;");
            sb.AppendLine();

            // Constructor
            sb.AppendLine($"{indent}    public unsafe {type.Name}View({type.Name}_Native* ptr)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _ptr = ptr;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Properties
            foreach (var field in type.Fields)
            {
                 ProcessField(sb, field, indent + "    ", registry, type.IsUnion);
            }

            GenerateToManagedMethod(sb, type, registry, indent);

            sb.AppendLine($"{indent}}}"); // End struct
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}"); // End namespace
            }

            return sb.ToString();
        }

        private void ProcessField(StringBuilder sb, FieldInfo field, string indent, GlobalTypeRegistry? registry, bool isUnion)
        {
            Stack<string> nativeFieldPath = new Stack<string>();
            string nativeFieldName = field.Name;
            
            if (isUnion)
            {
                if (field.HasAttribute("DdsDiscriminator"))
                {
                    nativeFieldName = "_d";
                }
                else
                {
                    nativeFieldName = $"_u.{field.Name}";
                }
            }

            // Optional
            if (IsOptional(field.TypeName))
            {
                EmitOptionalProperty(sb, field, indent, nativeFieldName);
                return;
            }

            // Unions (FCDC-ZC013)
            TypeInfo? fieldTypeInfo = field.Type;
            if (fieldTypeInfo == null && registry != null)
            {
                if (registry.TryGetDefinition(field.TypeName, out var def) && def?.TypeInfo != null)
                {
                    fieldTypeInfo = def.TypeInfo;
                }
            }
            
            if (fieldTypeInfo != null && fieldTypeInfo.IsUnion)
            {
                // registry might be null, but EmitUnionProperty handles usage safely or we accept warning suppression if logic guarantees it. 
                // However, EmitUnionProperty signature expects GlobalTypeRegistry? so let's update that signature too.
                EmitUnionProperty(sb, fieldTypeInfo, field.Name, indent, registry!);
                return;
            }

            var resolvedTypeName = registry != null ? ResolveType(field.TypeName, registry) : field.TypeName;

            // Fixed String wrappers
            if (IsFixedString(resolvedTypeName))
            {
                var fixedView = GetFixedStringViewType(resolvedTypeName);
                var fixedType = GetFixedStringType(resolvedTypeName);
                sb.AppendLine($"{indent}public unsafe {fixedView} {field.Name} => new {fixedView}(({fixedType}*)&_ptr->{nativeFieldName});");
            }
            // Primitive
            else if (IsPrimitive(field.TypeName, registry))
            {
               sb.AppendLine($"{indent}public unsafe {resolvedTypeName} {field.Name} => _ptr->{nativeFieldName};");
            }
            // Boolean
            else if (IsBool(field.TypeName, registry))
            {
               sb.AppendLine($"{indent}public unsafe bool {field.Name} => _ptr->{nativeFieldName} != 0;");
            }
            // Enum
            else if (IsEnum(field, registry))
            {
                 sb.AppendLine($"{indent}public unsafe {resolvedTypeName} {field.Name} => ({resolvedTypeName})_ptr->{nativeFieldName};");
            }
            // String (Scalar)
            else if (IsString(field.TypeName, registry))
            {
               sb.AppendLine($"{indent}public unsafe ReadOnlySpan<byte> {field.Name}Raw => DdsTextEncoding.GetSpanFromPtr((IntPtr)_ptr->{nativeFieldName});");
               sb.AppendLine($"{indent}public unsafe string? {field.Name} => DdsTextEncoding.FromNativeUtf8((IntPtr)_ptr->{nativeFieldName});");
            }
            // Fixed Array
            else if (IsFixedArray(field))
            {
                 EmitFixedArrayProperty(sb, field, indent, nativeFieldName);
            }
            // Sequence
            else if (IsSequence(field))
            {
                // Sequence members in Union? Might be complicated. 
                // Using generic logic but nativeFieldName
                EmitSequenceProperty(sb, field, indent, registry, nativeFieldName);
            }
            // System.Guid
            else if (field.TypeName == "System.Guid" || field.TypeName == "Guid")
            {
               sb.AppendLine($"{indent}public unsafe System.Guid {field.Name} => _ptr->{nativeFieldName}.ToManaged();");
            }
            // System.DateTime
            else if (field.TypeName == "System.DateTime" || field.TypeName == "DateTime")
            {
               sb.AppendLine($"{indent}public unsafe System.DateTime {field.Name} => new System.DateTime(_ptr->{nativeFieldName}, System.DateTimeKind.Utc);");
            }
            // Nested Struct
            else
            {
                string typeName = field.TypeName;
                string fullName = typeName;

                if (registry != null)
                {
                    if (registry.TryGetDefinition(typeName, out var def))
                    {
                        if (def.TypeInfo != null) fullName = def.TypeInfo.FullName;
                        else fullName = def.CSharpFullName;
                    }
                    else
                    {
                         // Fallback attempt to resolve by short name if unique
                         var candidates = registry.AllTypes
                                            .Where(t => t.CSharpFullName.EndsWith("." + typeName) || t.CSharpFullName == typeName)
                                            .ToList();
                         if (candidates.Count == 1)
                         {
                             if (candidates[0].TypeInfo != null) fullName = candidates[0].TypeInfo.FullName;
                             else fullName = candidates[0].CSharpFullName;
                         }
                    }
                }

                // Has to be a struct view
                string viewName = $"{fullName}View";
                string nativeType = $"{fullName}_Native";
               sb.AppendLine($"{indent}public unsafe {viewName} {field.Name} => new {viewName}(({nativeType}*)&_ptr->{nativeFieldName});");
            }
        }
        
        private bool IsSequenceType(string typeName)
        {
             return typeName.EndsWith("[]") || typeName.Contains("List<") || typeName.Contains("Sequence<"); 
        }

        private void EmitSequenceProperty(StringBuilder sb, FieldInfo field, string indent, GlobalTypeRegistry? registry, string nativeFieldName)
        {
            var elementType = GetSequenceElementType(field);
            var propName = field.Name;
            
            if (IsPrimitive(elementType, registry) || IsBool(elementType, registry))
            {
                // FCDC-ZC009
                sb.AppendLine($"{indent}/// <summary>Gets {field.Name} as ReadOnlySpan (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe ReadOnlySpan<{elementType}> {propName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        return new ReadOnlySpan<{elementType}>(");
                sb.AppendLine($"{indent}            (void*)_ptr->{nativeFieldName}.Buffer,");
                sb.AppendLine($"{indent}            (int)_ptr->{nativeFieldName}.Length");
                sb.AppendLine($"{indent}        );");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
            }
            else if (IsString(elementType, registry))
            {
                // FCDC-ZC011
                sb.AppendLine($"{indent}/// <summary>Gets the number of {field.Name} elements.</summary>");
                sb.AppendLine($"{indent}public int {propName}Count");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        unsafe {{ return (int)_ptr->{nativeFieldName}.Length; }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($"{indent}/// <summary>Gets {field.Name} element at index as UTF-8 bytes (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe ReadOnlySpan<byte> Get{propName}Raw(int index)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (index < 0 || index >= {propName}Count)");
                sb.AppendLine($"{indent}        throw new ArgumentOutOfRangeException(nameof(index));");
                sb.AppendLine();
                sb.AppendLine($"{indent}    IntPtr* ptrArray = (IntPtr*)_ptr->{nativeFieldName}.Buffer;");
                sb.AppendLine($"{indent}    return DdsTextEncoding.GetSpanFromPtr(ptrArray[index]);");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($"{indent}/// <summary>Gets {field.Name} element at index as C# string (allocates).</summary>");
                sb.AppendLine($"{indent}public unsafe string? Get{propName}(int index)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (index < 0 || index >= {propName}Count)");
                sb.AppendLine($"{indent}        throw new ArgumentOutOfRangeException(nameof(index));");
                sb.AppendLine();
                sb.AppendLine($"{indent}    IntPtr* ptrArray = (IntPtr*)_ptr->{nativeFieldName}.Buffer;");
                sb.AppendLine($"{indent}    return DdsTextEncoding.FromNativeUtf8(ptrArray[index]);");
                sb.AppendLine($"{indent}}}");
            }
            else if (elementType == "System.Guid" || elementType == "Guid")
            {
                sb.AppendLine($"{indent}/// <summary>Gets the number of {field.Name} elements.</summary>");
                sb.AppendLine($"{indent}public int {propName}Count");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        unsafe {{ return (int)_ptr->{nativeFieldName}.Length; }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($"{indent}/// <summary>Gets {field.Name} element at specified index.</summary>");
                sb.AppendLine($"{indent}public unsafe System.Guid Get{propName}(int index)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (index < 0 || index >= {propName}Count)");
                sb.AppendLine($"{indent}        throw new ArgumentOutOfRangeException(nameof(index));");
                sb.AppendLine();
                sb.AppendLine($"{indent}    CycloneDDS.Runtime.Interop.DdsGuid* arr = (CycloneDDS.Runtime.Interop.DdsGuid*)_ptr->{nativeFieldName}.Buffer;");
                sb.AppendLine($"{indent}    return arr[index].ToManaged();");
                sb.AppendLine($"{indent}}}");
            }
           else if (elementType == "System.DateTime" || elementType == "DateTime")
            {
                sb.AppendLine($"{indent}/// <summary>Gets the number of {field.Name} elements.</summary>");
                sb.AppendLine($"{indent}public int {propName}Count");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        unsafe {{ return (int)_ptr->{nativeFieldName}.Length; }}");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();

                sb.AppendLine($"{indent}/// <summary>Gets {field.Name} element at specified index.</summary>");
                sb.AppendLine($"{indent}public unsafe System.DateTime Get{propName}(int index)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    if (index < 0 || index >= {propName}Count)");
                sb.AppendLine($"{indent}        throw new ArgumentOutOfRangeException(nameof(index));");
                sb.AppendLine();
                sb.AppendLine($"{indent}    long* arr = (long*)_ptr->{nativeFieldName}.Buffer;");
                sb.AppendLine($"{indent}    return new System.DateTime(arr[index], System.DateTimeKind.Utc);");
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                // Check if element is a valid View target
                bool isViewable = true;
                if (IsSequenceType(elementType))
                {
                    isViewable = false;
                }
                else if (registry != null && registry.TryGetDefinition(elementType, out var def))
                {
                    if (def?.TypeInfo == null || (!def.TypeInfo.IsStruct && !def.TypeInfo.IsUnion))
                    {
                        isViewable = false;
                    }
                }

                if (isViewable)
                {
                    // FCDC-ZC010: Struct Sequence
                    var elementView = $"{elementType}View";
                    var elementNative = $"{elementType}_Native";

                    sb.AppendLine($"{indent}/// <summary>Gets the number of {field.Name} elements.</summary>");
                    sb.AppendLine($"{indent}public int {propName}Count");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        unsafe {{ return (int)_ptr->{nativeFieldName}.Length; }}");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine();

                    sb.AppendLine($"{indent}/// <summary>Gets {field.Name} element at specified index.</summary>");
                    sb.AppendLine($"{indent}public unsafe {elementView} Get{propName}(int index)");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    if (index < 0 || index >= {propName}Count)");
                    sb.AppendLine($"{indent}        throw new ArgumentOutOfRangeException(nameof(index));");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}    {elementNative}* arr = ({elementNative}*)_ptr->{nativeFieldName}.Buffer;");
                    sb.AppendLine($"{indent}    return new {elementView}(&arr[index]);");
                    sb.AppendLine($"{indent}}}");
                }
                else
                {
                    sb.AppendLine($"{indent}// View for sequence of '{elementType}' (Field: {field.Name}) is not supported in ZeroCopy yet.");
                }
            }
        }

        private void EmitUnionProperty(StringBuilder sb, TypeInfo unionType, string memberName, string indent, GlobalTypeRegistry registry)
        {
            string propName = memberName;
            string fieldName = memberName;
            
            var viewName = $"{unionType.FullName}View";
            var nativeType = $"{unionType.FullName}_Native";
            sb.AppendLine($"{indent}/// <summary>Gets view for {memberName}.</summary>");
            sb.AppendLine($"{indent}public unsafe {viewName} {propName} => new {viewName}(({nativeType}*)&_ptr->{fieldName});");
            sb.AppendLine();
            
            // Find discriminator field
            var discriminatorField = unionType.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            string discriminatorType = discriminatorField?.TypeName ?? "int";
            string discriminatorEnum = discriminatorType;

            // Discriminator accessor
            sb.AppendLine($"{indent}/// <summary>Gets the discriminator for {memberName}.</summary>");
            sb.AppendLine($"{indent}public unsafe {discriminatorEnum} {propName}Kind");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    get");
            sb.AppendLine($"{indent}    {{");
            if (discriminatorEnum == "bool" || discriminatorEnum == "Boolean" || discriminatorEnum == "System.Boolean")
            {
                sb.AppendLine($"{indent}        unsafe {{ return _ptr->{fieldName}._d != 0; }}");
            }
            else
            {
                sb.AppendLine($"{indent}        unsafe {{ return ({discriminatorEnum})_ptr->{fieldName}._d; }}");
            }
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            // Typed accessors
            foreach (var member in unionType.Fields)
            {
                if (member == discriminatorField) continue;

                var caseAttr = member.GetAttribute("DdsCase");
                if (caseAttr == null) continue;

                string caseName = member.Name;
                string caseType = member.TypeName;
                string caseField = member.Name;
                
                var values = caseAttr.Arguments;
                var conditions = new List<string>();
                foreach (var val in values)
                {
                    string valStr = val?.ToString() ?? "0";
                    if (val is bool b) valStr = b ? "true" : "false";
                    
                    if (discriminatorEnum == "bool" || discriminatorEnum == "Boolean" || discriminatorEnum == "System.Boolean")
                    {
                        if (val is int iVal) valStr = (iVal != 0) ? "true" : "false";
                        conditions.Add($"{propName}Kind == {valStr}");
                    }
                    else
                    {
                        conditions.Add($"{propName}Kind == ({discriminatorEnum}){valStr}");
                    }
                }
                
                // Also check DdsDefaultCase?
                if (member.HasAttribute("DdsDefaultCase"))
                {
                    // Default logic placeholder
                }
                
                if (conditions.Count == 0) continue;

                string conditionExpr = string.Join(" || ", conditions);

                if (IsString(caseType, registry))
                {
                    sb.AppendLine($"{indent}/// <summary>Gets {memberName} as string if discriminator matches (allocates).</summary>");
                    sb.AppendLine($"{indent}public unsafe string? {propName}As{caseName}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        if ({conditionExpr})");
                    sb.AppendLine($"{indent}             return DdsTextEncoding.FromNativeUtf8((IntPtr)_ptr->{fieldName}._u.{caseField});");
                    sb.AppendLine($"{indent}        return null;");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                }
                else if (IsBool(caseType, registry))
                {
                    sb.AppendLine($"{indent}/// <summary>Gets {memberName} as bool if discriminator matches.</summary>");
                    sb.AppendLine($"{indent}public unsafe bool? {propName}As{caseName}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        if ({conditionExpr})");
                    sb.AppendLine($"{indent}             return _ptr->{fieldName}._u.{caseField} != 0;");
                    sb.AppendLine($"{indent}        return null;");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                }
                else if (IsPrimitive(caseType, registry) || IsEnum(member, registry))
                {
                    sb.AppendLine($"{indent}/// <summary>Gets {memberName} as {caseType} if discriminator matches.</summary>");
                    sb.AppendLine($"{indent}public unsafe {caseType}? {propName}As{caseName}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        if ({conditionExpr})");
                    sb.AppendLine($"{indent}             return ({caseType})_ptr->{fieldName}._u.{caseField};");
                    sb.AppendLine($"{indent}        return null;");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                }
                else if (caseType == "System.Guid" || caseType == "Guid")
                {
                    sb.AppendLine($"{indent}/// <summary>Gets {memberName} as System.Guid if discriminator matches.</summary>");
                    sb.AppendLine($"{indent}public unsafe System.Guid? {propName}As{caseName}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        if ({conditionExpr})");
                    sb.AppendLine($"{indent}             return _ptr->{fieldName}._u.{caseField}.ToManaged();");
                    sb.AppendLine($"{indent}        return null;");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                }
                else if (caseType == "System.DateTime" || caseType == "DateTime")
                {
                    sb.AppendLine($"{indent}/// <summary>Gets {memberName} as System.DateTime if discriminator matches.</summary>");
                    sb.AppendLine($"{indent}public unsafe System.DateTime? {propName}As{caseName}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    get");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        if ({conditionExpr})");
                    sb.AppendLine($"{indent}             return new System.DateTime(_ptr->{fieldName}._u.{caseField}, System.DateTimeKind.Utc);");
                    sb.AppendLine($"{indent}        return null;");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                }
                else
                {
                     // Complex type (Struct/Union)
                     var caseViewName = $"{caseType}View";
                     var caseNativeType = $"{caseType}_Native";
                     
                     sb.AppendLine($"{indent}/// <summary>Gets {memberName} as {caseType} view if discriminator matches. Throws if mismatch.</summary>");
                     sb.AppendLine($"{indent}public unsafe {caseViewName} {propName}As{caseName}");
                     sb.AppendLine($"{indent}{{");
                     sb.AppendLine($"{indent}    get");
                     sb.AppendLine($"{indent}    {{");
                     sb.AppendLine($"{indent}        if ({conditionExpr})");
                     sb.AppendLine($"{indent}             return new {caseViewName}(({caseNativeType}*)&_ptr->{fieldName}._u.{caseField});");
                     sb.AppendLine($"{indent}        throw new InvalidOperationException($\"Union discriminator mismatch: Expected {caseName}, but got {propName}Kind\");");
                     sb.AppendLine($"{indent}    }}");
                     sb.AppendLine($"{indent}}}");
                }
                sb.AppendLine();
            }
        }

        private void EmitFixedArrayProperty(StringBuilder sb, FieldInfo field, string indent, string nativeFieldName)
        {
             string propName = field.Name;
             string elementType = GetElementType(field.TypeName);
             int? arrayLen = GetArrayLength(field);
             
             sb.AppendLine($"{indent}/// <summary>Gets {field.Name} as ReadOnlySpan (zero-copy).</summary>");
             sb.AppendLine($"{indent}public unsafe ReadOnlySpan<{elementType}> {propName}");
             sb.AppendLine($"{indent}{{");
             sb.AppendLine($"{indent}    get");
             sb.AppendLine($"{indent}    {{");
             sb.AppendLine($"{indent}        {elementType}* ptr = _ptr->{nativeFieldName};");
             sb.AppendLine($"{indent}        return new ReadOnlySpan<{elementType}>(ptr, {arrayLen});");
             sb.AppendLine($"{indent}    }}");
             sb.AppendLine($"{indent}}}");
        }

        private void GenerateToManagedMethod(StringBuilder sb, TypeInfo type, GlobalTypeRegistry? registry, string indent)
        {
            sb.AppendLine($"{indent}public {type.Name} ToManaged()");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    unsafe");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        var target = new {type.Name}();");

            if (!type.IsUnion)
            {
                foreach (var field in type.Fields)
                {
                    GenerateToManagedFieldAssignment(sb, field, indent + "        ", registry);
                }
            }
            else
            {
                GenerateToManagedUnionBody(sb, type, indent + "        ", registry);
            }

            sb.AppendLine($"{indent}        return target;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");
        }

        private void GenerateToManagedUnionBody(StringBuilder sb, TypeInfo type, string indent, GlobalTypeRegistry? registry)
        {
            var discriminatorField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminatorField == null) return;

            var discProp = discriminatorField.Name;
            var discType = discriminatorField.TypeName; 
            var resolvedDiscType = registry != null ? ResolveType(discType, registry) : discType;

            sb.AppendLine($"{indent}target.{discProp} = this.{discProp};");
            
            foreach (var field in type.Fields)
            {
                if (field == discriminatorField) continue;
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr == null) continue;
                
                var conditions = new List<string>();
                foreach (var val in caseAttr.Arguments)
                {
                    string valStr = val?.ToString() ?? "0";
                    if (val is bool b) valStr = b ? "true" : "false";
                    
                    if (resolvedDiscType == "bool" || resolvedDiscType == "Boolean" || resolvedDiscType == "System.Boolean") 
                    {
                       // Handle integer constants representing booleans (e.g. from IDL 1/0)
                       if (val is int iVal) valStr = (iVal != 0) ? "true" : "false";
                       conditions.Add($"this.{discProp} == {valStr}");
                    }
                    else 
                       conditions.Add($"this.{discProp} == ({resolvedDiscType}){valStr}");
                }
                
                if (conditions.Count == 0) continue;
                string conditionExpr = string.Join(" || ", conditions);
                
                sb.AppendLine($"{indent}if ({conditionExpr})");
                sb.AppendLine($"{indent}{{");
                
                GenerateToManagedFieldAssignment(sb, field, indent + "    ", registry);
                
                sb.AppendLine($"{indent}}}");
            }
        }

        private void GenerateToManagedFieldAssignment(StringBuilder sb, FieldInfo field, string indent, GlobalTypeRegistry? registry)
        {
            var propName = field.Name;
            var targetProp = propName;
            
            if (IsOptional(field.TypeName))
            {
                var baseType = GetBaseType(field.TypeName);
                if (IsPrimitive(baseType, registry) || IsBool(baseType, registry) || IsEnum(field, registry) || 
                    baseType == "System.Guid" || baseType == "Guid" || baseType == "System.DateTime" || baseType == "DateTime")
                {
                    sb.AppendLine($"{indent}target.{targetProp} = this.{propName};");
                }
                else if (IsString(baseType, registry))
                {
                    sb.AppendLine($"{indent}target.{targetProp} = this.{propName};");
                }
                else
                {
                     sb.AppendLine($"{indent}if (this.Has{propName})");
                     sb.AppendLine($"{indent}{{");
                     sb.AppendLine($"{indent}    target.{targetProp} = this.{propName}.ToManaged();");
                     sb.AppendLine($"{indent}}}");
                }
                return;
            }

            if (IsPrimitive(field.TypeName, registry) || IsBool(field.TypeName, registry) || IsEnum(field, registry) || IsString(field.TypeName, registry))
            {
                 sb.AppendLine($"{indent}target.{targetProp} = this.{propName};");
            }
            else if (IsFixedArray(field))
            {
                 sb.AppendLine($"{indent}target.{targetProp} = this.{propName}.ToArray();");
            }
            else if (IsSequence(field))
            {
                 var elementType = GetSequenceElementType(field);
                 var targetCollectionType = "System.Collections.Generic.List";

                 if (IsPrimitive(elementType, registry) || IsBool(elementType, registry))
                 {
                     sb.AppendLine($"{indent}target.{targetProp} = new {targetCollectionType}<{elementType}>(this.{propName}.ToArray());");
                 }
                 else if (IsString(elementType, registry))
                 {
                     sb.AppendLine($"{indent}target.{targetProp} = new {targetCollectionType}<string>(this.{propName}Count);");
                     sb.AppendLine($"{indent}for (int i = 0; i < this.{propName}Count; i++)");
                     sb.AppendLine($"{indent}{{");
                     sb.AppendLine($"{indent}    target.{targetProp}.Add(this.Get{propName}(i));");
                     sb.AppendLine($"{indent}}}");
                 }
                 else if (elementType == "System.Guid" || elementType == "Guid" || elementType == "System.DateTime" || elementType == "DateTime")
                 {
                     sb.AppendLine($"{indent}target.{targetProp} = new {targetCollectionType}<{elementType}>(this.{propName}Count);");
                     sb.AppendLine($"{indent}for (int i = 0; i < this.{propName}Count; i++)");
                     sb.AppendLine($"{indent}{{");
                     sb.AppendLine($"{indent}    target.{targetProp}.Add(this.Get{propName}(i));");
                     sb.AppendLine($"{indent}}}");
                 }
                 else
                 {
                     var elementTargetType = elementType; 
                     sb.AppendLine($"{indent}target.{targetProp} = new {targetCollectionType}<{elementTargetType}>(this.{propName}Count);");
                     sb.AppendLine($"{indent}for (int i = 0; i < this.{propName}Count; i++)");
                     sb.AppendLine($"{indent}{{");
                     sb.AppendLine($"{indent}    target.{targetProp}.Add(this.Get{propName}(i).ToManaged());");
                     sb.AppendLine($"{indent}}}");
                 }
            }
            else if (field.TypeName == "System.Guid" || field.TypeName == "Guid" || 
                     field.TypeName == "System.DateTime" || field.TypeName == "DateTime")
            {
                 sb.AppendLine($"{indent}target.{targetProp} = this.{propName};");
            }
            else
            {
                 sb.AppendLine($"{indent}target.{targetProp} = this.{propName}.ToManaged();");
            }
        }

        // Helpers
        private string ResolveType(string typeName, GlobalTypeRegistry? registry)
        {
             if (registry != null && registry.TryGetDefinition(typeName, out var def))
             {
                 if (def != null && def.IsAlias && !string.IsNullOrEmpty(def.BaseType)) 
                     return ResolveType(def.BaseType, registry);
             }
             return typeName;
        }

        private bool IsPrimitive(string typeName, GlobalTypeRegistry? registry = null)
        {
             var resolved = registry != null ? ResolveType(typeName, registry) : typeName;
             var lower = resolved.ToLower();
             if (lower.StartsWith("system.")) lower = lower.Substring(7);
             
             return lower == "byte" || lower == "sbyte" || 
                    lower == "short" || lower == "int16" || 
                    lower == "ushort" || lower == "uint16" || 
                    lower == "int" || lower == "int32" || 
                    lower == "uint" || lower == "uint32" || 
                    lower == "long" || lower == "int64" || 
                    lower == "ulong" || lower == "uint64" || 
                    lower == "float" || lower == "single" || 
                    lower == "double" || lower == "char"; 
        }

        private bool IsBool(string typeName, GlobalTypeRegistry? registry = null) 
        {
             var resolved = registry != null ? ResolveType(typeName, registry) : typeName;
             return resolved.ToLower() == "bool" || resolved == "Boolean" || resolved == "System.Boolean";
        }

        private bool IsString(string typeName, GlobalTypeRegistry? registry = null)
        {
             var resolved = registry != null ? ResolveType(typeName, registry) : typeName;
             return resolved == "string" || resolved == "System.String";
        }

        private bool IsEnum(FieldInfo field, GlobalTypeRegistry? registry)
        {
             if (field.Type != null && field.Type.IsEnum) return true;
             if (registry != null && registry.TryGetDefinition(field.TypeName, out var def) && def?.TypeInfo != null && def.TypeInfo.IsEnum) return true;
             return false;
        }

        private bool IsSequence(FieldInfo field)
        {
            return field.TypeName.EndsWith("[]") || field.TypeName.Contains("List<");
        }
        
        private string GetSequenceElementType(FieldInfo field)
        {
            if (field.GenericType != null) return field.GenericType.FullName;
            
            var name = field.TypeName;
            if (name.EndsWith("[]")) return name.Substring(0, name.Length - 2);
            
            int start = name.IndexOf('<');
            int end = name.LastIndexOf('>');
            if (start != -1 && end != -1)
            {
                 return name.Substring(start + 1, end - start - 1).Trim();
            }
            return name;
        }

        private bool IsFixedArray(FieldInfo field)
        {
             return GetArrayLength(field).HasValue;
        }

        private int? GetArrayLength(FieldInfo field)
        {
             var attr = field.GetAttribute("ArrayLength");
             if (attr != null && attr.Arguments.Count > 0 && attr.Arguments[0] is int i) return i;
             return null;
        }

        private string GetElementType(string typeName)
        {
             if (typeName.EndsWith("[]")) return typeName.Substring(0, typeName.Length - 2);
             return typeName;
        }

        private bool IsOptional(string typeName)
        {
            return typeName.EndsWith("?") || typeName.StartsWith("System.Nullable<") || typeName.StartsWith("Nullable<");
        }

        private string GetBaseType(string typeName)
        {
            if (typeName.EndsWith("?")) return typeName.Substring(0, typeName.Length - 1);
            if (typeName.StartsWith("System.Nullable<") && typeName.EndsWith(">"))
            {
                return typeName.Substring(16, typeName.Length - 17);
            }
            if (typeName.StartsWith("Nullable<") && typeName.EndsWith(">"))
            {
                return typeName.Substring(9, typeName.Length - 10);
            }
            return typeName;
        }

        private void EmitOptionalProperty(StringBuilder sb, FieldInfo field, string indent, string nativeFieldName)
        {
            var baseType = GetBaseType(field.TypeName);
            var propName = field.Name;

            if (IsPrimitive(baseType) || IsBool(baseType))
            {
                // Primitive/Bool: Native type is IntPtr
                sb.AppendLine($"{indent}/// <summary>Gets optional {field.Name} (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe {baseType}? {propName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (_ptr->{nativeFieldName} == default) return null;");
                sb.AppendLine($"{indent}        return *({baseType}*)_ptr->{nativeFieldName};");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
            }
            else if (IsString(baseType))
            {
                // Optional String
                sb.AppendLine($"{indent}/// <summary>Gets optional {field.Name} (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe string? {propName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (_ptr->{nativeFieldName} == default) return null;");
                sb.AppendLine($"{indent}        return DdsTextEncoding.FromNativeUtf8((IntPtr)_ptr->{nativeFieldName});");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
            }
            else if (baseType == "System.Guid" || baseType == "Guid")
            {
                sb.AppendLine($"{indent}/// <summary>Gets optional {field.Name} (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe System.Guid? {propName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (_ptr->{nativeFieldName} == default) return null;");
                sb.AppendLine($"{indent}        return (*(CycloneDDS.Runtime.Interop.DdsGuid*)_ptr->{nativeFieldName}).ToManaged();");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
            }
            else if (baseType == "System.DateTime" || baseType == "DateTime")
            {
                sb.AppendLine($"{indent}/// <summary>Gets optional {field.Name} (zero-copy).</summary>");
                sb.AppendLine($"{indent}public unsafe System.DateTime? {propName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        if (_ptr->{nativeFieldName} == default) return null;");
                sb.AppendLine($"{indent}        return new System.DateTime(*(long*)_ptr->{nativeFieldName}, System.DateTimeKind.Utc);");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                // Optional Struct
                var viewName = $"{baseType}View";
                var nativeType = $"{baseType}_Native";

                sb.AppendLine($"{indent}/// <summary>Returns true if optional {field.Name} is set.</summary>");
                sb.AppendLine($"{indent}public unsafe bool Has{propName} => _ptr->{nativeFieldName} != default;");

                sb.AppendLine($"{indent}/// <summary>Gets view for optional {field.Name}. Accessing this when Has{propName} is false may lead to undefined behavior.</summary>");
                sb.AppendLine($"{indent}public unsafe {viewName} {propName} => new {viewName}(({nativeType}*)_ptr->{nativeFieldName});");
            }
        }

        private bool IsFixedString(string typeName)
        {
            var resolved = typeName.Replace("global::", string.Empty);
            return resolved.EndsWith("FixedString32") ||
                   resolved.EndsWith("FixedString64") ||
                   resolved.EndsWith("FixedString128") ||
                   resolved.EndsWith("FixedString256");
        }

        private string GetFixedStringViewType(string typeName)
        {
            var resolved = typeName.Replace("global::", string.Empty);
            return resolved.EndsWith("FixedString32") ? "CycloneDDS.Schema.FixedString32View" :
                   resolved.EndsWith("FixedString64") ? "CycloneDDS.Schema.FixedString64View" :
                   resolved.EndsWith("FixedString128") ? "CycloneDDS.Schema.FixedString128View" :
                   "CycloneDDS.Schema.FixedString256View";
        }

        private string GetFixedStringType(string typeName)
        {
            var resolved = typeName.Replace("global::", string.Empty);
            return resolved.EndsWith("FixedString32") ? "CycloneDDS.Schema.FixedString32" :
                   resolved.EndsWith("FixedString64") ? "CycloneDDS.Schema.FixedString64" :
                   resolved.EndsWith("FixedString128") ? "CycloneDDS.Schema.FixedString128" :
                   "CycloneDDS.Schema.FixedString256";
        }
    }
}
