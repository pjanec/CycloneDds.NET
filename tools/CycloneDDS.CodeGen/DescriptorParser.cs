using System;
using System.Collections.Generic;
using System.Linq;
using CppAst;

namespace CycloneDDS.CodeGen
{
    public class DescriptorParser
    {
        private static readonly Dictionary<string, uint> MacroValues = new()
        {
            { "DDS_OP_RTS", 0x00u << 24 },
            { "DDS_OP_ADR", 0x01u << 24 },
            { "DDS_OP_JSR", 0x02u << 24 },
            { "DDS_OP_JEQ", 0x03u << 24 },
            { "DDS_OP_DLC", 0x04u << 24 },
            { "DDS_OP_PLC", 0x05u << 24 },
            { "DDS_OP_PLM", 0x06u << 24 },
            { "DDS_OP_KOF", 0x07u << 24 },
            { "DDS_OP_JEQ4", 0x08u << 24 },

            { "DDS_OP_TYPE_1BY", 0x01u << 16 },
            { "DDS_OP_TYPE_2BY", 0x02u << 16 },
            { "DDS_OP_TYPE_4BY", 0x03u << 16 },
            { "DDS_OP_TYPE_8BY", 0x04u << 16 },
            { "DDS_OP_TYPE_STR", 0x05u << 16 },
            { "DDS_OP_TYPE_BST", 0x06u << 16 },
            { "DDS_OP_TYPE_SEQ", 0x07u << 16 },
            { "DDS_OP_TYPE_ARR", 0x08u << 16 },
            { "DDS_OP_TYPE_UNI", 0x09u << 16 },
            { "DDS_OP_TYPE_STU", 0x0Au << 16 },
            { "DDS_OP_TYPE_BSQ", 0x0Bu << 16 },
            { "DDS_OP_TYPE_ENU", 0x0Cu << 16 },
            { "DDS_OP_TYPE_EXT", 0x0Du << 16 },
            { "DDS_OP_TYPE_BLN", 0x0Eu << 16 },
            { "DDS_OP_TYPE_BMK", 0x0Fu << 16 },

             // Flags
            { "DDS_OP_FLAG_KEY", 0x01u },
            { "DDS_OP_FLAG_DEF", 0x02u },
            { "DDS_OP_FLAG_SGN", 0x04u },
            { "DDS_OP_FLAG_FP",  0x08u },
            { "DDS_OP_FLAG_EXT", 0x10u },
            { "DDS_OP_FLAG_OPT", 0x20u },
            { "DDS_OP_FLAG_MU",  0x40u },
            
            // Type Codes (unshifted) for manual composition if needed
            { "DDS_OP_VAL_1BY", 0x01u },
            { "DDS_OP_VAL_2BY", 0x02u },
            { "DDS_OP_VAL_4BY", 0x03u },
            { "DDS_OP_VAL_8BY", 0x04u },
            { "DDS_OP_VAL_STR", 0x05u },
        };
        
        // Masks
        private const uint DDS_OP_MASK = 0xff000000;
        private const uint DDS_OP_TYPE_MASK = 0x007f0000;

