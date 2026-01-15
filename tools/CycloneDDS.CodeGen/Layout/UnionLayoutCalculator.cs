using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;
using System;

namespace CycloneDDS.CodeGen.Layout;

public class UnionLayout
{
    public string DiscriminatorType { get; set; } = "";
    public int DiscriminatorSize { get; set; }
    public int DiscriminatorAlignment { get; set; }
    public int PayloadOffset { get; set; } // Offset where union payload starts
    public int MaxArmSize { get; set; }
    public int MaxArmAlignment { get; set; }
    public int TotalSize { get; set; }
}

public class UnionLayoutCalculator
{
    /// <summary>
    /// Calculate C-compatible union layout.
    /// Union payload offset is determined by max(discriminator_alignment, max_arm_alignment).
    /// </summary>
    public UnionLayout CalculateLayout(TypeDeclarationSyntax type)
    {
        var layout = new UnionLayout();
        
        // Find discriminator
        var discriminatorField = type.Members
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsDiscriminator" or "DdsDiscriminatorAttribute"));
        
        if (discriminatorField == null)
        {
            // Error: union must have discriminator
            layout.TotalSize = 0;
            return layout;
        }
        
        var discriminatorType = discriminatorField.Declaration.Type.ToString();
        var idlDiscriminator = IdlTypeMapper.MapToIdl(discriminatorType);
        layout.DiscriminatorType = idlDiscriminator;
        layout.DiscriminatorSize = GetTypeSize(idlDiscriminator);
        layout.DiscriminatorAlignment = AlignmentCalculator.GetAlignment(idlDiscriminator);
        
        // Find all case arms
        var caseFields = type.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() is "DdsCase" or "DdsCaseAttribute" or "DdsDefaultCase" or "DdsDefaultCaseAttribute"));
        
        int maxArmSize = 0;
        int maxArmAlignment = 1;
        
        foreach (var arm in caseFields)
        {
            var armType = arm.Declaration.Type.ToString();
            var idlArmType = IdlTypeMapper.MapToIdl(armType);
            var armSize = GetTypeSize(idlArmType);
            var armAlignment = AlignmentCalculator.GetAlignment(idlArmType);
            
            maxArmSize = Math.Max(maxArmSize, armSize);
            maxArmAlignment = Math.Max(maxArmAlignment, armAlignment);
        }
        
        layout.MaxArmSize = maxArmSize;
        layout.MaxArmAlignment = maxArmAlignment;
        
        // Payload offset: discriminator, then padding to align to max arm alignment
        var payloadAlignment = Math.Max(layout.DiscriminatorAlignment, maxArmAlignment);
        layout.PayloadOffset = AlignmentCalculator.AlignUp(layout.DiscriminatorSize, payloadAlignment);
        
        // Total size: payload offset + max arm size + trailing padding
        var sizeBeforePadding = layout.PayloadOffset + maxArmSize;
        var unionAlignment = Math.Max(layout.DiscriminatorAlignment, maxArmAlignment);
        layout.TotalSize = AlignmentCalculator.AlignUp(sizeBeforePadding, unionAlignment);
        
        return layout;
    }
    
    private int GetTypeSize(string idlType)
    {
        // Reuse logic from StructLayoutCalculator
        // TODO: Extract to shared helper class to avoid duplication
        return idlType switch
        {
            "octet" or "int8" or "uint8" or "boolean" => 1,
            "int16" or "uint16" or "char" or "wchar" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            "string" => IntPtr.Size,
            _ when idlType.StartsWith("sequence<") => IntPtr.Size * 2,
            _ when idlType.Contains("[") => GetFixedArraySize(idlType),
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
