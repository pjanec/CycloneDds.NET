using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    public class SchemaValidator
    {
        private readonly HashSet<string> _knownTypeNames;
   
        public SchemaValidator(IEnumerable<TypeInfo> discoveredTypes, IEnumerable<string>? externalTypes = null)
        {
            _knownTypeNames = new HashSet<string>(
                discoveredTypes.Select(t => t.FullName)
            );
            
            if (externalTypes != null)
            {
                foreach (var t in externalTypes)
                {
                    _knownTypeNames.Add(t);
                }
            }
        }

        public ValidationResult Validate(TypeInfo type)
        {
            var errors = new List<string>();
            
            // 1. Check for circular dependencies
            if (HasCircularDependency(type))
                errors.Add($"Circular dependency detected in {type.FullName}");
            
            // 2. Validate field types
            foreach (var field in type.Fields)
            {
               ValidateFieldType(field, type.FullName, errors);
            }
            
            // 3. Check union structure (if [DdsUnion])
            if (type.HasAttribute("DdsUnion"))
            {
                ValidateUnion(type, errors);
            }
            
            return new ValidationResult(errors);
        }

        private void ValidateFieldType(FieldInfo field, string containerName, List<string> errors)
        {
            string typeName = field.TypeName;
            
            // Handle nullable
            if (typeName.EndsWith("?"))
                typeName = typeName.TrimEnd('?');
            
            // Primitives OK
            if (TypeMapper.IsPrimitive(typeName)) return;
            
            // Known wrappers OK
            if (typeName.Contains("FixedString")) return;
            if (typeName == "Guid" || typeName == "DateTime" || typeName == "TimeSpan") return;
            if (typeName.Contains("Vector") || typeName == "Quaternion") return;
            
            // Collections - recurse
            if (typeName.StartsWith("BoundedSeq<") || typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<"))
            {
                string innerType = ExtractGenericArgument(typeName);
                // Recursively validate inner type
                if (!IsValidUserType(innerType) && !TypeMapper.IsPrimitive(innerType) && innerType != "string" && innerType != "System.String")
                {
                    errors.Add($"Field '{containerName}.{field.Name}' uses collection of type '{innerType}', " +
                               $"which is not a valid DDS type. Mark '{innerType}' with [DdsStruct] or [DdsTopic].");
                }
                return;
            }
            
            // Managed strings
            if (typeName == "string")
            {
                // Already validated by ManagedTypeValidator
                return;
            }
            
            // User-defined types - THE STRICT CHECK
            if (!IsValidUserType(typeName))
            {
                errors.Add($"Field '{containerName}.{field.Name}' uses type '{typeName}', " +
                           $"which is not a valid DDS type. " +
                           $"Did you forget to add [DdsStruct] or [DdsTopic] to '{typeName}'?");
            }
        }
   
        private bool IsValidUserType(string typeName)
        {
            return _knownTypeNames.Contains(typeName);
        }
   
        private string ExtractGenericArgument(string typeName)
        {
            int start = typeName.IndexOf('<') + 1;
            int end = typeName.LastIndexOf('>');
            if (start > 0 && end > start)
            {
                return typeName.Substring(start, end - start).Trim();
            }
            return typeName;
        }
        
        private void ValidateUnion(TypeInfo type, List<string> errors)
        {
            // Must have exactly one [DdsDiscriminator]
            var discriminators = type.Fields.Where(f => f.HasAttribute("DdsDiscriminator")).ToList();
            if (discriminators.Count != 1)
                errors.Add($"Union {type.FullName} must have exactly one [DdsDiscriminator]");
            
            // All [DdsCase] values must be unique
            var caseValues = new HashSet<int>();
            foreach (var field in type.Fields.Where(f => f.HasAttribute("DdsCase")))
            {
                var attr = field.GetAttribute("DdsCase");
                if (attr != null)
                {
                    foreach (var c in attr.CaseValues)
                    {
                        if (!caseValues.Add(c))
                            errors.Add($"Duplicate case value {c} in union {type.FullName}");
                    }
                }
            }
        }

        private bool HasCircularDependency(TypeInfo type, HashSet<string>? visited = null)
        {
            visited ??= new HashSet<string>();
            
            if (!visited.Add(type.FullName))
                return true; // Cycle detected
            
            foreach (var field in type.Fields)
            {
                if (field.Type != null)
                {
                    if (HasCircularDependency(field.Type, new HashSet<string>(visited)))
                        return true;
                }
            }
            
            return false;
        }
    }
}
