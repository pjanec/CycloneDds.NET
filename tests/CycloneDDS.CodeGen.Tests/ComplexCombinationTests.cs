using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;

namespace CycloneDDS.CodeGen.Tests
{
    public class ComplexCombinationTests : CodeGenTestBase
    {
        [Fact]
        public void Struct_WithAllFeatures_RoundTrip()
        {
            var unionType = new TypeInfo
            {
                Name = "NestedUnion",
                Namespace = "TestNamespace",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "Val", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsCase", Arguments = new List<object>{1} } } }
                }
            };

            var type = new TypeInfo
            {
                Name = "AllFeatures",
                Namespace = "TestNamespace",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Name", TypeName = "string" },
                    new FieldInfo { Name = "OptValue", TypeName = "double?" },
                    new FieldInfo { Name = "Items", TypeName = "BoundedSeq<int>" },
                    new FieldInfo { Name = "Data", TypeName = "NestedUnion" }
                }
            };
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();
            
            string code = @"
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using CycloneDDS.Core;
using System.Collections.Generic;

namespace TestNamespace
{
    [DdsUnion]
    public partial struct NestedUnion
    {
        [DdsDiscriminator]
        public int D;
        [DdsCase(1)]
        public int Val;
    }

    public partial struct AllFeatures
    {
        public int Id;
        public string Name;
        public double? OptValue;
        public BoundedSeq<int> Items;
        public NestedUnion Data;
    }
}
";
            code += emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "AllFeatures");
                          
            var assembly = CompileToAssembly("ComplexTest1", code);
            var allFeaturesType = assembly.GetType("TestNamespace.AllFeatures");
            var nestedUnionType = assembly.GetType("TestNamespace.NestedUnion");
            
            var instance = Activator.CreateInstance(allFeaturesType);
            SetField(instance, "Id", 123);
            SetField(instance, "Name", "Tests");
            SetField(instance, "OptValue", 45.67);
            
            var seq = new BoundedSeq<int>(10);
            seq.Add(100);
            seq.Add(200);
            SetField(instance, "Items", seq);
            
            var unionInst = Activator.CreateInstance(nestedUnionType);
            SetField(unionInst, "D", 1);
            SetField(unionInst, "Val", 999);
            SetField(instance, "Data", unionInst);

            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Equal(123, GetField(result, "Id"));
            Assert.Equal("Tests", GetField(result, "Name"));
            Assert.Equal(45.67, GetField(result, "OptValue"));
            
            var resSeq = (BoundedSeq<int>)GetField(result, "Items");
            Assert.Equal(2, resSeq.Count);
            Assert.Equal(100, resSeq[0]);
            
            var resUnion = GetField(result, "Data");
            Assert.Equal(1, GetField(resUnion, "D"));
            Assert.Equal(999, GetField(resUnion, "Val"));
        }

        [Fact]
        public void NestedStructs_3Levels_RoundTrip()
        {
             // Level3 { int Value; }
             var lvl3 = new TypeInfo { Name = "Level3", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "Value", TypeName = "int" } } };
             // Level2 { Level3 Inner; string Name; }
             var lvl2 = new TypeInfo { Name = "Level2", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "Inner", TypeName = "Level3" }, new FieldInfo { Name = "Name", TypeName = "string" } } };
             // Level1 { Level2 Mid; int Id; }
             var lvl1 = new TypeInfo { Name = "Level1", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "Mid", TypeName = "Level2" }, new FieldInfo { Name = "Id", TypeName = "int" } } };

             var emitter = new SerializerEmitter();
             var demitter = new DeserializerEmitter();

             string code = @"
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using CycloneDDS.Core;

