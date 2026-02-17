using System;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Compiler.Common.IdlJson;

namespace CycloneDDS.IdlImporter;

/// <summary>
/// Maps IDL types to C# types with appropriate attributes.
/// Handles primitives, collections, and user-defined types.
/// </summary>
public class TypeMapper
{
    private static readonly Dictionary<string, string> _primitiveMapping = new Dictionary<string, string>
    {
        { "boolean", "bool" },
        { "char", "byte" },
        { "octet", "byte" },
        { "short", "short" },
        { "unsigned short", "ushort" },
        { "long", "int" },
        { "unsigned long", "uint" },
        { "long long", "long" },
        { "unsigned long long", "ulong" },
        { "float", "float" },
        { "double", "double" },
        { "string", "string" },
        // idlc output mappings
        { "int32_t", "int" },
        { "uint32_t", "uint" },
        { "int16_t", "short" },
        { "uint16_t", "ushort" },
        { "int64_t", "long" },
        { "uint64_t", "ulong" },
        { "int8_t", "sbyte" },
        { "uint8_t", "byte" },
        { "bool", "bool" }
    };

    private readonly Dictionary<string, string> _flattenedToScoped = new();
    private readonly Dictionary<string, string> _typedefMapping = new();

    public void RegisterType(JsonTypeDefinition type)
    {
        string scopedName = type.Name;
        
        // Geom::Point -> Geom_Point
        // We want to map Geom_Point -> Geom.Point in C#
        string flattened = scopedName.Replace("::", "_");
        string csName = scopedName.Replace("::", ".");
        _flattenedToScoped[flattened] = csName;

        // Register typedefs/aliases
        if (type.Kind == "alias" || type.Kind == "typedef")
        {
            if (!string.IsNullOrEmpty(type.Type))
            {
                // Map the alias name to the target type name
                // e.g. "CommonLib::Point2D" -> "CommonLib::Point"
                // The target type will be resolved recursively
                _typedefMapping[scopedName] = type.Type;
            }
        }
    }

    /// <summary>
    /// Maps an IDL primitive type name to its C# equivalent.
    /// </summary>
    /// <param name="idlType">IDL type name (e.g., "long", "double", "string")</param>
    /// <returns>C# type name (e.g., "int", "double", "string")</returns>
    public string MapPrimitive(string idlType)
    {
        if (_primitiveMapping.TryGetValue(idlType, out var csType))
        {
            return csType;
        }
        throw new NotImplementedException($"Type mapping not yet implemented for: {idlType} (IDLIMP-003)");
    }

    /// <summary>
    /// Maps an IDL member (field) to its C# representation with metadata.
    /// </summary>
    /// <param name="member">JSON member definition from idlc output</param>
    /// <returns>Tuple of (C# type, requires [DdsManaged], array length, bound)</returns>
    public (string CsType, bool IsManaged, int ArrayLen, int Bound) MapMember(JsonMember member)
    {
        string baseType = member.Type ?? "void";
        
        string csType;
        bool isManaged = false;
        int arrayLen = 0;
        int bound = 0;

        // Check for collections
        if (!string.IsNullOrEmpty(member.CollectionType))
        {
            string elementIdlType = member.Type ?? "void";
            string elementCsType = MapPrimitiveOrUserType(elementIdlType);

            if (member.CollectionType == "sequence") 
            {
                // Sequence -> List<T>
                csType = $"List<{elementCsType}>";
                isManaged = true; // Sequences are always managed in this binding
                if (member.Bound.HasValue)
                {
                    bound = member.Bound.Value;
                }
            }
            else if (member.CollectionType == "array")
            {
                // Array -> T[]
                csType = $"{elementCsType}[]";
                isManaged = true; // Arrays are always managed in this binding
                
                if (member.Dimensions != null && member.Dimensions.Count > 0)
                {
                    arrayLen = member.Dimensions[0];
                }
            }
            else 
            {
                csType = "object"; // Fallback
            }
        }
        else
        {
            // Single value
            csType = MapPrimitiveOrUserType(baseType);

            if (csType == "string")
            {
                isManaged = true; 
                if (member.Bound.HasValue)
                {
                    bound = member.Bound.Value;
                }
            }
            
            // Handle optional
            if (member.IsOptional)
            {
                // Value types need explicit nullable ?
                // We assume anything that isn't string, List<T>, or T[] is a value type (primitive or struct/enum)
                if (csType != "string" && !csType.StartsWith("List<") && !csType.EndsWith("[]"))
                {
                    csType += "?";
                }
            }
        }

        return (csType, isManaged, arrayLen, bound);
    }

    private string MapPrimitiveOrUserType(string idlType)
    {
        // Recursively resolve typedef/aliases
        int maxDepth = 10;
        int depth = 0;
        while (_typedefMapping.TryGetValue(idlType, out var baseType) && depth < maxDepth)
        {
            idlType = baseType;
            depth++;
        }

        // Handle idlc implicit sequence type names (e.g. dds_sequence_long)
        while (idlType.StartsWith("dds_sequence_"))
        {
            idlType = idlType.Substring("dds_sequence_".Length);
        }

        try 
        {
            return MapPrimitive(idlType);
        }
        catch 
        {
            // Check if flattened name matches a known type
            if (_flattenedToScoped.TryGetValue(idlType, out var scoped))
            {
                return scoped;
            }

            // User defined type (e.g. MyModule::MyType)
            // Replace :: with .
            return GetCSharpNamespace(idlType);
        }
    }

    /// <summary>
    /// Converts an IDL module path to a C# namespace.
    /// </summary>
    /// <param name="idlModulePath">IDL module path (e.g., "Module::SubModule")</param>
    /// <returns>C# namespace (e.g., "Module.SubModule")</returns>
    public string GetCSharpNamespace(string idlModulePath)
    {
        if (string.IsNullOrEmpty(idlModulePath)) return string.Empty;
        return idlModulePath.Replace("::", ".");
    }

    /// <summary>
    /// Determines if a type requires the [DdsManaged] attribute.
    /// </summary>
    /// <param name="csType">C# type name</param>
    /// <param name="isCollection">Whether the field is a collection</param>
    /// <returns>True if [DdsManaged] should be applied</returns>
    public bool RequiresManagedAttribute(string csType, bool isCollection)
    {
        // Always managed:
        // - string
        // - List<T> (sequences)
        // - T[] (arrays)
        
        if (csType == "string") return true;
        if (csType.StartsWith("List<")) return true;
        if (csType.EndsWith("[]")) return true;
        
        return false;
    }
}
