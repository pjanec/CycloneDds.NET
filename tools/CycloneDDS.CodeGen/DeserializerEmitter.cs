using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen
{
    public class DeserializerEmitter
    {
        public string EmitDeserializer(TypeInfo type)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("using CycloneDDS.Core;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }
            
            EmitPartialStruct(sb, type);
            EmitViewStruct(sb, type);
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}");
            }
            
            return sb.ToString();
        }
        
        private void EmitPartialStruct(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine($"    public partial struct {type.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static {type.Name}View Deserialize(ref CdrReader reader)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var view = new {type.Name}View();");
            
            // DHEADER
            sb.AppendLine("            reader.Align(4);");
            sb.AppendLine("            uint dheader = reader.ReadUInt32();");
            sb.AppendLine("            int endPos = reader.Position + (int)dheader;");
            
            foreach(var field in type.Fields)
            {
                sb.AppendLine($"            if (reader.Position < endPos)");
                sb.AppendLine("            {");
                string readCall = GetReadCall(field);
                sb.AppendLine($"                {readCall};");
                sb.AppendLine("            }");
            }
            
            sb.AppendLine();
            sb.AppendLine("            if (reader.Position < endPos)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.Seek(endPos);");
            sb.AppendLine("            }");
            
            sb.AppendLine("            return view;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        private void EmitViewStruct(StringBuilder sb, TypeInfo type)
        {
             sb.AppendLine($"    public ref struct {type.Name}View");
             sb.AppendLine("    {");
             foreach(var field in type.Fields)
             {
                 string typeName = MapToViewType(field);
                 sb.AppendLine($"        public {typeName} {field.Name};");
                 
                 // String convenience accessor
                 if (field.TypeName == "string" && field.HasAttribute("DdsManaged")) {
                      sb.AppendLine($"        public string Get{field.Name}() => Encoding.UTF8.GetString({field.Name});");
                 }
             }
             
             // ToOwned
             sb.AppendLine($"        public {type.Name} ToOwned()");
             sb.AppendLine("        {");
             sb.AppendLine($"            var instance = new {type.Name}();");
             foreach(var field in type.Fields)
             {
                 sb.AppendLine($"            instance.{field.Name} = {MapToOwnedConversion(field)};");
             }
             sb.AppendLine("            return instance;");
             sb.AppendLine("        }");
             
             sb.AppendLine("    }");
        }

        private string MapToViewType(FieldInfo field)
        {
            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
                return "ReadOnlySpan<byte>";
            
            if (field.TypeName.StartsWith("BoundedSeq"))
            {
                string elem = ExtractSequenceElementType(field.TypeName);
                // If primitive, return ReadOnlySpan<T>
                if (IsPrimitive(elem))
                    return $"ReadOnlySpan<{elem}>";
                // Else fallback (not fully supported zero-copy)
                // For now use array?
                return $"{elem}[]"; 
            }
            
            if (IsPrimitive(field.TypeName))
                return field.TypeName;
            
            // Nested struct
            return $"{field.TypeName}View";
        }
        
        private string GetReadCall(FieldInfo field)
        {
            int align = GetAlignment(field.TypeName);
            string alignCall = align > 1 ? $"reader.Align({align}); " : "";
            
            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
            {
                return $"reader.Align(4); view.{field.Name} = reader.ReadStringBytes()";
            }
            
            if (field.TypeName.StartsWith("BoundedSeq"))
            {
                return EmitSequenceReader(field);
            }
            
            if (IsPrimitive(field.TypeName))
            {
                 string method = TypeMapper.GetSizerMethod(field.TypeName).Replace("Write", "Read"); // Method names match?
                 // WriteInt32 -> ReadInt32
                 return $"{alignCall}view.{field.Name} = reader.{method}()";
            }
            
            // Nested
            return $"{alignCall}view.{field.Name} = {field.TypeName}.Deserialize(ref reader)";
        }
        
        private string EmitSequenceReader(FieldInfo field)
        {
            string elem = ExtractSequenceElementType(field.TypeName);
            if (IsPrimitive(elem))
            {
                int elemSize = GetSize(elem);
                // reader.Align(4) (Header) -> ReadUInt32 -> length
                // Align(element) ? NO.
                // If primitive sequence, elements are packed naturally? 
                // CdrSizer emitted Align BEFORE loop. And Align INSIDE loop.
                // So elements are aligned.
                // If elements are aligned, they might have gaps.
                // ReadOnlySpan view requires contiguous memory.
                // IF GAPS EXIST, WE CANNOT RETURN SPAN.
                // `int` (align 4) in sequence: 4,4,4. Contiguous.
                // `long` (align 8) in sequence: 8,8,8. Contiguous.
                // `short` (align 2): 2,2,2. Contiguous.
                // Is there any primitive where Align > Size? No.
                // Is there any case where `Align` inside loop causes gaps?
                // Only if previous item ended at misaligned address.
                // Primitives are Size == Align (except maybe bool? 1 byte, align 1).
                // So Primitives are always contiguous.
                // So we CAN use Span.
                
                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            reader.Align({GetAlignment(elem)});
            view.{field.Name} = MemoryMarshal.Cast<byte, {elem}>(reader.ReadFixedBytes((int){field.Name}_len * {elemSize}))";
            }
            
            // Non-primitive sequence
            return $"/* Sequence<{elem}> not fully supported in View */";
        }
        
        private string MapToOwnedConversion(FieldInfo field)
        {
            if (field.TypeName == "string")
                return $"Encoding.UTF8.GetString({field.Name})"; // Assuming field.Name in View is Span
            
            // Nested
            if (!IsPrimitive(field.TypeName) && !field.TypeName.StartsWith("BoundedSeq"))
                return $"{field.Name}.ToOwned()";

            if (field.TypeName.StartsWith("BoundedSeq"))
            {
                 string elem = ExtractSequenceElementType(field.TypeName);
                 if (IsPrimitive(elem))
                     return $"new BoundedSeq<{elem}>({field.Name}.ToArray().ToList())"; // Simplified
            }

            return field.Name;
        }

        private int GetAlignment(string typeName)
        {
            // Same as SerializerEmitter
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
        
        private int GetSize(string typeName)
        {
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
            int open = typeName.IndexOf('<');
            int close = typeName.LastIndexOf('>');
            if (open != -1 && close != -1)
            {
                string content = typeName.Substring(open + 1, close - open - 1);
                int comma = content.IndexOf(',');
                if (comma != -1) return content.Substring(0, comma).Trim();
                return content.Trim();
            }
            return "int";
        }

        private bool IsPrimitive(string typeName)
        {
            return typeName.ToLower() is 
                "byte" or "uint8" or "sbyte" or "int8" or "bool" or "boolean" or
                "short" or "int16" or "ushort" or "uint16" or
                "int" or "int32" or "uint" or "uint32" or "float" or
                "long" or "int64" or "ulong" or "uint64" or "double";
        }
    }
}