        public DescriptorMetadata ParseDescriptor(string cFilePath, string? typeName = null)
        {
            var options = new CppParserOptions
            {
                ParseMacros = true,
                ParseSystemIncludes = false
            };
            
            // Infer Header Path
            string dir = System.IO.Path.GetDirectoryName(cFilePath)!;
            string fn = System.IO.Path.GetFileNameWithoutExtension(cFilePath);
            string hFilePath = System.IO.Path.Combine(dir, fn + ".h");
            
            string cContent = System.IO.File.ReadAllText(cFilePath);
            string hContent = "";
            if (System.IO.File.Exists(hFilePath))
            {
                hContent = System.IO.File.ReadAllText(hFilePath);
            }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("typedef unsigned int uint32_t;");
            sb.AppendLine("typedef int int32_t;");
            sb.AppendLine("typedef unsigned int uint;");
            sb.AppendLine("typedef long long int64_t;");
            sb.AppendLine("typedef unsigned long long uint64_t;");
            sb.AppendLine("typedef short int16_t;");
            sb.AppendLine("typedef unsigned short uint16_t;");
            sb.AppendLine("typedef unsigned char uint8_t;");
            sb.AppendLine("typedef char int8_t;");
            // sb.AppendLine("typedef _Bool bool;");
            sb.AppendLine("typedef struct dds_topic_descriptor { void* m; } dds_topic_descriptor_t;");
            
            sb.AppendLine("#define offsetof(t,d) 195948557");
            sb.AppendLine("typedef struct dds_key_descriptor { const char * m_name; uint32_t m_offset; uint32_t m_idx; } dds_key_descriptor_t;");
            
            sb.AppendLine("enum DdsOpCodes {");
            foreach(var kvp in MacroValues)
            {
                sb.AppendLine($"    {kvp.Key} = {kvp.Value}u,");
            }
            sb.AppendLine("};");
            sb.AppendLine("#define NULL 0");
            sb.AppendLine("#define DDS_EXPORT"); // Handle DLL exports in headers

            // Helper to strip includes and append
            void AppendStripped(string content, bool skipDescriptorTypedefs = false)
            {
                bool skipping = false;
                foreach (var lineIter in content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    string line = lineIter;
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#include")) continue;
                    
                    if (skipDescriptorTypedefs)
                    {
                        if (trimmed.Contains("dds_topic_descriptor_t")) skipping = true;
                        
                        if (!skipping) sb.AppendLine(line);

                        if (skipping && trimmed.EndsWith(";")) skipping = false;
                    }
                    else
                    {
                        sb.AppendLine(line);
                    }
                }
            }
            
            // Extract structs from header content via Regex to bypass CppAst parsing issues with extern "C" etc
            if (!string.IsNullOrEmpty(hContent))
            {
                 var structRegex = new System.Text.RegularExpressions.Regex(
                     @"typedef\s+struct\s+(\w+)\s*\{(.*?)\}\s*\1\s*;", 
                     System.Text.RegularExpressions.RegexOptions.Singleline);
                 foreach(System.Text.RegularExpressions.Match m in structRegex.Matches(hContent))
                 {
                     string sName = m.Groups[1].Value;
                     string sBody = m.Groups[2].Value;
                     sb.AppendLine($"struct {sName} {{ {sBody} }};");
                 }
            }

            AppendStripped(cContent, skipDescriptorTypedefs: true);

            var compilation = CppParser.Parse(sb.ToString(), options);
            
            if (compilation.HasErrors)
            {
                foreach (var message in compilation.Diagnostics.Messages)
                {
                    Console.WriteLine($"Diag: {message}");
                }
            }
            var metadata = new DescriptorMetadata();
            
            foreach (var field in compilation.Fields)
            {
                if (field.Name.EndsWith("_ops"))
                {
                    if (!string.IsNullOrEmpty(typeName) && !field.Name.EndsWith($"{typeName}_ops")) continue;

                    metadata.OpsArrayName = field.Name;
                    metadata.TypeName = field.Name.Substring(0, field.Name.Length - 4); // Remove _ops
                    metadata.OpsValues = ParseArrayInitializer(field.InitExpression, isOps: true);
                }
                else if (field.Name.EndsWith("_keys"))
                {
                    metadata.KeysArrayName = field.Name;
                    // First try regex because CppAst often fails on struct initializers with our mocked headers
                    // Pass compilation to resolve offsets
                    metadata.KeyDescriptors = ParseKeysUsingRegex(cContent, field.Name, compilation, typeName);
                    if (metadata.KeyDescriptors.Length == 0)
                    {
                        metadata.KeyDescriptors = ParseKeyDescriptors(field.InitExpression);
                    }
                }
            }

            return metadata;
        }


