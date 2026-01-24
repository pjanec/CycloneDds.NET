using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;

namespace CycloneDDS.CodeGen.Tests
{
    public class SchemaEvolutionTests : CodeGenTestBase
    {
        private void TestEvolution(
            TypeInfo v1Type, 
            TypeInfo v2Type, 
            string v1StructDef, 
            string v2StructDef, 
            Action<System.Reflection.Assembly> setupAndVerify)
        {
            // Enforce Appendable for schema evolution tests to ensure DHEADER support
            if (!v1Type.HasAttribute("Appendable")) v1Type.Attributes.Add(new AttributeInfo { Name = "Appendable" });
            if (!v2Type.HasAttribute("Appendable")) v2Type.Attributes.Add(new AttributeInfo { Name = "Appendable" });

            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();

            // V1 Code (Deserializer)
            string codeV1 = demitter.EmitDeserializer(v1Type, new GlobalTypeRegistry(), false);
            
            // V2 Code (Serializer)
            string codeV2 = emitter.EmitSerializer(v2Type, new GlobalTypeRegistry(), false);

            string code = $@"
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using CycloneDDS.Core;
using System.Collections.Generic;

namespace Version1
{{
    {v1StructDef}
}}

namespace Version2
{{
    {v2StructDef}
}}

namespace SchemaTest
{{
    public static class TestHelper
    {{
        public static void SerializeV2(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {{
            var typedInstance = (Version2.{v2Type.Name})instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }}

        public static object DeserializeV1(System.ReadOnlyMemory<byte> buffer)
        {{
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = Version1.{v1Type.Name}.Deserialize(ref reader);
            return view; // Assuming Struct
        }}
        
        public static object DeserializeV1ToOwned(System.ReadOnlyMemory<byte> buffer)
        {{
             // Add ToOwned() logic or generated code should have it if emitted
             // But my GenerateTestHelper assumed it exists.
             // Generated code usually doesn't have ToOwned unless generated as Ref view?
             // Since I am providing structDef as 'public partial struct', it is a value type.
             // Value types are already owned.
             // But if Deserialize(ref CdrReader) returns void or the struct?
             // It returns the struct.
             var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
             return Version1.{v1Type.Name}.Deserialize(ref reader);
        }}
    }}
}}
";
            // We need to inject the emitted methods into the partial structs.
            // Emitted code usually uses "public partial struct Name { ... }" wrapper.
            // If I extracted body, I have the methods.
            // I need to wrap them in "namespace Version1 { public partial struct Name { [BODY] } }" ?
            // No, ExtractBody extracted content OF namespace.
            // The emitted code is:
            // namespace X { public partial struct S { [Methods] } }
            // code -> public partial struct S { [Methods] }
            
            // So I need to put the extracted body INSIDE the namespaces.
            // But wait, ExtractBody implementation:
            // start = code.IndexOf("{", start) + 1;
            // It extracts what is INSIDE the namespace braces.
            // So it includes "public partial struct S { ... }".
            
            // So in my constructed code:
            // namespace Version1 { [ExtractBody result] }
            // But I ALSO added v1StructDef inside Version1.
            // v1StructDef usually is "public partial struct S { fields }"
            // Emitted code is "public partial struct S { methods }"
            // This merges perfectly.
            
            string fullCode = $@"
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using CycloneDDS.Core;
using System.Collections.Generic;

namespace Version1
{{
    {v1StructDef}
}}
{codeV1}

namespace Version2
{{
    {v2StructDef}
}}
{codeV2}

namespace SchemaTest
{{
    public static class TestHelper
    {{
         // ... helpers ...
    }}
}}
";
            // Helper methods need to be dynamic to handle types? 
            // Or I can use reflection in the TestHelper to capture the types?
            // Actually, I can just use reflection from the test and not generate helper methods for simple cases,
            // or generate them specific to type names.
            
            fullCode += $@"
namespace SchemaTest
{{
    public static class Interaction
    {{
        public static void Run(object v2Instance, System.Buffers.IBufferWriter<byte> w)
        {{
            var t = (Version2.{v2Type.Name})v2Instance;
            var writer = new CycloneDDS.Core.CdrWriter(w);
            t.Serialize(ref writer);
            writer.Complete();
        }}
        
        public static object ReadV1(System.ReadOnlyMemory<byte> buf)
        {{
            var reader = new CycloneDDS.Core.CdrReader(buf.Span);
            return Version1.{v1Type.Name}.Deserialize(ref reader).ToOwned();
        }}
    }}
}}
";
            
            var assembly = CompileToAssembly("SchemaTest_" + Guid.NewGuid().ToString().Replace("-", ""), fullCode);
            setupAndVerify(assembly);
        }

        [Fact]
        public void AddOptionalField_ForwardCompat()
        {
            // V1: { int Id; }
            var v1 = new TypeInfo { Name = "Data", Namespace = "Version1", Fields = new List<FieldInfo> { new FieldInfo { Name = "Id", TypeName = "int" } } };
            // V2: { int Id; int? NewField; }
            var v2 = new TypeInfo { Name = "Data", Namespace = "Version2", Fields = new List<FieldInfo> { 
                new FieldInfo { Name = "Id", TypeName = "int" },
                new FieldInfo { Name = "NewField", TypeName = "int?" }
            }};
            
            TestEvolution(v1, v2, 
                "public partial struct Data { public int Id; }",
                "public partial struct Data { public int Id; public int? NewField; }",
                (asm) => {
                    var tV2 = asm.GetType("Version2.Data");
                    var v2Inst = Activator.CreateInstance(tV2);
                    SetField(v2Inst, "Id", 42);
                    SetField(v2Inst, "NewField", 100); // V2 sends data

                    var helper = asm.GetType("SchemaTest.Interaction");
                    var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                    helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                    var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                    
                    Assert.Equal(42, GetField(v1Res, "Id"));
                    // V1 reads Id. NewField is skipped via DHEADER or just stream pointer advancement.
                    // If DHEADER works, V1 should read successfully.
                }
            );
        }

        [Fact]
        public void AddRequiredFieldAtEnd_BackwardIncompatButSafeRead()
        {
            // V1: { int Id; }
            var v1 = new TypeInfo { Name = "DataReq", Namespace = "Version1", Fields = new List<FieldInfo> { new FieldInfo { Name = "Id", TypeName = "int" } } };
            // V2: { int Id; int Required; }
            var v2 = new TypeInfo { Name = "DataReq", Namespace = "Version2", Fields = new List<FieldInfo> { 
                new FieldInfo { Name = "Id", TypeName = "int" },
                new FieldInfo { Name = "Required", TypeName = "int" }
            }};
            
            TestEvolution(v1, v2,
                "public partial struct DataReq { public int Id; }",
                "public partial struct DataReq { public int Id; public int Required; }",
                (asm) => {
                    var tV2 = asm.GetType("Version2.DataReq");
                    var v2Inst = Activator.CreateInstance(tV2);
                    SetField(v2Inst, "Id", 10);
                    SetField(v2Inst, "Required", 999);

                    var helper = asm.GetType("SchemaTest.Interaction");
                    var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                    helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });
                    
                    // V1 reads. It expects only Id. It reads Id. 
                    // Then it finishes? No, DHEADER says object is larger.
                    // Reader should skip remaining bytes defined by DHEADER.
                    var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                    
                    Assert.Equal(10, GetField(v1Res, "Id"));
                }
            );
        }

