using System;
using System.Text;
using System.Linq;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Emitters
{
    public class ViewEmitter
    {
        public string EmitViewStruct(TypeInfo type, GlobalTypeRegistry registry)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using CycloneDDS.Core;");
            sb.AppendLine("using CycloneDDS.Runtime;");
            sb.AppendLine();
            
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
            sb.AppendLine($"{indent}    internal unsafe {type.Name}View({type.Name}_Native* ptr)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        _ptr = ptr;");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();

            // Properties
            foreach (var field in type.Fields)
            {
                 ProcessField(sb, field, indent + "    ", registry, type.IsUnion);
            }

            sb.AppendLine($"{indent}}}"); // End struct
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}"); // End namespace
            }

            return sb.ToString();
        }

        private void ProcessField(StringBuilder sb, FieldInfo field, string indent, GlobalTypeRegistry registry, bool isUnion)
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
            TypeInfo fieldTypeInfo = field.Type;
            if (fieldTypeInfo == null && registry != null)
            {
                if (registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null)
                {
                    fieldTypeInfo = def.TypeInfo;
                }
            }
            
            if (fieldTypeInfo != null && fieldTypeInfo.IsUnion)
            {
                EmitUnionProperty(sb, fieldTypeInfo, field.Name, indent, registry);
                return;
            }

            // Primitive
            if (IsPrimitive(field.TypeName))
            {
                sb.AppendLine($"{indent}public unsafe {field.TypeName} {ToPascalCase(field.Name)} => _ptr->{nativeFieldName};");
            }
            // Boolean
            else if (IsBool(field.TypeName))
            {
                sb.AppendLine($"{indent}public unsafe bool {ToPascalCase(field.Name)} => _ptr->{nativeFieldName} != 0;");
            }
            // Enum
            else if (IsEnum(field, registry))
            {
                 sb.AppendLine($"{indent}public unsafe {field.TypeName} {ToPascalCase(field.Name)} => ({field.TypeName})_ptr->{nativeFieldName};");
            }
            // String (Scalar)
            else if (IsString(field.TypeName))
            {
                sb.AppendLine($"{indent}public unsafe ReadOnlySpan<byte> {ToPascalCase(field.Name)}Raw => DdsTextEncoding.GetSpanFromPtr((IntPtr)_ptr->{nativeFieldName});");
                sb.AppendLine($"{indent}public unsafe string? {ToPascalCase(field.Name)} => DdsTextEncoding.FromNativeUtf8((IntPtr)_ptr->{nativeFieldName});");
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
            // Nested Struct
            else
            {
                // Has to be a struct view
                string viewName = $"{field.TypeName}View";
                sb.AppendLine($"{indent}public unsafe {viewName} {ToPascalCase(field.Name)} => new {viewName}(&_ptr->{nativeFieldName});");
            }
        }
        
        private bool IsSequenceType(string typeName)
        {
             return typeName.EndsWith("[]") || typeName.Contains("List<") || typeName.Contains("Sequence<"); 
        }

        private void EmitSequenceProperty(StringBuilder sb, FieldInfo field, string indent, GlobalTypeRegistry registry, string nativeFieldName)
        {
            var elementType = GetSequenceElementType(field);
            var propName = ToPascalCase(field.Name);
            
            if (IsPrimitive(elementType))
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
            else if (IsString(elementType))
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
                    if (def.TypeInfo == null || (!def.TypeInfo.IsStruct && !def.TypeInfo.IsUnion))
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
            string propName = ToPascalCase(memberName);
            string fieldName = memberName;
            
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

                string caseName = ToPascalCase(member.Name);
                string caseType = member.TypeName;
                string caseField = member.Name;
                
                var values = caseAttr.Arguments;
                var conditions = new List<string>();
                foreach (var val in values)
                {
                    string valStr = val.ToString();
                    if (val is bool b) valStr = b ? "true" : "false";
                    conditions.Add($"{propName}Kind == ({discriminatorEnum}){valStr}");
                }
                
                // Also check DdsDefaultCase?
                if (member.HasAttribute("DdsDefaultCase"))
                {
                    // Default logic placeholder
                }
                
                if (conditions.Count == 0) continue;

                string conditionExpr = string.Join(" || ", conditions);

                if (IsString(caseType))
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
                else if (IsBool(caseType))
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
                else if (IsPrimitive(caseType) || IsEnum(member, registry))
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
                else
                {
                     sb.AppendLine($"{indent}// Union member {propName}As{caseName} (Type: {caseType}) accessor not generated (Complex Type).");
                }
                sb.AppendLine();
            }
        }

        private void EmitFixedArrayProperty(StringBuilder sb, FieldInfo field, string indent, string nativeFieldName)
        {
             string propName = ToPascalCase(field.Name);
             string elementType = GetElementType(field.TypeName);
             int? arrayLen = GetArrayLength(field);
             
             sb.AppendLine($"{indent}/// <summary>Gets {field.Name} as ReadOnlySpan (zero-copy).</summary>");
             sb.AppendLine($"{indent}public unsafe ReadOnlySpan<{elementType}> {propName}");
             sb.AppendLine($"{indent}{{");
             sb.AppendLine($"{indent}    get");
             sb.AppendLine($"{indent}    {{");
             sb.AppendLine($"{indent}        fixed ({elementType}* ptr = _ptr->{nativeFieldName})");
             sb.AppendLine($"{indent}        {{");
             sb.AppendLine($"{indent}            return new ReadOnlySpan<{elementType}>(ptr, {arrayLen});");
             sb.AppendLine($"{indent}        }}");
             sb.AppendLine($"{indent}    }}");
             sb.AppendLine($"{indent}}}");
        }

        // Helpers
        private bool IsPrimitive(string typeName)
        {
             var lower = typeName.ToLower();
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

        private bool IsBool(string typeName) => typeName.ToLower() == "bool" || typeName == "Boolean" || typeName == "System.Boolean";

        private bool IsString(string typeName) => typeName == "string" || typeName == "System.String";

        private bool IsEnum(FieldInfo field, GlobalTypeRegistry registry)
        {
             if (field.Type != null && field.Type.IsEnum) return true;
             if (registry != null && registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum) return true;
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

        private string ToPascalCase(string output)
        {
            if (string.IsNullOrEmpty(output)) return output;
            return char.ToUpper(output[0]) + output.Substring(1);
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
            var propName = ToPascalCase(field.Name);

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
    }
}