        private KeyDescriptorInfo[] ParseKeysUsingRegex(string cContent, string keysArrayName, CppCompilation compilation, string? typeName)
        {
            // Find: static const dds_key_descriptor_t KeysArrayName[N] = { content };
            var regex = new System.Text.RegularExpressions.Regex(
                $@"static\s+const\s+dds_key_descriptor_t\s+{System.Text.RegularExpressions.Regex.Escape(keysArrayName)}\[\d+\]\s*=\s*\{{([^;]+)\}};",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            
            var match = regex.Match(cContent);
            if (!match.Success) 
            {
                 // Try without length?
                 regex = new System.Text.RegularExpressions.Regex(
                    $@"static\s+const\s+dds_key_descriptor_t\s+{System.Text.RegularExpressions.Regex.Escape(keysArrayName)}\s*\[.*\]\s*=\s*\{{([^;]+)\}};",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                 match = regex.Match(cContent);
            }
            
            if (!match.Success) return Array.Empty<KeyDescriptorInfo>();
            
            var content = match.Groups[1].Value;
            
            var list = new List<KeyDescriptorInfo>();
            // Split by }, { or just look for { ... } patterns
            // Initializer format: { "name", offset, idx }
            // Offset can be number or offsetof(...)
            var itemRegex = new System.Text.RegularExpressions.Regex(@"\{\s*""([^""]+)""\s*,\s*([^,]+),\s*(\d+)\s*\}");
            
            foreach(System.Text.RegularExpressions.Match itemMatch in itemRegex.Matches(content))
            {
                 var info = new KeyDescriptorInfo();
                 info.Name = itemMatch.Groups[1].Value;
                 
                 string offsetStr = itemMatch.Groups[2].Value.Trim();
                 bool offsetParsed = false;
                 if (uint.TryParse(offsetStr, out uint pOffset))
                 {
                     info.Offset = pOffset;
                     offsetParsed = true;
                 }
                 else
                 {
                     info.Offset = 0;
                 }

                 if (uint.TryParse(itemMatch.Groups[3].Value, out uint idx))
                    info.Index = idx;
                 else 
                    info.Index = 0;
                
                 // Try resolve offset from compilation if not parsed or if we suspect we need struct layout
                 // But trusting IDL generated offset seems safer if it is a literal.
                 if (!offsetParsed && compilation != null)
                 {
                     string targetStruct = typeName;
                     if (string.IsNullOrEmpty(targetStruct))
                        targetStruct = keysArrayName.Replace("_keys", ""); 
                     
                     var cppClass = compilation.Classes.FirstOrDefault(c => c.Name == targetStruct);
                     // If mangled/namespaced: Module_Struct vs Struct.
                     if (cppClass == null) cppClass = compilation.Classes.FirstOrDefault(c => c.Name.EndsWith("_" + targetStruct));
                     if (cppClass == null) cppClass = compilation.Classes.FirstOrDefault(c => c.Name == targetStruct || c.Name.EndsWith(targetStruct)); // Fuzzy

                     if (cppClass != null)
                     {
                         var field = cppClass.Fields.FirstOrDefault(f => f.Name == info.Name);
                         if (field != null)
                         {
                             // CppAst Offset is in Bytes for basic types?
                             // Observed: 4 aligned int32 -> 0, 4, 8.
                             info.Offset = (uint)field.Offset;
                         }
                     }
                 }

                 list.Add(info);
            }
            return list.ToArray();
        }

        private KeyDescriptorInfo[] ParseKeyDescriptors(CppExpression? initExpression)
        {
            if (initExpression is not CppInitListExpression initList)
            {
                // Console.WriteLine($"DEBUG: Not CppInitListExpression: {initExpression?.GetType().Name} - Value: {initExpression}");
                return Array.Empty<KeyDescriptorInfo>();
            }
            
            var list = new List<KeyDescriptorInfo>();
            foreach (var item in initList.Arguments)
            {
                // Console.WriteLine($"DEBUG: Item Type: {item.GetType().Name}, ToString: {item}");
                // Each item is a struct initializer { "name", offset, idx }
                if (item is CppInitListExpression structInit && structInit.Arguments.Count >= 3)
                {
                     var info = new KeyDescriptorInfo();
                     // Name is first argument (string literal)
                     info.Name = structInit.Arguments[0].ToString().Trim('"');
                     
                     // Offset is second - we ignore it because we'll resolve it in C# runtime via Marshal.OffsetOf
                     info.Offset = 0;
                     
                     // Index is third
                     var idx = EvaluateExpression(structInit.Arguments[2]);
                     info.Index = idx ?? 0;
                     
                     list.Add(info);
                }
            }
            return list.ToArray();
        }

        private uint[] ParseArrayInitializer(CppExpression? initExpression, bool isOps)
        {
            if (initExpression is not CppInitListExpression initList)
                return Array.Empty<uint>();

            var rawValues = new List<uint?>(); 

            foreach (var item in initList.Arguments)
            {
                rawValues.Add(EvaluateExpression(item));
            }

            if (!isOps)
            {
                return rawValues.Select(v => v ?? 0).ToArray();
            }

            return ResolveOffsets(rawValues);
        }

        private uint[] ResolveOffsets(List<uint?> rawValues)
        {
            // Debug dump
            // Console.Error.WriteLine($"ResolveOffsets input count: {rawValues.Count}");
            // for(int k=0; k<rawValues.Count; k++) Console.Error.WriteLine($"  raw[{k}]: {(rawValues[k].HasValue ? rawValues[k].Value.ToString() : "null")}");

            uint currentOffset = 0;
            var result = new uint[rawValues.Count];
            
            for (int i = 0; i < rawValues.Count; i++)
            {
                bool isOffsetField = false;
                uint prevOp = 0;
                
                if (i > 0)
                {
                    uint? pVal = rawValues[i - 1];
                    if (pVal.HasValue)
                    {
                        if ((pVal.Value & DDS_OP_MASK) == (0x01u << 24)) // ADR
                        {
                            isOffsetField = true;
                            prevOp = pVal.Value;
                        }
                    }
                }

                if (isOffsetField)
                {
                    uint type = (prevOp & DDS_OP_TYPE_MASK) >> 16;
                    var (size, align) = GetTypeSizeAndAlign(type);
                    
                    uint padding = (align - (currentOffset % align)) % align;
                    currentOffset += padding;
                    
                    if (!rawValues[i].HasValue || rawValues[i].GetValueOrDefault() == 0xBADF00D)
                    {
                        result[i] = currentOffset;
                    }
                    else
                    {
                        result[i] = rawValues[i]!.Value;
                    }
                    
                    currentOffset += size;
                }
                else
                {
                    result[i] = rawValues[i] ?? 0;
                }
            }
            
            return result;
        }

        private uint? EvaluateExpression(CppExpression expression)
        {
            // Debug logging
            // Console.Error.WriteLine($"Evaluating: {expression} ({expression.GetType().Name}) Kind={expression.Kind}");

            if (expression is CppLiteralExpression lit)
            {
                var valStr = lit.Value;
                // Console.Error.WriteLine($"  Literal Value: '{valStr}'");
                
                if (string.IsNullOrEmpty(valStr)) return null;
                
                // Remove suffix u/U/l/L
                valStr = valStr.TrimEnd('u', 'U', 'l', 'L');
                
                try {
                    if (valStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (uint.TryParse(valStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var val))
                            return val;
                    }
                    else
                    {
                         if (uint.TryParse(valStr, out var val))
                            return val;
                    }
                } catch { }
                return null;
            }
            
            if (expression is CppBinaryExpression bin)
            {
                // Console.Error.WriteLine($"  Binary Op: '{bin.Operator}'");
                if (bin.Arguments.Count < 2) return null;
                var left = EvaluateExpression(bin.Arguments[0]);
                var right = EvaluateExpression(bin.Arguments[1]);
                
                if (left.HasValue && right.HasValue)
                {
                     // Operator might have spaces?
                     var op = bin.Operator.Trim();
                     switch (op)
                     {
                         case "|": return left.Value | right.Value;
                         case "+": return left.Value + right.Value;
                         case "&": return left.Value & right.Value;
                         case "^": return left.Value ^ right.Value;
                         case "<<": return left.Value << (int)right.Value;
                         case ">>": return left.Value >> (int)right.Value;
                     }
                }
                return null;
            }
            
            // Handle raw names (macros/enums) via ToString()
            var text = expression?.ToString()?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            // Console.Error.WriteLine($"  Raw text: '{text}'");
             if (MacroValues.TryGetValue(text, out var mVal))
                return mVal;
                
             // Handle bitwise OR via textual splitting if parser didn't make it a binary expression
             if (text.Contains("|"))
             {
                 var parts = text.Split('|');
                 uint sum = 0;
                 foreach(var part in parts)
                 {
                     var p = part.Trim();
                     if (MacroValues.TryGetValue(p, out var v)) sum |= v;
                     else return null; // unknown part
                 }
                 return sum;
             }
             
             // Handle offsetof
             if (text.Contains("offsetof"))
                 return null; // Trigger override mechanism
                 
             return null;
        }

        private (uint size, uint align) GetTypeSizeAndAlign(uint typeCode)
        {
            switch (typeCode)
            {
                case 0x01: // 1BY
                case 0x0E: // BLN
                    return (1, 1);
                case 0x02: // 2BY
                    return (2, 2);
                case 0x03: // 4BY
                case 0x0C: // ENU
                case 0x0F: // BMK
                    return (4, 4); 
                case 0x04: // 8BY
                    return (8, 8);
                // Pointers / Complex types
                case 0x05: // STR 
                case 0x06: // BST 
                case 0x07: // SEQ 
                case 0x08: // ARR 
                case 0x0B: // BSQ 
                case 0x0D: // EXT 
                    return (8, 8); // Assuming 64-bit
                default:
                    return (1, 1);
            }
        }
    }
}
