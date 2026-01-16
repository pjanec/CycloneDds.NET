using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CycloneDDS.CodeGen.DescriptorExtraction;

public static class DescriptorExtractor
{
    private static readonly Dictionary<string, uint> OpConstants = new()
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

        { "DDS_OP_VAL_1BY", 0x01 },
        { "DDS_OP_VAL_2BY", 0x02 },
        { "DDS_OP_VAL_4BY", 0x03 },
        { "DDS_OP_VAL_8BY", 0x04 },
        { "DDS_OP_VAL_STR", 0x05 },
        { "DDS_OP_VAL_BST", 0x06 },
        { "DDS_OP_VAL_SEQ", 0x07 },
        { "DDS_OP_VAL_ARR", 0x08 },
        { "DDS_OP_VAL_UNI", 0x09 },
        { "DDS_OP_VAL_STU", 0x0a },
        { "DDS_OP_VAL_BSQ", 0x0b },
        { "DDS_OP_VAL_ENU", 0x0c },
        { "DDS_OP_VAL_EXT", 0x0d },
        { "DDS_OP_VAL_BLN", 0x0e },
        { "DDS_OP_VAL_BMK", 0x0f },

        { "DDS_OP_TYPE_1BY", 0x01u << 16 },
        { "DDS_OP_TYPE_2BY", 0x02u << 16 },
        { "DDS_OP_TYPE_4BY", 0x03u << 16 },
        { "DDS_OP_TYPE_8BY", 0x04u << 16 },
        { "DDS_OP_TYPE_STR", 0x05u << 16 },
        { "DDS_OP_TYPE_BST", 0x06u << 16 },
        { "DDS_OP_TYPE_SEQ", 0x07u << 16 },
        { "DDS_OP_TYPE_ARR", 0x08u << 16 },
        { "DDS_OP_TYPE_UNI", 0x09u << 16 },
        { "DDS_OP_TYPE_STU", 0x0au << 16 },
        { "DDS_OP_TYPE_BSQ", 0x0bu << 16 },
        { "DDS_OP_TYPE_ENU", 0x0cu << 16 },
        { "DDS_OP_TYPE_EXT", 0x0du << 16 },
        { "DDS_OP_TYPE_BLN", 0x0eu << 16 },
        { "DDS_OP_TYPE_BMK", 0x0fu << 16 },
        
        { "DDS_OP_SUBTYPE_1BY", 0x01u << 8 },
        { "DDS_OP_SUBTYPE_2BY", 0x02u << 8 },
        { "DDS_OP_SUBTYPE_4BY", 0x03u << 8 },
        { "DDS_OP_SUBTYPE_8BY", 0x04u << 8 },
        { "DDS_OP_SUBTYPE_STR", 0x05u << 8 },
        { "DDS_OP_SUBTYPE_BST", 0x06u << 8 },
        { "DDS_OP_SUBTYPE_SEQ", 0x07u << 8 },
        { "DDS_OP_SUBTYPE_ARR", 0x08u << 8 },
        { "DDS_OP_SUBTYPE_UNI", 0x09u << 8 },
        { "DDS_OP_SUBTYPE_STU", 0x0au << 8 },
        { "DDS_OP_SUBTYPE_BSQ", 0x0bu << 8 },
        { "DDS_OP_SUBTYPE_ENU", 0x0cu << 8 },
        { "DDS_OP_SUBTYPE_BLN", 0x0eu << 8 },
        { "DDS_OP_SUBTYPE_BMK", 0x0fu << 8 },

