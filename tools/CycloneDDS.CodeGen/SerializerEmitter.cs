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

        public string EmitSerializer(TypeInfo type, GlobalTypeRegistry registry, bool unused = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using CycloneDDS.Core;");
            sb.AppendLine("using CycloneDDS.Runtime;");
            sb.AppendLine("using CycloneDDS.Schema;");
            
            bool hasNamespace = !string.IsNullOrEmpty(type.Namespace);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }
            
            EmitSerializerCode(sb, type, registry);
            
            if (hasNamespace)
            {
                sb.AppendLine("}");
            }
            return sb.ToString();
        }

        public void EmitSerializerCode(StringBuilder sb, TypeInfo type, GlobalTypeRegistry registry)
        {
            _registry = registry;
            
            sb.AppendLine($"    public partial struct {type.Name}");
            sb.AppendLine("    {");
            
            EmitNativeSizer(sb, type);
            EmitMarshaller(sb, type);
            
            if (type.Fields.Any(f => f.HasAttribute("DdsKey")))
            {
               EmitKeyNativeSizer(sb, type);
               EmitKeyMarshaller(sb, type);
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            
            EmitGhostStruct(sb, type);
        }

        private void EmitNativeSizer(StringBuilder sb, TypeInfo type)
        {
             sb.AppendLine($"        public static unsafe int GetNativeSize(in {type.Name} source)");
             sb.AppendLine("        {");
             sb.AppendLine($"            int size = sizeof({type.Name}_Native);");
             foreach(var field in type.Fields)
             {
                 if (field.TypeName == "string")
                 {
                      sb.AppendLine($"            if (source.{field.Name} != null)");
                      sb.AppendLine("            {");
                      sb.AppendLine("                size = (size + 7) & ~7;");
                      sb.AppendLine($"                size += System.Text.Encoding.UTF8.GetByteCount(source.{field.Name}) + 1;");
                      sb.AppendLine("            }");
                 }
                 else if (field.TypeName.Contains("List") || field.TypeName.EndsWith("[]"))
                 {
                     string elemType = field.TypeName;
                     if (elemType.StartsWith("System.Collections.Generic.List<"))
                         elemType = elemType.Substring(32, elemType.Length - 33);
                     else if (elemType.Contains("List<"))
                         elemType = elemType.Substring(elemType.IndexOf("List<") + 5, elemType.Length - elemType.IndexOf("List<") - 6);
                     else if (elemType.EndsWith("[]"))
                         elemType = elemType.Substring(0, elemType.Length - 2);

                     bool isComplex = false;
                     if (field.GenericType != null) {
                         isComplex = field.GenericType.IsStruct || field.GenericType.IsUnion || field.GenericType.IsTopic;
                     } else if (_registry != null && _registry.TryGetDefinition(elemType, out var def) && def.TypeInfo != null) {
                         isComplex = def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic;
                     }
                     
                     string nativeElemType = elemType;
                     if (isComplex) nativeElemType = elemType + "_Native";
                     if (elemType == "string") nativeElemType = "IntPtr";
                     if (elemType == "bool") nativeElemType = "byte";

                     sb.AppendLine($"            if (source.{field.Name} != null)");
                     sb.AppendLine("            {");
                     sb.AppendLine("                size = (size + 7) & ~7;");
                     sb.AppendLine($"                size += source.{field.Name}.{(field.TypeName.EndsWith("[]") ? "Length" : "Count")} * sizeof({nativeElemType});");
                     
                     if (elemType == "string")
                     {
                          sb.AppendLine($"                foreach(var item in source.{field.Name})");
                          sb.AppendLine("                {");
                          sb.AppendLine($"                    if (item != null)");
                          sb.AppendLine("                    {");
                          sb.AppendLine("                        size = (size + 7) & ~7;");
                          sb.AppendLine($"                        size += System.Text.Encoding.UTF8.GetByteCount(item) + 1;");
                          sb.AppendLine("                    }");
                          sb.AppendLine("                }");
                     }
                     else if (isComplex)
                     {
                          sb.AppendLine($"                foreach(var item in source.{field.Name})");
                          sb.AppendLine("                {");
                          sb.AppendLine($"                    size += {elemType}.GetNativeSize(item) - sizeof({elemType}_Native);");
                          sb.AppendLine("                }");
                     }
                     sb.AppendLine("            }");
                 }
                 else
                 {
                    string fieldTypeName = field.TypeName;
                    if (fieldTypeName.EndsWith("?")) fieldTypeName = fieldTypeName.Substring(0, fieldTypeName.Length - 1);

                    bool isStruct = false; 
                    if (field.Type != null)
                    {
                        if (field.Type.IsStruct || field.Type.IsUnion || field.Type.IsTopic) isStruct = true;
                    }
                    else if (_registry != null && _registry.TryGetDefinition(fieldTypeName, out var def) && def.TypeInfo != null)
                    {
                         if(def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic) isStruct = true;
                    }
                    
                    if (isStruct)
                    {
                        if (IsOptional(field))
                        {
                            sb.AppendLine($"            if (source.{field.Name} != null)");
                            sb.AppendLine("            {");
                            sb.AppendLine("                size = (size + 7) & ~7;");
                            sb.AppendLine($"                var __temp_{field.Name} = source.{field.Name}.Value;");
                            sb.AppendLine($"                size += {fieldTypeName}.GetNativeSize(__temp_{field.Name});");
                            sb.AppendLine("            }");
                        }
                        else
                        {
                            sb.AppendLine($"            var __temp_{field.Name} = source.{field.Name};");
                            sb.AppendLine($"            size += {fieldTypeName}.GetNativeSize(__temp_{field.Name}) - sizeof({fieldTypeName}_Native);");
                        }
                    }
                 }
             }
             sb.AppendLine("            return size;");
             sb.AppendLine("        }");
        }

        private void EmitMarshaller(StringBuilder sb, TypeInfo type)
        {
             sb.AppendLine($"        public static unsafe void MarshalToNative(in {type.Name} source, IntPtr targetPtr, ref NativeArena arena)");
             sb.AppendLine("        {");
             sb.AppendLine($"            ref var target = ref System.Runtime.CompilerServices.Unsafe.AsRef<{type.Name}_Native>((void*)targetPtr);");
             sb.AppendLine($"            target = default;");
             sb.AppendLine($"            MarshalToNative(source, ref target, ref arena);");
             sb.AppendLine("        }");
             sb.AppendLine();
             
             sb.AppendLine($"        public static unsafe void MarshalToNative(in {type.Name} source, ref {type.Name}_Native target, ref NativeArena arena)");
             sb.AppendLine("        {");
            
            var discriminatorField = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator") || f.Name == "_d");
            string? discriminatorType = discriminatorField?.TypeName;
            bool isEnumDiscriminator = discriminatorField?.Type != null && discriminatorField.Type.IsEnum;

            foreach(var field in type.Fields)
            {
                var tabs = "            ";
                if (type.IsUnion && !field.HasAttribute("DdsDiscriminator") && field.Name != "_d")
                {
                    var cases = field.Attributes.Where(a => a.Name.Contains("DdsUnionCase")).SelectMany(a => a.Arguments).ToList();
                    if (cases.Count > 0)
                    {
                         var conditionsList = new List<string>();
                         foreach(var c in cases)
                         {
                             if (isEnumDiscriminator && !string.IsNullOrEmpty(discriminatorType))
                             {
                                 conditionsList.Add($"source._d == ({discriminatorType}){c}");
                             }
                             else
                             {
                                 conditionsList.Add($"source._d == {c}");
                             }
                         }
                         var conditions = string.Join(" || ", conditionsList);

                         sb.AppendLine($"            if ({conditions})");
                         sb.AppendLine($"            {{");
                         tabs = "                ";
                    }
                }

                string targetName = field.Name;
                if (type.IsUnion)
                {
                    if (field.HasAttribute("DdsDiscriminator") || field.Name == "_d")
                        targetName = "_d";
                    else
                        targetName = "_u." + field.Name;
                }

                if (field.TypeName == "string")
                {
                     sb.AppendLine($"{tabs}target.{targetName} = arena.CreateString(source.{field.Name});");
                }
                else if (field.TypeName.Contains("List") || field.TypeName.Contains("[]"))
                {
                     string elemType = field.TypeName;
                     if (elemType.StartsWith("System.Collections.Generic.List<"))
                         elemType = elemType.Substring(32, elemType.Length - 33);
                     else if (elemType.Contains("List<"))
                         elemType = elemType.Substring(elemType.IndexOf("List<") + 5, elemType.Length - elemType.IndexOf("List<") - 6); 
                     else if (elemType.EndsWith("[]"))
                         elemType = elemType.Substring(0, elemType.Length - 2);

                     bool isComplex = false;
                     if (field.GenericType != null) {
                         isComplex = field.GenericType.IsStruct || field.GenericType.IsUnion || field.GenericType.IsTopic;
                     } else if (_registry != null && _registry.TryGetDefinition(elemType, out var def) && def.TypeInfo != null) {
                         isComplex = def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic;
                     }

                     if (isComplex)
                     {
                         sb.AppendLine($"{tabs}if (source.{field.Name} != null)");
                         sb.AppendLine($"{tabs}{{");
                         string lenProp = field.TypeName.EndsWith("[]") ? "Length" : "Count";
                         sb.AppendLine($"{tabs}    int _len = source.{field.Name}.{lenProp};");
                            
                         sb.AppendLine($"{tabs}    target.{targetName} = arena.CreateSequence<{elemType}_Native>(_len);");
                         sb.AppendLine($"{tabs}    var _span = target.{targetName}.AsSpan<{elemType}_Native>();");
                         sb.AppendLine($"{tabs}    for(int i=0; i<_len; i++)");
                         sb.AppendLine($"{tabs}    {{");
                         if (field.TypeName.EndsWith("[]"))
                         {
                             sb.AppendLine($"{tabs}        {elemType}.MarshalToNative(in source.{field.Name}[i], ref _span[i], ref arena);");
                         }
                         else
                         {
                             sb.AppendLine($"{tabs}        var _el = source.{field.Name}[i];");
                             sb.AppendLine($"{tabs}        {elemType}.MarshalToNative(in _el, ref _span[i], ref arena);");
                         }
                         sb.AppendLine($"{tabs}    }}");
                         sb.AppendLine($"{tabs}}}");
                     }
                     else
                     {
                         if (field.TypeName.EndsWith("[]"))
                            sb.AppendLine($"{tabs}if (source.{field.Name} != null) target.{targetName} = arena.CreateSequence<{elemType}>(new ReadOnlySpan<{elemType}>(source.{field.Name}));");
                         else
                            sb.AppendLine($"{tabs}if (source.{field.Name} != null) target.{targetName} = arena.CreateSequence<{elemType}>(new ReadOnlySpan<{elemType}>(source.{field.Name}.ToArray()));");
                     }
                }
                else
                {
                    string fieldTypeName = field.TypeName;
                    if (fieldTypeName.EndsWith("?")) fieldTypeName = fieldTypeName.Substring(0, fieldTypeName.Length - 1);

                    bool isStruct = false; 
                    if (field.Type != null)
                    {
                        if (field.Type.IsStruct || field.Type.IsUnion || field.Type.IsTopic) isStruct = true;
                    }
                    else if (_registry != null && _registry.TryGetDefinition(fieldTypeName, out var def) && def.TypeInfo != null)
                    {
                         if(def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic) isStruct = true;
                    }
                    if (isStruct)
                    {
                         if (IsOptional(field))
                         {
                             sb.AppendLine($"{tabs}if (source.{field.Name} != null)");
                             sb.AppendLine($"{tabs}{{");
                             sb.AppendLine($"{tabs}    target.{targetName} = ({fieldTypeName}_Native*)arena.Allocate(sizeof({fieldTypeName}_Native));");
                             string sourceAccess = field.TypeName.EndsWith("?") ? $"source.{field.Name}.Value" : $"source.{field.Name}";
                             sb.AppendLine($"{tabs}    var __temp_{field.Name} = {sourceAccess};");
                             sb.AppendLine($"{tabs}    ref var __target_ref_{field.Name} = ref *target.{targetName};");
                             sb.AppendLine($"{tabs}    {fieldTypeName}.MarshalToNative(in __temp_{field.Name}, ref __target_ref_{field.Name}, ref arena);");
                             sb.AppendLine($"{tabs}}}");
                             sb.AppendLine($"{tabs}else");
                             sb.AppendLine($"{tabs}{{");
                             sb.AppendLine($"{tabs}    target.{targetName} = null;");
                             sb.AppendLine($"{tabs}}}");
                         }
                         else
                         {
                             sb.AppendLine($"{tabs}var __temp_{field.Name} = source.{field.Name};");
                             sb.AppendLine($"{tabs}{fieldTypeName}.MarshalToNative(in __temp_{field.Name}, ref target.{targetName}, ref arena);");
                         }
                    }
                    else
                    {
                         if (IsOptional(field))
                         {
                             sb.AppendLine($"{tabs}if (source.{field.Name} != null)");
                             sb.AppendLine($"{tabs}{{");
                             if (fieldTypeName == "bool" || fieldTypeName == "System.Boolean")
                             {
                                 sb.AppendLine($"{tabs}    target.{targetName} = (byte*)arena.Allocate(1);"); 
                                 sb.AppendLine($"{tabs}    *target.{targetName} = source.{field.Name}.Value ? (byte)1 : (byte)0;");
                             }
                             else
                             {
                                 sb.AppendLine($"{tabs}    target.{targetName} = ({fieldTypeName}*)arena.Allocate(sizeof({fieldTypeName}));");
                                 sb.AppendLine($"{tabs}    *target.{targetName} = source.{field.Name}.Value;");
                             }
                             sb.AppendLine($"{tabs}}}");
                             sb.AppendLine($"{tabs}else");
                             sb.AppendLine($"{tabs}{{");
                             sb.AppendLine($"{tabs}    target.{targetName} = null;");
                             sb.AppendLine($"{tabs}}}");
                         }
                         else
                         {
                             if (field.TypeName == "bool" || field.TypeName == "System.Boolean") 
                                 sb.AppendLine($"{tabs}target.{targetName} = source.{field.Name} ? (byte)1 : (byte)0;");
                             else
                                 sb.AppendLine($"{tabs}target.{targetName} = source.{field.Name};");
                         }
                    }
                }
                
                if (tabs.Length > 12) sb.AppendLine($"            }}");
            }
             sb.AppendLine("        }");
             sb.AppendLine();

             sb.AppendLine($"        public static unsafe void MarshalFromNative(IntPtr nativeData, out {type.Name} managedData)");
             sb.AppendLine("        {");
             sb.AppendLine($"            managedData = new {type.Name}();");
             sb.AppendLine($"            MarshalFromNative(ref managedData, in System.Runtime.CompilerServices.Unsafe.AsRef<{type.Name}_Native>((void*)nativeData));");
             sb.AppendLine("        }");
             sb.AppendLine();

             sb.AppendLine($"        public static unsafe void MarshalFromNative(ref {type.Name} target, in {type.Name}_Native source)");
             sb.AppendLine("        {");
             foreach(var field in type.Fields)
             {
                 var tabs = "            ";
                 if (type.IsUnion && !field.HasAttribute("DdsDiscriminator") && field.Name != "_d")
                 {
                    var cases = field.Attributes.Where(a => a.Name.Contains("DdsUnionCase")).SelectMany(a => a.Arguments).ToList();
                    if (cases.Count > 0)
                    {
                         var conditions = string.Join(" || ", cases.Select(c => $"source._d == {c}"));
                         sb.AppendLine($"            if ({conditions})");
                         sb.AppendLine($"            {{");
                         tabs = "                ";
                    }
                 }

                 string sourceName = field.Name;
                 if (type.IsUnion)
                 {
                     if (field.HasAttribute("DdsDiscriminator") || field.Name == "_d")
                         sourceName = "_d";
                     else
                         sourceName = "_u." + field.Name;
                 }

                 if (field.TypeName == "string")
                 {
                     sb.AppendLine($"{tabs}if (source.{sourceName} != IntPtr.Zero) target.{field.Name} = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(source.{sourceName});");
                 }
                 else if (field.TypeName.Contains("List") || field.TypeName.Contains("[]"))
                 {
                     string elemType = field.TypeName;
                     bool isArray = field.TypeName.EndsWith("[]");

                     if (elemType.StartsWith("System.Collections.Generic.List<"))
                         elemType = elemType.Substring(32, elemType.Length - 33);
                     else if (elemType.Contains("List<"))
                         elemType = elemType.Substring(elemType.IndexOf("List<") + 5, elemType.Length - elemType.IndexOf("List<") - 6); 
                     else if (elemType.EndsWith("[]"))
                         elemType = elemType.Substring(0, elemType.Length - 2);

                     bool isComplex = false;
                     if (field.GenericType != null) {
                         isComplex = field.GenericType.IsStruct || field.GenericType.IsUnion || field.GenericType.IsTopic;
                     } else if (_registry != null && _registry.TryGetDefinition(elemType, out var def) && def.TypeInfo != null) {
                         isComplex = def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic;
                     }
                     
                     if (isComplex)
                     {
                         sb.AppendLine($"{tabs}var _len = source.{sourceName}.Length;");
                         sb.AppendLine($"{tabs}if (_len > 0)");
                         sb.AppendLine($"{tabs}{{");
                         sb.AppendLine($"{tabs}    var _list = new System.Collections.Generic.List<{elemType}>((int)_len);");
                         sb.AppendLine($"{tabs}    var _span = source.{sourceName}.AsSpan<{elemType}_Native>();");
                         sb.AppendLine($"{tabs}    foreach(var _n in _span)");
                         sb.AppendLine($"{tabs}    {{");
                         sb.AppendLine($"{tabs}        {elemType} _m;");
                         sb.AppendLine($"{tabs}        _m = new {elemType}();");
                         sb.AppendLine($"{tabs}        {elemType}.MarshalFromNative(ref _m, in _n);");
                         sb.AppendLine($"{tabs}        _list.Add(_m);");
                         sb.AppendLine($"{tabs}    }}");
                         if (isArray)
                            sb.AppendLine($"{tabs}    target.{field.Name} = _list.ToArray();");
                         else
                            sb.AppendLine($"{tabs}    target.{field.Name} = _list;");
                         sb.AppendLine($"{tabs}}}");
                         sb.AppendLine($"{tabs}else");
                         sb.AppendLine($"{tabs}{{");
                         if (isArray)
                            sb.AppendLine($"{tabs}    target.{field.Name} = new {elemType}[0];");
                         else
                            sb.AppendLine($"{tabs}    target.{field.Name} = new System.Collections.Generic.List<{elemType}>();");
                         sb.AppendLine($"{tabs}}}");
                     }
                     else
                     {
                         if (isArray)
                             sb.AppendLine($"{tabs}target.{field.Name} = source.{sourceName}.ToArray<{elemType}>();");
                         else
                             sb.AppendLine($"{tabs}target.{field.Name} = source.{sourceName}.ToList<{elemType}>();");
                     }
                 }
                 else
                 {
                    string fieldTypeName = field.TypeName;
                    if (fieldTypeName.EndsWith("?")) fieldTypeName = fieldTypeName.Substring(0, fieldTypeName.Length - 1);

                    bool isStruct = false; 
                    if (field.Type != null)
                    {
                        if (field.Type.IsStruct || field.Type.IsUnion || field.Type.IsTopic) isStruct = true;
                    }
                    else if (_registry != null && _registry.TryGetDefinition(fieldTypeName, out var def) && def.TypeInfo != null)
                    {
                         if(def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic) isStruct = true;
                    }
                    if (isStruct)
                    {
                         if (IsOptional(field))
                         {
                              sb.AppendLine($"{tabs}if (source.{sourceName} != null)");
                              sb.AppendLine($"{tabs}{{");
                              sb.AppendLine($"{tabs}    {fieldTypeName} _tmp = new {fieldTypeName}();");
                              sb.AppendLine($"{tabs}    {fieldTypeName}.MarshalFromNative(ref _tmp, in *source.{sourceName});");
                              sb.AppendLine($"{tabs}    target.{field.Name} = _tmp;");
                              sb.AppendLine($"{tabs}}}");
                         }
                         else
                         {
                              sb.AppendLine($"{tabs}{fieldTypeName} _tmp_{field.Name} = default;");
                              sb.AppendLine($"{tabs}var __temp_src_{field.Name} = source.{sourceName};");
                              sb.AppendLine($"{tabs}{fieldTypeName}.MarshalFromNative(ref _tmp_{field.Name}, in __temp_src_{field.Name});");
                              sb.AppendLine($"{tabs}target.{field.Name} = _tmp_{field.Name};");
                         }
                    }
                    else
                    {
                         if (IsOptional(field))
                         {
                              sb.AppendLine($"{tabs}if (source.{sourceName} != null)");
                              sb.AppendLine($"{tabs}{{");
                              if (fieldTypeName == "bool" || fieldTypeName == "System.Boolean")
                              {
                                  sb.AppendLine($"{tabs}    target.{field.Name} = *source.{sourceName} != 0;");
                              }
                              else
                              {
                                  sb.AppendLine($"{tabs}    target.{field.Name} = *source.{sourceName};");
                              }
                              sb.AppendLine($"{tabs}}}");
                              sb.AppendLine($"{tabs}else");
                              sb.AppendLine($"{tabs}{{");
                              sb.AppendLine($"{tabs}    target.{field.Name} = null;");
                              sb.AppendLine($"{tabs}}}");
                         }
                         else
                         {
                             if (field.TypeName == "bool" || field.TypeName == "System.Boolean") 
                                 sb.AppendLine($"{tabs}target.{field.Name} = source.{sourceName} != 0;");
                             else
                                 sb.AppendLine($"{tabs}target.{field.Name} = source.{sourceName};");
                         }
                    }
                 }
                 
                 if (tabs.Length > 12) sb.AppendLine($"            }}");
             }
             sb.AppendLine("        }");
        }

        private void EmitKeyNativeSizer(StringBuilder sb, TypeInfo type)
        {
             sb.AppendLine($"        public static unsafe int GetKeyNativeSize(in {type.Name} source)");
             sb.AppendLine("        {");
             sb.AppendLine($"            int size = sizeof({type.Name}_Native);");
             foreach(var field in type.Fields)
             {
                 if (field.HasAttribute("DdsKey"))
                 {
                     if (field.TypeName == "string")
                     {
                          sb.AppendLine($"            if (source.{field.Name} != null)");
                          sb.AppendLine("            {");
                          sb.AppendLine("                size = (size + 7) & ~7;");
                          sb.AppendLine($"                size += System.Text.Encoding.UTF8.GetByteCount(source.{field.Name}) + 1;");
                          sb.AppendLine("            }");
                     }
                     else
                     {
                        string elemType = field.TypeName;
                        string fieldTypeName = field.TypeName;
                        if (fieldTypeName.EndsWith("?")) fieldTypeName = fieldTypeName.Substring(0, fieldTypeName.Length - 1);
                        
                        bool isStruct = false;
                        TypeInfo? structInfo = field.Type;
                        
                        // Try to resolve type info
                        if (structInfo != null)
                        {
                            if (structInfo.IsStruct || structInfo.IsUnion || structInfo.IsTopic) isStruct = true;
                        }
                        else if (_registry != null && _registry.TryGetDefinition(fieldTypeName, out var def) && def.TypeInfo != null)
                        {
                             structInfo = def.TypeInfo;
                             if(structInfo.IsStruct || structInfo.IsUnion || def.TypeInfo.IsTopic) isStruct = true;
                        }

                        if (isStruct && structInfo != null)
                        {
                            bool hasKeys = structInfo.Fields.Any(f => f.HasAttribute("DdsKey"));
                            string methodName = hasKeys ? "GetKeyNativeSize" : "GetNativeSize";
                            
                            if (IsOptional(field))
                            {
                                sb.AppendLine($"            if (source.{field.Name} != null)");
                                sb.AppendLine("            {");
                                sb.AppendLine("                size = (size + 7) & ~7;");
                                sb.AppendLine($"                var __temp_{field.Name} = source.{field.Name}.Value;");
                                sb.AppendLine($"                size += {fieldTypeName}.{methodName}(__temp_{field.Name});");
                                sb.AppendLine("            }");
                            }
                            else
                            {
                                sb.AppendLine($"            var __temp_{field.Name} = source.{field.Name};");
                                sb.AppendLine($"            size += {fieldTypeName}.{methodName}(__temp_{field.Name}) - sizeof({fieldTypeName}_Native);");
                            }
                        }
                     }
                 }
             }
             sb.AppendLine("            return size;");
             sb.AppendLine("        }");
        }

        private void EmitKeyMarshaller(StringBuilder sb, TypeInfo type)
        {
             sb.AppendLine($"        public static unsafe void MarshalKeyToNative(in {type.Name} source, ref {type.Name}_Native target, ref NativeArena arena)");
             sb.AppendLine("        {");
            foreach(var field in type.Fields)
            {
                if (field.HasAttribute("DdsKey"))
                {
                    if (field.TypeName == "string")
                    {
                         sb.AppendLine($"            target.{field.Name} = arena.CreateString(source.{field.Name});");
                    }
                    else if (field.TypeName.Contains("List") || field.TypeName.Contains("[]"))
                    {
                         string elemType = field.TypeName;
                         if (elemType.StartsWith("System.Collections.Generic.List<"))
                             elemType = elemType.Substring(32, elemType.Length - 33);
                         else if (elemType.Contains("List<"))
                             elemType = elemType.Substring(elemType.IndexOf("List<") + 5, elemType.Length - elemType.IndexOf("List<") - 6); 
                         else if (elemType.EndsWith("[]"))
                             elemType = elemType.Substring(0, elemType.Length - 2);
                         
                         sb.AppendLine($"            if (source.{field.Name} != null) target.{field.Name} = arena.CreateSequence<{elemType}>(new ReadOnlySpan<{elemType}>(source.{field.Name}.ToArray()));");
                    }
                    else
                    {
                        bool isStruct = false; 
                        TypeInfo? structInfo = field.Type;
                        
                        if (structInfo != null)
                        {
                            if (structInfo.IsStruct || structInfo.IsUnion || structInfo.IsTopic) isStruct = true;
                        }
                        else if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def.TypeInfo != null)
                        {
                             structInfo = def.TypeInfo;
                             if(structInfo.IsStruct || structInfo.IsUnion || structInfo.IsTopic) isStruct = true;
                        }
                        if (isStruct && structInfo != null)
                        {
                             string fieldTypeName = field.TypeName;
                             if (fieldTypeName.EndsWith("?")) fieldTypeName = fieldTypeName.Substring(0, fieldTypeName.Length - 1);
                             
                             bool hasKeys = structInfo.Fields.Any(f => f.HasAttribute("DdsKey"));

                             if (IsOptional(field))
                             {
                                 sb.AppendLine($"            if (source.{field.Name} != null)");
                                 sb.AppendLine($"            {{");
                                 sb.AppendLine($"                target.{field.Name} = ({fieldTypeName}_Native*)arena.Allocate(sizeof({fieldTypeName}_Native));");
                                 string sourceAccess = field.TypeName.EndsWith("?") ? $"source.{field.Name}.Value" : $"source.{field.Name}";
                                 sb.AppendLine($"                var __temp_{field.Name} = {sourceAccess};");
                                 
                                 if (hasKeys)
                                     sb.AppendLine($"                {fieldTypeName}.MarshalKeyToNative(in __temp_{field.Name}, ref *target.{field.Name}, ref arena);");
                                 else
                                     sb.AppendLine($"                {fieldTypeName}.MarshalToNative(in __temp_{field.Name}, ref *target.{field.Name}, ref arena);");
                                     
                                 sb.AppendLine($"            }}");
                                 sb.AppendLine($"            else");
                                 sb.AppendLine($"            {{");
                                 sb.AppendLine($"                target.{field.Name} = null;");
                                 sb.AppendLine($"            }}");
                             }
                             else
                             {
                                 sb.AppendLine($"            var __temp_{field.Name} = source.{field.Name};");
                                 if (hasKeys)
                                     sb.AppendLine($"            {field.TypeName}.MarshalKeyToNative(in __temp_{field.Name}, ref target.{field.Name}, ref arena);");
                                 else
                                     sb.AppendLine($"            {field.TypeName}.MarshalToNative(in __temp_{field.Name}, ref target.{field.Name}, ref arena);");
                             }
                        }
                        else
                        {
                             if (field.TypeName == "bool") 
                                 sb.AppendLine($"            target.{field.Name} = source.{field.Name} ? (byte)1 : (byte)0;");
                             else
                                 sb.AppendLine($"            target.{field.Name} = source.{field.Name};");
                        }
                    }
                }
            }
             sb.AppendLine("        }");
        }

        private void EmitGhostStruct(StringBuilder sb, TypeInfo type)
        {
            if (type.IsUnion)
            {
                sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                sb.AppendLine($"    public unsafe partial struct {type.Name}_Native");
                sb.AppendLine("    {");
                
                var discriminator = type.Fields.FirstOrDefault(f => f.HasAttribute("DdsDiscriminator"));
                if (discriminator == null)
                    discriminator = type.Fields.FirstOrDefault(f => f.Name == "_d");

                if (discriminator != null)
                {
                     sb.AppendLine($"        public {GetNativeType(discriminator)} _d;");
                }
                sb.AppendLine($"        public {type.Name}_Union_Native _u;");
                sb.AppendLine("    }");
                sb.AppendLine();
                
                sb.AppendLine("    [StructLayout(LayoutKind.Explicit)]");
                sb.AppendLine($"    public unsafe partial struct {type.Name}_Union_Native");
                sb.AppendLine("    {");
                foreach(var field in type.Fields)
                {
                    if (field == discriminator) continue;
                    
                    sb.AppendLine("        [FieldOffset(0)]");
                    sb.AppendLine($"        public {GetNativeType(field)} {field.Name};");
                }
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
                sb.AppendLine($"    public unsafe partial struct {type.Name}_Native");
                sb.AppendLine("    {");
                foreach(var field in type.Fields)
                {
                    sb.AppendLine($"        public {GetNativeType(field)} {field.Name};");
                }
                sb.AppendLine("    }");
            }
        }

        private string GetNativeType(FieldInfo field)
        {
                string nativeType = field.TypeName;
                bool isOptional = IsOptional(field);
                if (nativeType.EndsWith("?")) nativeType = nativeType.Substring(0, nativeType.Length - 1);

                if (nativeType == "string" || nativeType == "System.String") return "IntPtr";
                if (nativeType == "bool" || nativeType == "System.Boolean") return "byte"; 
                
                if (nativeType.Contains("[]") || nativeType.Contains("List"))
                {
                    return "DdsSequenceNative"; 
                }

                if (field.Type != null)
                {
                    if (field.Type.IsStruct || field.Type.IsUnion || field.Type.IsTopic)
                    {
                         string typeName = field.Type.FullName;
                         if (typeName.EndsWith("?")) typeName = typeName.Substring(0, typeName.Length - 1);
                         if (isOptional) return typeName + "_Native*";
                         return typeName + "_Native";
                    }
                }
                
                if (_registry != null && _registry.TryGetDefinition(nativeType, out var def) && def.TypeInfo != null)
                {
                    if (def.TypeInfo.IsStruct || def.TypeInfo.IsUnion || def.TypeInfo.IsTopic)
                    {
                        if (isOptional) return nativeType + "_Native*";
                        return nativeType + "_Native";
                    }
                }
                
                if (isOptional) return nativeType + "*";
                return nativeType;
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
                        EmitOptionalSizer(sb, type, field, isXcdr2);
                    }
                    else
                    {
                        bool isMutable = type.Extensibility == DdsExtensibilityKind.Mutable;
                        bool isAppendableExt = type.Extensibility == DdsExtensibilityKind.Appendable;
                        bool needsHeader = isXcdr2 && (isMutable || isAppendableExt) && NeedsMemberHeader(field);
                        
                        if (needsHeader)
                        {
                            // Determine LC
                            int lc = 2; // Default to 4 bytes (int/float)
                            string typeName = field.TypeName;
                            bool isPrim = IsPrimitive(typeName);
                            bool hasInBodyDHeader = false;

                            if (isPrim)
                            {
                                int align = GetAlignment(typeName);
                                if (align == 1) lc = 0;
                                else if (align == 2) lc = 1;
                                else if (align == 4) lc = 2;
                                else lc = 3;
                            }
                            else
                            {
                                // Sequences, Strings, Structs
                                // In XCDR2, Sequence/String of Constructed types (or Strings) starting with DHEADER can use LC=5
                                // Primitive Sequences do NOT have DHEADER in body, so use LC=6 (Next Int) -> Wait, LC=4 is Next Int.
                                // Lets check if body starts with Length.
                                if (typeName == "string" || typeName == "System.String")
                                {
                                     hasInBodyDHeader = true; // String starts with Length
                                }
                                else if (IsSequenceOrArray(typeName))
                                {
                                     hasInBodyDHeader = true;
                                }
                                else if (field.Type != null && field.Type.IsStruct)
                                {
                                    // Nested struct. Does it start with DHEADER?
                                    // Only if Mutable/Appendable?
                                    // For now assume LC=4 (Explicit Length) for safety if unsure.
                                    // But C might be using LC=5.
                                    // If we use LC=4, we are safe but larger.
                                    // Let's use LC=4 for nested structs for now.
                                }
                            }

                            if (hasInBodyDHeader) lc = 5; // ALSO_NEXT_INT
                            else if (!isPrim) lc = 4; // NEXT_INT (Explicit Length)

                            int headerOverhead = 4;
                            if (lc == 4) headerOverhead = 8;

                            sb.AppendLine("            // Member Header Sizing");
                            sb.AppendLine("            if (encoding == CdrEncoding.Xcdr2)");
                            sb.AppendLine("            {");
                            sb.AppendLine($"                sizer.Align(4);"); 
                            sb.AppendLine($"                sizer.Skip({headerOverhead});");
                            sb.AppendLine("            }");
                        }

                        string sizerCall = GetSizerCall(field, isXcdr2, isAppendable);
                        sb.AppendLine($"            {sizerCall}; // {field.Name}");
                    }
                }
            }
            

            
            sb.AppendLine();
            
            if (type.Name == "SequenceStringTopicAppendable")
            {
               try { System.IO.File.AppendAllText(@"D:\WORK\FastCycloneDdsCsharpBindings\debug_codegen.txt", "\n\n=== GETSERIALIZEDSIZE ===\n" + sb.ToString()); } catch {}
            }
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
                        string caseLabel = val!.ToString()!;
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
                        EmitOptionalSerializer(sb, type, field, fieldId, isXcdr2);
                    }
                    else
                    {
                        bool needsHeader = isXcdr2 && (type.Extensibility == DdsExtensibilityKind.Mutable || type.Extensibility == DdsExtensibilityKind.Appendable) && NeedsMemberHeader(field);
                        string headerPosVar = $"mhPos{fieldId}";
                        string bodyStartVar = $"mbStart{fieldId}";
                        
                        if (needsHeader)
                        {
                            // Determine LC
                            int lc = 2; // Default to 4 bytes
                            string typeName = field.TypeName;
                            bool isPrim = IsPrimitive(typeName);
                            bool hasInBodyDHeader = false;

                            if (isPrim)
                            {
                                int align = GetAlignment(typeName);
                                if (align == 1) lc = 0;
                                else if (align == 2) lc = 1;
                                else if (align == 4) lc = 2;
                                else lc = 3;
                            }
                            else
                            {
                                if (typeName == "string" || typeName == "System.String") hasInBodyDHeader = true;
                                else if (IsSequenceOrArray(typeName))
                                {
                                     // Sequence DHEADER disabled.
                                     // So hasInBodyDHeader = false.
                                }
                                else if (field.Type != null && field.Type.IsStruct)
                                {
                                    // Nested struct. Assume LC=4 (Explicit Length) for safety.
                                }
                            }
                            if (hasInBodyDHeader) lc = 5; // ALSO_NEXT_INT
                            else if (!isPrim) lc = 4; // NEXT_INT

                            sb.AppendLine("            // Member Header (EMHEADER)");
                            // sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
                            // sb.AppendLine("            {");
                            sb.AppendLine($"                writer.Align(4);");
                            sb.AppendLine($"                uint emHeader{fieldId} = (1u << 31) | ((uint){lc} << 28) | ((uint){fieldId} & 0x0FFFFFFFu);");
                            sb.AppendLine($"                writer.WriteUInt32(emHeader{fieldId});");
                            
                            if (lc == 4)
                            {
                                sb.AppendLine($"                int {headerPosVar} = writer.Position;");
                                sb.AppendLine($"                writer.WriteUInt32(0); // Length Placeholder");
                                sb.AppendLine($"                int {bodyStartVar} = writer.Position;");
                            }
                            
                            string writerCall = GetWriterCall(field, isXcdr2, isAppendable);
                            sb.AppendLine($"                {writerCall};");

                            if (lc == 4)
                            {
                                sb.AppendLine($"                int mbEnd{fieldId} = writer.Position;");
                                sb.AppendLine($"                writer.WriteUInt32At({headerPosVar}, (uint)(mbEnd{fieldId} - {bodyStartVar}));");
                            }
                            // sb.AppendLine("            }");
                            /*
                            sb.AppendLine("            else");
                            sb.AppendLine("            {");
                            string writerCallFallback = GetWriterCall(field, isXcdr2, isAppendable);
                            sb.AppendLine($"                {writerCallFallback};");
                            sb.AppendLine("            }");
                            */
                        }
                        else
                        {
                             string writerCall = GetWriterCall(field, isXcdr2, isAppendable);
                             sb.AppendLine($"            {writerCall}; // {field.Name}");
                        }
                    }
                }
            }
 
            if (isAppendable)
            {
                sb.AppendLine("            if (writer.Encoding == CdrEncoding.Xcdr2)");
                sb.AppendLine("            {");
                sb.AppendLine("                int bodyLen = writer.Position - bodyStart;");
                sb.AppendLine("                writer.WriteUInt32At(dheaderPos, (uint)bodyLen);");
                sb.AppendLine("            }");
            }

            if (type.Name == "SequenceStringTopicAppendable")
            {
               try { System.IO.File.AppendAllText(@"D:\WORK\FastCycloneDdsCsharpBindings\debug_codegen.txt", "\n\n=== SERIALIZE ===\n" + sb.ToString()); } catch {}
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
                                 caseLabel = val!.ToString()!;
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
        
        private void EmitOptionalSizer(StringBuilder sb, TypeInfo type, FieldInfo field, bool isXcdr2)
        {
            string access = $"this.{ToPascalCase(field.Name)}";
            string check;
            string baseType = GetBaseType(field.TypeName);
             if (field.TypeName.EndsWith("?"))
                 check = $"{access}.HasValue";
             else if (field.TypeName == "string" || IsReferenceType(baseType))
                 check = $"{access} != null";
             else
                 check = "true";

            sb.AppendLine($"            if ({check})");
            sb.AppendLine("            {");
            
            // XCDR2 Appendable uses 1 byte flag (1), then value.
            sb.AppendLine("                sizer.WriteByte(1); // Flag");
            
            var attrs = field.Attributes.Where(a => a.Name != "DdsOptional" && a.Name != "DdsOptionalAttribute").ToList();
            var nonOptionalField = new FieldInfo 
            { 
                Name = field.Name, 
                TypeName = GetBaseType(field.TypeName),
                Attributes = attrs,
                Type = field.Type 
            };
            
            // FIX: Only Mutable types need Member Header logic for Optionals in XCDR2.
            if (isXcdr2 && type.Extensibility == DdsExtensibilityKind.Mutable && NeedsMemberHeader(nonOptionalField))
            {
                 sb.AppendLine("                if (encoding == CdrEncoding.Xcdr2) { sizer.Align(4); sizer.WriteUInt32(0); }");
            }

            string sizerCall = GetSizerCall(nonOptionalField, isXcdr2);
            if (field.TypeName.EndsWith("?"))
            {
                 if (!sizerCall.Contains(".Value") && !sizerCall.Contains(".ToString")) 
                    sizerCall = sizerCall.Replace($"this.{ToPascalCase(field.Name)}", $"this.{ToPascalCase(field.Name)}.Value");
            }
            
            sb.AppendLine($"                {sizerCall};");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                sizer.WriteByte(0); // Flag");
            sb.AppendLine("            }");
        }

        private void EmitOptionalSerializer(StringBuilder sb, TypeInfo type, FieldInfo field, int fieldId, bool isXcdr2)
        {
            string access = $"this.{ToPascalCase(field.Name)}";
            string check;
            string baseType = GetBaseType(field.TypeName);
             if (field.TypeName.EndsWith("?"))
                 check = $"{access}.HasValue";
             else if (field.TypeName == "string" || IsReferenceType(baseType))
                 check = $"{access} != null";
             else
                 check = "true";

            sb.AppendLine($"            if ({check})");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteByte(1);");

            baseType = GetBaseType(field.TypeName);
            var attrs = field.Attributes.Where(a => a.Name != "DdsOptional" && a.Name != "DdsOptionalAttribute").ToList();
            var nonOptionalField = new FieldInfo 
            { 
                Name = field.Name, 
                TypeName = baseType,
                Attributes = attrs,
                Type = field.Type 
            };
            
            // FIX: Only Mutable types need Member Header (XCDR2) logic. Appendable simply appends value after flag.
            bool needsHeader = isXcdr2 && type.Extensibility == DdsExtensibilityKind.Mutable && NeedsMemberHeader(nonOptionalField);
            
            if (needsHeader)
            {
                string headerPosVar = $"mhPos{fieldId}";
                string bodyStartVar = $"mbStart{fieldId}";
                
                sb.AppendLine("                if (writer.IsXcdr2)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    writer.Align(4);");
                sb.AppendLine($"                    int {headerPosVar} = writer.Position;");
                sb.AppendLine($"                    writer.WriteInt32(0);");
                sb.AppendLine($"                    int {bodyStartVar} = writer.Position;");
                
                string writerCall = GetWriterCall(nonOptionalField, isXcdr2);
                if (field.TypeName.EndsWith("?"))
                     writerCall = writerCall.Replace(access, $"{access}.Value");
                sb.AppendLine($"                    {writerCall};");
                
                sb.AppendLine($"                    int mbEnd{fieldId} = writer.Position;");
                sb.AppendLine($"                    writer.WriteUInt32At({headerPosVar}, (uint)(mbEnd{fieldId} - {bodyStartVar}));");
                sb.AppendLine("                }");
                sb.AppendLine("                else");
                sb.AppendLine("                {");
                string writerCall2 = GetWriterCall(nonOptionalField, isXcdr2);
                if (field.TypeName.EndsWith("?"))
                     writerCall2 = writerCall2.Replace(access, $"{access}.Value");
                sb.AppendLine($"                    {writerCall2};");
                sb.AppendLine("                }");
            }
            else
            {
                string writerCall = GetWriterCall(nonOptionalField, isXcdr2);
                if (field.TypeName.EndsWith("?"))
                     writerCall = writerCall.Replace(access, $"{access}.Value");
                sb.AppendLine($"                {writerCall};");
            }
            
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteByte(0);");
            sb.AppendLine("            }");
        }

        private bool NeedsMemberHeader(FieldInfo field)
        {
            if (IsOptional(field)) return true; 

            // Resolve Type
            TypeInfo? typeInfo = field.Type;
            if (typeInfo == null && _registry != null)
            {
                 if (_registry.TryGetDefinition(field.TypeName, out var def))
                     typeInfo = def?.TypeInfo;
            }

            if (typeInfo != null)
            {
                 // if (IsAppendable(typeInfo)) return true; 
            }

            // Sequence/Array of constructed
            if (IsSequenceOrArray(field.TypeName))
            {
                 string elem = ExtractSequenceElementType(field.TypeName);
                 if (!IsPrimitive(elem)) return true;
            }
            
            return true;
        }

        private bool IsSequenceOrArray(string typeName)
        {
             return typeName.EndsWith("[]") || typeName.StartsWith("List") || typeName.StartsWith("BoundedSeq") || typeName.Contains("Collections.Generic.List");
        }

        private bool IsOptional(FieldInfo field)
        {
            return field.TypeName.EndsWith("?") || field.HasAttribute("DdsOptional");
        }

        private string GetBaseType(string typeName)
        {
            if (typeName.EndsWith("?"))
                return typeName.Substring(0, typeName.Length - 1);
            return typeName;
        }

        private bool IsReferenceType(string typeName)
        {
            return typeName == "string" || typeName.StartsWith("BoundedSeq") || typeName.StartsWith("List") || typeName.Contains("Collections.Generic.List");
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
                if (_registry.TryGetDefinition(typeName, out var def) && def!.TypeInfo != null)
                    return GetTypeAlignment(def.TypeInfo);
                
                // Try replacing dots with colons for scoped lookup
                if (_registry.TryGetDefinition(typeName.Replace(".", "::"), out var def2) && def2!.TypeInfo != null)
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

            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def!.TypeInfo != null && def.TypeInfo.IsEnum)
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
            
            if (_registry != null && _registry.TryGetDefinition(field.TypeName, out var def) && def!.TypeInfo != null && def.TypeInfo.IsEnum)
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
            // - We omit Sequence DHEADER.
/*
            if (isAppendableStruct && !isPrimitive)
            {
                 headerSizer = "sizer.Align(4); sizer.WriteInt32(0); ";
            }
*/
            
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
            // - We omit Sequence DHEADER as C seems to not use it (LC=4 Member Header includes Length).
/*
            if (isAppendableStruct && isXcdr2 && !IsPrimitive(elementType))
            {
                 headerStart = $@"
                writer.Align(4);
                int seqStartPos_{field.Name} = writer.Position;
                writer.WriteUInt32(0); // Placeholder Sequence DHEADER";
                 headerEnd = $@"
                int seqEndPos_{field.Name} = writer.Position;
                writer.WriteUInt32At(seqStartPos_{field.Name}, (uint)(seqEndPos_{field.Name} - seqStartPos_{field.Name}));";
            }
*/
            
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

            bool isEnum = false;
            if (_registry != null && _registry.TryGetDefinition(elementType, out var def) && def!.TypeInfo != null && def.TypeInfo.IsEnum)
            {
                 isEnum = true;
            }

            if (writerMethod != null)
            {
                loopBody = $"writer.Align({alignA}); writer.{writerMethod}({fieldAccess}[i]);";
            }
            else if (isEnum)
            {
                 loopBody = $"writer.Align(4); writer.WriteInt32((int){fieldAccess}[i]);";
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
             // However, List<Union> or List<Complex> DOES have a Sequence DHEADER.
/*
             if (isAppendableStruct && isXcdr2 && !IsPrimitive(elementType))
             {
                 dheaderStart = $@"
                writer.Align(4);
                int seqStartPos_{field.Name} = writer.Position;
                writer.WriteUInt32(0); // Placeholder Sequence DHEADER";
                 dheaderEnd = $@"
                int seqEndPos_{field.Name} = writer.Position;
                writer.WriteUInt32At(seqStartPos_{field.Name}, (uint)(seqEndPos_{field.Name} - seqStartPos_{field.Name}));";
             }
*/
             
             bool isEnum = false;
             if (_registry != null && _registry.TryGetDefinition(elementType, out var def))
             {
                if (def!.TypeInfo != null && def.TypeInfo.IsEnum) isEnum = true;
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
                if (def!.TypeInfo != null && def.TypeInfo.IsEnum) isEnum = true;
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
/*
            if (isAppendableStruct && !isPrimitive)
            {
               dheader = "sizer.Align(4); sizer.WriteUInt32(0); ";
            }
*/




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
