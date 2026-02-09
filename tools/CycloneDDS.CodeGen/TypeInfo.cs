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
