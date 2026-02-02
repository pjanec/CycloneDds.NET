namespace CycloneDDS.CodeGen
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class GlobalTypeRegistry
    {
        private Dictionary<string, IdlTypeDefinition> _types = new Dictionary<string, IdlTypeDefinition>();

        public void RegisterLocal(TypeInfo type, string sourceFileName, string idlFile, string idlModule)
        {
            var def = new IdlTypeDefinition
            {
                CSharpFullName = type.FullName,
                TargetIdlFile = idlFile,
                TargetModule = idlModule,
                TypeInfo = type,
                IsExternal = false,
                SourceFile = sourceFileName
            };

            CheckForCollisions(def);
            _types[type.FullName] = def;
        }

        public void RegisterExternal(string fullName, string idlFile, string idlModule, TypeInfo? typeInfo = null)
        {
            var def = new IdlTypeDefinition
            {
                CSharpFullName = fullName,
                TargetIdlFile = idlFile,
                TargetModule = idlModule,
                IsExternal = true,
                TypeInfo = typeInfo
            };
            
            // For external types, we replace if exists (last one wins or merge?)
            // Usually we assume external types are authoritative.
            _types[fullName] = def;
        }

        public bool TryGetDefinition(string fullName, out IdlTypeDefinition? def)
        {
            return _types.TryGetValue(fullName, out def);
        }

        public IEnumerable<IdlTypeDefinition> LocalTypes => _types.Values.Where(t => !t.IsExternal);
        public IEnumerable<IdlTypeDefinition> AllTypes => _types.Values;

        private void CheckForCollisions(IdlTypeDefinition newDef)
        {
            string newIdentity = GetIdlIdentity(newDef);

            foreach (var existing in _types.Values)
            {
                if (existing.IsExternal) continue; // Don't check against external implicitly? Or we should? 
                // The requirement says "Detect IDL identity collisions". 
                
                string existingIdentity = GetIdlIdentity(existing);
                if (newIdentity == existingIdentity && existing.CSharpFullName != newDef.CSharpFullName)
                {
                    // Collision detected
                    throw new InvalidOperationException(
                        $"IDL name collision detected! \n" +
                        $"Type '{newDef.CSharpFullName}' and '{existing.CSharpFullName}' both map to:\n" +
                        $"  File: {newDef.TargetIdlFile}.idl\n" +
                        $"  Module: {newDef.TargetModule}\n" +
                        $"  Name: {newDef.TypeInfo?.Name}\n" +
                        $"Use [DdsIdlModule] or [DdsIdlFile] to resolve this."
                    );
                }
            }
        }

        private string GetIdlIdentity(IdlTypeDefinition def)
        {
            // Identity is File + Module + TypeName
            // Note: TypeInfo might be null for External types if we don't have full info, 
            // but RegisterExternal doesn't take TypeInfo.
            // Wait, for External types we typically have the CSharpFullName.
            // If we are checking collisions for Local types, we mostly care about other Local types.
            // External types are pre-compiled and we can't change them.
            
            string simpleName = def.TypeInfo != null ? def.TypeInfo.Name : GetSimpleName(def.CSharpFullName);
            return $"{def.TargetIdlFile}::{def.TargetModule}::{simpleName}";
        }

        private string GetSimpleName(string fullName)
        {
            int lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }
    }
}
