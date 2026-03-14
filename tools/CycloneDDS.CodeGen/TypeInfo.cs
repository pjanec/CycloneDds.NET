using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen
{
    public class TypeInfo
    {
        public DdsExtensibilityKind Extensibility { get; set; } = DdsExtensibilityKind.Appendable;
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
        public string SourceFile { get; set; } = string.Empty;
        
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
        public List<AttributeInfo> Attributes { get; set; } = new List<AttributeInfo>();
        
        public bool IsEnum { get; set; }
        public bool IsTopic { get; set; }
        public bool IsStruct { get; set; }
        public bool IsClass { get; set; } // Added to support managed classes
        public bool IsUnion { get; set; }
        public List<string> EnumMembers { get; set; } = new List<string>();

        /// <summary>Bit width of the enum's underlying type. 8 for byte/sbyte, 16 for short/ushort, 32 for default (int/uint).</summary>
        public int EnumBitBound { get; set; } = 32;

        /// <summary>Resolved DDS topic name. Populated by SchemaDiscovery when IsTopic is true.</summary>
        public string? TopicName { get; set; }

        public bool HasAttribute(string name) => Attributes.Any(a => a.Name == name || a.Name == name + "Attribute");
        public AttributeInfo? GetAttribute(string name) => Attributes.FirstOrDefault(a => a.Name == name || a.Name == name + "Attribute");

        public bool IsManagedType()
        {
            return Attributes != null && 
                   Attributes.Any(a => a.Name == "DdsManaged");
        }
    }

    public class FieldInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public TypeInfo? Type { get; set; } // Resolved nested type, null if primitive/external
        public TypeInfo? GenericType { get; set; } // Resolved generic argument type (e.g. T in List<T>)
        public List<AttributeInfo> Attributes { get; set; } = new List<AttributeInfo>();

        /// <summary>True when the field is a C# fixed-size buffer (e.g. <c>public fixed byte Buf[64];</c>).</summary>
        public bool IsFixedSizeBuffer { get; set; }
        /// <summary>Number of elements in the fixed-size buffer (0 when <see cref="IsFixedSizeBuffer"/> is false).</summary>
        public int FixedSize { get; set; }
        /// <summary>True when the field type is decorated with <c>[System.Runtime.CompilerServices.InlineArray(N)]</c>.
        /// When true, <see cref="IsFixedSizeBuffer"/> is also true and the element type / count are stored
        /// in <see cref="TypeName"/> and <see cref="FixedSize"/> respectively.</summary>
        public bool IsInlineArray { get; set; }

        public bool HasAttribute(string name) => Attributes.Any(a => a.Name == name || a.Name == name + "Attribute");
        public AttributeInfo? GetAttribute(string name) => Attributes.FirstOrDefault(a => a.Name == name || a.Name == name + "Attribute");

        public bool IsManagedFieldType()
        {
            return TypeName == "string" || 
                   TypeName.StartsWith("List<") ||
                   TypeName.StartsWith("System.Collections.Generic.List<");
        }
    }

    public class AttributeInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<object> Arguments { get; set; } = new List<object>();
        
        // Return all arguments as case values, allowing bool, enum (int), etc.
        public List<object> CaseValues => Arguments;
    }
}
