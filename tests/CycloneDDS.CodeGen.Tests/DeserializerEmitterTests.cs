using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using System.Buffers;

namespace CycloneDDS.CodeGen.Tests
{
    public class DeserializerEmitterTests
    {
        [Fact]
        public void Deserialize_Primitives_Correctly()
        {
            var type = new TypeInfo
            {
                Name = "PrimitiveData",
                Namespace = "TestNamespace",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Value", TypeName = "double" }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var deserializerEmitter = new DeserializerEmitter();
            
            string serializedCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry());
            string deserializedCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry());
            
            string combinedCode = @"
using System;
using System.Text;
using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Buffers;

namespace TestNamespace
{
    // Struct Definition
    public partial struct PrimitiveData
    { 
        public int Id; 
        public double Value; 
    }
}

" + serializedCode.Substring(serializedCode.IndexOf("namespace") + "namespace TestNamespace".Length + 2) // Hacky merge
  + deserializedCode.Substring(deserializedCode.IndexOf("namespace") + "namespace TestNamespace".Length + 2);

            // Clean up the merge (removing duplicate namespace braces manually or just generating cleaner)
            // Better: Emit without namespace in test helper?
            // Or just put them in the same file block.
            // The emitters include namespace wrapper.
            // Be careful.
            
            // Re-generating clean code
            combinedCode = 
@"using System;
using System.Text;
using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Buffers;

namespace TestNamespace
{
    public partial struct PrimitiveData
    { 
        public int Id; 
        public double Value; 
    }
" + ExtractBody(serializedCode) + "\n" + ExtractBody(deserializedCode) + @"

    public static class TestHelper
    {
        public static object RoundTrip(object input)
        {
            var data = (PrimitiveData)input;
            var writerBuffer = new ArrayBufferWriter<byte>();
            var writer = new CdrWriter(writerBuffer);
            data.Serialize(ref writer);
            writer.Complete();
            
            var reader = new CdrReader(writerBuffer.WrittenSpan, CdrEncoding.Xcdr1);
            var view = PrimitiveData.Deserialize(ref reader);
            
            return view.ToOwned();
        }
    }
}";

            var assembly = CompileToAssembly(combinedCode, "DeserializerPrimitives");
            var dataType = assembly.GetType("TestNamespace.PrimitiveData");
            var input = Activator.CreateInstance(dataType);
            dataType.GetField("Id").SetValue(input, 12345);
            dataType.GetField("Value").SetValue(input, 3.14159);
            
            var methods = assembly.GetType("TestNamespace.TestHelper").GetMethod("RoundTrip");
            var result = methods.Invoke(null, new object[] { input });
            
            Assert.Equal(12345, (int)dataType.GetField("Id").GetValue(result));
            Assert.Equal(3.14159, (double)dataType.GetField("Value").GetValue(result), 5);
        }
        
        [Fact]
        public void Deserialize_String_Correctly()
        {
            var type = new TypeInfo
            {
                Name = "StringData",
                Namespace = "TestNamespace",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Message", TypeName = "string", Attributes = new List<AttributeInfo> { new AttributeInfo { Name = "DdsManaged" } } }
                }
            };
            
            var serializerEmitter = new SerializerEmitter();
            var deserializerEmitter = new DeserializerEmitter();
            string serializedCode = serializerEmitter.EmitSerializer(type, new GlobalTypeRegistry());
            string deserializedCode = deserializerEmitter.EmitDeserializer(type, new GlobalTypeRegistry());
            
            string combinedCode = 
@"using System;
using System.Text;
using CycloneDDS.Core;
using System.Runtime.InteropServices;
using System.Buffers;
using CycloneDDS.Schema;

namespace TestNamespace
{
    public partial struct StringData
    { 
        [DdsManaged]
        public string Message; 
    }
" + ExtractBody(serializedCode) + "\n" + ExtractBody(deserializedCode) + @"

    public static class TestHelper
    {
        public static object RoundTrip(object input)
        {
            var data = (StringData)input;
            var writerBuffer = new ArrayBufferWriter<byte>();
            var writer = new CdrWriter(writerBuffer);
            data.Serialize(ref writer);
            writer.Complete();
            
            var reader = new CdrReader(writerBuffer.WrittenSpan);
            var view = StringData.Deserialize(ref reader);
            
            return view.ToOwned(); // Checks ToOwned logic AND indirectly View reading
        }
    }
}";
            var assembly = CompileToAssembly(combinedCode, "DeserializerString");
            var dataType = assembly.GetType("TestNamespace.StringData");
            var input = Activator.CreateInstance(dataType);
            dataType.GetField("Message").SetValue(input, "Hello World from DDS!");
            
            var methods = assembly.GetType("TestNamespace.TestHelper").GetMethod("RoundTrip");
            var result = methods.Invoke(null, new object[] { input });
            
            Assert.Equal("Hello World from DDS!", (string)dataType.GetField("Message").GetValue(result));
        }

        private string ExtractBody(string code)
        {
            // Extract content inside namespace { ... }
            int start = code.IndexOf("namespace");
            if (start == -1) return code;
            start = code.IndexOf("{", start) + 1;
            int end = code.LastIndexOf("}");
            return code.Substring(start, end - start);
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
                MetadataReference.CreateFromFile(typeof(IBufferWriter<>).Assembly.Location), 
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location) 
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
                errorMsg += "\n\nCode:\n" + code;
                throw new Exception("Compilation failed:\n" + errorMsg);
            }

            ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }
    }
}

