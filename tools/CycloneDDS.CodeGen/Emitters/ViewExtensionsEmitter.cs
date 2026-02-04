using System.Text;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Emitters
{
    public class ViewExtensionsEmitter
    {
        public string EmitExtensions(TypeInfo type, GlobalTypeRegistry? registry = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using CycloneDDS.Core;");
            sb.AppendLine("using CycloneDDS.Runtime;");

            bool hasNamespace = !string.IsNullOrEmpty(type.Namespace);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }

            EmitExtensions(sb, type, registry);

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        public void EmitExtensions(StringBuilder sb, TypeInfo type, GlobalTypeRegistry? registry = null)
        {
            var indent = "    ";
            
            sb.AppendLine($"{indent}public static class {type.Name}Extensions");
            sb.AppendLine($"{indent}{{");
            
            sb.AppendLine($"{indent}    public static {type.Name}View AsView(this DdsSample<{type.Name}> sample)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        unsafe {{ return new {type.Name}View(({type.Name}_Native*)sample.NativePtr); }}");
            sb.AppendLine($"{indent}    }}");
            
            sb.AppendLine($"{indent}}}"); // End class
        }
    }
}
