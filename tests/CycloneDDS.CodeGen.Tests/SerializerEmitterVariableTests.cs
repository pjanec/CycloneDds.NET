using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema;
using System.Buffers;

namespace CycloneDDS.CodeGen.Tests
{
    public class SerializerEmitterVariableTests
    {
        [Fact]
        public void String_Serializes_Correctly()
        {
            var type = new TypeInfo
            {
                Name = "MessageData",
                Namespace = "TestNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo 
                    { 
                        Name = "Message", 
                        TypeName = "string",
                        Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } }
                    }
                }
            };
            
            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            string structDef = @"
namespace TestNamespace
{
    public partial struct MessageData
    {
        public int Id;
        [DdsManaged]
        public string Message;
    }
}
";
            string testHelper = @"
namespace TestNamespace
{
    public static class TestHelper
    {
        public static void SerializeWithBuffer(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {
            var typedInstance = (MessageData)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }
    }
}
";
            string code = "using System.Buffers;\nusing CycloneDDS.Schema;\n" + generatedCode + "\n" + structDef + "\n" + testHelper;
            
            var assembly = CompileToAssembly(code, "StringAssembly");
            var generatedType = assembly.GetType("TestNamespace.MessageData");
            
            var instance = Activator.CreateInstance(generatedType);
            generatedType.GetField("Id").SetValue(instance, 10);
            generatedType.GetField("Message").SetValue(instance, "Hello"); // 5 chars + NUL = 6 bytes. Aligned 4.
            
            // Size: Id (4) + String Length (4) + String (6) = 14.
            var getSerializedSizeMethod = generatedType.GetMethod("GetSerializedSize", new Type[] { typeof(int) });
            int size = (int)getSerializedSizeMethod.Invoke(instance, new object[] { 0 });
            Assert.Equal(14, size); 

            // Serialize
            var writerBuffer = new ArrayBufferWriter<byte>();
            var testHelperType = assembly.GetType("TestNamespace.TestHelper");
            testHelperType.GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, writerBuffer });
            
            // Verify
            // DHEADER: No Header (Final)
            // Id: 10 -> 0A 00 00 00
            // String Len: 6 -> 06 00 00 00
            // String: 'H' 'e' 'l' 'l' 'o' '\0' -> 48 65 6C 6C 6F 00
            
            string expected = "0A 00 00 00 06 00 00 00 48 65 6C 6C 6F 00";
            string actual = ToHex(writerBuffer.WrittenSpan.ToArray());
            Assert.Equal(expected.Replace(" ", ""), actual.Replace(" ", ""));
        }

        [Fact]
        public void Sequence_Of_Primitives_Serializes_Correctly()
        {
             var type = new TypeInfo
            {
                Name = "SeqData",
                Namespace = "TestNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Numbers", TypeName = "BoundedSeq<int>" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry());
             
            string structDef = @"
namespace TestNamespace
{
    public partial struct SeqData
    {
        public BoundedSeq<int> Numbers;
    }
}
";
            string testHelper = @"
namespace TestNamespace
{
    public static class TestHelper
    {
        public static void SerializeWithBuffer(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {
            var typedInstance = (SeqData)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }
    }
}
";
            string code = "using System.Buffers;\nusing CycloneDDS.Schema;\n" + generatedCode + "\n" + structDef + "\n" + testHelper;
            var assembly = CompileToAssembly(code, "SeqAssembly");
            var generatedType = assembly.GetType("TestNamespace.SeqData");
            
            var instance = Activator.CreateInstance(generatedType);
            // Init BoundedSeq
            var boundedSeqType = typeof(BoundedSeq<int>);
            // BoundedSeq constructor takes capacity
            var seqInstance = Activator.CreateInstance(boundedSeqType, new object[] { 10 });
            // Add items
            var addMethod = boundedSeqType.GetMethod("Add");
            addMethod.Invoke(seqInstance, new object[] { 100 });
            addMethod.Invoke(seqInstance, new object[] { 200 });
            
            generatedType.GetField("Numbers").SetValue(instance, seqInstance);
            
            // Size: SeqLen (4) + 2*4 = 12.
            var getSerializedSizeMethod = generatedType.GetMethod("GetSerializedSize", new Type[] { typeof(int) });
            int size = (int)getSerializedSizeMethod.Invoke(instance, new object[] { 0 });
            Assert.Equal(12, size);
            
            // Serialize
            var writerBuffer = new ArrayBufferWriter<byte>();
            assembly.GetType("TestNamespace.TestHelper").GetMethod("SerializeWithBuffer").Invoke(null, new object[] { instance, writerBuffer });
            
            // Verify
            // DHEADER: No Header (Final)
            // SeqLen: 2 -> 02 00 00 00
            // Item 1: 100 -> 64 00 00 00
            // Item 2: 200 -> C8 00 00 00
            
            string expected = "02 00 00 00 64 00 00 00 C8 00 00 00";
             string actual = ToHex(writerBuffer.WrittenSpan.ToArray());
            Assert.Equal(expected.Replace(" ", ""), actual.Replace(" ", ""));
        }

        [Fact]
        public void Nested_Variable_Struct_Serializes_Correctly()
        {
             var nestedType = new TypeInfo
            {
                Name = "InnerData",
                Namespace = "TestNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Text", TypeName = "string", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
                }
            };
            
             var outerType = new TypeInfo
            {
                Name = "OuterData",
                Namespace = "TestNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Inner", TypeName = "InnerData", Type = nestedType }
                }
            };
            
            var emitter = new SerializerEmitter();
            string generatedNested = emitter.EmitSerializer(nestedType, new GlobalTypeRegistry());
            generatedNested = generatedNested.Substring(generatedNested.IndexOf("namespace"));
            string generatedOuter = emitter.EmitSerializer(outerType, new GlobalTypeRegistry());
            generatedOuter = generatedOuter.Substring(generatedOuter.IndexOf("namespace"));
            
            string structDef = @"
