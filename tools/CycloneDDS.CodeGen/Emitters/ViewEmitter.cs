using System.Text;
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
                 if (IsPrimitive(field.TypeName))
                 {
                     sb.AppendLine($"{indent}    public unsafe {field.TypeName} {field.Name} => _ptr->{field.Name};");
                 }
                 else if (field.TypeName == "bool" || field.TypeName == "Boolean" || field.TypeName == "System.Boolean")
                 {
                     sb.AppendLine($"{indent}    public unsafe bool {field.Name} => _ptr->{field.Name} != 0;");
                 }
                 else if (field.Type != null && field.Type.IsEnum)
                 {
                     sb.AppendLine($"{indent}    public unsafe {field.TypeName} {field.Name} => ({field.TypeName})_ptr->{field.Name};");
                 }
                 else if (field.TypeName == "string" || field.TypeName == "System.String")
                 {
                     sb.AppendLine($"{indent}    public unsafe ReadOnlySpan<byte> {field.Name}Raw => DdsTextEncoding.GetSpanFromPtr(_ptr->{field.Name});");
                     sb.AppendLine($"{indent}    public unsafe string? {field.Name} => DdsTextEncoding.FromNativeUtf8(_ptr->{field.Name});");
                 }
                 else
                 {
                     var arrayLen = GetArrayLength(field);
                     if (arrayLen.HasValue)
                     {
                         string elementType = GetElementType(field.TypeName);
                         sb.AppendLine($"{indent}    public unsafe ReadOnlySpan<{elementType}> {field.Name}");
                         sb.AppendLine($"{indent}    {{");
                         sb.AppendLine($"{indent}        get");
                         sb.AppendLine($"{indent}        {{");
                         sb.AppendLine($"{indent}            return new ReadOnlySpan<{elementType}>(&_ptr->{field.Name}[0], {arrayLen.Value});");
                         sb.AppendLine($"{indent}        }}");
                         sb.AppendLine($"{indent}    }}");
                     }
                 }
            }

            sb.AppendLine($"{indent}}}"); // End struct
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}"); // End namespace
            }

            return sb.ToString();
        }

        private bool IsPrimitive(string typeName)
        {
             var lower = typeName.ToLower();
             return lower == "byte" || lower == "sbyte" || lower == "short" || lower == "ushort" || 
                    lower == "int" || lower == "uint" || lower == "long" || lower == "ulong" || 
                    lower == "float" || lower == "double" || lower == "char"; 
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
    }
}
