using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;

namespace CycloneDDS.CodeGen.Tests
{
    public class EdgeCaseTests : CodeGenTestBase
    {
        [Fact]
        public void EmptyString_RoundTrip()
        {
            var type = new TypeInfo { Name = "StrStruct", Namespace = "EdgeCase", Fields = new List<FieldInfo> { new FieldInfo { Name = "S", TypeName = "string" } } };
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
  public partial struct StrStruct { public string S; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "StrStruct");

            var assembly = CompileToAssembly("EdgeCaseString", code);
            var t = assembly.GetType("EdgeCase.StrStruct");
            var inst = Activator.CreateInstance(t);
            SetField(inst, "S", "");
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });

            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Equal("", GetField(result, "S"));
        }

        [Fact]
        public void NullOptional_RoundTrip()
        {
            var type = new TypeInfo { Name = "AllNull", Namespace = "EdgeCase", Fields = new List<FieldInfo> {
                new FieldInfo { Name = "Opt1", TypeName = "int?" },
                new FieldInfo { Name = "Opt2", TypeName = "string?" }
             }};
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
  public partial struct AllNull { public int? Opt1; public string Opt2; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "AllNull");

            var assembly = CompileToAssembly("EdgeCaseNull", code);
            var t = assembly.GetType("EdgeCase.AllNull");
            var inst = Activator.CreateInstance(t); // defaults are null
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Null(GetField(result, "Opt1"));
            Assert.Null(GetField(result, "Opt2"));
        }

        [Fact]
        public void MaxSequenceSize_RoundTrip()
        {
            int max = 1000;
            var type = new TypeInfo { Name = "MaxSeq", Namespace = "EdgeCase", Fields = new List<FieldInfo> {
                new FieldInfo { Name = "S", TypeName = "BoundedSeq<int>" }
             }};
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
  public partial struct MaxSeq { public BoundedSeq<int> S; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "MaxSeq");

            var assembly = CompileToAssembly("EdgeCaseMaxSeq", code);
            var t = assembly.GetType("EdgeCase.MaxSeq");
            var inst = Activator.CreateInstance(t);
            
            var seq = new BoundedSeq<int>(max);
            for(int i=0; i<max; i++) seq.Add(i);
            SetField(inst, "S", seq);
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resS = (BoundedSeq<int>)GetField(result, "S");
            Assert.Equal(max, resS.Count);
            Assert.Equal(999, resS[999]);
        }

        [Fact]
        public void DeeplyNestedStruct_RoundTrip()
        {
            // Level 10 -> Level 9 -> ... -> Level 0
            // Generates code for all
            string structs = "";
            string typesEmit = "";
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            TypeInfo prev = null;
            for(int i=0; i<=10; i++) 
            {
                 var name = $"L{i}";
                 var fields = new List<FieldInfo>();
                 structs += $"public partial struct {name} {{ ";
                 if (i==0) {
                     fields.Add(new FieldInfo { Name = "Val", TypeName = "int" });
                     structs += "public int Val; ";
                 } else {
                     fields.Add(new FieldInfo { Name = "Inner", TypeName = $"L{i-1}" });
                     structs += $"public L{i-1} Inner; ";
                 }
                 structs += "} \n";
                 
                 var t = new TypeInfo { Name = name, Namespace = "EdgeCase", Fields = fields };
                 typesEmit += emitter.EmitSerializer(t, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(t, new GlobalTypeRegistry(), false) + "\n";
            }
            
            string code = $@"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {{
  {structs}
}}";
            code += typesEmit;
            code += GenerateTestHelper("EdgeCase", "L10");

            var assembly = CompileToAssembly("EdgeCaseDeep", code);
            var tL10 = assembly.GetType("EdgeCase.L10");
            
            object current = Activator.CreateInstance(assembly.GetType("EdgeCase.L0"));
            SetField(current, "Val", 1337);
            
            for(int i=1; i<=10; i++) {
                var next = Activator.CreateInstance(assembly.GetType($"EdgeCase.L{i}"));
                SetField(next, "Inner", current);
                current = next;
            }
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { current, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            // Verify
            object ptr = result;
            for(int i=10; i>0; i--) {
                ptr = GetField(ptr, "Inner");
            }
            Assert.Equal(1337, GetField(ptr, "Val"));
        }

        [Fact]
        public void UnionWithDefaultCase_UnknownDiscriminator()
        {
            var unionType = new TypeInfo
            {
                Name = "UDefault",
                Namespace = "EdgeCase",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "X", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsCase", Arguments = new List<object>{1} } } },
                    new FieldInfo { Name = "Def", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDefaultCase" } } }
                }
            };
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
  [DdsUnion]
  public partial struct UDefault {
    [DdsDiscriminator] public int D;
    [DdsCase(1)] public int X;
    [DdsDefaultCase] public int Def;
  }
}";
            code += emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "UDefault");

            var assembly = CompileToAssembly("EdgeCaseUDef", code);
            var t = assembly.GetType("EdgeCase.UDefault");
            var inst = Activator.CreateInstance(t);
            SetField(inst, "D", 5); // 5 not in cases, selects Default logic?
            SetField(inst, "Def", 12345);
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });

            // Verify result
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            Assert.Equal(5, GetField(result, "D"));
            Assert.Equal(12345, GetField(result, "Def"));
        }

        [Fact]
        public void OptionalUnion_RoundTrip()
        {
             var unionType = new TypeInfo
            {
                Name = "USimple",
                Namespace = "EdgeCase",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "X", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsCase", Arguments = new List<object>{1} } } }
                }
            };

            var structType = new TypeInfo
            {
                Name = "SOptU",
                Namespace = "EdgeCase",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "OptU", TypeName = "USimple?" }
                }
            };

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
  [DdsUnion] public partial struct USimple { [DdsDiscriminator] public int D; [DdsCase(1)] public int X; }
  public partial struct SOptU { public USimple? OptU; }
}";
            code += emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(structType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(structType, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "SOptU");

            var assembly = CompileToAssembly("EdgeCaseOptU", code);
            var tStruct = assembly.GetType("EdgeCase.SOptU");
            var tUnion = assembly.GetType("EdgeCase.USimple");
            
            var inst = Activator.CreateInstance(tStruct);
            SetField(inst, "OptU", null); // Test null
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            Assert.Null(GetField(result, "OptU"));

            var u = Activator.CreateInstance(tUnion);
            SetField(u, "D", 1); SetField(u, "X", 88);
            var inst2 = Activator.CreateInstance(tStruct);
            SetField(inst2, "OptU", u);
            
            buffer.Clear();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst2, buffer });
            var result2 = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resU = GetField(result2, "OptU");
            Assert.NotNull(resU);
            Assert.Equal(88, GetField(resU, "X"));
        }

        [Fact]
        public void ZeroValuePrimitives_RoundTrip()
        {
             var type = new TypeInfo { Name = "Zeros", Namespace = "EdgeCase", Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "I", TypeName = "int" },
                 new FieldInfo { Name = "D", TypeName = "double" },
                 new FieldInfo { Name = "B", TypeName = "byte" },
                 new FieldInfo { Name = "S", TypeName = "short" },
                 new FieldInfo { Name = "L", TypeName = "long" }
             }};
             
             var emitter = new SerializerEmitter();
             var demitter = new DeserializerEmitter();

             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
   public partial struct Zeros { public int I; public double D; public byte B; public short S; public long L; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "Zeros");

            var assembly = CompileToAssembly("EdgeCaseZeros", code);
            var t = assembly.GetType("EdgeCase.Zeros");
            var inst = Activator.CreateInstance(t); // All 0 by default
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Equal(0, GetField(result, "I"));
            Assert.Equal(0.0, GetField(result, "D"));
            Assert.Equal((byte)0, GetField(result, "B"));
            Assert.Equal((short)0, GetField(result, "S"));
            Assert.Equal(0L, GetField(result, "L"));
        }

        [Fact]
        public void UnicodeString_RoundTrip()
        {
             var type = new TypeInfo { Name = "Unicode", Namespace = "EdgeCase", Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "S", TypeName = "string" }
             }};
             var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace EdgeCase {
   public partial struct Unicode { public string S; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("EdgeCase", "Unicode");

            var assembly = CompileToAssembly("EdgeCaseUnicode", code);
            var t = assembly.GetType("EdgeCase.Unicode");
            var inst = Activator.CreateInstance(t);
            string val = "Hello 世界 🌍";
            SetField(inst, "S", val);
            
            var helper = assembly.GetType("EdgeCase.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Equal(val, GetField(result, "S"));
        }
    }
}