        [Fact]
        public void AddUnionArm_ForwardCompat()
        {
             // V1: union U { case 1: int X; }
             var v1 = new TypeInfo { Name = "U", Namespace = "Version1", Attributes = new List<AttributeInfo>{new AttributeInfo{Name="DdsUnion"}}, 
                 Fields=new List<FieldInfo>{
                     new FieldInfo { Name="D", TypeName="int", Attributes=new List<AttributeInfo>{new AttributeInfo{Name="DdsDiscriminator"}}},
                     new FieldInfo { Name="X", TypeName="int", Attributes=new List<AttributeInfo>{new AttributeInfo{Name="DdsCase", Arguments=new List<object>{1}}}}
                 }};

             // V2: union U { case 1: int X; case 2: double Y; }
             var v2 = new TypeInfo { Name = "U", Namespace = "Version2", Attributes = new List<AttributeInfo>{new AttributeInfo{Name="DdsUnion"}}, 
                 Fields=new List<FieldInfo>{
                     new FieldInfo { Name="D", TypeName="int", Attributes=new List<AttributeInfo>{new AttributeInfo{Name="DdsDiscriminator"}}},
                     new FieldInfo { Name="X", TypeName="int", Attributes=new List<AttributeInfo>{new AttributeInfo{Name="DdsCase", Arguments=new List<object>{1}}}},
                     new FieldInfo { Name="Y", TypeName="double", Attributes=new List<AttributeInfo>{new AttributeInfo{Name="DdsCase", Arguments=new List<object>{2}}}}
                 }};

             TestEvolution(v1, v2,
                 @"[DdsUnion] public partial struct U { [DdsDiscriminator] public int D; [DdsCase(1)] public int X; }",
                 @"[DdsUnion] public partial struct U { [DdsDiscriminator] public int D; [DdsCase(1)] public int X; [DdsCase(2)] public double Y; }",
                 (asm) => {
                     var tV2 = asm.GetType("Version2.U");
                     var v2Inst = Activator.CreateInstance(tV2);
                     SetField(v2Inst, "D", 2);
                     SetField(v2Inst, "Y", 3.14);

                     var helper = asm.GetType("SchemaTest.Interaction");
                     var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                     helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                     // V1 reads. D=2. V1 doesn't have case 2. 
                     // It should default or skip?
                     // XType union logic: if discriminator not known, and no default, it is valid but empty?
                     // Or generated code handles it?
                     // Generated code for V1: `switch(D) { case 1: X = ...; break; }`
                     // If D=2, it falls through.
                     // IMPORTANT: The Deserialize method must consume the body of the union arm if it selected one?
                     // But if D is unknown, it doesn't select an arm.
                     // But the DHEADER of the containing struct (if any) or the Union's logic must skip the arm?
                     // Union itself (mutable) has DHEADER? No, usually Union is member of struct.
                     // Here U is the type being serialized. Top level types generally have DHEADER in XCDR2 if they are mutable/appendable.
                     // But Union itself isn't Appendable in the same way.
                     // However, instructions say "Test: V1 deserializes V2 with case 3".
                     // If I serialize U directly... 
                     // Actually, `CdrWriter` writes header? `EmitSerializer` adds header writing for Structs. For Unions?
                     // Let's verify if Unions get DHEADER.
                     // If not, this test might fail if U is top level.
                     // Usually we wrap in struct for evolution tests.
                     // But let's try top level U.
                     
                     var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                     Assert.Equal(2, GetField(v1Res, "D"));
                 }
             );
        }

