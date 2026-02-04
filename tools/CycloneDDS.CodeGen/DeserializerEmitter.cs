using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using CycloneDDS.Schema;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen
{
    public class DeserializerEmitter
    {
        private readonly ViewEmitter _viewEmitter = new ViewEmitter();
        private readonly ViewExtensionsEmitter _viewExtensionsEmitter = new ViewExtensionsEmitter();

        public void EmitDeserializerCode(StringBuilder sb, TypeInfo type, GlobalTypeRegistry registry)
        {
            // 1. Emit View Struct (Zero-Copy)
            _viewEmitter.EmitViewStruct(sb, type, registry);
            sb.AppendLine();

            // 2. Emit Extension Methods (.AsView)
            _viewExtensionsEmitter.EmitExtensions(sb, type, registry);
            sb.AppendLine();

            // 3. Emit Convenience Logic (ToManaged) inside the partial struct
            // Removed: The ViewEmitter.EmitToManagedMethod generates code that assumes 'this' is a View struct (with HasProp, GetProp(i) etc).
            // Placing it inside the Managed struct (POCO) is incorrect because the POCO doesn't have these members.
            // The View struct (generated in step 1) already includes ToManaged().
        }
        
        // Legacy EmitDeserializer method removed/deprecated
        public string EmitDeserializer(TypeInfo type, GlobalTypeRegistry registry, bool generateUsings = true)
        {
            return "// Legacy CDR deserializer removed.";
        }
    }
}

