namespace CycloneDDS.CodeGen.Emitters;

public static class IdlTypeMapper
{
    public static string MapToIdl(string csType)
    {
        return csType switch
        {
            // Primitives
            "byte" => "octet",
            "sbyte" => "int8",
            "short" => "int16",
            "ushort" => "uint16",
            "int" => "long",
            "uint" => "unsigned long",
            "long" => "long long",
            "ulong" => "unsigned long long",
            "float" => "float",
            "double" => "double",
            "bool" => "boolean",
            "char" => "wchar",  // Or char depending on encoding
            
            // String types
            "string" => "string",  // Unbounded
            
            // Fixed strings (wrapper types)
            "FixedString32" => "octet[32]",
            "FixedString64" => "octet[64]",
            "FixedString128" => "octet[128]",
            
            // Special types with typedefs
            "Guid" or "System.Guid" => "Guid16",
            "DateTime" or "System.DateTime" => "Int64TicksUtc",
            
            // Arrays handled separately (string[] -> sequence<string>)
            
            _ => MapCustomType(csType)
        };
    }
    
    private static string MapCustomType(string csType)
    {
        if (csType.Contains("Quaternion")) return "QuaternionF32x4";

        // Handle array types: int[] -> sequence<long>
        if (csType.EndsWith("[]"))
        {
            var elementType = csType[..^2];
            var idlElementType = MapToIdl(elementType);
            return $"sequence<{idlElementType}>";
        }
        
        // Handle nullable types: int? -> @optional long
        if (csType.EndsWith("?"))
        {
            var baseType = csType[..^1];
            return MapToIdl(baseType);  // @optional handled separately
        }
        
        // Handle generic types like BoundedSeq<T, N>
        if (csType.StartsWith("BoundedSeq<"))
        {
            // Extract T and N from BoundedSeq<T, N>
            // Return sequence<MapToIdl(T), N>
            // This is simplified - you'll need proper parsing
            // For now, let's assume simple format or return sequence
             var content = csType.Substring(11, csType.Length - 12); // Remove BoundedSeq< and >
             var parts = content.Split(',');
             if (parts.Length == 2)
             {
                 var t = parts[0].Trim();
                 var n = parts[1].Trim();
                 return $"sequence<{MapToIdl(t)}, {n}>";
             }
        }
        
        // Assume it's a user-defined type or enum
        return csType;
    }
    
    public static bool IsNullableType(string csType)
    {
        return csType.EndsWith("?");
    }
    
    public static bool IsArrayType(string csType)
    {
        return csType.EndsWith("[]");
    }
    
    public static bool RequiresTypedef(string csType)
    {
        // Types that need typedef (Guid, DateTime, Quaternion, etc.)
        return csType switch
        {
            "Guid" => true,
            "DateTime" => true,
            "System.Guid" => true,
            "System.DateTime" => true,
            _ when csType.Contains("Quaternion") => true,
            _ => false
        };
    }
    
    public static string GetTypedefMapping(string csType)
    {
        return csType switch
        {
            "Guid" or "System.Guid" => "typedef octet Guid16[16];",
            "DateTime" or "System.DateTime" => "typedef long long Int64TicksUtc;",
            _ when csType.Contains("Quaternion") => "struct QuaternionF32x4 { float x; float y; float z; float w; };",
            _ => $"// Unknown typedef for {csType}"
        };
    }
}