namespace TestNamespace
{
    public partial struct Level3 { public int Value; }
    public partial struct Level2 { public Level3 Inner; public string Name; }
    public partial struct Level1 { public Level2 Mid; public int Id; }
}
";
             code += emitter.EmitSerializer(lvl3, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(lvl3, new GlobalTypeRegistry(), false) + "\n" +
                     emitter.EmitSerializer(lvl2, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(lvl2, new GlobalTypeRegistry(), false) + "\n" +
                     emitter.EmitSerializer(lvl1, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(lvl1, new GlobalTypeRegistry(), false) + "\n" +
                     GenerateTestHelper("TestNamespace", "Level1");

             var assembly = CompileToAssembly("ComplexTest2", code);
             var t1 = assembly.GetType("TestNamespace.Level1");
             var t2 = assembly.GetType("TestNamespace.Level2");
             var t3 = assembly.GetType("TestNamespace.Level3");

             var i3 = Activator.CreateInstance(t3); SetField(i3, "Value", 88);
             var i2 = Activator.CreateInstance(t2); SetField(i2, "Inner", i3); SetField(i2, "Name", "L2");
             var i1 = Activator.CreateInstance(t1); SetField(i1, "Mid", i2); SetField(i1, "Id", 1);

             var helper = assembly.GetType("TestNamespace.TestHelper");
             var buffer = new System.Buffers.ArrayBufferWriter<byte>();
             helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { i1, buffer });
             var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });

             Assert.Equal(1, GetField(result, "Id"));
             var resMid = GetField(result, "Mid");
             Assert.Equal("L2", GetField(resMid, "Name"));
             var resInner = GetField(resMid, "Inner");
             Assert.Equal(88, GetField(resInner, "Value"));
        }

        [Fact]
        public void SequenceOfUnions_RoundTrip()
        {
            var unionType = new TypeInfo
            {
                Name = "MyUnion",
                Namespace = "TestNamespace",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "X", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsCase", Arguments = new List<object>{1} } } }
                }
            };
            
            var dataType = new TypeInfo
            {
                Name = "Data",
                Namespace = "TestNamespace",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Unions", TypeName = "BoundedSeq<MyUnion>" }
                }
            };

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using CycloneDDS.Core;

namespace TestNamespace
{
    [DdsUnion]
    public partial struct MyUnion
    {
        [DdsDiscriminator]
        public int D;
        [DdsCase(1)]
        public int X;
    }

