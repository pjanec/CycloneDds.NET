using System;
using System.Collections.Generic;
using System.Linq;

namespace CycloneDDS.CodeGen
{
    public class SchemaValidator
    {
        private readonly HashSet<string> _knownTypeNames;
        private readonly Dictionary<string, HashSet<string>> _knownTypeSimpleNames;
   
        public SchemaValidator(IEnumerable<TypeInfo> discoveredTypes, IEnumerable<string>? externalTypes = null)
        {
            _knownTypeNames = new HashSet<string>(
                discoveredTypes.Select(t => t.FullName)
            );
            _knownTypeSimpleNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            AddSimpleNames(discoveredTypes.Select(t => t.FullName));
            
            if (externalTypes != null)
            {
                foreach (var t in externalTypes)
                {
                    _knownTypeNames.Add(t);
                }
                AddSimpleNames(externalTypes);
            }
        }

        private void AddSimpleNames(IEnumerable<string> typeNames)
        {
            foreach (var name in typeNames)
            {
                var simple = GetSimpleName(name);
                if (!_knownTypeSimpleNames.TryGetValue(simple, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    _knownTypeSimpleNames[simple] = set;
                }
                set.Add(name);
            }
        }

        private static string GetSimpleName(string typeName)
        {
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
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

            ValidateCollectionAttributes(field, containerName, errors);
            
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
            
            // Arrays - recurse
            if (typeName.EndsWith("[]"))
            {
                string innerType = typeName.Substring(0, typeName.Length - 2);
                // Recursively validate inner type
                if (!IsValidUserType(innerType) && !TypeMapper.IsPrimitive(innerType) && innerType != "string" && innerType != "System.String")
                {
                    errors.Add($"Field '{containerName}.{field.Name}' uses array of type '{innerType}', " +
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
   
        /// <summary>
        /// [ArrayLength] and [MaxLength] combinations the marshaller cannot honour: they would make the
        /// IDL (and so the wire format and type identity) disagree with the native layout it writes.
        /// </summary>
        private void ValidateCollectionAttributes(FieldInfo field, string containerName, List<string> errors)
        {
            string typeName = field.TypeName;
            bool isList = typeName.StartsWith("List<") || typeName.StartsWith("System.Collections.Generic.List<");
            bool isArray = typeName.EndsWith("[]");
            var arrayLength = field.GetAttribute("ArrayLength");

            if (arrayLength != null && !field.IsFixedSizeBuffer && (isList || isArray))
            {
                errors.Add($"Field '{containerName}.{field.Name}': [ArrayLength] is not supported on '{typeName}', " +
                           "which is always a sequence. For a fixed-length IDL array use a fixed buffer " +
                           "(`public fixed int X[4];`), or bound the sequence with [DdsBound(n)].");
            }

            if (arrayLength != null && field.IsFixedSizeBuffer && arrayLength.Arguments.Count > 0)
            {
                long product = 1;
                foreach (var d in arrayLength.Arguments) product *= Convert.ToInt64(d);
                if (product != field.FixedSize)
                    errors.Add($"Field '{containerName}.{field.Name}': [ArrayLength({string.Join(", ", arrayLength.Arguments)})] " +
                               $"has {product} elements but the fixed buffer has {field.FixedSize}.");
            }

            if (typeName is "char" or "System.Char" or "char?" or "List<char>" or "char[]")
            {
                errors.Add($"Field '{containerName}.{field.Name}': a C# char is two bytes, an IDL char one. " +
                           "Use [DdsChar] byte (or sbyte, List<byte>, byte[], fixed byte) for IDL char.");
            }

            if (field.HasAttribute("DdsChar"))
            {
                string element = isArray ? typeName.Substring(0, typeName.Length - 2)
                    : isList ? ExtractGenericArgument(typeName) : typeName.TrimEnd('?');
                if (element is not ("byte" or "sbyte" or "System.Byte" or "System.SByte"))
                    errors.Add($"Field '{containerName}.{field.Name}': [DdsChar] applies to byte or sbyte (or a collection of them), not '{typeName}'.");
            }

            if (field.HasAttribute("DdsUInt8"))
            {
                string element = isArray ? typeName.Substring(0, typeName.Length - 2)
                    : isList ? ExtractGenericArgument(typeName) : typeName.TrimEnd('?');
                if (element is not ("byte" or "System.Byte"))
                    errors.Add($"Field '{containerName}.{field.Name}': [DdsUInt8] applies to byte (or a collection of bytes), not '{typeName}'.");
            }

            if ((isList || isArray) && field.HasAttribute("MaxLength"))
            {
                string element = isArray ? typeName.Substring(0, typeName.Length - 2) : ExtractGenericArgument(typeName);
                if (element == "string" || element == "System.String")
                    errors.Add($"Field '{containerName}.{field.Name}': [MaxLength] on a string collection is not supported " +
                               "(bounded-string elements are stored inline in C). Bound the sequence with [DdsBound(n)] " +
                               "and leave the strings unbounded.");
            }
        }

        private void ValidateUnion(TypeInfo type, List<string> errors)
        {
            // Must have exactly one [DdsDiscriminator]
            var discriminators = type.Fields.Where(f => f.HasAttribute("DdsDiscriminator")).ToList();
            if (discriminators.Count != 1)
                errors.Add($"Union {type.FullName} must have exactly one [DdsDiscriminator]");
            
            // All [DdsCase] values must be unique
            var caseValues = new HashSet<object>();
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

        private bool IsValidUserType(string typeName)
        {
            if (_knownTypeNames.Contains(typeName))
            {
                return true;
            }

            var simple = GetSimpleName(typeName);
            if (_knownTypeSimpleNames.TryGetValue(simple, out var matches))
            {
                return matches.Count == 1;
            }

            return false;
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
    }
}
