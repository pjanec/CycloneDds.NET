using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen
{
    public class SerializerEmitter
    {
        private GlobalTypeRegistry? _registry;

        public string EmitSerializer(TypeInfo type, GlobalTypeRegistry registry, bool generateUsings = true)
        {
            _registry = registry;
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
             return type.Extensibility == DdsExtensibilityKind.Appendable || type.Extensibility == DdsExtensibilityKind.Mutable;
        }

        private void EmitGetSerializedSize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public int GetSerializedSize(int currentOffset)");
            sb.AppendLine("        {");
            var defaultEncoding = IsAppendable(type) ? "CdrEncoding.Xcdr2" : "CdrEncoding.Xcdr1";
            sb.AppendLine($"            return GetSerializedSize(currentOffset, {defaultEncoding});");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine("        public int GetSerializedSize(int currentOffset, CdrEncoding encoding)");
            sb.AppendLine("        {");
            sb.AppendLine("            var sizer = new CdrSizer(currentOffset, encoding);");
            sb.AppendLine("            bool isXcdr2 = encoding == CdrEncoding.Xcdr2;");
            sb.AppendLine();
            
            bool isAppendable = IsAppendable(type);
            bool isXcdr2 = isAppendable; // Used as dummy for helper calls
            
            if (isAppendable)
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            if (encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                sizer.Align(4);");
                sb.AppendLine("                sizer.WriteUInt32(0);");
                sb.AppendLine("            }");
                sb.AppendLine();
            }

            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionGetSerializedSizeBody(sb, type, isXcdr2);
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
                        EmitOptionalSizer(sb, field, isXcdr2);
                    }
                    else
                    {
                        string sizerCall = GetSizerCall(field, isXcdr2, isAppendable);
                        sb.AppendLine($"            {sizerCall}; // {field.Name}");
                    }
                }
            }
            
            if (isAppendable)
            {
                sb.AppendLine("            if (isXcdr2) sizer.Align(4);");
            }
            
            sb.AppendLine();
            
            sb.AppendLine("            int size = sizer.GetSizeDelta(currentOffset);");
            sb.AppendLine("            return size;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        private void EmitUnionGetSerializedSizeBody(StringBuilder sb, TypeInfo type, bool isXcdr2)
        {
            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) throw new Exception($"Union {type.Name} missing [DdsDiscriminator] field");

            string discSizer = GetSizerCall(discriminator, isXcdr2);
            sb.AppendLine($"            {discSizer}; // Discriminator {discriminator.Name}");
            
            string castType = GetDiscriminatorCastType(discriminator.TypeName);
            string castExpr = castType == "bool" ? "" : $"({castType})";
            sb.AppendLine($"            switch ({castExpr}this.{ToPascalCase(discriminator.Name)})");
            sb.AppendLine("            {");

            foreach (var field in type.Fields)
            {
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr != null)
                {
                    foreach (var val in caseAttr.CaseValues)
                    {
                        string caseLabel = val.ToString();
                        if (val is bool b) caseLabel = b ? "true" : "false";
                        sb.AppendLine($"                case {caseLabel}:");
                    }
                    sb.AppendLine($"                    {GetSizerCall(field, isXcdr2)};");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    {GetSizerCall(defaultField, isXcdr2)};");
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
             if (typeName == "bool" || typeName == "System.Boolean") return "bool";
             // If enum simplify to int, assuming 32-bit discriminator for now as per instructions (Write int32)
             // But if it's long, we might need long.
             // Instructions: "Discriminator: Write int32."
             return "int";
        }
        
        private void EmitSerialize(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine("        public void Serialize(ref CdrWriter writer)");
            sb.AppendLine("        {");
            
            bool isAppendable = IsAppendable(type);
            bool isXcdr2 = isAppendable;

            if (isAppendable)
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            int dheaderPos = 0;");
                sb.AppendLine("            int bodyStart = 0;");
                sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                writer.Align(4);");
                sb.AppendLine("                dheaderPos = writer.Position;");
                sb.AppendLine("                writer.WriteUInt32(0);");
                sb.AppendLine("                bodyStart = writer.Position;");
                sb.AppendLine("            }");
            }

            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionSerializeBody(sb, type, isXcdr2);
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
                        EmitOptionalSerializer(sb, field, fieldId, isXcdr2);
                    }
                    else
                    {
                        string writerCall = GetWriterCall(field, isXcdr2, isAppendable);
                        sb.AppendLine($"            {writerCall}; // {field.Name}");
                    }
                }
            }
 
            if (isAppendable)
            {
                sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                writer.Align(4);");
                sb.AppendLine("                int bodyLen = writer.Position - bodyStart;");
                sb.AppendLine("                writer.WriteUInt32At(dheaderPos, (uint)bodyLen);");
                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
        }

        private void EmitUnionSerializeBody(StringBuilder sb, TypeInfo type, bool isXcdr2)
        {
            var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
            if (discriminator == null) throw new Exception($"Union {type.Name} missing [DdsDiscriminator] field");

            string discWriter = GetWriterCall(discriminator, isXcdr2);
            sb.AppendLine($"            {discWriter}; // Discriminator {discriminator.Name}");
            
            sb.AppendLine($"            switch (this.{ToPascalCase(discriminator.Name)})");
            sb.AppendLine("            {");

            foreach (var field in type.Fields)
            {
                var caseAttr = field.GetAttribute("DdsCase");
                if (caseAttr != null)
                {
                    foreach (var val in caseAttr.CaseValues)
                    {
                        string caseLabel;
                        if (val is bool b)
                        {
                            caseLabel = b ? "true" : "false";
                        }
                        else
                        {
                             if (!TypeMapper.IsPrimitive(discriminator.TypeName) && discriminator.TypeName != "string")
                             {
                                 caseLabel = $"({discriminator.TypeName}){val}"; 
                             }
                             else
                             {
                                 caseLabel = val.ToString();
                             }
                        }
                        sb.AppendLine($"                case {caseLabel}:");
                    }
                    sb.AppendLine($"                    {GetWriterCall(field, isXcdr2)};");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    {GetWriterCall(defaultField, isXcdr2)};");
                sb.AppendLine("                    break;");
            }
            else
            {
                sb.AppendLine("                default:");
                sb.AppendLine("                    break;");
            }

            sb.AppendLine("            }");
        }
        
        private void EmitOptionalSizer(StringBuilder sb, FieldInfo field, bool isXcdr2)
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
                 string sizerCall = GetSizerCall(nonOptionalField, isXcdr2);
                 // Replace `this.Name` with `this.Name.Value` if needed
                 if (!sizerCall.Contains(".Value") && !sizerCall.Contains(".ToString")) 
                    sizerCall = sizerCall.Replace($"this.{ToPascalCase(field.Name)}", $"this.{ToPascalCase(field.Name)}.Value");
                 
                 sb.AppendLine($"                {sizerCall};");
            }
            else
            {
                 // Reference type (string?), just use name
                 string sizerCall = GetSizerCall(nonOptionalField, isXcdr2);
                 sb.AppendLine($"                {sizerCall};");
            }

            sb.AppendLine("            }");
        }

        private void EmitOptionalSerializer(StringBuilder sb, FieldInfo field, int fieldId, bool isXcdr2)
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
            
            string writerCall = GetWriterCall(nonOptionalField, isXcdr2);
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
            if (typeName.StartsWith("System.")) typeName = typeName.Substring(7);
            return typeName.ToLower() is 
                "byte" or "uint8" or "sbyte" or "int8" or "bool" or "boolean" or
                "short" or "int16" or "ushort" or "uint16" or
                "int" or "int32" or "uint" or "uint32" or "float" or
                "long" or "int64" or "ulong" or "uint64" or "double";
        }
        
        private int GetAlignment(string typeName)
        {
            // Primitives
            string t = typeName;
            if (t.StartsWith("System.")) t = t.Substring(7);
            t = t.ToLowerInvariant();
            
            switch(t)
            {
                case "byte": case "uint8": case "sbyte": case "int8": case "bool": case "boolean": return 1;
                case "short": case "int16": case "ushort": case "uint16": return 2;
                case "int": case "int32": case "uint": case "uint32": case "float": case "single": return 4;
                case "vector2": case "numerics.vector2": return 4;
                case "vector3": case "numerics.vector3": return 4;
                case "vector4": case "numerics.vector4": return 4;
                case "quaternion": case "numerics.quaternion": return 4;
                case "matrix4x4": case "numerics.matrix4x4": return 4;
                
                case "long": case "int64": case "ulong": case "uint64": case "double": return 8;
                case "datetime": case "timespan": case "datetimeoffset": return 8;
                case "guid": return 1;
            }

            if (typeName == "string") return 4;
            if (typeName.Contains("FixedString")) return 1;

            // Arrays / Sequences / Lists
            if (typeName.EndsWith("[]") || typeName.StartsWith("List") || typeName.StartsWith("System.Collections.Generic.List") || typeName.StartsWith("BoundedSeq"))
            {
                 // NATIVE BEHAVIOR HACK: Propagate alignment of elements
                 string elementType = ExtractElementType(typeName);
                 return GetAlignment(elementType);
            }

            // Registry Lookup
            if (_registry != null)
            {
                if (_registry.TryGetDefinition(typeName, out var def) && def.TypeInfo != null)
                    return GetTypeAlignment(def.TypeInfo);
                
                // Try replacing dots with colons for scoped lookup
                if (_registry.TryGetDefinition(typeName.Replace(".", "::"), out var def2) && def2.TypeInfo != null)
                    return GetTypeAlignment(def2.TypeInfo);
            }

            // Fallback
            return 1;
        }

        private string ExtractElementType(string typeName)
        {
            if (typeName.EndsWith("[]")) return typeName.Substring(0, typeName.Length - 2);
            int start = typeName.IndexOf('<');
            int end = typeName.LastIndexOf('>');
            if (start > 0 && end > start) return typeName.Substring(start + 1, end - start - 1);
            return "int"; // fallback
        }

        private int GetTypeAlignment(TypeInfo type)
        {
            if (type.IsUnion) 
            {
                 // XCDR Standard: The alignment of the union is the alignment of its discriminator.
                 var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
                 if (discriminator != null)
                     return GetAlignment(discriminator.TypeName);
                 return 1;
            }

            int maxAlign = 1;

            // Simple recursion protection by name check? 
            // We assume DAG for now as we don't pass visited list.
            
            foreach(var field in type.Fields)
            {
                // We must be careful not to recurse indefinitely if a type contains a List of itself.
                // Since GetAlignment for List calls GetAlignment for Element (Type).
                // However, standard says List alignment is 4. We Changd it to Propagate.
                // If A contains List<A>. GetAl(A) -> GetAl(List<A>) -> GetAl(A). Loop.
                
                if (field.TypeName.Contains(type.Name)) continue; // Skip recursive fields

                int fa = GetAlignment(field.TypeName);
                if (fa > maxAlign) maxAlign = fa;
            }
            return maxAlign;
        }


        private string GetSizerCall(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            // 1. Strings (Variable)
            if (field.TypeName == "string")
            {
                 return $"sizer.Align(4); sizer.WriteString(this.{ToPascalCase(field.Name)}, isXcdr2)";
            }

            // Handle List<T>
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListSizer(field, isXcdr2, isAppendableStruct);
            }

            // 2. Sequences
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
            {
                 return EmitSequenceSizer(field, isXcdr2, isAppendableStruct);
            }

            if (field.TypeName.EndsWith("[]"))
            {
                 return EmitArraySizer(field, isXcdr2, isAppendableStruct);
            }

            // 3. Fixed Strings
            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"sizer.Align(1); sizer.WriteFixedString((string)null, {size})";
            }

            string? method = TypeMapper.GetSizerMethod(field.TypeName);
            if (method != null)
            {
                string dummy = "0";
                if (method == "WriteBool") dummy = "false";
                int align = GetAlignment(field.TypeName); 
                string alignCall;
                if (align > 4)
                    alignCall = $"if (encoding == CdrEncoding.Xcdr2) sizer.Align(4); else sizer.Align({align});";
                else
                    alignCall = $"sizer.Align({align});";
                return $"{alignCall} sizer.{method}({dummy})";
            }

            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                 return $"sizer.Align(4); sizer.WriteInt32(0)";
            }

            else
            {
                // Nested struct
                // Use actual instance for variable sizing logic
                int align = GetAlignment(field.TypeName);
                return $"sizer.Skip(this.{ToPascalCase(field.Name)}.GetSerializedSize(sizer.Position, encoding))"; // Pass encoding
            }
        }
        
        private string GetWriterCall(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            
            // 1. Strings (Variable)
            if (field.TypeName == "string")
            {
                 return $"writer.Align(4); writer.WriteString({fieldAccess}, writer.IsXcdr2)";
            }

            // Handle List<T>
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListWriter(field, isXcdr2, isAppendableStruct);
            }

            // 2. Sequences
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
            {
                 return EmitSequenceWriter(field, isXcdr2, isAppendableStruct);
            }

            if (field.TypeName.EndsWith("[]"))
            {
                 return EmitArrayWriter(field, isXcdr2, isAppendableStruct);
            }

            if (field.TypeName.Contains("FixedString"))
            {
                 var size = new string(field.TypeName.Where(char.IsDigit).ToArray());
                 if (string.IsNullOrEmpty(size)) size = "32"; 
                 return $"writer.Align(1); writer.WriteFixedString({fieldAccess}, {size})";
            }

            string? method = TypeMapper.GetWriterMethod(field.TypeName);
            if (method != null)
            {
                int align = GetAlignment(field.TypeName);
                string alignCall;
                if (align > 4)
                    alignCall = $"if (writer.IsXcdr2) writer.Align(4); else writer.Align({align});";
                else
                    alignCall = $"writer.Align({align});";
                return $"{alignCall} writer.{method}({fieldAccess})";
            }
            
            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                 return $"writer.Align(4); writer.WriteInt32((int){fieldAccess})";
            }
            
            else
            {
                return $"{fieldAccess}.Serialize(ref writer)";
            }
        }

        private string EmitArraySizer(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = field.TypeName.Substring(0, field.TypeName.Length - 2);
            bool isFixed = field.HasAttribute("ArrayLength") || field.HasAttribute("ArrayLengthAttribute");
            string lengthWrite = isFixed ? "" : "sizer.Align(4); sizer.WriteUInt32(0); // Length";

            if (TypeMapper.IsBlittable(elementType))
            {
                int align = GetAlignment(elementType); string alignA = align.ToString();
                int size = TypeMapper.GetSize(elementType);
                
                return $@"{lengthWrite}
            if ({fieldAccess}.Length > 0)
            {{
                sizer.Align({align});
                sizer.Skip({fieldAccess}.Length * {size});
            }}";
            }
            
            // Loop code similar to sequence
            if (elementType == "string" || elementType == "String" || elementType == "System.String") 
            {
                string headerWrite = "";
                if (isAppendableStruct)
                    headerWrite = "if (encoding == CdrEncoding.Xcdr2) { sizer.Align(4); sizer.WriteUInt32(0); } // XCDR2 Array Header\r\n            ";

                return $@"{lengthWrite}
            {headerWrite}for (int i = 0; i < {fieldAccess}.Length; i++)
            {{
                sizer.Align(4); sizer.WriteString({fieldAccess}[i], isXcdr2);
            }}";
            }

            string? sizerMethod = TypeMapper.GetSizerMethod(elementType);
            if (sizerMethod != null)
            {
                string dummy = "0";
                if (sizerMethod == "WriteBool") dummy = "false";
                int align = GetAlignment(elementType); string alignA = align.ToString();
                return $@"{lengthWrite}
                for (int i = 0; i < {fieldAccess}.Length; i++)
                {{
                    sizer.Align({align}); sizer.{sizerMethod}({dummy});
                }}";
            }
             
            // Nested structs
            return $@"{lengthWrite}
            for (int i = 0; i < {fieldAccess}.Length; i++)
            {{
                sizer.Skip({fieldAccess}[i].GetSerializedSize(sizer.Position, encoding));
            }}";
        }
        
        private string EmitArrayWriter(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = field.TypeName.Substring(0, field.TypeName.Length - 2);
            bool isFixed = field.HasAttribute("ArrayLength") || field.HasAttribute("ArrayLengthAttribute");
            string lengthWrite = isFixed ? "" : $@"writer.Align(4);
            writer.WriteUInt32((uint){fieldAccess}.Length);";

            if (TypeMapper.IsBlittable(elementType))
            {
                int align = GetAlignment(elementType);
                string alignA = align == 8 ? "8" : align.ToString();
                return $@"{lengthWrite}
            if ({fieldAccess}.Length > 0)
            {{
                writer.Align({alignA});
                var span = new System.ReadOnlySpan<{elementType}>({fieldAccess});
                var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(span);
                writer.WriteBytes(byteSpan);
            }}";
            }
            
            // Loop fallback
            string? writerMethod = TypeMapper.GetWriterMethod(elementType);
            int alignEl = GetAlignment(elementType); string alignElA = alignEl == 8 ? "8" : alignEl.ToString();
            string loopBody;

            if (elementType == "string" || elementType == "String" || elementType == "System.String")
            { 
                loopBody = $"writer.Align(4); writer.WriteString({fieldAccess}[i], writer.IsXcdr2);";
                if (isAppendableStruct)
                {
                    return $@"{lengthWrite}
            int arrayHeaderPos{field.Name} = 0;
            int arrayBodyStart{field.Name} = 0;
            if (writer.IsXcdr2)
            {{

                writer.Align(4);
                arrayHeaderPos{field.Name} = writer.Position;
                writer.WriteInt32(0); // Placeholder
                arrayBodyStart{field.Name} = writer.Position;
            }}
            for (int i = 0; i < {fieldAccess}.Length; i++)
            {{
                {loopBody}
            }}
            if (writer.IsXcdr2)
            {{
                int arrayBodyEnd{field.Name} = writer.Position;
                writer.WriteUInt32At(arrayHeaderPos{field.Name}, (uint)(arrayBodyEnd{field.Name} - arrayBodyStart{field.Name}));

            }}";
                }
            }
            else if (writerMethod != null)
                loopBody = $"writer.Align({alignElA}); writer.{writerMethod}({fieldAccess}[i]);";
            else
                loopBody = $"var item = {fieldAccess}[i]; item.Serialize(ref writer);";

            return $@"{lengthWrite}
            for (int i = 0; i < {fieldAccess}.Length; i++)
            {{
                {loopBody}
            }}";
        }

        private string EmitSequenceSizer(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractSequenceElementType(field.TypeName);
            
            string headerSizer = "";
            bool isPrimitive = IsPrimitive(elementType);
            
            // In XCDR2, Sequences in Appendable structs:
            // - Primitive: NO Member Header.
            // - Complex (Enum, Union, Struct): YES Member Header.
            if (isAppendableStruct && !isPrimitive)
            {
                 headerSizer = "if (encoding == CdrEncoding.Xcdr2) { sizer.Align(4); sizer.WriteInt32(0); } ";
            }
            
            // For primitive sequences, we can loop calling WritePrimitive(0)
            // This handles alignment correctly via CdrSizer methods.
            
            // If element is string
            if (elementType == "string" || elementType == "String" || elementType == "System.String") 
            {
                // Writer writes header for string too. 
                // Writer uses headerStart/End. 
                // Sizer block for string:
                return $@"{headerSizer}sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                sizer.Align(4); sizer.WriteString({fieldAccess}[i], isXcdr2);
            }}";
            }

            string? sizerMethod = TypeMapper.GetSizerMethod(elementType);
            
            if (sizerMethod != null)
            {
                string dummy = "0";
                if (sizerMethod == "WriteBool") dummy = "false";
                int align = GetAlignment(elementType); 
                
                string alignCall;
                if (align > 4) alignCall = $"if (encoding == CdrEncoding.Xcdr2) sizer.Align(4); else sizer.Align({align});";
                else alignCall = $"sizer.Align({align});";

                return $@"{headerSizer}sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                {alignCall} sizer.{sizerMethod}({dummy});
            }}";
            }
             
            // Nested structs
            return $@"{headerSizer}sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                sizer.Skip({fieldAccess}[i].GetSerializedSize(sizer.Position, encoding));
            }}";
        }

        private string EmitSequenceWriter(FieldInfo field, bool isXcdr2, bool isAppendableStruct = false)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractSequenceElementType(field.TypeName);
            
            string headerStart = "";
            string headerEnd = "";
            
            // In XCDR2, Sequences in Appendable structs:
            // - Primitive: NO Member Header.
            // - Complex (Enum, Union, Struct): YES Member Header.
            if (isAppendableStruct && isXcdr2 && !IsPrimitive(elementType))
            {
                headerStart = $@"int seqHeadPos{ToPascalCase(field.Name)} = 0; int seqBodyStart{ToPascalCase(field.Name)} = 0; 
            if (writer.IsXcdr2) {{ writer.Align(4); seqHeadPos{ToPascalCase(field.Name)} = writer.Position; writer.WriteInt32(0); seqBodyStart{ToPascalCase(field.Name)} = writer.Position; }}";
                headerEnd = $@"if (writer.IsXcdr2) {{ int seqBodyEnd{ToPascalCase(field.Name)} = writer.Position; writer.WriteUInt32At(seqHeadPos{ToPascalCase(field.Name)}, (uint)(seqBodyEnd{ToPascalCase(field.Name)} - seqBodyStart{ToPascalCase(field.Name)})); }}";
            }
            
            if (elementType == "string" || elementType == "String" || elementType == "System.String")
            {
                return $@"{headerStart}
            writer.Align(4); 
            writer.WriteUInt32((uint){fieldAccess}.Count);
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                writer.Align(4); writer.WriteString({fieldAccess}[i], writer.IsXcdr2);
            }}
            {headerEnd}";
            }
            
            // OPTIMIZATION for BoundedSeq primitives
            if (TypeMapper.IsBlittable(elementType))
            {
                 int alignP = GetAlignment(elementType);
                 string alignAP = alignP == 8 ? "8" : alignP.ToString();
                 // BoundedSeq exposes AsSpan() which internally uses CollectionsMarshal
                 return $@"{headerStart}
            writer.Align(4); 
            writer.WriteUInt32((uint){fieldAccess}.Count);
            if ({fieldAccess}.Count > 0)
            {{
                writer.Align({alignAP});
                var span = {fieldAccess}.AsSpan();
                var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(span);
                writer.WriteBytes(byteSpan);
            }}
            {headerEnd}";
            }
            
            string? writerMethod = TypeMapper.GetWriterMethod(elementType);
            int align = GetAlignment(elementType);
            string alignA = align == 8 ? "8" : align.ToString();
            
            string loopBody;

            if (writerMethod != null)
            {
                loopBody = $"writer.Align({alignA}); writer.{writerMethod}({fieldAccess}[i]);";
            }
            else if (elementType == "string")
            {
                loopBody = $"writer.Align(4); writer.WriteString({fieldAccess}[i], writer.IsXcdr2);";
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

            return $@"{headerStart}
            writer.Align(4); writer.WriteUInt32((uint){fieldAccess}.Count);
            for (int i = 0; i < {fieldAccess}.Count; i++)
            {{
                {loopBody}
            }}
            {headerEnd}";
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

        private bool IsVariableType(TypeInfo parent, FieldInfo field)
        {
            if (field.TypeName == "string")
            {
                if (ShouldUseManagedSerialization(parent, field)) return true;
                // Validation ensures string is always managed, so technically this is always true if valid
            }
            
            if (field.TypeName.StartsWith("BoundedSeq") || field.TypeName.Contains("BoundedSeq<"))
                return true;
            
            // Checks for List<T> (Managed)
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
                return true;

            // Check if nested struct is variable
            if (field.Type != null && HasVariableFields(field.Type))
                return true;
            
            return false;
        }
        
        private bool ShouldUseManagedSerialization(TypeInfo type, FieldInfo field)
        {
            return type.HasAttribute("DdsManaged") || field.HasAttribute("DdsManaged");
        }

        private bool HasVariableFields(TypeInfo type)
        {
            return type.Fields.Any(f => IsVariableType(type, f));
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

        private string EmitListWriter(FieldInfo field, bool isXcdr2, bool isAppendableStruct)
        {
             string fieldAccess = $"this.{ToPascalCase(field.Name)}";
             string elementType = ExtractGenericType(field.TypeName);
             
             // OPTIMIZATION: Block copy for primitives
             /*
             if (IsPrimitive(elementType))
             {
                 int alignP = GetAlignment(elementType);
                 string alignAP = alignP == 8 ? "8" : alignP.ToString();
                 
                 string dheaderStartP = "";
                 string dheaderEndP = "";

                 // No Header for Primitives in Appendable

                 return $@"{dheaderStartP}writer.Align(4); 
            writer.WriteUInt32((uint){fieldAccess}.Count);
            if ({fieldAccess}.Count > 0)
            {{
                writer.Align({alignAP});
                var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan({fieldAccess});
                var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(span);
                writer.WriteBytes(byteSpan);
            }}{dheaderEndP}";
             }
             */
             
             string? writerMethod = TypeMapper.GetWriterMethod(elementType);
             int align = GetAlignment(elementType);

             // XCDR2: If element is Appendable/Mutable, we used to force alignment to 4.
             // BUT, if the body needs 8-byte alignment, forcing 4 misaligns the body (Length 4 + DHEADER 4 = 8 offset).
             // So we should respect natural alignment.


             string alignA = align == 8 ? "8" : align.ToString();
             string lengthAlign = "4";
             
             string dheaderStart = "";
             string dheaderEnd = "";
             
             // XCDR2: Lists/Sequences in Appendable Structs generally DO NOT have Member Headers for primitives (e.g. List<int>).
             // However, List<Union> or List<Complex> DOES have a Member Header.
             // See TestSequenceInt32Appendable (No Header) vs TestSequenceUnionAppendable (Header).
             if (isAppendableStruct && isXcdr2 && !IsPrimitive(elementType))
             {
                 dheaderStart = $@"int listHeadPos{ToPascalCase(field.Name)} = 0; int listBodyStart{ToPascalCase(field.Name)} = 0; 
            if (writer.IsXcdr2) {{ writer.Align(4); listHeadPos{ToPascalCase(field.Name)} = writer.Position; writer.WriteInt32(0); listBodyStart{ToPascalCase(field.Name)} = writer.Position; }}";
                 dheaderEnd = $@"if (writer.IsXcdr2) {{ int listBodyEnd{ToPascalCase(field.Name)} = writer.Position; writer.WriteUInt32At(listHeadPos{ToPascalCase(field.Name)}, (uint)(listBodyEnd{ToPascalCase(field.Name)} - listBodyStart{ToPascalCase(field.Name)})); }}";
             }
             
             bool isEnum = false;
             if (_registry != null && _registry.TryGetDefinition(elementType, out var def))
             {
                if (def.TypeInfo != null && def.TypeInfo.IsEnum) isEnum = true;
             }

             string loopBody;
             if (writerMethod != null)
             {
                 loopBody = $"writer.Align({alignA}); writer.{writerMethod}(item);";
             }
             else if (elementType == "string" || elementType == "System.String")
             {
                 loopBody = $"writer.Align(4); writer.WriteString(item, writer.IsXcdr2);";
             }
             else if (isEnum)
             {
                 loopBody = $"writer.Align(4); writer.WriteInt32((int)item);";
             }
             else
             {
                 loopBody = "item.Serialize(ref writer);";
             }
             
             return $@"{dheaderStart}writer.Align({lengthAlign}); writer.WriteUInt32((uint){fieldAccess}.Count);
            foreach (var item in {fieldAccess})
            {{
                {loopBody}
            }}{dheaderEnd}";
        }

        private string EmitListSizer(FieldInfo field, bool isXcdr2, bool isAppendableStruct)
        {
            string fieldAccess = $"this.{ToPascalCase(field.Name)}";
            string elementType = ExtractGenericType(field.TypeName);
            
            string? sizerMethod = TypeMapper.GetSizerMethod(elementType);

            bool isEnum = false;
            if (_registry != null && _registry.TryGetDefinition(elementType, out var def))
            {
                if (def.TypeInfo != null && def.TypeInfo.IsEnum) isEnum = true;
            }

            int align = GetAlignment(elementType);
            
            // XCDR2: If element is Appendable/Mutable, we used to force alignment to 4.
            // BUT, if the body needs 8-byte alignment, forcing 4 misaligns the body (Length 4 + DHEADER 4 = 8 offset).
            // So we should respect natural alignment.


            string lengthAlign = "4";
            
            string dheader = "";
            bool isPrimitive = IsPrimitive(elementType);
            // Appendable structs do NOT use Member Headers (DHEADER) for Primitives in XCDR2
            // But Complex Types (Seq<Enum>, Seq<Union>) DO use them.
            if (isAppendableStruct && !isPrimitive)
            {
               dheader = "if (encoding == CdrEncoding.Xcdr2) { sizer.Align(4); sizer.WriteUInt32(0); } ";
            }




            if (sizerMethod != null)
            {
                string dummy = "0";
                if (sizerMethod == "WriteBool") dummy = "false";
                
                return $@"{dheader}sizer.Align({lengthAlign}); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Align({align}); sizer.{sizerMethod}({dummy});
            }}";
            }
            
            if (elementType == "string" || elementType == "System.String")
            {
                return $@"{dheader}sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Align(4); sizer.WriteString(item, isXcdr2);
            }}";
            }

            if (isEnum)
            {
                return $@"{dheader}sizer.Align(4); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                sizer.Align(4); sizer.WriteInt32(0);
            }}";
            }
            
            return $@"{dheader}sizer.Align({lengthAlign}); sizer.WriteUInt32(0); // Sequence Length
            foreach (var item in {fieldAccess})
            {{
                int sz_{field.Name} = item.GetSerializedSize(sizer.Position, encoding);
                sizer.Skip(sz_{field.Name});
            }}";
        }

        private int GetMaxLength(FieldInfo field)
        {
            var attr = field.GetAttribute("MaxLength");
            if (attr != null && attr.CaseValues != null && attr.CaseValues.Count > 0)
            {
                 if (attr.CaseValues[0] is int val) return val;
                 if (attr.CaseValues[0] is string s && int.TryParse(s, out int i)) return i;
            }
            return -1;
        }

        private string ToPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
