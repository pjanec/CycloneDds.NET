using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen
{
    public class SerializerEmitter
    {
        public string EmitSerializer(TypeInfo type)
        {
            var sb = new StringBuilder();
            
            // Using directives
            sb.AppendLine("using CycloneDDS.Core;");
            sb.AppendLine("using System.Runtime.InteropServices;"); // Just in case
            sb.AppendLine();
            
            // Namespace
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }
            
            // Partial struct (assuming struct as per instructions)
            sb.AppendLine($"    public partial struct {type.Name}");
            sb.AppendLine("    {");
            
            // GetSerializedSize method
            EmitGetSerializedSize(sb, type);
            
            // Serialize method
            EmitSerialize(sb, type);
            
            // Close class
            sb.AppendLine("    }");
            
            // Close namespace
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}");
            }
            
            return sb.ToString();
        }
        
        private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public int GetSerializedSize(int currentOffset)");
            sb.AppendLine("        {");
            sb.AppendLine("            var sizer = new CdrSizer(currentOffset);");
            sb.AppendLine();
            sb.AppendLine("            // DHEADER (required for @appendable)");
            sb.AppendLine("            sizer.WriteUInt32(0);");
            sb.AppendLine();
            sb.AppendLine("            // Struct body");
            
            foreach (var field in type.Fields)
            {
                string sizerCall = GetSizerCall(field);
                sb.AppendLine($"            {sizerCall}; // {field.Name}");
            }
            
            sb.AppendLine();
            sb.AppendLine("            return sizer.GetSizeDelta(currentOffset);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void EmitSerialize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public void Serialize(ref CdrWriter writer)");
            sb.AppendLine("        {");
            sb.AppendLine("            // DHEADER");
            sb.AppendLine("            int dheaderPos = writer.Position;");
            sb.AppendLine("            writer.WriteUInt32(0);");
            sb.AppendLine();
            sb.AppendLine("            int bodyStart = writer.Position;");
            sb.AppendLine();
            sb.AppendLine("            // Struct body");
            
            foreach (var field in type.Fields)
            {
                string writerCall = GetWriterCall(field);
                sb.AppendLine($"            {writerCall}; // {field.Name}");
            }
            
            sb.AppendLine();
            sb.AppendLine("            // Patch DHEADER");
            sb.AppendLine("            int bodySize = writer.Position - bodyStart;");
            sb.AppendLine("            writer.PatchUInt32(dheaderPos, (uint)bodySize);");
            sb.AppendLine("        }");
        }
        
        private string GetSizerCall(FieldInfo field)
        {
            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"sizer.WriteFixedString(null, {size})";
            }

            string method = TypeMapper.GetSizerMethod(field.TypeName);
            if (method != null)
            {
                string dummy = "0";
                if (method == "WriteBool") dummy = "false";
                return $"sizer.{method}({dummy})";
            }
            else
            {
                // Nested struct - Must use Skip!
                 return $"sizer.Skip(default({field.TypeName}).GetSerializedSize(sizer.Position))";
            }
        }
        
        private string GetWriterCall(FieldInfo field)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            
            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"writer.WriteFixedString({fieldAccess}, {size})";
            }

            string method = TypeMapper.GetWriterMethod(field.TypeName);
            if (method != null)
            {
                return $"writer.{method}({fieldAccess})";
            }
            else
            {
                return $"{fieldAccess}.Serialize(ref writer)";
            }
        }

        private string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
