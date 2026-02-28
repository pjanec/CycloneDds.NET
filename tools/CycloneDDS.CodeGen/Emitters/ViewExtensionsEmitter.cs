using System.Text;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Emitters
{
    public class ViewExtensionsEmitter
    {
        public string EmitExtensions(TypeInfo type, GlobalTypeRegistry registry, bool generateUsings = true)
        {
            var sb = new StringBuilder();
            
            if (generateUsings)
            {
                sb.AppendLine("using System;");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine("using CycloneDDS.Core;");
                sb.AppendLine("using CycloneDDS.Runtime;");
                sb.AppendLine();
            }
            
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
            
            sb.AppendLine();
            sb.AppendLine($"{indent}    public static System.Collections.Generic.List<{type.Name}> ReadCopied(this DdsReader<{type.Name}> reader, int maxSamples = 32)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        using var samples = reader.Read(maxSamples);");
            sb.AppendLine($"{indent}        var result = new System.Collections.Generic.List<{type.Name}>(samples.Count);");
            sb.AppendLine($"{indent}        foreach (var sample in samples)");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            if (sample.IsValid)");
            sb.AppendLine($"{indent}            {{");
            sb.AppendLine($"{indent}                result.Add(sample.AsView().ToManaged());");
            sb.AppendLine($"{indent}            }}");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}        return result;");
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
