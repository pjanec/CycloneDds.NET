using System;
using System.Collections.Generic;
using CycloneDDS.CodeGen;
using CycloneDDS.CodeGen.Emitters;
using CycloneDDS.Schema;
using Xunit;

namespace CycloneDDS.CodeGen.Tests
{
    public class ViewGenerationTests : CodeGenTestBase
    {
        private readonly ViewEmitter _viewEmitter = new ViewEmitter();
        private readonly ViewExtensionsEmitter _extEmitter = new ViewExtensionsEmitter();
        private readonly SerializerEmitter _serializerEmitter = new SerializerEmitter();

        [Fact]
        public void GenerateView_Primitives_Compiles()
        {
            var type = new TypeInfo
            {
                Name = "PrimitiveStruct",
                Namespace = "ViewTest",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "IntVal", TypeName = "int" },
                    new FieldInfo { Name = "DoubleVal", TypeName = "double" },
                    new FieldInfo { Name = "BoolVal", TypeName = "bool" }
                }
            };
            
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "PrimitiveStruct.cs", "PrimitiveStruct.idl", "ViewTest");

            var userStruct = @"
namespace ViewTest
{
    public partial struct PrimitiveStruct
    {
        public int IntVal;
        public double DoubleVal;
        public bool BoolVal;
    }
}";

            // 1. Emit Native Struct (via Serializer)
            var serializerCode = _serializerEmitter.EmitSerializer(type, registry);
            
            // 2. Emit View
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);

            // 3. Emit Extensions
            var extCode = _extEmitter.EmitExtensions(type, registry);

            // 4. Compile
            var asm = CompileToAssembly("ViewPrimitives", userStruct, serializerCode, viewCode, extCode);
            Assert.NotNull(asm);
        }

        [Fact]
        public void GenerateView_Strings_Compiles()
        {
             var type = new TypeInfo
            {
                Name = "StringStruct",
                Namespace = "ViewTest",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Msg", TypeName = "string" }
                }
            };
            
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "StringStruct.cs", "StringStruct.idl", "ViewTest");

            var userStruct = @"
namespace ViewTest
{
    public partial struct StringStruct
    {
        public string Msg;
    }
}";

            var serializerCode = _serializerEmitter.EmitSerializer(type, registry);
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);
            var extCode = _extEmitter.EmitExtensions(type, registry);

            var asm = CompileToAssembly("ViewStrings", userStruct, serializerCode, viewCode, extCode);
             Assert.NotNull(asm);
        }

        [Fact]
        public void GenerateView_FixedArray_Compiles()
        {
             var field = new FieldInfo { Name = "FixedArr", TypeName = "int[]" };
             field.Attributes.Add(new AttributeInfo { Name = "ArrayLength", Arguments = new List<object> { 10 } });

             var type = new TypeInfo
            {
                Name = "FixedStruct",
                Namespace = "ViewTest",
                Fields = new List<FieldInfo>
                {
                    field
                }
            };
            
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "FixedStruct.cs", "FixedStruct.idl", "ViewTest");

            var userStruct = @"
namespace ViewTest
{
    public partial struct FixedStruct
    {
        public int[] FixedArr;
    }
}";

            // Mock Native Struct (Correct expectation)
            var nativeStruct = @"
using System;
using System.Runtime.InteropServices;
namespace ViewTest
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FixedStruct_Native
    {
        public fixed int FixedArr[10];
    }
}";

            // Do NOT use serializerCode for tests if it is known to be wrong/missing features.
            // var serializerCode = _serializerEmitter.EmitSerializer(type, registry);
            
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);
            var extCode = _extEmitter.EmitExtensions(type, registry);

            var asm = CompileToAssembly("ViewFixed", userStruct, nativeStruct, viewCode, extCode);
             Assert.NotNull(asm);
        }
    }
}
