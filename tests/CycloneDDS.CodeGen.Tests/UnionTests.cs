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
    public class UnionTests
    {
        [Fact]
        public void GeneratedCode_Serializes_Union_MatchesXCDRSpec()
        {
            // Define type
            var type = new TypeInfo
            {
                Name = "Shape",
                Namespace = "UnionNamespace",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsUnion" } },
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { 
                         Name = "Kind", TypeName = "int",
                         Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsDiscriminator" } }
                    },
                    new FieldInfo { 
                         Name = "Radius", TypeName = "double",
                         Attributes = new List<AttributeInfo> { 
                              new AttributeInfo { Name = "DdsCase", Arguments = new List<object> { 1 } } 
                         }
                    },
                    new FieldInfo { 
                         Name = "Side", TypeName = "int",
                         Attributes = new List<AttributeInfo> { 
                              new AttributeInfo { Name = "DdsCase", Arguments = new List<object> { 2 } } 
                         }
                    }
                }
            };
            
            // Generate Serializer code
            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry());
            
            // Generate Deserializer code
            var demitter = new DeserializerEmitter();
            string generatedDCode = demitter.EmitDeserializer(type, new GlobalTypeRegistry());

            // Define the struct
            string structDef = @"
using CycloneDDS.Schema;
namespace UnionNamespace
{
    [DdsUnion]
    public partial struct Shape
    {
        [DdsDiscriminator]
        public int Kind;
        
        [DdsCase(1)]
        public double Radius;
        
        [DdsCase(2)]
        public int Side;
    }
}
";

            // Helper


            // Helper
            string testHelper = @"
namespace UnionNamespace
{
    public static class TestHelper
    {
        public static void SerializeWithBuffer(object instance, System.Buffers.IBufferWriter<byte> buffer)
        {
            var typedInstance = (Shape)instance;
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            typedInstance.Serialize(ref writer);
            writer.Complete();
        }

        public static object DeserializeFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            var reader = new CycloneDDS.Core.CdrReader(buffer.Span);
            var view = Shape.Deserialize(ref reader);
            return view.ToOwned();
        }
    }
}
";
            
            string header = @"
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CycloneDDS.Core;
using CycloneDDS.Schema;
";

            string code = header + 
                          RemoveUsings(generatedCode) + "\n" + 
                          RemoveUsings(generatedDCode) + "\n" + 
                          RemoveUsings(structDef) + "\n" + 
                          RemoveUsings(testHelper);
            
            // Compile
            var assembly = CompileToAssembly(code, "ShapeAssembly");
            var generatedType = assembly.GetType("UnionNamespace.Shape");
            Assert.NotNull(generatedType);
            
            // 1. Test Serialization Case 1 (Radius)
            var instance = Activator.CreateInstance(generatedType);
            generatedType.GetField("Kind").SetValue(instance, 1);
            generatedType.GetField("Radius").SetValue(instance, 10.5);
            
            var writerBuffer = new ArrayBufferWriter<byte>();
            var testHelperType = assembly.GetType("UnionNamespace.TestHelper");
            var serializeWrapper = testHelperType.GetMethod("SerializeWithBuffer");
            serializeWrapper.Invoke(null, new object[] { instance, writerBuffer });
            
            // Expected: Disc(1) | Radius(10.5)
            // 01 00 00 00 | 00 00 00 00 | 00 00 00 00 00 00 25 40
            string expectedHex = "01 00 00 00 00 00 00 00 00 00 00 00 00 00 25 40";
            string actualHex = ToHex(writerBuffer.WrittenSpan.ToArray());
            Assert.Equal(expectedHex.Replace(" ", ""), actualHex.Replace(" ", ""));

            // 2. Test Deserialization (Round Trip)
            var deserializeWrapper = testHelperType.GetMethod("DeserializeFromBuffer");
            var deserializedObj = deserializeWrapper.Invoke(null, new object[] { writerBuffer.WrittenMemory });
            
            int kind = (int)generatedType.GetField("Kind").GetValue(deserializedObj);
            double radius = (double)generatedType.GetField("Radius").GetValue(deserializedObj);
            
            Assert.Equal(1, kind);
            Assert.Equal(10.5, radius);

            // 3. Test Serialization Case 2 (Side)
            var instance2 = Activator.CreateInstance(generatedType);
            generatedType.GetField("Kind").SetValue(instance2, 2);
            generatedType.GetField("Side").SetValue(instance2, 55);
            
            var writerBuffer2 = new ArrayBufferWriter<byte>();
            serializeWrapper.Invoke(null, new object[] { instance2, writerBuffer2 });
            
            // Expected: Disc(2) | Side(55)
            // Side(55) = 0x37. 37 00 00 00.
            // 02 00 00 00 | 37 00 00 00
            expectedHex = "02 00 00 00 37 00 00 00";
            actualHex = ToHex(writerBuffer2.WrittenSpan.ToArray());
            Assert.Equal(expectedHex.Replace(" ", ""), actualHex.Replace(" ", ""));
            
            var deserializedObj2 = deserializeWrapper.Invoke(null, new object[] { writerBuffer2.WrittenMemory });
            int kind2 = (int)generatedType.GetField("Kind").GetValue(deserializedObj2);
            int side2 = (int)generatedType.GetField("Side").GetValue(deserializedObj2);
            Assert.Equal(2, kind2);
            Assert.Equal(55, side2);

             // 4. Test Unknown Case (Skipping)
             // Construct buffer manually with unknown case
             // Disc(3) | Extra(4 bytes junk)
             // 03 00 00 00 | FF FF FF FF
             byte[] unknownBytes = ParseHex("03 00 00 00 FF FF FF FF");
             
             var deserializedObj3 = deserializeWrapper.Invoke(null, new object[] { (ReadOnlyMemory<byte>)unknownBytes });
             // Should not throw
             int kind3 = (int)generatedType.GetField("Kind").GetValue(deserializedObj3);
             Assert.Equal(3, kind3);
             // Other fields should be default
             int side3 = (int)generatedType.GetField("Side").GetValue(deserializedObj3);
             Assert.Equal(0, side3);
        }

        private string RemoveUsings(string code)
        {
            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            return string.Join("\n", lines.Where(l => !l.TrimStart().StartsWith("using ")));
        }

        private Assembly CompileToAssembly(string code, string assemblyName)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            // Get necessary references
            // We need core library.
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.InteropServices.Marshal).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Core.CdrWriter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Schema.DdsUnionAttribute).Assembly.Location) 
            };
            
            // Check implicit refs location
            var trustPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            
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
        
        private byte[] ParseHex(string hex)
        {
            hex = hex.Replace(" ", "");
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
    }
}

