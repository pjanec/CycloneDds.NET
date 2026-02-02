using System.Text;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Emitters
{
    public class ViewExtensionsEmitter
    {
        public string EmitExtensions(TypeInfo type, GlobalTypeRegistry registry)
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
            
            sb.AppendLine($"{indent}public static class {type.Name}Extensions");
            sb.AppendLine($"{indent}{{");
            
            sb.AppendLine($"{indent}    public static {type.Name}View AsView(this DdsSample<{type.Name}> sample)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        unsafe {{ return new {type.Name}View(({type.Name}_Native*)sample.NativePtr); }}");
            sb.AppendLine($"{indent}    }}");
            
            sb.AppendLine($"{indent}}}"); // End class
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}"); // End namespace
            }

            return sb.ToString();
        }
    }
}
