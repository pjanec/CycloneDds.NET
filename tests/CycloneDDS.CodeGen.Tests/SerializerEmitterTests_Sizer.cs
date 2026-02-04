using System;
using System.Collections.Generic;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    public class SerializerEmitterTests_Sizer
    {
        [Fact]
        public void GetNativeSize_PrimitiveOnly()
        {
            var type = new TypeInfo
            {
                Name = "Test",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "id", TypeName = "int" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("public static unsafe int GetNativeSize(in Test source)", code);
        }

        [Fact]
        public void GetNativeSize_StringField()
        {
            var type = new TypeInfo
            {
                Name = "Test",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "msg", TypeName = "string" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("public static unsafe int GetNativeSize(in Test source)", code);
        }

        [Fact]
        public void GetNativeSize_SequenceField()
        {
             var type = new TypeInfo
            {
                Name = "Test",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "data", TypeName = "List<double>" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("public static unsafe int GetNativeSize(in Test source)", code);
        }
        
        [Fact]
        public void GetNativeSize_NestedStruct()
        {
             var nested = new TypeInfo { Name = "Nested", Namespace = "TestNs", IsStruct = true };
             var type = new TypeInfo
            {
                Name = "Test",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "child", TypeName = "Nested", Type = nested }
                }
            };
            
            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(nested, "dummy.idl", "dummy.idl", null);
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, registry);
            
            Assert.Contains("public static unsafe int GetNativeSize(in Test source)", code);
        }
    }
}