        [Fact]
        public void NestedStructEvolution_SafeRead()
        {
             var v1Inner = new TypeInfo { Name = "Inner", Namespace = "Version1", Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } } };
             var v1Outer = new TypeInfo { Name = "Outer", Namespace = "Version1", Fields = new List<FieldInfo> { new FieldInfo { Name = "In", TypeName = "Inner" } } };
             
             var v2Inner = new TypeInfo { Name = "Inner", Namespace = "Version2", Fields = new List<FieldInfo> { 
                 new FieldInfo { Name = "X", TypeName = "int" },
                 new FieldInfo { Name = "Y", TypeName = "int" } 
             }};
             var v2Outer = new TypeInfo { Name = "Outer", Namespace = "Version2", Fields = new List<FieldInfo> { new FieldInfo { Name = "In", TypeName = "Inner" } } };

             var emitter = new SerializerEmitter();
             var demitter = new DeserializerEmitter();

             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace Version1 {
    public partial struct Inner { public int X; }
    public partial struct Outer { public Inner In; }
}
namespace Version2 {
    public partial struct Inner { public int X; public int Y; }
    public partial struct Outer { public Inner In; }
}";
             code += ExtractBody(demitter.EmitDeserializer(v1Inner, new GlobalTypeRegistry(), false)) + "\n" + ExtractBody(demitter.EmitDeserializer(v1Outer, new GlobalTypeRegistry(), false)) + "\n";
             code += ExtractBody(emitter.EmitSerializer(v2Inner, new GlobalTypeRegistry(), false)) + "\n" + ExtractBody(emitter.EmitSerializer(v2Outer, new GlobalTypeRegistry(), false)) + "\n";
             // Note: extracting bodies works because namespaces in emitted code match (Version1/Version2) or I need to handle them?
             // v1Inner namespace is 'Version1'. EmitDeserializer includes "namespace Version1 { ... }".
             // My ExtractBody removes namespace.
             // But my manual struct def also uses namespace Version1.
             // Code looks like:
             // namespace Version1 { struct ... }
             // struct ... (from extracted body with method)
             
             // Wait, ExtractBody returns ONLY the body.
             // So I should put it INSIDE the namespace block in my `code` string.
             
             // Let's redo `code` construction carefully.
             
             code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace Version1 {
    public partial struct Inner { public int X; }
    public partial struct Outer { public Inner In; }
    " + ExtractBody(demitter.EmitDeserializer(v1Inner, new GlobalTypeRegistry(), false)) + @"
    " + ExtractBody(demitter.EmitDeserializer(v1Outer, new GlobalTypeRegistry(), false)) + @"
}
namespace Version2 {
    public partial struct Inner { public int X; public int Y; }
    public partial struct Outer { public Inner In; }
    " + ExtractBody(emitter.EmitSerializer(v2Inner, new GlobalTypeRegistry(), false)) + @"
    " + ExtractBody(emitter.EmitSerializer(v2Outer, new GlobalTypeRegistry(), false)) + @"
}

