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
            sb.AppendLine($"            var view = new {type.Name}();");
            
            if (IsAppendable(type))
            {
                sb.AppendLine("            // DHEADER");
                sb.AppendLine("            int endPos = int.MaxValue;");
                sb.AppendLine("            if (reader.Encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                reader.Align(4);");
                sb.AppendLine("                uint dheader = reader.ReadUInt32();");
                sb.AppendLine("                endPos = reader.Position + (int)dheader;");
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
            sb.AppendLine($"                {GetReadCall(type, discriminator)};");
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
                 sb.AppendLine($"        public {typeName} {field.Name};");
                 

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
                return "string";

            // Handle List<T>
             if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
             {
                 return typeName;
             }

            if (typeName == "string") return "string?"; 

            if (typeName.StartsWith("BoundedSeq"))
            {
                string elem = ExtractSequenceElementType(typeName);
                if (IsPrimitive(elem))
                    return $"ReadOnlySpan<{elem}>"; 
                
                // If element View is a ref struct, we cannot have an array of it.
                // We fallback to array of DTOs (Owned).
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
            string alignCall = align > 1 ? $"reader.Align({align}); " : "";
            
            if (field.TypeName == "string")
            {
                if (ShouldUseManagedDeserialization(type, field))
                    return $"reader.Align(4); view.{field.Name} = reader.ReadString()";
                return $"reader.Align(4); view.{field.Name} = Encoding.UTF8.GetString(reader.ReadStringBytes().ToArray())";
            }

            if (field.TypeName.EndsWith("[]"))
            {
                 return EmitArrayReader(field);
            }

            if (field.TypeName.StartsWith("BoundedSeq"))
            {
                return EmitSequenceReader(field);
            }

            // Handle List<T>
            if (field.TypeName.StartsWith("List<") || field.TypeName.StartsWith("System.Collections.Generic.List<"))
            {
                 return EmitListReader(field);
            }
            
            if (IsPrimitive(field.TypeName))
            {
                 string method = TypeMapper.GetSizerMethod(field.TypeName)!.Replace("Write", "Read"); // Method names match?
                 // WriteInt32 -> ReadInt32
                 return $"{alignCall}view.{field.Name} = reader.{method}()";
            }
            
            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                 return $"{alignCall}view.{field.Name} = ({field.TypeName})reader.ReadInt32()";
            }
            
            // Nested
            return $"{alignCall}view.{field.Name} = {field.TypeName}.Deserialize(ref reader)";
        }
        
        private string EmitArrayReader(FieldInfo field)
        {
            string elementType = field.TypeName.Substring(0, field.TypeName.Length - 2);
            string fieldAccess = $"view.{field.Name}";

            if (TypeMapper.IsBlittable(elementType))
            {
                 int align = GetAlignment(elementType);
                 return $@"reader.Align(4);
            int length{field.Name} = (int)reader.ReadUInt32();
            if (length{field.Name} > 0)
            {{
                reader.Align({align});
                var bytes = reader.ReadFixedBytes(length{field.Name} * {TypeMapper.GetSize(elementType)});
                {fieldAccess} = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, {elementType}>(bytes).ToArray();
            }}
            else
            {{
                {fieldAccess} = System.Array.Empty<{elementType}>();
            }}";
            }

            // Fallback loop
            string? writerMethod = TypeMapper.GetWriterMethod(elementType);
            string? readMethod = writerMethod?.Replace("Write", "Read");
            if (readMethod == "ReadBool") readMethod = "ReadBoolean";
            
             if (readMethod != null)
             {
                 return $@"reader.Align(4);
            int length{field.Name} = (int)reader.ReadUInt32();
            {fieldAccess} = new {elementType}[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                reader.Align({GetAlignment(elementType)});
                {fieldAccess}[i] = reader.{readMethod}();
            }}";
             }
             
             if (elementType == "string")
             {
                 return $@"reader.Align(4);
            int length{field.Name} = (int)reader.ReadUInt32();
            {fieldAccess} = new string[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                reader.Align(4);
                {fieldAccess}[i] = reader.ReadString();
            }}";
             }

             // Nested
             return $@"reader.Align(4);
            int length{field.Name} = (int)reader.ReadUInt32();
            {fieldAccess} = new {elementType}[length{field.Name}];
            for (int i = 0; i < length{field.Name}; i++)
            {{
                {fieldAccess}[i] = {elementType}.Deserialize(ref reader);
            }}";
        }

        private string EmitSequenceReader(FieldInfo field)
        {
            var boundsAttr = field.GetAttribute("DdsBounds");
            string boundsCheck = "";
            if (boundsAttr != null && boundsAttr.Arguments.Count > 0)
            {
                var bound = boundsAttr.Arguments[0];
                boundsCheck = $@"if ({field.Name}_len > {bound}) throw new IndexOutOfRangeException(""Sequence length exceeds bound {bound}"");";
            }

            string elem = ExtractSequenceElementType(field.TypeName);
            if (IsPrimitive(elem))
            {
                int elemSize = GetSize(elem);
                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            reader.Align({GetAlignment(elem)});
            {{
                var span = MemoryMarshal.Cast<byte, {elem}>(reader.ReadFixedBytes((int){field.Name}_len * {elemSize}));
                view.{field.Name} = new BoundedSeq<{elem}>(new System.Collections.Generic.List<{elem}>(span.ToArray()));
            }}";
            }
            
            if (elem == "string")
            {
                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            var list = new System.Collections.Generic.List<string>((int){field.Name}_len);
            for(int i=0; i<{field.Name}_len; i++)
            {{
                reader.Align(4);
                list.Add(Encoding.UTF8.GetString(reader.ReadStringBytes().ToArray()));
            }}
            view.{field.Name} = new BoundedSeq<string>(list);";
            }

            // Non-primitive sequence
            // Assuming Own Types for now
            string itemType = elem; 
            string deserializerCall = $"{elem}.Deserialize(ref reader).ToOwned()";
            
            return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            {boundsCheck}
            var list = new System.Collections.Generic.List<{itemType}>((int){field.Name}_len);
            for(int i=0; i<{field.Name}_len; i++)
            {{
                list.Add({deserializerCall});
            }}
            view.{field.Name} = new BoundedSeq<{itemType}>(list);";
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

             // Handle List<T>
             if (typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
             {
                 return fieldName;
             }
            
            if (!IsPrimitive(typeName) && !typeName.StartsWith("BoundedSeq"))
                return $"{fieldName}.ToOwned()";

            if (typeName.StartsWith("BoundedSeq"))
            {
                 string elem = ExtractSequenceElementType(typeName);
                 if (IsPrimitive(elem))
                     return $"new BoundedSeq<{elem}>({fieldName}.ToArray().ToList())"; 
                 
                 // If we stored DTOs (because element was ref struct), just ToList
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
            if (typeName == "string") return 4;
            if (typeName.StartsWith("BoundedSeq") || typeName.Contains("BoundedSeq<") || typeName.EndsWith("[]")) return 4;
            if (typeName.Contains("FixedString")) return 1;
            
            return typeName.ToLower() switch
            {
                "byte" or "uint8" or "sbyte" or "int8" or "bool" or "boolean" or "system.byte" or "system.sbyte" or "system.boolean" => 1,
                "short" or "int16" or "ushort" or "uint16" or "system.int16" or "system.uint16" => 2,
                "int" or "int32" or "uint" or "uint32" or "float" or "system.int32" or "system.uint32" or "system.single" or
                "vector2" or "system.numerics.vector2" or
                "vector3" or "system.numerics.vector3" or
                "vector4" or "system.numerics.vector4" or
                "quaternion" or "system.numerics.quaternion" or
                "matrix4x4" or "system.numerics.matrix4x4" => 4,
                "long" or "int64" or "ulong" or "uint64" or "double" or "system.int64" or "system.uint64" or "system.double" or
                "datetime" or "system.datetime" or "timespan" or "system.timespan" => 8,
                "guid" or "system.guid" => 1,
                "datetimeoffset" or "system.datetimeoffset" => 8,
                _ => 1
            };
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

        private string EmitListReader(FieldInfo field)
        {
            string elementType = ExtractGenericType(field.TypeName);
            
            // OPTIMIZATION: Block copy for primitives (int, double, etc.)
            if (IsPrimitive(elementType))
            {
                int elemSize = GetSize(elementType);
                int align = GetAlignment(elementType);
                
                return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            view.{field.Name} = new List<{elementType}>((int){field.Name}_len);
            System.Runtime.InteropServices.CollectionsMarshal.SetCount(view.{field.Name}, (int){field.Name}_len);
            var targetSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(view.{field.Name});
            reader.Align({align});
            var sourceBytes = reader.ReadFixedBytes((int){field.Name}_len * {elemSize});
            System.Runtime.InteropServices.MemoryMarshal.Cast<byte, {elementType}>(sourceBytes).CopyTo(targetSpan);";
            }

            string? sizerMethod = TypeMapper.GetSizerMethod(elementType);
            string? readMethod = sizerMethod?.Replace("Write", "Read");
            
            string addStatement;
            
            bool isEnum = false;
            if (_registry != null && _registry.TryGetDefinition(elementType, out var def))
            {
                if (def.TypeInfo != null && def.TypeInfo.IsEnum)
                {
                    isEnum = true;
                }
            }

            if (readMethod != null)
            {
                 int align = GetAlignment(elementType);
                 addStatement = $"reader.Align({align}); view.{field.Name}.Add(reader.{readMethod}());";
            }
            else if (elementType == "string" || elementType == "System.String")
            {
                 addStatement = $"reader.Align(4); view.{field.Name}.Add(reader.ReadString());";
            }
            else if (isEnum)
            {
                 addStatement = $"reader.Align(4); view.{field.Name}.Add(({elementType})reader.ReadInt32());";
            }
            else
            {
                 addStatement = $"view.{field.Name}.Add({elementType}.Deserialize(ref reader).ToOwned());";
            }

            return $@"reader.Align(4);
            uint {field.Name}_len = reader.ReadUInt32();
            view.{field.Name} = new List<{elementType}>((int){field.Name}_len);
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
    }
}
