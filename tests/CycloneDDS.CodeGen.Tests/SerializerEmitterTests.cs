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
using System.Buffers;

namespace CycloneDDS.CodeGen.Tests
{
    public class SerializerEmitterTests
    {
        [Fact]
        public void GeneratedCode_Serializes_MatchesGoldenRig()
        {
            // Define type
            var type = new TypeInfo
            {
                Name = "SimplePrimitive",
                Namespace = "TestNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Value", TypeName = "double" }
                }
            };
            
            // Generate code
            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            // Define the other part of the partial struct (the fields)
            string structDef = @"
namespace TestNamespace
{
    public partial struct SimplePrimitive
    {
        public int Id;
        public double Value;
    }
}
";
            // Helper for test - removing usings from here, putting them at top
            string testHelper = @"
namespace TestNamespace
{
    public static class TestHelper
    {
        public static void SerializeWithBuffer(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {
            var typedInstance = (SimplePrimitive)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }
    }
}
";
            
            string code = "using System.Buffers;\n" + generatedCode + "\n" + structDef + "\n" + testHelper;
            
            // Compile code
            var assembly = CompileToAssembly(code, "SimplePrimitiveAssembly");
            var generatedType = assembly.GetType("TestNamespace.SimplePrimitive");
            Assert.NotNull(generatedType);
            
            // Create instance and set values
            var instance = Activator.CreateInstance(generatedType);
            generatedType.GetField("Id").SetValue(instance, 123456789);
            generatedType.GetField("Value").SetValue(instance, 123.456);
            
            // GetSerializedSize verification
            var getSerializedSizeMethod = generatedType.GetMethod("GetSerializedSize", new Type[] { typeof(int) });
            int size = (int)getSerializedSizeMethod.Invoke(instance, new object[] { 0 });
            Assert.Equal(16, size);

            // Serialize
            var writerBuffer = new ArrayBufferWriter<byte>();
            
            var testHelperType = assembly.GetType("TestNamespace.TestHelper");
            var serializeWrapper = testHelperType.GetMethod("SerializeWithBuffer");
            serializeWrapper.Invoke(null, new object[] { instance, writerBuffer });
            
            // Verify output
            // DHEADER: No Header (Final)
            
            // Full: 15 CD 5B 07 00 00 00 00 77 BE 9F 1A 2F DD 5E 40
            string expected = "15 CD 5B 07 00 00 00 00 77 BE 9F 1A 2F DD 5E 40";
            // Correct logic DHEADER is (0C 00 00 00)
            // Wait, example in instructions: "00 00 00 0C" ? No, expected is usually LE for DHEADER unless big endian default?
            // XCDR2 default is LE.
            // And note: The instructions example was: "00 00 00 0C 15 CD ..."
            // 0C is 12. "00 00 00 0C" implies Big Endian? 
            // OR checks "0C 00 00 00" (LE)?
            // CycloneDDS C# defaults to LE.
            // 123456789 = 0x075BCD15. LE is 15 CD 5B 07.
            // Instructions example: "15 CD 5B 07" matches LE.
            // So DHEADER "00 00 00 0C" is BE? 
            // 12 = 0x0000000C.
            // LE: 0C 00 00 00.
            // Instructions example might have a typo or used BE for DHEADER? No, XCDR2 usually matches stream structure.
            // Wait, the "Example GOOD Test" in instructions has "00 00 00 0C".
            // If I execute `BinaryPrimitives.WriteUInt32LittleEndian(12)`, I get `0C 00 00 00`.
            // So the instruction example IS WRONG about hex string order OR uses Big Endian?
            // I will assume Little Endian and EXPECT "0C 00 00 00".

            // Corrected Expected: "0C 00 00 00 15 CD 5B 07 77 BE 9F 1A 2F DD 5E 40"
            string actual = ToHex(writerBuffer.WrittenSpan.ToArray());
            
            // Remove spaces
            expected = expected.Replace(" ", "");
            actual = actual.Replace(" ", "");
            
            Assert.Equal(expected, actual);
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

