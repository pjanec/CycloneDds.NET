using System;
using System.Collections.Generic;
using CycloneDDS.CodeGen;
using CycloneDDS.CodeGen.Emitters;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.CodeGen.Tests
{
    public class ViewSystemTypesTests : CodeGenTestBase
    {
        private readonly ViewEmitter _viewEmitter = new ViewEmitter();
        private readonly SerializerEmitter _serializerEmitter = new SerializerEmitter();

        [Fact]
        public void GenerateView_GuidAndDateTime_CompilesAndLogicIsCorrect()
        {
            var type = new TypeInfo
            {
                Name = "SystemTypesStruct",
                Namespace = "ViewTest",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "System.Guid" },
                    new FieldInfo { Name = "Timestamp", TypeName = "System.DateTime" }
                }
            };
            
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "SystemTypesStruct.cs", "SystemTypesStruct.idl", "ViewTest");

            // User struct definition
            var userStruct = @"
using System;
namespace ViewTest
{
    [CycloneDDS.Schema.DdsStruct]
    public partial struct SystemTypesStruct
    {
        public Guid Id;
        public DateTime Timestamp;
    }
}";

            // 1. Emit Native Struct (via Serializer)
            var serializerCode = _serializerEmitter.EmitSerializer(type, registry);
            
            // 2. Emit View
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);

            // Verify generated code contains expected logic
            Assert.Contains("public unsafe System.Guid Id => _ptr->Id.ToManaged();", viewCode);
            Assert.Contains("public unsafe System.DateTime Timestamp => new System.DateTime(_ptr->Timestamp, System.DateTimeKind.Utc);", viewCode);
            
            // ToManaged check
            Assert.Contains("target.Id = this.Id;", viewCode);
            Assert.Contains("target.Timestamp = this.Timestamp;", viewCode);
            
            // Verify Native Struct contains correct types
            Assert.Contains("public CycloneDDS.Runtime.Interop.DdsGuid Id;", serializerCode);
            Assert.Contains("public long Timestamp;", serializerCode);

            // 3. Compile
             var asm = CompileToAssembly("ViewSystemTypes", userStruct, serializerCode, viewCode);
             Assert.NotNull(asm);
        }

        [Fact]
        public void GenerateView_SystemTypes_OptionalAndSequence_Compiles()
        {
             var type = new TypeInfo
            {
                Name = "ComplexSystemTypes",
                Namespace = "ViewTest",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "OptId", TypeName = "System.Guid?" },
                    new FieldInfo { Name = "IdList", TypeName = "System.Collections.Generic.List<System.Guid>" }
                }
            };
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "ComplexSystemTypes.cs", "ComplexSystemTypes.idl", "ViewTest");

             var userStruct = @"
using System;
using System.Collections.Generic;
namespace ViewTest
{
    [CycloneDDS.Schema.DdsStruct]
    public partial struct ComplexSystemTypes
    {
        public Guid? OptId;
        public List<Guid> IdList;
    }
}";
            var serializerCode = _serializerEmitter.EmitSerializer(type, registry);
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);
            
            // Check Optional
            Assert.Contains("public unsafe System.Guid? OptId", viewCode);
            Assert.Contains("(*(CycloneDDS.Runtime.Interop.DdsGuid*)_ptr->OptId).ToManaged()", viewCode);
            
            // Check Sequence
            Assert.Contains("public unsafe System.Guid GetIdList(int index)", viewCode);
            Assert.Contains("return arr[index].ToManaged();", viewCode);

            var asm = CompileToAssembly("ViewComplexSystemTypes", userStruct, serializerCode, viewCode);
            Assert.NotNull(asm);
        }
    }
}
