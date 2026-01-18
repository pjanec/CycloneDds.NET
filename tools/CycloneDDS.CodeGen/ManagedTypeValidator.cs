using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    /// <summary>
    /// Validates that types using managed fields (string, List&lt;T&gt;) are marked with [DdsManaged].
    /// </summary>
    public class ManagedTypeValidator
    {
        public List<ValidationMessage> Validate(TypeInfo type)
        {
            var diagnostics = new List<ValidationMessage>();
            
            if (type == null) return diagnostics;
            
            // Check each field for managed types
            foreach (var field in type.Fields ?? Enumerable.Empty<FieldInfo>())
            {
                if (IsManagedFieldType(field.TypeName))
                {
                    if (!HasDdsManagedAttribute(type) && !HasDdsManagedAttribute(field))
                    {
                        diagnostics.Add(new ValidationMessage
                        {
                            Severity = ValidationSeverity.Error,
                            Message = $"Type '{type.FullName ?? type.Name}' has field '{field.Name}' " +
                                      $"of managed type '{field.TypeName}' but is not marked with [DdsManaged]. " +
                                      $"Add [DdsManaged] attribute to type or field to acknowledge GC allocations."
                        });
                    }
                }
            }
            
            return diagnostics;
        }
        
        private bool IsManagedFieldType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            
            return typeName == "string" ||
                   typeName.StartsWith("List<") ||
                   typeName.StartsWith("System.Collections.Generic.List<");
        }
        
        private bool HasDdsManagedAttribute(TypeInfo type)
        {
            return type.Attributes?.Any(a => a.Name == "DdsManaged") ?? false;
        }
        
        private bool HasDdsManagedAttribute(FieldInfo field)
        {
            return field.Attributes?.Any(a => a.Name == "DdsManaged") ?? false;
        }
    }

    public class ValidationMessage 
    {
        public ValidationSeverity Severity { get; set; }
        public required string Message { get; set; }
    }

    public enum ValidationSeverity
    {
        Error,
        Warning,
        Info
    }
}
