using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using System.Buffers;
using System.Text;

namespace CycloneDDS.CodeGen.Tests
{
    public class OptionalTests
    {
        private TypeInfo CreateOptionalType()
        {
            return new TypeInfo
            {
                Name = "OptionalData",
                Namespace = "OptionalTests",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "OptInt", TypeName = "int?" },
                    new FieldInfo { Name = "OptDouble", TypeName = "double?" },
                    new FieldInfo { Name = "OptString", TypeName = "string?" }
                }
            };
        }

        private string GetStructDef()
        {
            return @"
namespace OptionalTests
{
    public partial struct OptionalData
    {
        public int Id;
        public int? OptInt;
        public double? OptDouble;
        public string? OptString;
    }
}";
        }


        [Fact]
        public void GeneratedCode_Compiles()
        {
            var type = CreateOptionalType();
            var serializerEmitter = new SerializerEmitter();
            var serializerCode = serializerEmitter.EmitSerializer(type);
            var deserializerEmitter = new DeserializerEmitter();
            var deserializerCode = deserializerEmitter.EmitDeserializer(type);

            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef());
            Assert.NotNull(assembly);
        }

        [Fact]
        public void Optional_Present_SerializesWithEMHEADER()
        {
            var type = CreateOptionalType();
            var serializerCode = new SerializerEmitter().EmitSerializer(type);
            var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
            var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public static byte[] Serialize(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        return writer.WrittenSpan.ToArray();
    }
}";
            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
            var harness = assembly.GetType("Harness");
            
            byte[] bytes = (byte[])harness.GetMethod("Serialize").Invoke(null, new object[] { 100, (int?)42, (double?)null, (string?)null });
            
            // Expected Layout:
            // DHEADER (4)
            // Id (4) = 100
            // OptInt EMHEADER (4) = (4 << 3) | 1 = 0x21
            // OptInt Value (4) = 42
            // Total: 16 bytes
            
            Assert.Equal(16, bytes.Length);
            Assert.Equal(100, BitConverter.ToInt32(bytes, 4));
            // EMHEADER for 4-byte int with ID=1: (4 << 3) | 1 = 0x21
            Assert.Equal(0x00000021, (int)BitConverter.ToUInt32(bytes, 8));
            Assert.Equal(42, BitConverter.ToInt32(bytes, 12));
        }

        [Fact]
        public void Optional_Absent_SerializesAsZeroBytes()
        {
            var type = CreateOptionalType();
            var serializerCode = new SerializerEmitter().EmitSerializer(type);
            var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
            var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public static byte[] Serialize(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        return writer.WrittenSpan.ToArray();
    }
}";
            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
            var harness = assembly.GetType("Harness");
            
            byte[] bytes = (byte[])harness.GetMethod("Serialize").Invoke(null, new object[] { 100, (int?)null, (double?)null, (string?)null });
            
            Assert.Equal(8, bytes.Length);
            Assert.Equal(100, BitConverter.ToInt32(bytes, 4));
        }

        [Fact]
        public void Optional_String_Present_SerializesCorrectly()
        {
             var type = CreateOptionalType();
            var serializerCode = new SerializerEmitter().EmitSerializer(type);
            var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
            var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public static byte[] Serialize(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        return writer.WrittenSpan.ToArray();
    }
}";
            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
            var harness = assembly.GetType("Harness");
            
            byte[] bytes = (byte[])harness.GetMethod("Serialize").Invoke(null, new object[] { 200, (int?)null, (double?)null, "Hello" });
            
            Assert.Equal(22, bytes.Length); 
            // EMHEADER for 10-byte string with ID=3: (10 << 3) | 3 = 0x53
            Assert.Equal(0x00000053, (int)BitConverter.ToUInt32(bytes, 8)); 
            Assert.Equal(6, BitConverter.ToInt32(bytes, 12)); 
        }

        [Fact]
        public void RoundTrip_MixedOptionals()
        {
            var type = CreateOptionalType();
            var serializerCode = new SerializerEmitter().EmitSerializer(type);
            var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
            var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public class Result {
        public int Id;
        public int? OptInt;
        public double? OptDouble;
        public string OptString;
    }

    public static Result RoundTrip(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        // Serialize
        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        byte[] bytes = writer.WrittenSpan.ToArray();

        // Deserialize
        var reader = new CdrReader(bytes);
        var view = OptionalData.Deserialize(ref reader);
        var owned = view.ToOwned();
        
        return new Result { 
            Id = owned.Id,
            OptInt = owned.OptInt,
            OptDouble = owned.OptDouble,
            OptString = owned.OptString
        };
    }
}";
            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
            var harness = assembly.GetType("Harness");
            
            var result = harness.GetMethod("RoundTrip").Invoke(null, new object[] { 999, (int?)123, (double?)null, "Roundtrip" });
            var resType = result.GetType();
            
            Assert.Equal(999, (int)resType.GetField("Id").GetValue(result));
            Assert.Equal(123, (int?)resType.GetField("OptInt").GetValue(result));
            Assert.Null(resType.GetField("OptDouble").GetValue(result));
            Assert.Equal("Roundtrip", (string)resType.GetField("OptString").GetValue(result));
        }

        [Fact]
        public void EMHEADER_BitLayout_FollowsXCDR2Spec()
        {
            var type = CreateOptionalType();
            var serializerCode = new SerializerEmitter().EmitSerializer(type);
            var deserializerCode = new DeserializerEmitter().EmitDeserializer(type);
            var harnessCode = @"
using System;
using System.Buffers;
using CycloneDDS.Core;
using OptionalTests;

public class Harness
{
    public static byte[] Serialize(int id, int? optInt, double? optDouble, string optString)
    {
        var data = new OptionalData();
        data.Id = id;
        data.OptInt = optInt;
        data.OptDouble = optDouble;
        data.OptString = optString;

        var writer = new ArrayBufferWriter<byte>();
        var cdr = new CdrWriter(writer);
        data.Serialize(ref cdr);
        cdr.Complete();
        return writer.WrittenSpan.ToArray();
    }
}";
            var assembly = CompileToAssembly(serializerCode, deserializerCode, GetStructDef(), harnessCode);
            var harness = assembly.GetType("Harness");
            
            byte[] bytes = (byte[])harness.GetMethod("Serialize").Invoke(null, new object[] { 100, (int?)42, (double?)null, (string?)null });
            
            // EMHEADER at offset 8 (after DHEADER + Id)
            uint emheader = BitConverter.ToUInt32(bytes, 8);
            
            // XCDR2 EMHEADER bit layout: [M:1bit][Length:28bits][ID:3bits]
            uint mustUnderstand = (emheader >> 31) & 0x1;      // Bit 31
            uint length = (emheader >> 3) & 0x0FFFFFFF;        // Bits 30-3
            uint memberId = emheader & 0x7;                    // Bits 2-0
            
            // Verify bit fields
            Assert.Equal(0u, mustUnderstand);  // Appendable types have M=0
            Assert.Equal(4u, length);          // int is 4 bytes
            Assert.Equal(1u, memberId);        // First optional field gets ID=1
            
            // Verify complete EMHEADER value
            Assert.Equal(0x00000021u, emheader); // (4 << 3) | 1
        }

        private Assembly CompileToAssembly(params string[] sources)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s));
            
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), 
                MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Xunit.Assert).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CycloneDDS.Core.CdrWriter).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Buffers.ArrayBufferWriter<>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
            };

            var compilation = CSharpCompilation.Create(
                "OptionalTests_" + Guid.NewGuid().ToString("N"),
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var peStream = new MemoryStream())
            {
                var result = compilation.Emit(peStream);

                if (!result.Success)
                {
                    var failures = result.Diagnostics
                        .Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    
                    var sb = new StringBuilder();
                    foreach (var diagnostic in failures)
                    {
                        sb.AppendLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
                    }
                    throw new Exception("Compilation failed:\n" + sb.ToString() + "\nSource:\n" + string.Join("\n---\n", sources));
                }

                peStream.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(peStream.ToArray());
            }
        }
    }
}
