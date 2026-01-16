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
            sb.AppendLine("            sizer.Align(4);");
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
            sb.AppendLine("            writer.Align(4);");
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
            // 1. Strings (Variable)
            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
            {
                 return $"sizer.Align(4); sizer.WriteString(this.{ToPascalCase(field.Name)})";
            }

            // 2. Sequences
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
            {
                 return EmitSequenceSizer(field);
            }

            // 3. Fixed Strings
            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"sizer.Align(1); sizer.WriteFixedString(null, {size})";
            }

            string method = TypeMapper.GetSizerMethod(field.TypeName);
            if (method != null)
            {
                string dummy = "0";
                if (method == "WriteBool") dummy = "false";
                int align = GetAlignment(field.TypeName);
                return $"sizer.Align({align}); sizer.{method}({dummy})";
            }
            else
            {
                // Nested struct
                // Use actual instance for variable sizing logic
                return $"sizer.Skip(this.{ToPascalCase(field.Name)}.GetSerializedSize(sizer.Position))";
            }
        }
        
        private string GetWriterCall(FieldInfo field)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            
            // 1. Strings (Variable)
            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
            {
                 return $"writer.Align(4); writer.WriteString({fieldAccess})";
            }

            // 2. Sequences
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
            {
                 return EmitSequenceWriter(field);
            }

            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"writer.Align(1); writer.WriteFixedString({fieldAccess}, {size})";
            }

            string method = TypeMapper.GetWriterMethod(field.TypeName);
            if (method != null)
            {
                int align = GetAlignment(field.TypeName);
                return $"writer.Align({align}); writer.{method}({fieldAccess})";
            }
            else
            {
                return $"{fieldAccess}.Serialize(ref writer)";
            }
        }

        private string EmitSequenceSizer(FieldInfo field)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractSequenceElementType(field.TypeName);
            
            // For primitive sequences, we can loop calling WritePrimitive(0)
            // This handles alignment correctly via CdrSizer methods.
            string sizerMethod = TypeMapper.GetSizerMethod(elementType);
            
            if (sizerMethod != null)
            {
                string dummy = "0";
                if (sizerMethod == "WriteBool") dummy = "false";
                int align = GetAlignment(elementType);
                
                return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                sizer.Align({align}); sizer.{sizerMethod}({dummy});
            }}";
            }
            
            // For nested structs or strings in sequence
            // If element is string
            if (elementType == "string") 
            {
                return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                sizer.Align(4); sizer.WriteString({fieldAccess}[i]);
            }}";
            }
             
            // Nested structs
            return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                sizer.Skip({fieldAccess}[i].GetSerializedSize(sizer.Position));
            }}";
        }

        private string EmitSequenceWriter(FieldInfo field)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractSequenceElementType(field.TypeName);
            
            string writerMethod = TypeMapper.GetWriterMethod(elementType);
            int align = GetAlignment(elementType);
            
            string loopBody;
            if (writerMethod != null)
            {
                loopBody = $"writer.Align({align}); writer.{writerMethod}({fieldAccess}[i]);";
            }
            else if (elementType == "string")
            {
                loopBody = $"writer.Align(4); writer.WriteString({fieldAccess}[i]);";
            }
            else
            {
                // Nested struct - need to handle ref writer if needed, but struct array access returns value.
                // We need to call Serialize on the element.
                // If it takes `ref writer`, we can pass it.
                // But `this.Prop[i]` returns a copy if it's a struct and using indexer?
                // `BoundedSeq` indexer returns T. T is struct. It returns a copy.
                // `Serialize` modifies writer. Passing `ref writer` is fine.
                // BUT calling method on r-value copy?
                // `GetSerializedSize` is fine.
                // `Serialize` logic:
                // var item = this.Prop[i];
                // item.Serialize(ref writer);
                loopBody = $@"var item = {fieldAccess}[i];
                item.Serialize(ref writer);";
            }

            return $@"writer.Align(4); writer.WriteUInt32((uint){fieldAccess}.Count);
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                {loopBody}
            }}";
        }

        private int GetAlignment(string typeName)
        {
            if (typeName == "string") return 4;
            if (typeName.StartsWith("BoundedSeq") || typeName.Contains("BoundedSeq<")) return 4;
            if (typeName.Contains("FixedString")) return 1;
            
            return typeName.ToLower() switch
            {
                "byte" or "uint8" or "sbyte" or "int8" or "bool" or "boolean" => 1,
                "short" or "int16" or "ushort" or "uint16" => 2,
                "int" or "int32" or "uint" or "uint32" or "float" => 4,
                "long" or "int64" or "ulong" or "uint64" or "double" => 8,
                _ => 1
            };
        }

        private string ExtractSequenceElementType(string typeName)
        {
            // Format: BoundedSeq<Type> or BoundedSeq<Type, 100> (if that exists)
            // Or fully qualified.
            int open = typeName.IndexOf('<');
            int close = typeName.LastIndexOf('>');
            if (open != -1 && close != -1)
            {
                string content = typeName.Substring(open + 1, close - open - 1);
                // If there is comma, take first part
                int comma = content.IndexOf(',');
                if (comma != -1)
                {
                    return content.Substring(0, comma).Trim();
                }
                return content.Trim();
            }
            return "int"; // Fallback
        }

        private bool IsVariableType(FieldInfo field)
        {
            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
                return true;
            
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
                return true;
            
            // Check if nested struct is variable
            if (field.Type != null && HasVariableFields(field.Type))
                return true;
            
            return false;
        }

        private bool HasVariableFields(TypeInfo type)
        {
            return type.Fields.Any(f => IsVariableType(f));
        }

        private string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