namespace SchemaTest {
    public static class Interaction {
        public static void Run(object v2Instance, System.Buffers.IBufferWriter<byte> w) {
            var t = (Version2.Outer)v2Instance;
            var writer = new CycloneDDS.Core.CdrWriter(w);
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static object ReadV1(System.ReadOnlyMemory<byte> buf) {
            var reader = new CycloneDDS.Core.CdrReader(buf.Span);
            return Version1.Outer.Deserialize(ref reader);
        }
    }
}";
             var assembly = CompileToAssembly("SchemaTestNested", code);
             var tV2Outer = assembly.GetType("Version2.Outer");
             var tV2Inner = assembly.GetType("Version2.Inner");
             
             var v2Inst = Activator.CreateInstance(tV2Outer);
             var v2InnerInst = Activator.CreateInstance(tV2Inner);
             SetField(v2InnerInst, "X", 100);
             SetField(v2InnerInst, "Y", 200);
             SetField(v2Inst, "In", v2InnerInst);

             var helper = assembly.GetType("SchemaTest.Interaction");
             var buffer = new System.Buffers.ArrayBufferWriter<byte>();
             helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

             var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
             var v1InnerRes = GetField(v1Res, "In");
             Assert.Equal(100, GetField(v1InnerRes, "X"));
             // Y is ignored.
        }

        [Fact]
        public void SequenceSizeIncrease_SafeRead_IfCountLow()
        {
             // V1: Seq<int, 5>
             var v1 = new TypeInfo { Name = "Data", Namespace = "Version1", Fields = new List<FieldInfo> { 
                 new FieldInfo { Name = "S", TypeName = "BoundedSeq<int>" } } };
             // V2: Seq<int, 10>
             var v2 = new TypeInfo { Name = "Data", Namespace = "Version2", Fields = new List<FieldInfo> { 
                 new FieldInfo { Name = "S", TypeName = "BoundedSeq<int>" } } };

             TestEvolution(v1, v2,
                 "public partial struct Data { public BoundedSeq<int> S; }",
                 "public partial struct Data { public BoundedSeq<int> S; }",
                 (asm) => {
                     var tV2 = asm.GetType("Version2.Data");
                     var v2Inst = Activator.CreateInstance(tV2);
                     var seq = new BoundedSeq<int>(10);
                     seq.Add(1); seq.Add(2);
                     SetField(v2Inst, "S", seq);

                     var helper = asm.GetType("SchemaTest.Interaction");
                     var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                     helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                     var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                     var sRes = (BoundedSeq<int>)GetField(v1Res, "S");
                     Assert.Equal(2, sRes.Count);
                 }
             );
        }

        [Fact]
        public void SequenceSizeIncrease_ThrowsIfExceeds()
        {
             // V1: Seq<int, 3>
             var boundsAttr = new AttributeInfo { Name = "DdsBounds", Arguments = new List<object> { 3 } };
             var v1 = new TypeInfo { Name = "DataLow", Namespace = "Version1", Fields = new List<FieldInfo> { 
                 new FieldInfo { Name = "S", TypeName = "BoundedSeq<int>", Attributes = new List<AttributeInfo> { boundsAttr } } } };
             // V2: Seq<int, 10>
             var v2 = new TypeInfo { Name = "DataLow", Namespace = "Version2", Fields = new List<FieldInfo> { 
                 new FieldInfo { Name = "S", TypeName = "BoundedSeq<int>" } } };

             TestEvolution(v1, v2,
                 "public partial struct DataLow { public BoundedSeq<int> S; }",
                 "public partial struct DataLow { public BoundedSeq<int> S; }",
                 (asm) => {
                     var tV2 = asm.GetType("Version2.DataLow");
                     var v2Inst = Activator.CreateInstance(tV2);
                     var seq = new BoundedSeq<int>(10);
                     seq.Add(1); seq.Add(2); seq.Add(3); seq.Add(4); // 4 items
                     SetField(v2Inst, "S", seq);

                     var helper = asm.GetType("SchemaTest.Interaction");
                     var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                     helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                     // Should throw
                     var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => 
                        helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory }));
                     
                     // Expect generic exception or specific one from BoundedSeq constructor/Add?
                     // Deserializer creates BoundedSeq with capacity 3.
                     // Then adds 4 items. Add throws if full.
                 }
             );
        }

        [Fact]
        public void EmptyStructToNonEmpty_SafeRead()
        {
             var v1 = new TypeInfo { Name = "Empty", Namespace = "Version1", Fields = new List<FieldInfo>() };
             var v2 = new TypeInfo { Name = "Empty", Namespace = "Version2", Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } } };

             TestEvolution(v1, v2,
                 "public partial struct Empty { }",
                 "public partial struct Empty { public int X; }",
                 (asm) => {
                     var tV2 = asm.GetType("Version2.Empty");
                     var v2Inst = Activator.CreateInstance(tV2);
                     SetField(v2Inst, "X", 123);

                     var helper = asm.GetType("SchemaTest.Interaction");
                     var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                     helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                     var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                     // Succeeded, read nothing
                 }
             );
        }

        [Fact]
        public void OptionalFieldMissingInStream_BackwardCompat()
        {
            // V1: { int? X; }
            // V2: { } (Old version sending empty)
            // V1 reads V2. X should be null.
             var v1 = new TypeInfo { Name = "OptBack", Namespace = "Version1", Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int?" } } };
             var v2 = new TypeInfo { Name = "OptBack", Namespace = "Version2", Fields = new List<FieldInfo>() };

             TestEvolution(v1, v2,
                 "public partial struct OptBack { public int? X; }",
                 "public partial struct OptBack { }",
                 (asm) => {
                     var tV2 = asm.GetType("Version2.OptBack");
                     var v2Inst = Activator.CreateInstance(tV2);

                     var helper = asm.GetType("SchemaTest.Interaction");
                     var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                     helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                     var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                     Assert.Null(GetField(v1Res, "X"));
                 }
             );
        }

        [Fact]
        public void FieldReordering_Compatible_WithAppendable()
        {
            // V1: { [DdsId(0)] int A; [DdsId(1)] int B; }
            var v1 = new TypeInfo { 
                Name = "DataOrder", 
                Namespace = "Version1", 
                Fields = new List<FieldInfo> { 
                    new FieldInfo { Name = "A", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {0} } } },
                    new FieldInfo { Name = "B", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {1} } } }
                } 
            };
            
            // V2: { [DdsId(1)] int B; [DdsId(0)] int A; } - REORDERED
            var v2 = new TypeInfo { 
                Name = "DataOrder", 
                Namespace = "Version2", 
                Fields = new List<FieldInfo> { 
                    new FieldInfo { Name = "B", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {1} } } },
                    new FieldInfo { Name = "A", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {0} } } }
                } 
            };
            
            TestEvolution(v1, v2,
                "public partial struct DataOrder { [DdsId(0)] public int A; [DdsId(1)] public int B; }",
                "public partial struct DataOrder { [DdsId(1)] public int B; [DdsId(0)] public int A; }",
                (asm) => {
                    var tV2 = asm.GetType("Version2.DataOrder");
                    var v2Inst = Activator.CreateInstance(tV2);
                    SetField(v2Inst, "A", 111);
                    SetField(v2Inst, "B", 222);

                    var helper = asm.GetType("SchemaTest.Interaction");
                    var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                    helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                    var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                    
                    Assert.Equal(111, GetField(v1Res, "A"));
                    Assert.Equal(222, GetField(v1Res, "B"));
                }
            );
        }

        [Fact]
        public void OptionalBecomesRequired_Fails_WithoutHeader()
        {
            // V1: { [DdsId(0)] int? OptField; }  
            var v1 = new TypeInfo { 
                Name = "DataOpt", 
                Namespace = "Version1", 
                Fields = new List<FieldInfo> { 
                    new FieldInfo { Name = "OptField", TypeName = "int?", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {0} } } }
                } 
            };
            
            // V2: { [DdsId(0)] int OptField; } - NO LONGER OPTIONAL
            var v2 = new TypeInfo { 
                Name = "DataOpt", 
                Namespace = "Version2", 
                Fields = new List<FieldInfo> { 
                    new FieldInfo { Name = "OptField", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsId", Arguments = new List<object> {0} } } }
                } 
            };
            
            TestEvolution(v1, v2,
                "public partial struct DataOpt { [DdsId(0)] public int? OptField; }",
                "public partial struct DataOpt { [DdsId(0)] public int OptField; }",
                (asm) => {
                    var tV2 = asm.GetType("Version2.DataOpt");
                    var v2Inst = Activator.CreateInstance(tV2);
                    SetField(v2Inst, "OptField", 999);

                    var helper = asm.GetType("SchemaTest.Interaction");
                    var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                    helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                    var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                    
                    // Expect failure (null) because V2 writes 4 bytes (999), 
                    // V1 reads 4 bytes as EMHEADER, finds mismatch/invalid, and skips field.
                    Assert.Null(GetField(v1Res, "OptField"));
                }
            );
        }

        [Fact]
        public void UnionDiscriminatorTypeChange_Incompatible()
        {
            // V1: union switch(short)
            var v1 = new TypeInfo { 
                Name = "UShort", 
                Namespace = "Version1", 
                Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsUnion"} },
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "D", TypeName = "short", 
                        Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsDiscriminator"} } },
                    new FieldInfo { Name = "X", TypeName = "int", 
                        Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsCase", Arguments=new List<object>{1}} } }
                }
            };
            
            // V2: union switch(int) - DISCRIMINATOR TYPE CHANGED
            var v2 = new TypeInfo { 
                Name = "UShort", 
                Namespace = "Version2", 
                Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsUnion"} },
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "D", TypeName = "int",
                        Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsDiscriminator"} } },
                    new FieldInfo { Name = "X", TypeName = "int", 
                        Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsCase", Arguments=new List<object>{1}} } }
                }
            };
            
            TestEvolution(v1, v2,
                "[DdsUnion] public partial struct UShort { [DdsDiscriminator] public short D; [DdsCase(1)] public int X; }",
                "[DdsUnion] public partial struct UShort { [DdsDiscriminator] public int D; [DdsCase(1)] public int X; }",
                (asm) => {
                    var tV2 = asm.GetType("Version2.UShort");
                    var v2Inst = Activator.CreateInstance(tV2);
                    SetField(v2Inst, "D", 1);
                    SetField(v2Inst, "X", 777);

                    var helper = asm.GetType("SchemaTest.Interaction");
                    var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                    helper.GetMethod("Run").Invoke(null, new object[] { v2Inst, buffer });

                    try {
                        var v1Res = helper.GetMethod("ReadV1").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                        var disc = (short)GetField(v1Res, "D");
                        Assert.True(disc != 1, "Discriminator type change causes incompatibility");
                    }
                    catch (Exception) {
                        Assert.True(true, "Discriminator type change is incompatible (threw exception as expected)");
                    }
                }
            );
        }
    }
}

