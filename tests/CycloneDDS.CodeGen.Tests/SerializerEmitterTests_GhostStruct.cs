using System;
using System.Collections.Generic;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    public class SerializerEmitterTests_GhostStruct
    {
        [Fact]
        public void EmitGhostStruct_PrimitiveStruct()
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
            
            Assert.Contains("public unsafe partial struct Test_Native", code);
            Assert.Contains("public int id;", code);
        }

        [Fact]
        public void EmitGhostStruct_HasLayoutAttribute()
        {
            var type = new TypeInfo { Name = "Test", Namespace = "TestNs" };
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("[StructLayout(LayoutKind.Sequential)]", code);
        }

        [Fact]
        public void EmitGhostStruct_StringField()
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
            
            Assert.Contains("public IntPtr msg;", code);
        }
        
        [Fact]
        public void EmitGhostStruct_SequenceField()
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
            
            Assert.Contains("public DdsSequenceNative data;", code);
        }

        [Fact]
        public void EmitGhostStruct_BooleanUsesBytes()
        {
             var type = new TypeInfo
            {
                Name = "Test",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "flag", TypeName = "bool" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("public byte flag;", code);
        }

        [Fact]
        public void EmitGhostStruct_NestedStruct()
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
            
            Assert.Contains("public TestNs.Nested_Native child;", code);
        }
        
        [Fact]
        public void EmitGhostStruct_UnionHasDiscriminator()
        {
             var type = new TypeInfo
            {
                Name = "TestUnion",
                Namespace = "TestNs",
                IsUnion = true,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "_d", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "part", TypeName = "int" }
                }
            };
            // Manually add attribute as it's parsed from IDL usually
            type.Attributes.Add(new AttributeInfo { Name = "DdsUnion" });
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("public int _d;", code);
            Assert.Contains("public TestUnion_Union_Native _u;", code);
        }

        [Fact]
        public void EmitGhostStruct_UnionFieldsOverlap()
        {
             var type = new TypeInfo
            {
                Name = "TestUnion",
                Namespace = "TestNs",
                IsUnion = true,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "_d", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "part1", TypeName = "int" },
                    new FieldInfo { Name = "part2", TypeName = "double" }
                }
            };
            type.Attributes.Add(new AttributeInfo { Name = "DdsUnion" });
            
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            Assert.Contains("[StructLayout(LayoutKind.Explicit)]", code);
            Assert.Contains("public unsafe partial struct TestUnion_Union_Native", code);
            Assert.Contains("[FieldOffset(0)]", code);
            Assert.Contains("public int part1;", code);
            Assert.Contains("public double part2;", code);
        }
    }
}
