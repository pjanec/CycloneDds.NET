using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;
using System;

namespace CycloneDDS.CodeGen.Layout;

public class FieldLayout
{
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = "";
    public int Offset { get; set; }
    public int Size { get; set; }
    public int Alignment { get; set; }
    public int PaddingBefore { get; set; }
}

public class StructLayout
{
    public List<FieldLayout> Fields { get; set; } = new();
    public int TotalSize { get; set; }
    public int MaxAlignment { get; set; }
    public int TrailingPadding { get; set; }
}

public class StructLayoutCalculator
{
    /// <summary>
    /// Calculate C-compatible struct layout with padding.
    /// </summary>
    public StructLayout CalculateLayout(TypeDeclarationSyntax type)
    {
        var layout = new StructLayout();
        var fields = type.Members.OfType<FieldDeclarationSyntax>().ToList();
        
        int currentOffset = 0;
        int maxAlignment = 1;
        
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
            
            // Map C# type to IDL/C type for alignment calculation
            var idlType = IdlTypeMapper.MapToIdl(fieldType);
            var alignment = AlignmentCalculator.GetAlignment(idlType);
            var size = GetTypeSize(idlType);
            
            // Calculate padding needed
            var padding = AlignmentCalculator.CalculatePadding(currentOffset, alignment);
            var fieldOffset = currentOffset + padding;
            
            layout.Fields.Add(new FieldLayout
            {
                FieldName = fieldName,
                FieldType = idlType,
                Offset = fieldOffset,
                Size = size,
                Alignment = alignment,
                PaddingBefore = padding
            });
            
            currentOffset = fieldOffset + size;
            maxAlignment = Math.Max(maxAlignment, alignment);
        }
        
        // Add trailing padding to align struct to its max field alignment
        var trailingPadding = AlignmentCalculator.CalculatePadding(currentOffset, maxAlignment);
        
        layout.MaxAlignment = maxAlignment;
        layout.TotalSize = currentOffset + trailingPadding;
        layout.TrailingPadding = trailingPadding;
        
        return layout;
    }
    
    private int GetTypeSize(string idlType)
    {
        return idlType switch
        {
            "octet" or "int8" or "uint8" or "boolean" => 1,
            "int16" or "uint16" or "char" or "wchar" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            
            // Pointers for strings/sequences
            "string" => IntPtr.Size,
            _ when idlType.StartsWith("sequence<") => IntPtr.Size * 2, // ptr + length
            
            // Fixed arrays: "octet[32]" -> 32 bytes
            _ when idlType.Contains("[") => GetFixedArraySize(idlType),
            
            // Unknown types - conservative estimate
            _ => 4
        };
    }
    
    private int GetFixedArraySize(string idlType)
    {
        // Extract size from "octet[32]"
        var start = idlType.IndexOf('[');
        var end = idlType.IndexOf(']');
        if (start > 0 && end > start)
        {
            var sizeStr = idlType.Substring(start + 1, end - start - 1);
            if (int.TryParse(sizeStr, out var arraySize))
            {
                var elementType = idlType.Substring(0, start).Trim();
                var elementSize = GetTypeSize(elementType);
                return elementSize * arraySize;
            }
        }
        return 4; // Fallback
    }
}
