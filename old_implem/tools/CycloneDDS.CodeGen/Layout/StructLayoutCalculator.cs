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
        
        // Handle both fields and properties
        var members = type.Members.Where(m => m is FieldDeclarationSyntax or PropertyDeclarationSyntax);

        int currentOffset = 0;
        int maxAlignment = 1;
        
        foreach (var member in members)
        {
            string fieldType;
            string fieldName;

            if (member is FieldDeclarationSyntax f)
            {
                 fieldType = f.Declaration.Type.ToString();
                 fieldName = f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "unknown";
            }
            else if (member is PropertyDeclarationSyntax p)
            {
                 fieldType = p.Type.ToString();
                 fieldName = p.Identifier.Text;
            }
            else
            {
                continue; 
            }
            
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
            "boolean" => 1,
            "octet" => 1,
            "char" => 1,
            "short" => 2,
            "unsigned short" => 2,
            "long" => 4,
            "unsigned long" => 4,
            "long long" => 8,
            "unsigned long long" => 8,
            "int8" => 1,
            "uint8" => 1,
            "int16" => 2,
            "uint16" => 2,
            "int32" => 4,
            "uint32" => 4,
            "int64" => 8,
            "uint64" => 8,
            "float" => 4,
            "double" => 8,
            "string" => 8, // Pointer on x64
            // Sequence, Array, Struct? 
            // Simplified: treat as pointer for sequences/strings? 
            // Fixed array needs special handling. 
            // BATCH-06 scope limits to primitives + string.
            // Complex types (sequences) are pointers (8 bytes) usually.
            _ => 1 // Fallback
        };
    }
}
