using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen
{
    public class SerializerEmitter
    {
        public string EmitSerializer(TypeInfo type, bool generateUsings = true)
        {
            var sb = new StringBuilder();
            
            // Using directives
            if (generateUsings)
            {
                sb.AppendLine("using CycloneDDS.Core;");
                sb.AppendLine("using System.Runtime.InteropServices;"); // Just in case
                sb.AppendLine("using System.Text;");
                sb.AppendLine();
            }
            
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
        
        private bool IsAppendable(TypeInfo type)
        {
             if (type.Fields.Any(f => IsOptional(f))) return true;
             return type.HasAttribute("DdsAppendable") || type.HasAttribute("DdsMutable") || type.HasAttribute("Appendable");
        }

        private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public int GetSerializedSize(int currentOffset)");
            sb.AppendLine("        {");
            sb.AppendLine("            var sizer = new CdrSizer(currentOffset);");
            sb.AppendLine();
            
            if (IsAppendable(type))
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            sizer.Align(4);");
                sb.AppendLine("            sizer.WriteUInt32(0);");
                sb.AppendLine();
            }

            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionGetSerializedSizeBody(sb, type);
            }
            else
            {
                sb.AppendLine("            // Struct body");
                
                var fieldsWithIds = type.Fields.Select((f, i) => new { Field = f, Id = GetFieldId(f, i) }).OrderBy(x => x.Id).ToList();

                foreach (var item in fieldsWithIds)
                {
                    var field = item.Field;
                    if (IsOptional(field))
                    {
                        EmitOptionalSizer(sb, field);
                    }
                    else
                    {
                        string sizerCall = GetSizerCall(field);
                        sb.AppendLine($"            {sizerCall}; // {field.Name}");
                    }
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("            return sizer.GetSizeDelta(currentOffset);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void EmitUnionGetSerializedSizeBody(StringBuilder sb, TypeInfo type)
        {
            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) throw new Exception($"Union {type.Name} missing [DdsDiscriminator] field");

            // Write Discriminator
            string discSizer = GetSizerCall(discriminator);
            sb.AppendLine($"            {discSizer}; // Discriminator {discriminator.Name}");
            
            sb.AppendLine($"            switch (({GetDiscriminatorCastType(discriminator.TypeName)})this.{ToPascalCase(discriminator.Name)})");
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
                    sb.AppendLine($"                    {GetSizerCall(field)};");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    {GetSizerCall(defaultField)};");
                sb.AppendLine("                    break;");
            }
            else
            {
               // If no default case, and unknown discriminator value, nothing extra is written?
               // But usually we should at least break.
               sb.AppendLine("                default:");
               sb.AppendLine("                    break;");
            }

            sb.AppendLine("            }");
        }

        private string GetDiscriminatorCastType(string typeName)
        {
             // If enum simplify to int, assuming 32-bit discriminator for now as per instructions (Write int32)
             // But if it's long, we might need long.
             // Instructions: "Discriminator: Write int32."
             return "int";
        }
        
        private void EmitSerialize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public void Serialize(ref CdrWriter writer)");
            sb.AppendLine("        {");
            if (IsAppendable(type))
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            writer.Align(4);");
                sb.AppendLine("            int dheaderPos = writer.Position;");
                sb.AppendLine("            writer.WriteUInt32(0);");
                sb.AppendLine();
                sb.AppendLine("            int bodyStart = writer.Position;");
                sb.AppendLine();
            }

            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionSerializeBody(sb, type);
            }
            else
            {
                sb.AppendLine("            // Struct body");
                
                var fieldsWithIds = type.Fields.Select((f, i) => new { Field = f, Id = GetFieldId(f, i) }).OrderBy(x => x.Id).ToList();

                foreach (var item in fieldsWithIds)
                {
                    var field = item.Field;
                    int fieldId = item.Id;

                    if (IsOptional(field))
                    {
                        EmitOptionalSerializer(sb, field, fieldId);
                    }
                    else
                    {
                        string writerCall = GetWriterCall(field);
                        sb.AppendLine($"            {writerCall}; // {field.Name}");
                    }
                }
            }
            
            if (IsAppendable(type))
            {
                sb.AppendLine();
                sb.AppendLine("            // Patch DHEADER");
                sb.AppendLine("            int bodySize = writer.Position - bodyStart;");
                sb.AppendLine("            writer.PatchUInt32(dheaderPos, (uint)bodySize);");
            }
            sb.AppendLine("        }");
        }

        private void EmitUnionSerializeBody(StringBuilder sb, TypeInfo type)
        {
            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) throw new Exception($"Union {type.Name} missing [DdsDiscriminator] field");

            // Write Discriminator
            string discWriter = GetWriterCall(discriminator);
            sb.AppendLine($"            {discWriter}; // Discriminator {discriminator.Name}");
            
            sb.AppendLine($"            switch (({GetDiscriminatorCastType(discriminator.TypeName)})this.{ToPascalCase(discriminator.Name)})");
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
                    sb.AppendLine($"                    {GetWriterCall(field)};");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    {GetWriterCall(defaultField)};");
                sb.AppendLine("                    break;");
            }
            else
            {
                sb.AppendLine("                default:");
                sb.AppendLine("                    break;");
            }

            sb.AppendLine("            }");
        }
        
        private void EmitOptionalSizer(StringBuilder sb, FieldInfo field)
        {
            string access = $"this.{ToPascalCase(field.Name)}";
            string check = field.TypeName == "string?" ? $"{access} != null" : $"{access}.HasValue";
            
            sb.AppendLine($"            if ({check})");
            sb.AppendLine("            {");
            sb.AppendLine("                sizer.WriteUInt32(0); // EMHEADER");
            
            // For optional, we need to size the value as if it was not optional
            var nonOptionalField = new FieldInfo 
            { 
                Name = field.Name, 
                TypeName = GetBaseType(field.TypeName),
                Attributes = field.Attributes,
                Type = field.Type 
            };
            
            // Special handling for ".Value" access if value type
            string baseType = GetBaseType(field.TypeName);
            if (baseType != "string" && !IsReferenceType(baseType))
            {
                 // We need to trick GetSizerCall to use .Value
                 // Actually GetSizerCall uses "this.Name", so we might need a modified version or just hack it
                 // The easiest way is to use a temporary variable or change how GetSizerCall works.
                 // But proper way since we are inside `if (HasValue)`:
                 // The field passed to GetSizerCall will generate `this.Name`. 
                 // If it is nullable int?, `this.Name` refers to Nullable<int>.
                 // `sizer.WriteInt32(nullable)` might work if overload exists? No.
                 // We need `sizer.WriteInt32(nullable.Value)`.
                 
                 // However, TypeMapper methods usually take the value.
                 // GetSizerCall generates: `sizer.Method(this.FieldName)`
                 // We want: `sizer.Method(this.FieldName.Value)`
                 
                 // Let's manually constructing the sizer call here for optionals might be cleaner
                 string sizerCall = GetSizerCall(nonOptionalField);
                 // Replace `this.Name` with `this.Name.Value` if needed
                 if (!sizerCall.Contains(".Value") && !sizerCall.Contains(".ToString")) 
                    sizerCall = sizerCall.Replace($"this.{ToPascalCase(field.Name)}", $"this.{ToPascalCase(field.Name)}.Value");
                 
                 sb.AppendLine($"                {sizerCall};");
            }
            else
            {
                 // Reference type (string?), just use name
                 string sizerCall = GetSizerCall(nonOptionalField);
                 sb.AppendLine($"                {sizerCall};");
            }

            sb.AppendLine("            }");
        }

        private void EmitOptionalSerializer(StringBuilder sb, FieldInfo field, int fieldId)
        {
            string access = $"this.{ToPascalCase(field.Name)}";
            string check = field.TypeName == "string?" ? $"{access} != null" : $"{access}.HasValue";
            
            sb.AppendLine($"            if ({check})");
            sb.AppendLine("            {");
            
            sb.AppendLine("                int emHeaderPos = writer.Position;");
            sb.AppendLine("                writer.WriteUInt32(0); // Placeholder");
            sb.AppendLine("                int emBodyStart = writer.Position;");

            string baseType = GetBaseType(field.TypeName);
            var nonOptionalField = new FieldInfo 
            { 
                Name = field.Name, 
                TypeName = baseType,
                Attributes = field.Attributes,
                Type = field.Type 
            };
            
            string writerCall = GetWriterCall(nonOptionalField);
            if (baseType != "string" && !IsReferenceType(baseType))
            {
                 writerCall = writerCall.Replace($"this.{ToPascalCase(field.Name)}", $"this.{ToPascalCase(field.Name)}.Value");
            }
            
            sb.AppendLine($"                {writerCall};");
            
            sb.AppendLine("                int emBodyLen = writer.Position - emBodyStart;");
            // XCDR2 EMHEADER format: [M:1bit][Length:28bits][ID:3bits]
            // M=0 for appendable, Length in bits 30-3, ID in bits 2-0
            sb.AppendLine($"                uint emHeader = ((uint)emBodyLen << 3) | (uint)({fieldId} & 0x7);");
            sb.AppendLine("                writer.PatchUInt32(emHeaderPos, emHeader);");

            sb.AppendLine("            }");
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
        
        private bool IsPrimitive(string typeName)
        {
            return typeName.ToLower() is 
                "byte" or "uint8" or "sbyte" or "int8" or "bool" or "boolean" or
                "short" or "int16" or "ushort" or "uint16" or
                "int" or "int32" or "uint" or "uint32" or "float" or
                "long" or "int64" or "ulong" or "uint64" or "double";
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

        private string GetSizerCall(FieldInfo field)
        {
            // 1. Strings (Variable)
            if (field.TypeName == "string")
            {
                 return $"sizer.Align(4); sizer.WriteString(this.{ToPascalCase(field.Name)})";
            }

            // Handle List<T>
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListSizer(field);
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
                 return $"sizer.Align(1); sizer.WriteFixedString((string)null, {size})";
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
            if (field.TypeName == "string")
            {
                 return $"writer.Align(4); writer.WriteString({fieldAccess})";
            }

            // Handle List<T>
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListWriter(field);
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
                "long" or "int64" or "ulong" or "uint64" or "double" => 4,
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

        private int GetFieldId(FieldInfo field, int defaultId)
        {
            var idAttr = field.GetAttribute("DdsId");
            if (idAttr != null && idAttr.Arguments.Count > 0)
            {
                 if (idAttr.Arguments[0] is int id) return id;
                 if (idAttr.Arguments[0] is string s && int.TryParse(s, out int sid)) return sid;
            }
            return defaultId;
        }

        private string ExtractGenericType(string typeName)
        {
            int start = typeName.IndexOf('<') + 1;
            int end = typeName.LastIndexOf('>');
            return typeName.Substring(start, end - start).Trim();
        }

        private string EmitListWriter(FieldInfo field)
        {
             string fieldAccess = $"this.{ToPascalCase(field.Name)}";
             string elementType = ExtractGenericType(field.TypeName);
             
             string writerMethod = TypeMapper.GetWriterMethod(elementType);
             int align = GetAlignment(elementType);
             
             string loopBody;
             if (writerMethod != null)
             {
                 loopBody = $"writer.Align({align}); writer.{writerMethod}(item);";
             }
             else if (elementType == "string")
             {
                 loopBody = "writer.Align(4); writer.WriteString(item);";
             }
             else
             {
                 loopBody = "item.Serialize(ref writer);";
             }
             
             return $@"writer.Align(4); writer.WriteUInt32((uint){fieldAccess}.Count);
            foreach (var item in {fieldAccess})
            {{
                {loopBody}
            }}";
        }

        private string EmitListSizer(FieldInfo field)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractGenericType(field.TypeName);
            
            string sizerMethod = TypeMapper.GetSizerMethod(elementType);
            
            if (sizerMethod != null)
            {
                string dummy = "0";
                if (sizerMethod == "WriteBool") dummy = "false";
                int align = GetAlignment(elementType);
                
                return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Align({align}); sizer.{sizerMethod}({dummy});
            }}";
            }
            
            if (elementType == "string")
            {
                return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Align(4); sizer.WriteString(item);
            }}";
            }
            
            return $@"sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Skip(item.GetSerializedSize(sizer.Position));
            }}";
        }

        private string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
