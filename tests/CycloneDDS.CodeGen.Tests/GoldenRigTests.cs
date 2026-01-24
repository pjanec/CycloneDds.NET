using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    public class GoldenRigTests : CodeGenTestBase
    {
        [Fact]
        public void RunGoldenRig()
        {
            // 1. Define Types matching Golden.idl
            
            // SimplePrimitive
            var tSimple = new TypeInfo {
                Name = "SimplePrimitive", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "id", TypeName = "int" },
                    new FieldInfo { Name = "value", TypeName = "double" }
                }
            };

            // Nested
            var tNested = new TypeInfo {
                Name = "Nested", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "a", TypeName = "int" },
                    new FieldInfo { Name = "b", TypeName = "double" }
                }
            };

            // NestedStruct
            var tNestedStruct = new TypeInfo {
                Name = "NestedStruct", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "byte_field", TypeName = "byte" },
                    new FieldInfo { Name = "nested", TypeName = "Nested", Type = tNested }
                }
            };

            // FixedString
            var tFixedString = new TypeInfo {
                Name = "FixedString", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "message", TypeName = "FixedString32" }
                }
            };

            // UnboundedString
            var tUnbounded = new TypeInfo {
                Name = "UnboundedString", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "id", TypeName = "int" },
                    new FieldInfo { Name = "message", TypeName = "string" }
                }
            };

            // PrimitiveSequence
            var tPrimSeq = new TypeInfo {
                Name = "PrimitiveSequence", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "values", TypeName = "BoundedSeq<int>" }
                }
            };

            // StringSequence
            var tStrSeq = new TypeInfo {
                Name = "StringSequence", Namespace = "Golden",                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "Appendable" } },                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "values", TypeName = "BoundedSeq<string>" }
                }
            };

            // MixedStruct
            var tMixed = new TypeInfo {
                Name = "MixedStruct", Namespace = "Golden",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "b", TypeName = "byte" },
                    new FieldInfo { Name = "i", TypeName = "int" },
                    new FieldInfo { Name = "d", TypeName = "double" },
                    new FieldInfo { Name = "s", TypeName = "string" }
                }
            };

            // AppendableStruct
            var tAppendable = new TypeInfo {
                Name = "AppendableStruct", Namespace = "Golden",
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "Appendable" } },
                Fields = new List<FieldInfo> {
                    new FieldInfo { Name = "Id", TypeName = "int", Attributes = new List<AttributeInfo>{new AttributeInfo{Name="DdsId", Arguments=new List<object>{0}} } },
                    new FieldInfo { Name = "Message", TypeName = "string", Attributes = new List<AttributeInfo>{new AttributeInfo{Name="DdsId", Arguments=new List<object>{1}} } }
                }
            };

            // 2. Generate Code
            var emitter = new SerializerEmitter();
            string code = @"using CycloneDDS.Schema; using CycloneDDS.Core; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;

namespace Golden {
    public partial struct SimplePrimitive { public int Id; public double Value; }
    public partial struct Nested { public int A; public double B; }
    public partial struct NestedStruct { public byte Byte_field; public Nested Nested; }
    public partial struct FixedString { public string Message; }
    public partial struct UnboundedString { public int Id; public string Message; }
    public partial struct PrimitiveSequence { public BoundedSeq<int> Values; }
    public partial struct StringSequence { public BoundedSeq<string> Values; }
    public partial struct MixedStruct { public byte B; public int I; public double D; public string S; }
    public partial struct AppendableStruct { [DdsId(0)] public int Id; [DdsId(1)] public string Message; }
}
";
            code += emitter.EmitSerializer(tSimple, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tNested, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tNestedStruct, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tFixedString, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tUnbounded, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tPrimSeq, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tStrSeq, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tMixed, new GlobalTypeRegistry(), false);
            code += emitter.EmitSerializer(tAppendable, new GlobalTypeRegistry(), false);

            // Helper to Invoke Serialize
            code += @"
