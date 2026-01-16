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
            { "DDS_OP_FLAG_OPT", 0x02u },
            { "DDS_OP_FLAG_MU",  0x04u },
            { "DDS_OP_FLAG_SGN", 0x20u }, 
            { "DDS_OP_FLAG_FP",  0x40u },
            
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

        public DescriptorMetadata ParseDescriptor(string cFilePath)
        {
            var options = new CppParserOptions
            {
                ParseMacros = true,
                ParseSystemIncludes = false
            };
            
            string fileContent = System.IO.File.ReadAllText(cFilePath);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("typedef unsigned int uint32_t;");
            sb.AppendLine("typedef unsigned int uint;");
            sb.AppendLine("#define offsetof(t,d) 195948557");
            
            sb.AppendLine("enum DdsOpCodes {");
            foreach(var kvp in MacroValues)
            {
                sb.AppendLine($"    {kvp.Key} = {kvp.Value}u,");
            }
            sb.AppendLine("};");
            sb.AppendLine(fileContent);

            var compilation = CppParser.Parse(sb.ToString(), options);
            
            if (compilation.HasErrors)
            {
                foreach (var message in compilation.Diagnostics.Messages)
                {
                    Console.WriteLine($"Diag: {message}");
                }
            }
            var metadata = new DescriptorMetadata();
            
            // ... (rest of the code)


            foreach (var field in compilation.Fields)
            {
                if (field.Name.EndsWith("_ops"))
                {
                    metadata.OpsArrayName = field.Name;
                    metadata.TypeName = field.Name.Substring(0, field.Name.Length - 4); // Remove _ops
                    metadata.OpsValues = ParseArrayInitializer(field.InitExpression, isOps: true);
                }
                else if (field.Name.EndsWith("_keys"))
                {
                    metadata.KeysArrayName = field.Name;
                    metadata.KeysValues = ParseArrayInitializer(field.InitExpression, isOps: false);
                }
            }

            return metadata;
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
                    
                    if (!rawValues[i].HasValue || rawValues[i].Value == 0xBADF00D)
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
            var text = expression.ToString().Trim();
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