    public partial struct Data
    {
        public BoundedSeq<MyUnion> Unions;
    }
}
";
            code += emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(dataType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(dataType, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "Data");

            var assembly = CompileToAssembly("ComplexTest3", code);
            var tData = assembly.GetType("TestNamespace.Data");
            var tUnion = assembly.GetType("TestNamespace.MyUnion");

            var instance = Activator.CreateInstance(tData);
            
            // For Sequence of Unions, we need BoundedSeq<MyUnion>.
            // BoundedSeq needs to be instantiated with Type MyUnion which is only in the assembly.
            // But CodeGenTestBase references CycloneDDS.Schema which has the GENERIC definition.
            // So we can make the generic type.
            var seqType = typeof(BoundedSeq<>).MakeGenericType(tUnion);
            var seq = Activator.CreateInstance(seqType, new object[] { 5 });
            
            var u1 = Activator.CreateInstance(tUnion); SetField(u1, "D", 1); SetField(u1, "X", 10);
            var u2 = Activator.CreateInstance(tUnion); SetField(u2, "D", 1); SetField(u2, "X", 20);

            // Add via reflection
            seqType.GetMethod("Add").Invoke(seq, new object[] { u1 });
            seqType.GetMethod("Add").Invoke(seq, new object[] { u2 });

            SetField(instance, "Unions", seq);

            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resSeq = GetField(result, "Unions");
            var count = (int)resSeq.GetType().GetProperty("Count").GetValue(resSeq);
            Assert.Equal(2, count);
            
            var itemProperty = resSeq.GetType().GetProperty("Item");
            var r1 = itemProperty.GetValue(resSeq, new object[] { 0 });
            var r2 = itemProperty.GetValue(resSeq, new object[] { 1 });
            
            Assert.Equal(10, GetField(r1, "X"));
            Assert.Equal(20, GetField(r2, "X"));
        }

        [Fact]
        public void OptionalNestedStruct_RoundTrip()
        {
            var inner = new TypeInfo { Name = "InnerStruct", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } } };
            var outer = new TypeInfo { Name = "Outer", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "OptInner", TypeName = "InnerStruct?" } } };

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  public partial struct InnerStruct { public int X; }
  public partial struct Outer { public InnerStruct? OptInner; }
}";
            code += emitter.EmitSerializer(inner, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(inner, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(outer, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(outer, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "Outer");

            var assembly = CompileToAssembly("ComplexTest4", code);
            var tOuter = assembly.GetType("TestNamespace.Outer");
            var tInner = assembly.GetType("TestNamespace.InnerStruct");

            var inst1 = Activator.CreateInstance(tOuter); // OptInner is null by default
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst1, buffer });
            var res1 = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.Null(GetField(res1, "OptInner"));

            var inst2 = Activator.CreateInstance(tOuter);
            var innerInst = Activator.CreateInstance(tInner); SetField(innerInst, "X", 88);
            SetField(inst2, "OptInner", innerInst);

            buffer.Clear();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst2, buffer });
            var res2 = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resInner = GetField(res2, "OptInner");
            Assert.NotNull(resInner);
            Assert.Equal(88, GetField(resInner, "X"));
        }

        [Fact]
        public void EmptyStruct_RoundTrip()
        {
            var empty = new TypeInfo { Name = "Empty", Namespace = "TestNamespace", Fields = new List<FieldInfo>() };

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  public partial struct Empty { }
}";
            code += emitter.EmitSerializer(empty, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(empty, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "Empty");
            
            var assembly = CompileToAssembly("ComplexTestEmpty", code);
            var tEmpty = assembly.GetType("TestNamespace.Empty");
            var inst = Activator.CreateInstance(tEmpty);
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var res = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            Assert.NotNull(res);
        }

        [Fact]
        public void MultipleSequentialOptionals_RoundTrip()
        {
            var multi = new TypeInfo { Name = "MultiOpt", Namespace = "TestNamespace", Fields = new List<FieldInfo> {
                new FieldInfo { Name = "Id", TypeName = "int" },
                new FieldInfo { Name = "Opt1", TypeName = "int?" },
                new FieldInfo { Name = "Opt2", TypeName = "double?" },
                new FieldInfo { Name = "Opt3", TypeName = "string?" },
                new FieldInfo { Name = "Opt4", TypeName = "int?" }
            }};

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  public partial struct MultiOpt {
       public int Id;
       // We use nullable value types for int? and double?
       public int? Opt1;
       public double? Opt2;
       public string Opt3; 
       public int? Opt4;
  }
}";
            code += emitter.EmitSerializer(multi, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(multi, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "MultiOpt");

            var assembly = CompileToAssembly("ComplexTestMultiOpt", code);
            var tMulti = assembly.GetType("TestNamespace.MultiOpt");
            var inst = Activator.CreateInstance(tMulti);
            SetField(inst, "Id", 123);
            SetField(inst, "Opt1", 10);
            SetField(inst, "Opt2", null);
            SetField(inst, "Opt3", "Hello");
            SetField(inst, "Opt4", null);

            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { inst, buffer });
            var res = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });

            Assert.Equal(123, GetField(res, "Id"));
            Assert.Equal(10, GetField(res, "Opt1"));
            Assert.Null(GetField(res, "Opt2"));
            Assert.Equal("Hello", GetField(res, "Opt3"));
            Assert.Null(GetField(res, "Opt4"));
        }

        [Fact]
        public void StringArrayInUnion_RoundTrip()
        {
             var unionType = new TypeInfo
            {
                Name = "DataUnion",
                Namespace = "TestNamespace",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } } },
                    new FieldInfo { Name = "Strings", TypeName = "BoundedSeq<string>", 
                        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsCase", Arguments = new List<object>{1} } } }
                }
            };
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  [DdsUnion]
  public partial struct DataUnion {
      [DdsDiscriminator] public int D;
      [DdsCase(1)] public BoundedSeq<string> Strings;
  }
}";
            code += emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "DataUnion");

            var assembly = CompileToAssembly("ComplexTestStrUnion", code);
            var tUnion = assembly.GetType("TestNamespace.DataUnion");
            var instance = Activator.CreateInstance(tUnion);
            SetField(instance, "D", 1);
            
            var seq = new BoundedSeq<string>(10);
            seq.Add("Hello");
            seq.Add("World");
            SetField(instance, "Strings", seq);
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resStrings = (BoundedSeq<string>)GetField(result, "Strings");
            Assert.Equal(2, resStrings.Count);
            Assert.Equal("Hello", resStrings[0]);
        }

        [Fact]
        public void UnionWithOptionalMembers_RoundTrip()
        {
            var optStruct = new TypeInfo { Name = "OptionalStruct", Namespace = "TestNamespace", Fields = new List<FieldInfo> { new FieldInfo { Name = "A", TypeName = "int?" } } };
            
            var unionType = new TypeInfo
            {
                 Name = "DataUnionOpt",
                 Namespace = "TestNamespace",
                 Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                 Fields = new List<FieldInfo> {
                      new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name="DdsDiscriminator"} } },
                      new FieldInfo { Name = "ValueA", TypeName = "OptionalStruct", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name="DdsCase", Arguments=new List<object>{1} } } }
                 }
            };

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  public partial struct OptionalStruct { public int? A; }
  [DdsUnion]
  public partial struct DataUnionOpt {
      [DdsDiscriminator] public int D;
      [DdsCase(1)] public OptionalStruct ValueA;
  }
}";
            code += emitter.EmitSerializer(optStruct, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(optStruct, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(unionType, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(unionType, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "DataUnionOpt");

            var assembly = CompileToAssembly("ComplexTestUnionOpt", code);
            var tUnion = assembly.GetType("TestNamespace.DataUnionOpt");
            var tOpt = assembly.GetType("TestNamespace.OptionalStruct");
            
            var instance = Activator.CreateInstance(tUnion);
            SetField(instance, "D", 1);
            var optInst = Activator.CreateInstance(tOpt);
            SetField(optInst, "A", 99);
            SetField(instance, "ValueA", optInst);
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resOpt = GetField(result, "ValueA");
            Assert.Equal(99, GetField(resOpt, "A"));
        }

        [Fact]
        public void LargeStruct_RoundTrip()
        {
             var fields = new List<FieldInfo>();
             var sb = new System.Text.StringBuilder();
             sb.Append("public partial struct LargeStruct { ");
             for(int i=0; i<120; i++) {
                 fields.Add(new FieldInfo { Name = $"F{i}", TypeName = "int" });
                 sb.Append($"public int F{i}; ");
             }
             sb.Append("}");

             var type = new TypeInfo { Name = "LargeStruct", Namespace = "TestNamespace", Fields = fields };
             
             var emitter = new SerializerEmitter();
             var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
" + sb.ToString() + @"
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "LargeStruct");

            var assembly = CompileToAssembly("ComplexTestLarge", code);
            var tLarge = assembly.GetType("TestNamespace.LargeStruct");
            var instance = Activator.CreateInstance(tLarge);
            for(int i=0; i<120; i++) {
                SetField(instance, $"F{i}", i);
            }
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            for(int i=0; i<120; i++) {
                Assert.Equal(i, GetField(result, $"F{i}"));
            }
        }

        [Fact]
        public void SequenceOfSequences_RoundTrip()
        {
            // InnerRow { BoundedSeq<int> Cols; }
            var inner = new TypeInfo { 
                Name = "InnerRow", 
                Namespace = "TestNamespace", 
                Fields = new List<FieldInfo> { 
                    new FieldInfo { Name = "Cols", TypeName = "BoundedSeq<int>" } 
                } 
            };
            // Matrix { BoundedSeq<InnerRow> Rows; }
            var matrix = new TypeInfo {
                Name = "Matrix",
                Namespace = "TestNamespace",
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "Rows", TypeName = "BoundedSeq<InnerRow>" }
                }
            };
             
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace TestNamespace {
  public partial struct InnerRow { public BoundedSeq<int> Cols; }
  public partial struct Matrix { public BoundedSeq<InnerRow> Rows; }
}";
            code += emitter.EmitSerializer(inner, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(inner, new GlobalTypeRegistry(), false) + "\n" +
                    emitter.EmitSerializer(matrix, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(matrix, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("TestNamespace", "Matrix");

            var assembly = CompileToAssembly("ComplexTestMatrix", code);
            var tMatrix = assembly.GetType("TestNamespace.Matrix");
            var tRow = assembly.GetType("TestNamespace.InnerRow");
            
            var instance = Activator.CreateInstance(tMatrix);
            
            // Create Rows
            var rowsSeqType = typeof(BoundedSeq<>).MakeGenericType(tRow);
            var rows = Activator.CreateInstance(rowsSeqType, new object[] { 3 });

            // Row 1: [1, 2]
            var r1 = Activator.CreateInstance(tRow);
            var seqIntType = typeof(BoundedSeq<int>); 
            var c1 = new BoundedSeq<int>(5); c1.Add(1); c1.Add(2);
            SetField(r1, "Cols", c1);
            rowsSeqType.GetMethod("Add").Invoke(rows, new object[]{ r1 });

            // Row 2: [3, 4, 5]
            var r2 = Activator.CreateInstance(tRow);
            var c2 = new BoundedSeq<int>(5); c2.Add(3); c2.Add(4); c2.Add(5);
            SetField(r2, "Cols", c2);
            rowsSeqType.GetMethod("Add").Invoke(rows, new object[]{ r2 });

            SetField(instance, "Rows", rows);
            
            var helper = assembly.GetType("TestNamespace.TestHelper");
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            helper.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, buffer });
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            
            var resRows = GetField(result, "Rows");
            Assert.Equal(2, rowsSeqType.GetProperty("Count").GetValue(resRows));
            
            var itemProp = rowsSeqType.GetProperty("Item");
            var rr1 = itemProp.GetValue(resRows, new object[]{0});
            var cc1 = (BoundedSeq<int>)GetField(rr1, "Cols");
            Assert.Equal(1, cc1[0]);
            Assert.Equal(2, cc1[1]);
        }
    }
}

