using System;

namespace CycloneDDS.CodeGen.Layout;

public static class AlignmentCalculator
{
    /// <summary>
    /// Get the alignment requirement for a C type in bytes.
    /// </summary>
    public static int GetAlignment(string cType)
    {
        return cType switch
        {
            // Primitives
            "octet" or "int8" or "uint8" => 1,
            "int16" or "uint16" => 2,
            "long" or "unsigned long" or "float" => 4,
            "long long" or "unsigned long long" or "double" => 8,
            "boolean" => 1,
            "char" or "wchar" => 1, // or 2 depending on encoding
            
            // String/sequence pointers
            "string" => IntPtr.Size, // Pointer size (4 or 8 bytes)
            _ when cType.StartsWith("sequence<") => IntPtr.Size,
            
            // Fixed arrays - alignment of element type
            _ when cType.Contains("[") => GetArrayAlignment(cType),
            
            // Assume user-defined struct/enum - need more context
            // For now, default to pointer alignment for safety
            _ => 4 // Conservative default
        };
    }
    
    private static int GetArrayAlignment(string cType)
    {
        // Extract element type from "octet[32]" -> "octet"
        var openBracket = cType.IndexOf('[');
        if (openBracket > 0)
        {
            var elementType = cType.Substring(0, openBracket).Trim();
            return GetAlignment(elementType);
        }
        return 1;
    }
    
    /// <summary>
    /// Calculate the next aligned offset for a given alignment.
    /// </summary>
    public static int AlignUp(int offset, int alignment)
    {
        if (alignment <= 1) return offset;
        return (offset + alignment - 1) & ~(alignment - 1);
    }
    
    /// <summary>
    /// Calculate padding needed to align offset.
    /// </summary>
    public static int CalculatePadding(int currentOffset, int alignment)
    {
        var aligned = AlignUp(currentOffset, alignment);
        return aligned - currentOffset;
    }
}