        { "DDS_OP_FLAG_KEY", 1u << 0 },
        { "DDS_OP_FLAG_DEF", 1u << 1 },
        { "DDS_OP_FLAG_FP", 1u << 1 },
        { "DDS_OP_FLAG_SGN", 1u << 2 },
        { "DDS_OP_FLAG_MU", 1u << 3 },
        { "DDS_OP_FLAG_BASE", 1u << 4 },
        { "DDS_OP_FLAG_OPT", 1u << 5 },
    };

    public static DescriptorData ExtractFromIdlcOutput(string cFilePath, string cycloneIncludePath)
    {
        var content = File.ReadAllText(cFilePath);
        var data = new DescriptorData();

        data.TypeName = ExtractValue(content, @"\.m_typename\s*=\s*""([^""]+)""");
        
        var nOpsStr = ExtractValue(content, @"\.m_nops\s*=\s*(\d+)u?");
        uint fileNOps = uint.TryParse(nOpsStr, out var no) ? no : 0;

        var nKeysStr = ExtractValue(content, @"\.m_nkeys\s*=\s*(\d+)u?");
        data.NKeys = uint.TryParse(nKeysStr, out var nk) ? nk : 0;

        // Size and Align - return 0, will be handled by sizeof(T) in generated code if needed
        data.Size = 0; 
        data.Align = 0;

        // Ops
        var opsRegex = new Regex(@"_ops\s*\[\]\s*=\s*\{([\s\S]*?)\};");
        var opsMatch = opsRegex.Match(content);
        if (opsMatch.Success)
        {
            data.Ops = ParseOpsArray(opsMatch.Groups[1].Value);
        }
        else
        {
            data.Ops = Array.Empty<uint>();
        }

        if (data.Ops.Length > 0 && data.Ops.Length != fileNOps)
        {
            data.NOps = (uint)data.Ops.Length;
        }
        else if (data.Ops.Length == 0 && fileNOps > 0)
        {
            data.NOps = 0;
        }
        else
        {
            data.NOps = fileNOps;
        }

        // Check keys (fixed array)
        if (data.NKeys > 0)
        {
            var keysRegex = new Regex(@"static const dds_key_descriptor_t\s+(\w+)\s*\[\d+\]\s*=\s*\{([\s\S]*?)\};");
            var keysMatch = keysRegex.Match(content);
            if (keysMatch.Success)
            {
                data.Keys = ParseKeys(keysMatch.Groups[2].Value);
            }
        }

        // TypeInfo
        var typeInfoRegex = new Regex(@"#define TYPE_INFO_CDR_\w+\s*\(unsigned char \[\]\)\{\s*([\s\S]*?)\s*\}");
        var tiMatch = typeInfoRegex.Match(content);
        if (tiMatch.Success)
        {
            data.TypeInfo = ParseByteArray(tiMatch.Groups[1].Value);
        }
        else
        {
             data.TypeInfo = Array.Empty<byte>();
        }

        // TypeMap
        var typeMapRegex = new Regex(@"#define TYPE_MAP_CDR_\w+\s*\(unsigned char \[\]\)\{\s*([\s\S]*?)\s*\}");
        var tmMatch = typeMapRegex.Match(content);
        if (tmMatch.Success)
        {
            data.TypeMap = ParseByteArray(tmMatch.Groups[1].Value);
        }
        else
        {
            data.TypeMap = Array.Empty<byte>();
        }
        
        data.Meta = "";

        return data;
    }
    
    private static string ExtractValue(string content, string pattern)
    {
        var match = Regex.Match(content, pattern);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static KeyDescriptor[] ParseKeys(string body)
    {
        var keys = new List<KeyDescriptor>();
        // Body example: { "Id", 20, 0 }
        
        // Simple regex to match { "name", offset, idx }
        var matches = Regex.Matches(body, @"\{\s*""([^""]+)""\s*,\s*(\d+)\s*,\s*(\d+)\s*\}");
        foreach (Match m in matches)
        {
             keys.Add(new KeyDescriptor 
             {
                 Name = m.Groups[1].Value,
                 Flags = (ushort)uint.Parse(m.Groups[2].Value), // Storing Offset in Flags
                 Index = (ushort)uint.Parse(m.Groups[3].Value)
             });
        }
        return keys.ToArray();
    }

    private static uint[] ParseOpsArray(string body)
    {
        try 
        {
            var instructions = new List<uint>();
            body = Regex.Replace(body, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var parts = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            uint currentOffset = 0;
            var fieldOffsets = new Dictionary<string, uint>();
            uint pendingAlign = 1;
            uint pendingSize = 0;

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                
                var offsetMatch = Regex.Match(part, @"offsetof\s*\(\s*\w+\s*,\s*(\w+)\s*\)");
                if (offsetMatch.Success)
                {
                    var fieldName = offsetMatch.Groups[1].Value;
                    
                    if (fieldOffsets.TryGetValue(fieldName, out var cachedOff))
                    {
                        instructions.Add(cachedOff);
                    }
                    else
                    {
                        if (pendingAlign > 1)
                        {
                            uint mask = pendingAlign - 1;
                            if ((currentOffset & mask) != 0)
                            {
                                currentOffset = (currentOffset + mask) & ~mask;
                            }
                        }
                        
                        fieldOffsets[fieldName] = currentOffset;
                        instructions.Add(currentOffset);
                        
                        currentOffset += pendingSize;
                        
                        pendingAlign = 1;
                        pendingSize = 0;
                    }
                    continue;
                }
                
                if (part.Contains("sizeof"))
                {
                    instructions.Add(0); 
                    continue;
                }

                uint val = EvaluateExpression(part);
                instructions.Add(val);

                AnalyzeOpCode(val, ref pendingAlign, ref pendingSize);
            }
            
            return instructions.ToArray();
        }
        catch
        {
            return Array.Empty<uint>();
        }
    }
    
    // Fix: Make GetAlignment static and fix return type
    private static uint GetAlignment(uint typeCode)
    {
        switch (typeCode)
        {
             case 0x01: return 1;
             case 0x02: return 2;
             case 0x03: return 4;
             case 0x04: return 8;
             case 0x05: return 8; 
             default: return 1;
        }
    }       

    private static void AnalyzeOpCode(uint op, ref uint pendingAlign, ref uint pendingSize)
    {
        uint type = (op >> 16) & 0xFF;
        uint subType = (op >> 8) & 0xFF;

        switch (type)
        {
            case 0x01: // 1BY
            case 0x0E: // BLN
                 pendingSize = 1; pendingAlign = 1; break;
            case 0x02: // 2BY
                 pendingSize = 2; pendingAlign = 2; break;
            case 0x03: // 4BY
            case 0x0C: // ENU
            case 0x0F: // BMK
                 pendingSize = 4; pendingAlign = 4; break;
            case 0x04: // 8BY
                 pendingSize = 8; pendingAlign = 8; break;
            case 0x05: // STR
            case 0x0D: // EXT
            case 0x07: // SEQ
                 pendingSize = 8; pendingAlign = 8; break;
            case 0x08: // ARR
                 pendingAlign = GetAlignment(subType);
                 pendingSize = 0; 
                 break;
            default:
                 pendingSize = 0; pendingAlign = 1;
                 break;
        }
    }

    private static uint EvaluateExpression(string expr)
    {
        expr = expr.Replace("u", "").Replace("(", "").Replace(")", "");
        var parts = expr.Split(new[]{'+'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); // Removed trim entries here to keep simple
        // Wait, Need proper trimming
        // Using TrimEntries in options if available in netstandard? No.
        // Manual trim.
        
        uint sum = 0;
        foreach (var p_raw in parts)
        {
            var p = p_raw.Trim();
            if (p.Contains("<<"))
            {
                var shifts = p.Split(new[]{"<<"}, StringSplitOptions.RemoveEmptyEntries);
                uint val = Resolve(shifts[0].Trim());
                int shift = (int)Resolve(shifts[1].Trim());
                sum += val << shift;
            }
            else if (p.Contains("|"))
            {
                var ors = p.Split(new[]{'|'}, StringSplitOptions.RemoveEmptyEntries);
                uint val = 0;
                foreach (var o in ors) val |= Resolve(o.Trim());
                sum += val;
            }
            else
            {
                sum += Resolve(p);
            }
        }
        return sum;
    }
    
    private static uint Resolve(string part)
    {
        if (OpConstants.TryGetValue(part, out var val)) return val;
        
        if (part.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            try { return Convert.ToUInt32(part, 16); } catch { return 0; }
        }
        
        if (uint.TryParse(part, out var i)) return i;
        
        return 0; 
    }
    
    private static byte[] ParseByteArray(string body)
    {
        var bytes = new List<byte>();
        body = body.Replace("\\", "");
        var parts = body.Split(new[] { ',', '\n', '\r', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var clean = p.Trim();
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try {
                    bytes.Add(Convert.ToByte(clean, 16));
                } catch {}
            }
        }
        return bytes.ToArray();
    }
}
