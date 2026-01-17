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
            
            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionDeserializeBody(sb, type);
            }
            else
            {
                int currentId = 0;
                foreach(var field in type.Fields)
                {
                    int fieldId = currentId++;

                    if (IsOptional(field))
                    {
                        EmitOptionalReader(sb, field, fieldId);
                    }
                    else
                    {
                        sb.AppendLine($"            if (reader.Position < endPos)");
                        sb.AppendLine("            {");
                        string readCall = GetReadCall(field);
                        sb.AppendLine($"                {readCall};");
                        sb.AppendLine("            }");
                    }
                }
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

        private void EmitOptionalReader(StringBuilder sb, FieldInfo field, int fieldId)
        {
            string baseType = GetBaseType(field.TypeName);
            var nonOptField = new FieldInfo { Name = field.Name, TypeName = baseType, Attributes = field.Attributes, Type = field.Type };
            
            sb.AppendLine($"            // Optional {field.Name}");
            sb.AppendLine("            {");
            sb.AppendLine("                int emHeaderPos = reader.Position;");
            sb.AppendLine("                bool isPresent = false;");
            sb.AppendLine("                if (reader.Position + 4 <= endPos)");
            sb.AppendLine("                {");
            sb.AppendLine("                    uint emHeader = reader.ReadUInt32();");
            // EMHEADER: (Length << 3) | ID
            sb.AppendLine("                    ushort id = (ushort)(emHeader & 0x7);");
            sb.AppendLine($"                    if (id == {fieldId})");
            sb.AppendLine("                    {");
            sb.AppendLine("                        isPresent = true;");
            sb.AppendLine("                    }");
            sb.AppendLine("                    else");
            sb.AppendLine("                    {");
            sb.AppendLine("                        reader.Seek(emHeaderPos);");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            
            sb.AppendLine("                if (isPresent)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {GetReadCall(nonOptField)};");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            if (IsReferenceType(baseType))
                sb.AppendLine($"                    view.{field.Name} = null;");
            else
                sb.AppendLine($"                    view.{field.Name} = null;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }

        private void EmitUnionDeserializeBody(StringBuilder sb, TypeInfo type)
        {
            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) throw new Exception($"Union {type.Name} missing [DdsDiscriminator] field");
            
            // Read Discriminator
            sb.AppendLine($"            if (reader.Position < endPos)");
            sb.AppendLine("            {");
            sb.AppendLine($"                {GetReadCall(discriminator)};");
            sb.AppendLine("            }");

            sb.AppendLine($"            switch (({GetDiscriminatorCastType(discriminator.TypeName)})view.{discriminator.Name})");
            sb.AppendLine("            {");
            
            foreach (var field in type.Fields)
            {
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr != null)
                {
                    foreach (var val in caseAttr.CaseValues)
                    {
                        sb.AppendLine($"                case {val}:");
                    }
                    sb.AppendLine($"                    if (reader.Position < endPos) {{ {GetReadCall(field)}; }}");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    if (reader.Position < endPos) {{ {GetReadCall(defaultField)}; }}");
                sb.AppendLine("                    break;");
            }
            else
            {
                sb.AppendLine("                default:");
                // Unknown case: handled by the generic seek(endPos) outside.
                // But DHEADER logic says "Seek(EndPos)" for unknown cases.
                // The outer generic code `if (reader.Position < endPos) reader.Seek(endPos);` handles this!
                // So checking `switch` logic, it executes one branch. If that branch consumes data, `reader.Position` advances.
                // If unknown branch (default empty), `reader.Position` stays at discriminator end.
                // Outer code sees `Position < endPos` and skips remainder. Correct.
                sb.AppendLine("                    break;");
            }
            
            sb.AppendLine("            }");
        }

        private string GetDiscriminatorCastType(string typeName)
        {
             return "int";
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
             
             if (type.HasAttribute("DdsUnion"))
             {
                 var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
                 if (discriminator != null)
                 {
                     sb.AppendLine($"            instance.{discriminator.Name} = {MapToOwnedConversion(discriminator)};");
                     
                     sb.AppendLine($"            switch (({GetDiscriminatorCastType(discriminator.TypeName)})instance.{discriminator.Name})");
                     sb.AppendLine("            {");
                     
                     foreach (var field in type.Fields)
                     {
                         var caseAttr = field.GetAttribute("DdsCase");
                         if (caseAttr != null)
                         {
                             foreach (var val in caseAttr.CaseValues)
                             {
                                 sb.AppendLine($"                case {val}:");
                             }
                             sb.AppendLine($"                    instance.{field.Name} = {MapToOwnedConversion(field)};");
                             sb.AppendLine("                    break;");
                         }
                     }
                     
                     var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
                     if (defaultField != null)
                     {
                         sb.AppendLine("                default:");
                         sb.AppendLine($"                    instance.{defaultField.Name} = {MapToOwnedConversion(defaultField)};");
                         sb.AppendLine("                    break;");
                     }
                     else
                     {
                         sb.AppendLine("                default: break;");
                     }
                     
                     sb.AppendLine("            }");
                 }
             }
             else
             {
                 foreach(var field in type.Fields)
                 {
                     sb.AppendLine($"            instance.{field.Name} = {MapToOwnedConversion(field)};");
                 }
             }
             
             sb.AppendLine("            return instance;");
             sb.AppendLine("        }");
             
             sb.AppendLine("    }");
        }

        private string MapToViewType(FieldInfo field)
        {
            if (IsOptional(field))
            {
                string baseType = GetBaseType(field.TypeName);
                string viewType = MapBaseToViewType(baseType, field);
                if (IsReferenceType(baseType))
                   return viewType; // string? is already nullable ref type (in context) or just string
                return $"{viewType}?"; 
            }
            return MapBaseToViewType(field.TypeName, field);
        }

        private string MapBaseToViewType(string typeName, FieldInfo field) // Refactored
        {
            if (typeName == "string" && field.HasAttribute("DdsManaged"))
                return "ReadOnlySpan<byte>"; // Usually optionals use string?, but View mapping uses Span for Managed?
            // If optional string? -> string? field.
            // If Managed string? -> ReadOnlySpan<byte>? (Span cannot be null?)
            // Span is struct. Nullable<Span> is illegal.
            // So for Managed string?, we probably can't use ReadOnlySpan<byte>?
            // We use ReadOnlySpan<byte> and Empty means null? Or we use separate bool HasField?
            // But View struct has fields.
            // For now, let's assume Optional Strings are just string? (if not Managed) or we fallback to string?
            if (typeName == "string") return "string?"; 

            if (typeName.StartsWith("BoundedSeq"))
            {
                string elem = ExtractSequenceElementType(typeName);
                if (IsPrimitive(elem))
                    return $"ReadOnlySpan<{elem}>"; // Span cannot be null
                // BoundedSeq? -> use array or List?
                return $"{elem}[]"; 
            }
            
            if (IsPrimitive(typeName))
                return typeName;
            
            return $"{typeName}View";
        }
        
        private string GetReadCall(FieldInfo field)
        {
            int align = GetAlignment(field.TypeName);
            string alignCall = align > 1 ? $"reader.Align({align}); " : "";
            
            if (field.TypeName == "string")
            {
                if (field.HasAttribute("DdsManaged"))
                    return $"reader.Align(4); view.{field.Name} = reader.ReadStringBytes()";
                return $"reader.Align(4); view.{field.Name} = Encoding.UTF8.GetString(reader.ReadStringBytes().ToArray())";
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
            if (IsOptional(field))
            {
                string baseType = GetBaseType(field.TypeName);
                string access = $"this.{field.Name}";
                
                // TODO: Handle Optional Managed Strings (ReadOnlySpan)
                if (baseType == "string")
                    return access; 

                if (!IsReferenceType(baseType))
                {
                    // Struct/Primitive
                    return access; 
                }
            }

            if (field.TypeName == "string" && field.HasAttribute("DdsManaged"))
                return $"Encoding.UTF8.GetString(this.{field.Name})";

            return MapBaseToOwnedConversion(field.TypeName, field.Name);
        }

        private string MapBaseToOwnedConversion(string typeName, string fieldName)
        {
            if (typeName == "string")
                return fieldName; 
            
            if (!IsPrimitive(typeName) && !typeName.StartsWith("BoundedSeq"))
                return $"{fieldName}.ToOwned()";

            if (typeName.StartsWith("BoundedSeq"))
            {
                 string elem = ExtractSequenceElementType(typeName);
                 if (IsPrimitive(elem))
                     return $"new BoundedSeq<{elem}>({fieldName}.ToArray().ToList())"; 
            }

            return fieldName;
        }

        private bool IsOptional(FieldInfo field)
        {
            return field.TypeName.EndsWith("?");
        }

        private string GetBaseType(string typeName)
        {
            if (typeName.EndsWith("?"))
                return typeName.Substring(0, typeName.Length - 1);
            return typeName;
        }

        private bool IsReferenceType(string typeName)
        {
            return typeName == "string" || typeName.StartsWith("BoundedSeq");
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