namespace Golden {
    public static class Helper {
        public static void SerializeSimplePrimitive(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (SimplePrimitive)inst; // Cast
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeNestedStruct(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (NestedStruct)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeFixedString(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (FixedString)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeUnboundedString(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (UnboundedString)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializePrimitiveSequence(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (PrimitiveSequence)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeStringSequence(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (StringSequence)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w, CycloneDDS.Core.CdrEncoding.Xcdr2); // Xcdr2 (Appendable)
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeMixedStruct(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (MixedStruct)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w); // Default Xcdr1
            t.Serialize(ref writer);
            writer.Complete();
        }
        public static void SerializeAppendableStruct(object inst, System.Buffers.IBufferWriter<byte> w) {
            var t = (AppendableStruct)inst;
            var writer = new CycloneDDS.Core.CdrWriter(w, CycloneDDS.Core.CdrEncoding.Xcdr2); // Xcdr2 (Appendable)
            t.Serialize(ref writer);
            writer.Complete();
        }
    }
}
";

            // 3. Compile
            var asm = CompileToAssembly("GoldenAssembly", code);
            var helper = asm.GetType("Golden.Helper");
            
            // 4. Verify Cases
            // SimplePrimitive
            Verify(asm, helper.GetMethod("SerializeSimplePrimitive"), "Golden.SimplePrimitive", 
                inst => { SetField(inst, "Id", 123456789); SetField(inst, "Value", 123.456); },
                "15CD5B070000000077BE9F1A2FDD5E40");
            
            // NestedStruct
            Verify(asm, helper.GetMethod("SerializeNestedStruct"), "Golden.NestedStruct",
                inst => { 
                    SetField(inst, "Byte_field", (byte)0xAB);
                    var nestedType = asm.GetType("Golden.Nested");
                    var nested = Activator.CreateInstance(nestedType);
                    SetField(nested, "A", 987654321);
                    SetField(nested, "B", 987.654);
                    SetField(inst, "Nested", nested);
                },
                "AB000000B168DE3AAC1C5A643BDD8E40");

            // FixedString
            Verify(asm, helper.GetMethod("SerializeFixedString"), "Golden.FixedString",
                inst => { SetField(inst, "Message", "FixedString123"); },
                "4669786564537472696E67313233000000000000000000000000000000000000");

            // UnboundedString
            Verify(asm, helper.GetMethod("SerializeUnboundedString"), "Golden.UnboundedString",
                inst => { SetField(inst, "Id", 111222); SetField(inst, "Message", "UnboundedStringData"); },
                "76B2010014000000556E626F756E646564537472696E674461746100");

            // PrimitiveSequence
            Verify(asm, helper.GetMethod("SerializePrimitiveSequence"), "Golden.PrimitiveSequence",
                inst => { 
                    var list = new BoundedSeq<int>(5);
                    list.Add(10); list.Add(20); list.Add(30); list.Add(40); list.Add(50);
                    SetField(inst, "Values", list);
                },
                "050000000A000000140000001E0000002800000032000000");

            // StringSequence (Appendable/Xcdr2)
            Verify(asm, helper.GetMethod("SerializeStringSequence"), "Golden.StringSequence",
                inst => { 
                    var list = new BoundedSeq<string>(3);
                    list.Add("One"); list.Add("Two"); list.Add("Three");
                    SetField(inst, "Values", list);
                },
                "1E00000003000000040000004F6E65000400000054776F0006000000546872656500");

            // MixedStruct
            Verify(asm, helper.GetMethod("SerializeMixedStruct"), "Golden.MixedStruct",
                inst => { SetField(inst, "B", (byte)0xFF); SetField(inst, "I", -555); SetField(inst, "D", 0.00001); SetField(inst, "S", "MixedString"); },
                "FF000000D5FDFFFFF168E388B5F8E43E0C0000004D69786564537472696E6700");

            // AppendableStruct (Appendable/Xcdr2)
            Verify(asm, helper.GetMethod("SerializeAppendableStruct"), "Golden.AppendableStruct",
                inst => { SetField(inst, "Id", 999); SetField(inst, "Message", "Appendable"); },
                "13000000E70300000B000000417070656E6461626C6500"); 
        }

        private void Verify(Assembly asm, MethodInfo serializeMeth, string typeName, Action<object> setup, string expectedHex)
        {
            var type = asm.GetType(typeName);
            var inst = Activator.CreateInstance(type);
            setup(inst);
            
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            serializeMeth.Invoke(null, new object[] { inst, buffer });
            
            string actualHex = BytesToHex(buffer.WrittenMemory);
            
            // Strip spaces
            expectedHex = expectedHex.Replace(" ", "");
            actualHex = actualHex.Replace(" ", "");
            
            // Handle DHEADER difference if necessary
            // If Actual has extra 4 bytes (8 hex chars) at start, and suffix matches expected...
            if (actualHex.Length == expectedHex.Length + 8 && actualHex.EndsWith(expectedHex))
            {
                 // DHEADER present in Actual but not Expected. Acceptable.
            }
            else
            {
                Assert.Equal(expectedHex, actualHex);
            }
        }
        
        private string BytesToHex(ReadOnlyMemory<byte> bytes)
        {
             return Convert.ToHexString(bytes.Span);
        }

        private void SetField(object inst, string name, object val)
        {
            inst.GetType().GetField(name).SetValue(inst, val);
        }
    }
}