namespace TestNamespace
{
    public partial struct InnerData
    {
        [DdsManaged]
        public string Text;
    }
    public partial struct OuterData
    {
        public InnerData Inner;
    }
}
";
            string testHelper = @"
namespace TestNamespace
{
    public static class TestHelper
    {
        public static void SerializeWithBuffer(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {
            var typedInstance = (OuterData)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }
    }
}
";
            string code = "using System.Buffers;\nusing CycloneDDS.Schema;\nusing CycloneDDS.Core;\nusing System.Runtime.InteropServices;\n" + generatedNested + "\n" + generatedOuter + "\n" + structDef + "\n" + testHelper;
            var assembly = CompileToAssembly(code, "NestedAssembly");
            
            var outerTypeGenerated = assembly.GetType("TestNamespace.OuterData");
            var innerTypeGenerated = assembly.GetType("TestNamespace.InnerData");
            
            var outerInstance = Activator.CreateInstance(outerTypeGenerated);
            var innerInstance = Activator.CreateInstance(innerTypeGenerated);
            innerTypeGenerated.GetField("Text").SetValue(innerInstance, "Abc"); // 3 + 1 = 4. Aligned.
            
            outerTypeGenerated.GetField("Inner").SetValue(outerInstance, innerInstance);
            
            // Size:
            // Text Len (4)
            // Text (4) ("Abc\0")
            // Inner Total = 8.
            // Outer Total = 8.
            
            var getSerializedSizeMethod = outerTypeGenerated.GetMethod("GetSerializedSize", new Type[] { typeof(int) });
            int size = (int)getSerializedSizeMethod.Invoke(outerInstance, new object[] { 0 });
            Assert.Equal(8, size);
            
            // Serialize
             var writerBuffer = new ArrayBufferWriter<byte>();
            assembly.GetType("TestNamespace.TestHelper").GetMethod("SerializeWithBuffer").Invoke(null, new object[] { outerInstance, writerBuffer });
            
            // Verify
            // Text Len: 4 (04 00 00 00)
            // Text: 41 62 63 00
            
            string expected = "04 00 00 00 41 62 63 00";
             string actual = ToHex(writerBuffer.WrittenSpan.ToArray());
            Assert.Equal(expected.Replace(" ", ""), actual.Replace(" ", ""));
        }

        private Assembly CompileToAssembly(string code, string assemblyName)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Core.CdrWriter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Schema.BoundedSeq<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location), // System.Memory
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location)
            };

            var compilation = CSharpCompilation.Create(assemblyName)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(tree);

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => 
                    diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                
                var errorMsg = string.Join("\n", failures.Select(d => $"{d.Id}: {d.GetMessage()}"));
                throw new Exception($"Compilation failed:\n{errorMsg}\n\nCode:\n{code}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return AssemblyLoadContext.Default.LoadFromStream(ms);
        }

        private string ToHex(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }
    }
}

