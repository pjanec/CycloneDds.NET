using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen
{
    public class DeserializerEmitter
    {
        private HashSet<string> _generatedRefStructs = new HashSet<string>();
        private GlobalTypeRegistry? _registry;

        public string EmitDeserializer(TypeInfo type, GlobalTypeRegistry registry, bool generateUsings = true)
        {
            _registry = registry;
            var sb = new StringBuilder();
            sb.AppendLine("// CodeGen Version: DEBUG-CHECK-1");
            
            if (generateUsings)
            {
                sb.AppendLine("using CycloneDDS.Core;");
                sb.AppendLine("using System.Runtime.InteropServices;");
                sb.AppendLine("using System.Text;");
                sb.AppendLine("using System.Linq;");
                sb.AppendLine("using System.Collections.Generic;");
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }
            
            EmitPartialStruct(sb, type);
            // EmitViewStruct(sb, type); // View not used for simple structs in this binding mode
            
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

        private void EmitPartialStruct(StringBuilder sb, TypeInfo type)
        {
            sb.AppendLine($"    public partial struct {type.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        public static {type.Name} Deserialize(ref CdrReader reader)");
            sb.AppendLine("        {");
            sb.AppendLine($"            System.Console.WriteLine(\"DEBUG: Deserialize {type.Name} Pos=\" + reader.Position);");
            sb.AppendLine($"            var view = new {type.Name}();");
            // sb.AppendLine($"            System.Console.WriteLine(\"[Type={type.Name}] Pos=\" + reader.Position + \" Enc=\" + reader.Encoding + \" IsApp={IsAppendable(type)}\");");
            
            if (IsAppendable(type))
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            int endPos = int.MaxValue;");
                sb.AppendLine("            if (reader.Encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                reader.Align(4);");
                sb.AppendLine("                uint dheader = reader.ReadUInt32();");
                sb.AppendLine("                endPos = reader.Position + (int)dheader;");
                
                // NATIVE BEHAVIOR: 
                // XCDR2 spec says "Padding to reach alignment of the type". 
                // BUT implementation observation shows Body is NOT aligned to type alignment immediately after DHEADER.
                // It respects Member alignment. 
                // So we comment it out
                //int typeAlign = GetAlignment(type);
                //if (typeAlign > 4)
                //{
                //     sb.AppendLine($"                reader.Align({typeAlign});");
                //}
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine("            int endPos = int.MaxValue;");
            }
            
            if (type.HasAttribute("DdsUnion"))
            {
                EmitUnionDeserializeBody(sb, type);
            }
            else
            {
                var fieldsWithIds = type.Fields.Select((f, i) => new { Field = f, Id = GetFieldId(f, i) }).OrderBy(x => x.Id).ToList();

                foreach(var item in fieldsWithIds)
                {
                    var field = item.Field;
                    int fieldId = item.Id;

                    if (IsOptional(field))
                    {
                        // Optional logic
                        EmitOptionalReader(sb, type, field, fieldId);
                    }
                    else
                    {
                        if (IsAppendable(type))
                        {
                            sb.AppendLine($"            if (reader.Position < endPos)");
                            sb.AppendLine("            {");
                        }
                        
                        string readCall = GetReadCall(type, field);
                        sb.AppendLine($"                {readCall};");
                        
                        if (IsAppendable(type))
                        {
                            sb.AppendLine("            }");
                        }
                    }
                }
            }
            
            if (IsAppendable(type))
            {
                sb.AppendLine();
                sb.AppendLine("            if (endPos != int.MaxValue && reader.Position < endPos)");
                sb.AppendLine("            {");
                sb.AppendLine("                reader.Seek(endPos);");
                sb.AppendLine("            }");
            }
            
            sb.AppendLine("            return view;");
            sb.AppendLine("        }");
            
            sb.AppendLine($"        public {type.Name} ToOwned()");
            sb.AppendLine("        {");
            sb.AppendLine("            return this;");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
        }

        private void EmitOptionalReader(StringBuilder sb, TypeInfo type, FieldInfo field, int fieldId)
        {
            string baseType = GetBaseType(field.TypeName);
            var nonOptField = new FieldInfo { Name = field.Name, TypeName = baseType, Attributes = field.Attributes, Type = field.Type };
            
            sb.AppendLine($"            // Optional {field.Name}");
            sb.AppendLine("            {");
            sb.AppendLine("                int emHeaderPos = reader.Position;");
            sb.AppendLine("                bool isPresent = false;");
            sb.AppendLine("                if (reader.Remaining >= 4 && reader.Position + 4 <= endPos)");
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
            sb.AppendLine($"                    {GetReadCall(type, nonOptField)};");
            sb.AppendLine("                }");
            sb.AppendLine("                else");
            sb.AppendLine("                {");
            // ToPascalCase added
            if (IsReferenceType(baseType))
                sb.AppendLine($"                    view.{ToPascalCase(field.Name)} = null;");
            else
                sb.AppendLine($"                    view.{ToPascalCase(field.Name)} = null;");
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
            sb.AppendLine($"                {GetReadCall(type, discriminator)};");
            sb.AppendLine("            }");

            sb.AppendLine($"            switch (view.{ToPascalCase(discriminator.Name)})");
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
                            // If discriminator is enum, we need to cast the integer value back to the enum type
                            // so the case label matches the switch type.
                            // If discriminator is int/short/byte, val (assuming it's int/object) is fine as is.
                            if (!IsPrimitive(discriminator.TypeName) && discriminator.TypeName != "string")
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
                    sb.AppendLine($"                    if (reader.Position < endPos) {{ {GetReadCall(type, field)}; }}");
                    sb.AppendLine("                    break;");
                }
            }
            
            var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
            if (defaultField != null)
            {
                sb.AppendLine("                default:");
                sb.AppendLine($"                    if (reader.Position < endPos) {{ {GetReadCall(type, defaultField)}; }}");
                sb.AppendLine("                    break;");
            }
            else
            {
                sb.AppendLine("                default:");
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
             // NOTE: EmitViewStruct is commented out in usage but included here.
             // I will update it just in case.
             bool needsRef = type.Fields.Any(f => 
             {
                 string baseType = GetBaseType(f.TypeName);
                 if (baseType.StartsWith("BoundedSeq"))
                 {
                     string elem = ExtractSequenceElementType(baseType);
                     return IsPrimitive(elem); // Span requires ref struct
                 }
                 if (baseType == "string" && f.HasAttribute("DdsManaged")) return true;
                 
                 return _generatedRefStructs.Contains(baseType);
             });

             if (needsRef) _generatedRefStructs.Add(type.Name);

             string decl = needsRef ? "ref struct" : "struct";
             sb.AppendLine($"    public {decl} {type.Name}View");
             sb.AppendLine("    {");
             foreach(var field in type.Fields)
             {
                 string typeName = MapToViewType(field);
                 sb.AppendLine($"        public {typeName} {field.Name};"); // This defines fields on the VIEW struct. Probably original name is fine?
                 // But ToOwned maps from View to Main Struct.
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
                     // ToPascalCase added
                     sb.AppendLine($"            instance.{ToPascalCase(discriminator.Name)} = {MapToOwnedConversion(discriminator)};");
                     
                     sb.AppendLine($"            switch (({GetDiscriminatorCastType(discriminator.TypeName)})instance.{ToPascalCase(discriminator.Name)})");
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
                             sb.AppendLine($"                    instance.{ToPascalCase(field.Name)} = {MapToOwnedConversion(field)};");
                             sb.AppendLine("                    break;");
                         }
                     }
                     
                     var defaultField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDefaultCase"));
                     if (defaultField != null)
                     {
                         sb.AppendLine("                default:");
                         sb.AppendLine($"                    instance.{ToPascalCase(defaultField.Name)} = {MapToOwnedConversion(defaultField)};");
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
                     sb.AppendLine($"            instance.{ToPascalCase(field.Name)} = {MapToOwnedConversion(field)};"); // ToPascalCase added
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
                   return viewType;
                return $"{viewType}?"; 
            }
            return MapBaseToViewType(field.TypeName, field);
        }

        private string MapBaseToViewType(string typeName, FieldInfo field)
        {
            if (typeName == "string" && field.HasAttribute("DdsManaged"))
                return "string";
            if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
                 return typeName;

            if (typeName == "string") return "string?"; 

            if (typeName.StartsWith("BoundedSeq"))
            {
                string elem = ExtractSequenceElementType(typeName);
                if (IsPrimitive(elem))
                    return $"ReadOnlySpan<{elem}>"; 
                if (_generatedRefStructs.Contains(elem))
                    return $"{elem}[]";
                if (elem == "string") return "string[]";
                return $"{elem}View[]"; 
            }
            if (IsPrimitive(typeName))
                return typeName;
            return $"{typeName}View";
        }
        
        private bool ShouldUseManagedDeserialization(TypeInfo type, FieldInfo field)
        {
            return type.HasAttribute("DdsManaged") || field.HasAttribute("DdsManaged");
        }

        private string GetReadCall(TypeInfo type, FieldInfo field)
        {
            int align = GetAlignment(field.TypeName);
            string alignA = align.ToString();
            string alignCall;
            if (align > 4)
            {
                 // XCDR2 aligns 8-byte primitives to 4 bytes
                 alignCall = $"if (reader.IsXcdr2) reader.Align(4); else reader.Align({alignA}); ";
            }
            else
            {
                 alignCall = align > 1 ? $"reader.Align({alignA}); " : "";
            }
            
            // ToPascalCase added to all field access below
            if (field.TypeName == "string")
            {
                if (ShouldUseManagedDeserialization(type, field))
                    return $"reader.Align(4); view.{ToPascalCase(field.Name)} = reader.ReadString()";
                return $"reader.Align(4); view.{ToPascalCase(field.Name)} = Encoding.UTF8.GetString(reader.ReadStringBytes().ToArray())";
            }

            if (field.TypeName.EndsWith("[]"))
            {
                 return EmitArrayReader(field, type);
            }

            if (field.TypeName.StartsWith("BoundedSeq"))
            {
                return EmitSequenceReader(field, type);
            }

            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListReader(field, type);
            }
            
            if (IsPrimitive(field.TypeName))
            {
                 string method = TypeMapper.GetSizerMethod(field.TypeName)!.Replace("Write", "Read"); 
                 return $"{alignCall}view.{ToPascalCase(field.Name)} = reader.{method}()";
            }
            
            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                 return $"{alignCall}view.{ToPascalCase(field.Name)} = ({field.TypeName})reader.ReadInt32()";
            }
            
            return $"{alignCall}view.{ToPascalCase(field.Name)} = {field.TypeName}.Deserialize(ref reader)";
        }
        
        private string EmitArrayReader(FieldInfo field, TypeInfo parentType)
        {
            string elementType = field.TypeName.Substring(0, field.TypeName.Length - 2);
            string fieldAccess = $"view.{ToPascalCase(field.Name)}"; // ToPascalCase added

            int arrayLen = GetArrayLength(field);
            string lengthRead = arrayLen >= 0 
                 ? $"int length{field.Name} = {arrayLen};"
                 : $@"reader.Align(4);
            int length{field.Name} = (int)reader.ReadUInt32();";

            if (elementType == "byte" || elementType == "octet" || elementType == "uint8")
            {
                 return $@"{lengthRead}
            if (length{field.Name} > 0)
            {{
                {fieldAccess} = reader.ReadFixedBytes(length{field.Name}).ToArray();
            }}
            else
            {{
                {fieldAccess} = System.Array.Empty<byte>();
            }}";
            }

            if (elementType == "string" || elementType == "String" || elementType == "System.String")
             {
                 string headerRead = "";
                 if (IsAppendable(parentType))
                 {
                    headerRead = "if (reader.Encoding == CdrEncoding.Xcdr2) { reader.ReadInt32(); } // XCDR2 Array Header\r\n            ";
                 }

                 return $@"{lengthRead}
            {headerRead}{fieldAccess} = new string[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                reader.Align(4);
                {fieldAccess}[i] = reader.ReadString();
            }}";
             }

            string? writerMethod = TypeMapper.GetWriterMethod(elementType);
            string? readMethod = writerMethod?.Replace("Write", "Read");
            if (readMethod == "ReadBool") readMethod = "ReadBoolean";
            
             if (readMethod != null)
             {
                 return $@"{lengthRead}
            {fieldAccess} = new {elementType}[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                System.Console.WriteLine(""DEBUG: ReadArrayElem {elementType} Index="" + i + "" Pos="" + reader.Position);
                reader.Align({GetAlignment(elementType)});
                {fieldAccess}[i] = reader.{readMethod}();
            }}";
             }

             return $@"{lengthRead}
            {fieldAccess} = new {elementType}[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                {fieldAccess}[i] = {elementType}.Deserialize(ref reader);
            }}";
        }

        private string EmitSequenceReader(FieldInfo field, TypeInfo parentType)
        {
            var boundsAttr = field.GetAttribute("DdsBounds");
            string boundsCheck = "";
            if (boundsAttr != null && boundsAttr.Arguments.Count > 0)
            {
                var bound = boundsAttr.Arguments[0];
                boundsCheck = $@"if ({field.Name}_len > {bound}) throw new IndexOutOfRangeException(""Sequence length exceeds bound {bound}"");";
            }

            string fieldAccess = $"view.{ToPascalCase(field.Name)}"; // ToPascalCase added

            string elem = ExtractSequenceElementType(field.TypeName);
            
            string headerRead = "";
            if (IsAppendable(parentType))
            {
                 headerRead = "if (reader.IsXcdr2) { reader.ReadUInt32(); } // XCDR2 Sequence Header\r\n            ";
            }

            
            if (elem == "string" || elem == "String" || elem == "System.String")
            {
                return $@"reader.Align(4);
            {headerRead}uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            var list = new System.Collections.Generic.List<string>((int){field.Name}_len);
            for(int i=0; i<{field.Name}_len; i++)
            {{
                reader.Align(4);
                list.Add(reader.ReadString()); // Use ReadString helper
            }}
            {fieldAccess} = new BoundedSeq<string>(list);";
            }

            if (TypeMapper.IsBlittable(elem))
            {
                int elemSize = GetSize(elem);
                // Align 4 for Header
                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            if ({field.Name}_len > 0)
            {{
                reader.Align({GetAlignment(elem)});
                var span = MemoryMarshal.Cast<byte, {elem}>(reader.ReadFixedBytes((int){field.Name}_len * {elemSize}));
                {fieldAccess} = new BoundedSeq<{elem}>(new System.Collections.Generic.List<{elem}>(span.ToArray()));
            }}
            else
            {{
                {fieldAccess} = new BoundedSeq<{elem}>(new System.Collections.Generic.List<{elem}>());
            }}";
            }

            string itemType = elem; 
            string deserializerCall = $"{elem}.Deserialize(ref reader).ToOwned()";
            
            return $@"// FieldType: {field.TypeName} SeqAlign: {GetAlignment(field.TypeName)} [SequenceReader.Complex]
            reader.Align(4);
            {headerRead}uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            var list = new System.Collections.Generic.List<{itemType}>((int){field.Name}_len);
            for(int i=0; i<{field.Name}_len; i++)
            {{
                list.Add({deserializerCall});
            }}
            {fieldAccess} = new BoundedSeq<{itemType}>(list);";
        }
        
        private string MapToOwnedConversion(FieldInfo field)
        {
            if (IsOptional(field))
            {
                string baseType = GetBaseType(field.TypeName);
                string access = $"this.{field.Name}"; // Note: this is on ViewStruct which I assume keeps Original names if I didn't change MapToViewType?
                // Wait, EmitViewStruct generated `public {typeName} {field.Name};`. So view struct members are NOT pascal cased?
                // If I change Deserializer logic to write to PascalCase members on the Main Struct, that's fine.
                // But EmitViewStruct is for reading View structs.
                // The main deserializer (EmitPartialStruct) writes directly to MAIN Struct (`var view = new {type.Name}()`).
                // So my changes above affect Main Struct member access.
                
                // For EmitViewStruct:
                // I left `public {typeName} {field.Name}` (line 257).
                // But `ToOwned` uses `instance.{ToPascalCase(field.Name)}`.
                // So this looks consistent with the goal (Main struct has PascalCase, View struct has original case).
                
                if (baseType == "string")
                    return access; 

                if (!IsReferenceType(baseType))
                {
                    if (IsPrimitive(baseType)) return access;
                    return $"{access}?.ToOwned()";
                }
            }

            return MapBaseToOwnedConversion(field.TypeName, field.Name);
        }

        private string MapBaseToOwnedConversion(string typeName, string fieldName)
        {
            if (typeName == "string")
                return fieldName;
             if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
                 return fieldName;
            
            if (!IsPrimitive(typeName) && !typeName.StartsWith("BoundedSeq"))
                return $"{fieldName}.ToOwned()";

            if (typeName.StartsWith("BoundedSeq"))
            {
                 string elem = ExtractSequenceElementType(typeName);
                 if (IsPrimitive(elem))
                     return $"new BoundedSeq<{elem}>({fieldName}.ToArray().ToList())"; 
                 if (_generatedRefStructs.Contains(elem) || elem == "string")
                     return $"new BoundedSeq<{elem}>({fieldName}.ToList())";

                 return $"new BoundedSeq<{elem}>({fieldName}.Select(x => x.ToOwned()).ToList())";
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

            // Arrays / Sequences
            if (typeName.EndsWith("[]") || typeName.StartsWith("List") || typeName.StartsWith("System.Collections.Generic.List") || typeName.StartsWith("BoundedSeq"))
            {
                // NATIVE BEHAVIOR: Propagate alignment
                string elemType = ExtractSequenceElementType(typeName);
                return GetAlignment(elemType);
            }

            // Registry Lookup
            if (_registry != null)
            {
                 if (_registry.TryGetDefinition(typeName, out var def) && def.TypeInfo != null)
                     return GetAlignment(def.TypeInfo);
                     
                 if (_registry.TryGetDefinition(typeName.Replace(".", "::"), out var def2) && def2.TypeInfo != null)
                     return GetAlignment(def2.TypeInfo);
            }

            return 1;
        }

        
        private int GetSize(string typeName)
        {
            return TypeMapper.GetSize(typeName);
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

        private string ExtractGenericType(string typeName)
        {
            int start = typeName.IndexOf('<') + 1;
            int end = typeName.LastIndexOf('>');
            return typeName.Substring(start, end - start).Trim();
        }

        private string EmitListReader(FieldInfo field, TypeInfo parentType)
        {
            string elementType = ExtractGenericType(field.TypeName);
            string fieldAccess = $"view.{ToPascalCase(field.Name)}"; // ToPascalCase added
            int seqAlign = GetAlignment(field.TypeName);

            string headerRead = "";
            bool isPrimitive = IsPrimitive(elementType);
            if (IsAppendable(parentType) && !isPrimitive)
            {
                 headerRead = "if (reader.IsXcdr2) { reader.Align(4); reader.ReadUInt32(); } // XCDR2 List Header\r\n            ";
            }


            if (IsPrimitive(elementType))
            {
                int elemSize = GetSize(elementType);
                int align = GetAlignment(elementType);
                string alignA = align.ToString();

                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            {fieldAccess} = new List<{elementType}>((int){field.Name}_len);
            System.Runtime.InteropServices.CollectionsMarshal.SetCount({fieldAccess}, (int){field.Name}_len);
            var targetSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan({fieldAccess});
            if ({field.Name}_len > 0)
            {{
                reader.Align({alignA});
                var sourceBytes = reader.ReadFixedBytes((int){field.Name}_len * {elemSize});
                System.Runtime.InteropServices.MemoryMarshal.Cast<byte, {elementType}>(sourceBytes).CopyTo(targetSpan);
            }}";
            }

            string? sizerMethod = TypeMapper.GetSizerMethod(elementType);
            string? readMethod = sizerMethod?.Replace("Write", "Read");
            
            string addStatement;
            
            bool isEnum = false;
            if (_registry != null && _registry.TryGetDefinition(elementType, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                isEnum = true;
                if (def.TypeInfo.HasAttribute("DdsBitmask")) isEnum = true; // Handle bitmask as scalar
            }

            if (readMethod != null)
            {
                 int align = GetAlignment(elementType);
                 string alignA = align.ToString();
                 addStatement = $"reader.Align({alignA}); {fieldAccess}.Add(reader.{readMethod}());";
            }
            else if (elementType == "string" || elementType == "System.String")
            {
                 addStatement = $"reader.Align(4); {fieldAccess}.Add(reader.ReadString());";
            }
            else if (isEnum)
            {
                 addStatement = $"reader.Align(4); {fieldAccess}.Add(({elementType})reader.ReadInt32());";
            }
            else
            {
                 addStatement = $"{fieldAccess}.Add({elementType}.Deserialize(ref reader).ToOwned());";
            }

            return $@"// FieldType: {field.TypeName} SeqAlign: {seqAlign} Ext: {parentType.Extensibility}
            // Force 4-byte alignment for Sequence Header (Length/DHeader)
            reader.Align(4);
            {headerRead}uint {field.Name}_len = reader.ReadUInt32();
            {fieldAccess} = new List<{elementType}>((int){field.Name}_len);
            for(int i=0; i<{field.Name}_len; i++)
            {{
                {addStatement}
            }}";
        }

        private bool IsPrimitive(string typeName)
        {
            return TypeMapper.IsPrimitive(typeName);
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

        private int GetArrayLength(FieldInfo field)
        {
            var attr = field.GetAttribute("ArrayLength");
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

        private int GetAlignment(TypeInfo type)
        {
            // Recursive protection could be added but assuming DAG for now
            int maxAlign = 1;

            if (type.IsUnion) 
            {
                // For Unions, the alignment requirement is determined by the Discriminator
                // which is the first thing serialized. The max alignment of members does NOT matters for the start.
                var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
                if (discriminator != null)
                    return GetFieldAlignment(discriminator);
                
                return 4;
            }

            foreach(var field in type.Fields)
            {
                int fa = GetFieldAlignment(field);
                if (fa > maxAlign) maxAlign = fa;
            }
            return maxAlign;
        }

        private int GetFieldAlignment(FieldInfo field)
        {
            if (field.Type != null) return GetAlignment(field.Type);

            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null)
            {
                return GetAlignment(def.TypeInfo);
            }
            
            return GetPrimitiveAlignment(field.TypeName);
        }

        private int GetPrimitiveAlignment(string name)
        {
             switch (name)
             {
                 case "double":
                 case "long": // C# long is 64-bit
                 case "ulong":
                 case "Int64":
                 case "UInt64":
                 case "System.Double":
                 case "System.Int64":
                 case "System.UInt64":
                    return 8;
                 case "int":
                 case "uint":
                 case "float":
                 case "Int32":
                 case "UInt32":
                 case "System.Int32":
                 case "System.UInt32":
                 case "System.Single":
                 case "Single":
                    return 4;
                 case "short":
                 case "ushort":
                 case "Int16":
                 case "UInt16":
                 case "char":
                 case "Char":
                 case "System.Int16":
                 case "System.UInt16":
                 case "System.Char":
                    return 2;
                 case "bool":
                 case "Boolean":
                 case "byte":
                 case "sbyte":
                 case "Byte":
                 case "SByte":
                 case "octet":
                 case "System.Boolean":
                 case "System.Byte":
                 case "System.SByte":
                    return 1;
                 case "string":
                 case "String":
                 case "System.String":
                    return 4;
                 default:
                    if (name.StartsWith("List<") || name.Contains("[]")) return 4;
                    if (name.StartsWith("System.Collections.Generic.List")) return 4;
                    return 1;
             }
        }
    }
}
