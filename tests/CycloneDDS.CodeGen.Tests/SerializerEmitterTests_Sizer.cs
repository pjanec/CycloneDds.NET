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
            
            Assert.Contains("public static int GetNativeSize(in Test source)", code);
            Assert.Contains("int size = Unsafe.SizeOf<Test_Native>();", code);
            // No Dynamic calls
            Assert.DoesNotContain("GetDynamicSize", code);
            Assert.Contains("return size;", code);
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
            
            Assert.Contains("size += GetDynamicSize(source);", code);
            Assert.Contains("DdsTextEncoding.GetUtf8Size(source.msg)", code);
            Assert.Contains("currentOffset = (currentOffset + 7) & ~7;", code); // Alignment check
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
            
            Assert.Contains("currentOffset = (currentOffset + 7) & ~7;", code);
            Assert.Contains("source.data.Count * Unsafe.SizeOf<double>()", code);
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
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("TestNs.Nested.GetNativeSize(source.child)", code);
            Assert.Contains("- Unsafe.SizeOf<TestNs.Nested_Native>()", code);
        }
    }
}
