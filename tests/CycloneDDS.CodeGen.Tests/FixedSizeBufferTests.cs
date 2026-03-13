using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using CycloneDDS.CodeGen;
using CycloneDDS.CodeGen.Emitters;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// Comprehensive tests for C# fixed-size buffer support in the DSL code generator.
    /// Fixed-size buffers are declared as <c>public fixed T Name[N];</c> and result in
    /// zero-allocation inline arrays on the wire.
    /// </summary>
    public class FixedSizeBufferTests : CodeGenTestBase, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempDir;
        private readonly SerializerEmitter _serEmitter = new();
        private readonly ViewEmitter _viewEmitter = new();

        public FixedSizeBufferTests(ITestOutputHelper output)
        {
            _output = output;
            _tempDir = Path.Combine(Path.GetTempPath(), "CG_FSB_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        // ── Helper ────────────────────────────────────────────────────────────────

        private FieldInfo MakeFixedByteField(string name, int size) => new()
        {
            Name = name,
            TypeName = "byte",
            IsFixedSizeBuffer = true,
            FixedSize = size
        };

        private FieldInfo MakeFixedIntField(string name, int size) => new()
        {
            Name = name,
            TypeName = "int",
            IsFixedSizeBuffer = true,
            FixedSize = size
        };

        private FieldInfo MakeFixedFloatField(string name, int size) => new()
        {
            Name = name,
            TypeName = "float",
            IsFixedSizeBuffer = true,
            FixedSize = size
        };

        // ─────────────────────────────────────────────────────────────────────────
        // 1. Model – FieldInfo stores fixed-size buffer metadata
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void FieldInfo_DefaultIsNotFixedSizeBuffer()
        {
            var field = new FieldInfo { Name = "X", TypeName = "int" };
            Assert.False(field.IsFixedSizeBuffer);
            Assert.Equal(0, field.FixedSize);
        }

        [Fact]
        public void FieldInfo_FixedSizeBufferPropertiesStored()
        {
            var field = MakeFixedByteField("Payload", 64);
            Assert.True(field.IsFixedSizeBuffer);
            Assert.Equal(64, field.FixedSize);
            Assert.Equal("byte", field.TypeName);
        }

        [Fact]
        public void FieldInfo_FixedSizeBuffer_IntElement()
        {
            var field = MakeFixedIntField("Data", 16);
            Assert.True(field.IsFixedSizeBuffer);
            Assert.Equal(16, field.FixedSize);
            Assert.Equal("int", field.TypeName);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 2. SchemaDiscovery – Roslyn scanner recognises fixed buffers
        // ─────────────────────────────────────────────────────────────────────────

        private string CreateTempFile(string content)
        {
            var path = Path.Combine(_tempDir, "Src_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void SchemaDiscovery_FixedByteBuffer_IsDetected()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Scan
{
    [DdsTopic(""T"")]
    public unsafe partial struct FixedByteMsg
    {
        [DdsKey] public int Id;
        public unsafe fixed byte Payload[32];
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.SingleOrDefault(t => t.Name == "FixedByteMsg");
            Assert.NotNull(type);

            var payloadField = type!.Fields.SingleOrDefault(f => f.Name == "Payload");
            Assert.NotNull(payloadField);
            Assert.True(payloadField!.IsFixedSizeBuffer, "IsFixedSizeBuffer should be true");
            Assert.Equal(32, payloadField.FixedSize);
            Assert.Equal("byte", payloadField.TypeName);
        }

        [Fact]
        public void SchemaDiscovery_FixedIntBuffer_IsDetected()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Scan
{
    [DdsStruct]
    public unsafe struct IntBufStruct
    {
        public unsafe fixed int Samples[8];
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.SingleOrDefault(t => t.Name == "IntBufStruct");
            Assert.NotNull(type);

            var f = type!.Fields.SingleOrDefault(f2 => f2.Name == "Samples");
            Assert.NotNull(f);
            Assert.True(f!.IsFixedSizeBuffer);
            Assert.Equal(8, f.FixedSize);
            Assert.Equal("int", f.TypeName);
        }

        [Fact]
        public void SchemaDiscovery_FixedFloatBuffer_IsDetected()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Scan
{
    [DdsStruct]
    public unsafe struct FloatBuf
    {
        public unsafe fixed float Matrix[16];
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.SingleOrDefault(t => t.Name == "FloatBuf");
            Assert.NotNull(type);

            var f = type!.Fields.SingleOrDefault(f2 => f2.Name == "Matrix");
            Assert.NotNull(f);
            Assert.True(f!.IsFixedSizeBuffer);
            Assert.Equal(16, f.FixedSize);
            Assert.Equal("float", f.TypeName);
        }

        [Fact]
        public void SchemaDiscovery_RegularFieldNotTaggedAsFixed()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Scan
{
    [DdsStruct]
    public struct NormalStruct
    {
        public int Id;
        public double Value;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.SingleOrDefault(t => t.Name == "NormalStruct");
            Assert.NotNull(type);
            Assert.All(type!.Fields, f => Assert.False(f.IsFixedSizeBuffer));
        }

        [Fact]
        public void SchemaDiscovery_MixedFields_OnlyFixedTagged()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Scan
{
    [DdsTopic(""Mixed"")]
    public unsafe partial struct MixedMsg
    {
        [DdsKey] public int Id;
        public double Value;
        public unsafe fixed byte Buffer[64];
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.SingleOrDefault(t => t.Name == "MixedMsg");
            Assert.NotNull(type);

            var idField   = type!.Fields.Single(f => f.Name == "Id");
            var valField  = type!.Fields.Single(f => f.Name == "Value");
            var bufField  = type!.Fields.Single(f => f.Name == "Buffer");

            Assert.False(idField.IsFixedSizeBuffer);
            Assert.False(valField.IsFixedSizeBuffer);
            Assert.True(bufField.IsFixedSizeBuffer);
            Assert.Equal(64, bufField.FixedSize);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 3. SerializerEmitter – Ghost struct generation
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void GhostStruct_FixedByteBuffer_EmitsFixedKeyword()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 64) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            _output.WriteLine(code);
            Assert.Contains("public fixed byte Payload[64];", code);
        }

        [Fact]
        public void GhostStruct_FixedIntBuffer_EmitsFixedKeyword()
        {
            var type = new TypeInfo
            {
                Name = "IntBuf",
                Namespace = "Ns",
                Fields = { MakeFixedIntField("Data", 16) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("public fixed int Data[16];", code);
        }

        [Fact]
        public void GhostStruct_FixedFloatBuffer_EmitsFixedKeyword()
        {
            var type = new TypeInfo
            {
                Name = "FloatBuf",
                Namespace = "Ns",
                Fields = { MakeFixedFloatField("Matrix", 16) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("public fixed float Matrix[16];", code);
        }

        [Fact]
        public void GhostStruct_FixedBuffer_DoesNotEmitSequenceNative()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 32) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Fixed buffers are inline – must NOT use DdsSequenceNative
            Assert.DoesNotContain("DdsSequenceNative", code);
        }

        [Fact]
        public void GhostStruct_MixedFields_CorrectTypes()
        {
            var type = new TypeInfo
            {
                Name = "Mixed",
                Namespace = "Ns",
                Fields =
                {
                    new FieldInfo { Name = "Id",    TypeName = "int"    },
                    MakeFixedByteField("Buf", 16),
                    new FieldInfo { Name = "Value", TypeName = "double" }
                }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("public int Id;",             code);
            Assert.Contains("public fixed byte Buf[16];", code);
            Assert.Contains("public double Value;",        code);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 4. SerializerEmitter – Fixed buffers must not be treated as dynamic
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Sizer_FixedBuffer_NoDynamicSizeMethod()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Data", 64) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // No GetDynamicSize helper should be generated since there's no dynamic data
            Assert.DoesNotContain("GetDynamicSize", code);
        }

        [Fact]
        public void Sizer_FixedBuffer_SizeIsSizeofNative()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Data", 64) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // GetNativeSize should only use Unsafe.SizeOf and not add dynamic parts
            Assert.Contains("Unsafe.SizeOf<BufMsg_Native>()", code);
            Assert.DoesNotContain("GetDynamicSize", code);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 5. SerializerEmitter – Marshal to native uses Buffer.MemoryCopy
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Marshal_FixedBuffer_EmitsMemoryCopy()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 32) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            _output.WriteLine(code);
            Assert.Contains("System.Buffer.MemoryCopy", code);
            Assert.Contains("sizeof(byte)", code);
            Assert.Contains("32 * sizeof(byte)", code);
        }

        [Fact]
        public void Marshal_FixedIntBuffer_EmitsCorrectSizeof()
        {
            var type = new TypeInfo
            {
                Name = "IntBuf",
                Namespace = "Ns",
                Fields = { MakeFixedIntField("Samples", 8) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("System.Buffer.MemoryCopy", code);
            Assert.Contains("8 * sizeof(int)", code);
        }

        [Fact]
        public void Marshal_FixedBuffer_PinsSourceAndTarget()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 16) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Uses fixed() to pin in/ref parameters so their fixed buffers are addressable
            Assert.Contains("fixed (byte* __src = source.Payload)", code);
            Assert.Contains("fixed (byte* __dst = target.Payload)", code);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 6. SerializerEmitter – Unmarshal from native uses Buffer.MemoryCopy
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Unmarshal_FixedBuffer_EmitsMemoryCopy()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 32) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Verify unmarshal path also contains MemoryCopy (should appear twice: marshal + unmarshal)
            var count = System.Text.RegularExpressions.Regex.Matches(code, "System.Buffer.MemoryCopy").Count;
            Assert.True(count >= 2, $"Expected at least 2 MemoryCopy calls (marshal + unmarshal), found {count}");
        }

        [Fact]
        public void Unmarshal_FixedBuffer_PinsSourceAndTarget()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 16) }
            };

            var code = _serEmitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Unmarshal: fixed() pins the in/ref params (source=native, target=managed)
            Assert.Contains("fixed (byte* __src = source.Payload)", code);
            Assert.Contains("fixed (byte* __dst = target.Payload)", code);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 7. IdlEmitter – Fixed-size buffers map to fixed-length IDL arrays
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void IdlEmitter_FixedByteBuffer_EmitsOctetArray()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "BufMsg", Namespace = "Idl" };
            type.Fields.Add(MakeFixedByteField("Payload", 32));
            registry.RegisterLocal(type, "src.cs", "File1", "Idl");

            var outputDir = Path.Combine(_tempDir, "idl");
            Directory.CreateDirectory(outputDir);

            new IdlEmitter().EmitIdlFiles(registry, outputDir);
            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));

            _output.WriteLine(content);
            // byte -> octet; fixed[32] -> [32] suffix
            Assert.Contains("octet Payload[32]", content);
        }

        [Fact]
        public void IdlEmitter_FixedIntBuffer_EmitsInt32Array()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "IntBuf", Namespace = "Idl" };
            type.Fields.Add(MakeFixedIntField("Data", 8));
            registry.RegisterLocal(type, "src.cs", "File2", "Idl");

            var outputDir = Path.Combine(_tempDir, "idl2");
            Directory.CreateDirectory(outputDir);

            new IdlEmitter().EmitIdlFiles(registry, outputDir);
            var content = File.ReadAllText(Path.Combine(outputDir, "File2.idl"));

            _output.WriteLine(content);
            Assert.Contains("int32 Data[8]", content);
        }

        [Fact]
        public void IdlEmitter_FixedFloatBuffer_EmitsFloatArray()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "FloatBuf", Namespace = "Idl" };
            type.Fields.Add(MakeFixedFloatField("Matrix", 16));
            registry.RegisterLocal(type, "src.cs", "File3", "Idl");

            var outputDir = Path.Combine(_tempDir, "idl3");
            Directory.CreateDirectory(outputDir);

            new IdlEmitter().EmitIdlFiles(registry, outputDir);
            var content = File.ReadAllText(Path.Combine(outputDir, "File3.idl"));

            _output.WriteLine(content);
            Assert.Contains("float Matrix[16]", content);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 8. ViewEmitter – Fixed-size buffers generate ReadOnlySpan properties
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ViewEmitter_FixedByteBuffer_EmitsReadOnlySpan()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 64) }
            };

            var code = _viewEmitter.EmitViewStruct(type, null);

            _output.WriteLine(code);
            Assert.Contains("ReadOnlySpan<byte>", code);
            Assert.Contains("Payload", code);
            Assert.Contains("64", code);
        }

        [Fact]
        public void ViewEmitter_FixedIntBuffer_EmitsReadOnlySpanInt()
        {
            var type = new TypeInfo
            {
                Name = "IntBuf",
                Namespace = "Ns",
                Fields = { MakeFixedIntField("Data", 8) }
            };

            var code = _viewEmitter.EmitViewStruct(type, null);

            Assert.Contains("ReadOnlySpan<int>", code);
        }

        [Fact]
        public void ViewEmitter_FixedBuffer_EmitsSpanCopyToInToManaged()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 32) }
            };

            var code = _viewEmitter.EmitViewStruct(type, null);

            _output.WriteLine(code);
            // ToManaged uses a named pointer variable + element-by-element loop (avoids CS0131)
            Assert.Contains("var __srcSpan = this.Payload", code);
            Assert.Contains("byte* __dstPtr = target.Payload", code);
            Assert.Contains("for (int __i = 0; __i < 32; __i++)", code);
            Assert.Contains("__dstPtr[__i] = __srcSpan[__i]", code);
        }

        [Fact]
        public void ViewEmitter_FixedBuffer_NotTreatedAsSequence()
        {
            var type = new TypeInfo
            {
                Name = "BufMsg",
                Namespace = "Ns",
                Fields = { MakeFixedByteField("Payload", 16) }
            };

            var code = _viewEmitter.EmitViewStruct(type, null);

            // Should NOT generate sequence-style access (Count, GetXxx)
            Assert.DoesNotContain("PayloadCount", code);
            Assert.DoesNotContain("GetPayload(", code);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 9. Compilation tests – generated code must compile cleanly
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void GeneratedSerializer_FixedByteBuffer_Compiles()
        {
            var type = new TypeInfo
            {
                Name = "FbMsg",
                Namespace = "FbNs",
                Fields = { new FieldInfo { Name = "Id", TypeName = "int" }, MakeFixedByteField("Payload", 16) }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Fb", "FbNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace FbNs
{
    public unsafe partial struct FbMsg
    {
        public int Id;
        public unsafe fixed byte Payload[16];
    }
}";

            _output.WriteLine(serCode);
            var assembly = CompileToAssembly("FbSerializerAsm", userStruct, serCode);
            Assert.NotNull(assembly);
        }

        [Fact]
        public void GeneratedSerializer_FixedIntBuffer_Compiles()
        {
            var type = new TypeInfo
            {
                Name = "IntBufMsg",
                Namespace = "FbNs",
                Fields = { MakeFixedIntField("Samples", 8) }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "IntFb", "FbNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace FbNs
{
    public unsafe partial struct IntBufMsg
    {
        public unsafe fixed int Samples[8];
    }
}";

            var assembly = CompileToAssembly("FbIntSerializerAsm", userStruct, serCode);
            Assert.NotNull(assembly);
        }

        [Fact]
        public void GeneratedView_FixedByteBuffer_Compiles()
        {
            var type = new TypeInfo
            {
                Name = "FbViewMsg",
                Namespace = "FbvNs",
                Fields = { new FieldInfo { Name = "Id", TypeName = "int" }, MakeFixedByteField("Payload", 32) }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Fbv", "FbvNs");

            var serCode  = _serEmitter.EmitSerializer(type, registry);
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);

            var userStruct = @"
namespace FbvNs
{
    public unsafe partial struct FbViewMsg
    {
        public int Id;
        public unsafe fixed byte Payload[32];
    }
}";

            _output.WriteLine("=== Serializer ===");
            _output.WriteLine(serCode);
            _output.WriteLine("=== View ===");
            _output.WriteLine(viewCode);

            var assembly = CompileToAssembly("FbViewAsm", userStruct, serCode, viewCode);
            Assert.NotNull(assembly);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 10. End-to-end roundtrip – marshal and unmarshal must preserve all bytes
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_FixedByteBuffer_PreservesAllBytes()
        {
            var type = new TypeInfo
            {
                Name = "RtMsg",
                Namespace = "RtNs",
                Fields =
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    MakeFixedByteField("Payload", 8)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Rt", "RtNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace RtNs
{
    public unsafe partial struct RtMsg
    {
        public int Id;
        public unsafe fixed byte Payload[8];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using RtNs;

namespace RtNs
{
    public static class RtRunner
    {
        public static unsafe string Run()
        {
            try
            {
                var input = new RtMsg();
                input.Id = 42;
                {
                    byte* p = input.Payload;
                    for (int i = 0; i < 8; i++) p[i] = (byte)(10 + i);
                }

                byte[] buffer = new byte[512];
                RtMsg_Native native = default;

                fixed (byte* bufPtr = buffer)
                {
                    int headSize = Unsafe.SizeOf<RtMsg_Native>();
                    var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)bufPtr, headSize);
                    RtMsg.MarshalToNative(in input, ref native, ref arena);
                }

                // Verify native struct
                if (native.Id != 42)
                    return $""Id mismatch: got {native.Id}"";

                for (int i = 0; i < 8; i++)
                {
                    if (native.Payload[i] != (byte)(10 + i))
                        return $""native.Payload[{i}] mismatch: expected {10 + i}, got {native.Payload[i]}"";
                }

                // Unmarshal back
                var output = new RtMsg();
                RtMsg.MarshalFromNative(ref output, in native);

                if (output.Id != 42)
                    return $""Roundtrip Id mismatch: got {output.Id}"";

                {
                    byte* p = output.Payload;
                    for (int i = 0; i < 8; i++)
                    {
                        if (p[i] != (byte)(10 + i))
                            return $""roundtrip Payload[{i}]: expected {10 + i}, got {p[i]}"";
                    }
                }

                return ""SUCCESS"";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}";

            _output.WriteLine(serCode);

            var asm    = CompileToAssembly("RtAsm", userStruct, serCode, testRunner);
            var runner = asm.GetType("RtNs.RtRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void Roundtrip_FixedIntBuffer_PreservesAllValues()
        {
            var type = new TypeInfo
            {
                Name = "IntRtMsg",
                Namespace = "IntRtNs",
                Fields =
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    MakeFixedIntField("Samples", 4)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "IntRt", "IntRtNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace IntRtNs
{
    public unsafe partial struct IntRtMsg
    {
        public int Id;
        public unsafe fixed int Samples[4];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using IntRtNs;

namespace IntRtNs
{
    public static class IntRtRunner
    {
        public static unsafe string Run()
        {
            try
            {
                var input = new IntRtMsg();
                input.Id = 7;
                {
                    int* p = input.Samples;
                    p[0] = 100; p[1] = 200; p[2] = 300; p[3] = 400;
                }

                byte[] buffer = new byte[512];
                IntRtMsg_Native native = default;

                fixed (byte* bufPtr = buffer)
                {
                    int headSize = Unsafe.SizeOf<IntRtMsg_Native>();
                    var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)bufPtr, headSize);
                    IntRtMsg.MarshalToNative(in input, ref native, ref arena);
                }

                var output = new IntRtMsg();
                IntRtMsg.MarshalFromNative(ref output, in native);

                if (output.Id != 7) return $""Id mismatch: {output.Id}"";

                {
                    int* p = output.Samples;
                    int[] expected = { 100, 200, 300, 400 };
                    for (int i = 0; i < 4; i++)
                        if (p[i] != expected[i]) return $""Samples[{i}]: expected {expected[i]}, got {p[i]}"";
                }

                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}";

            var asm    = CompileToAssembly("IntRtAsm", userStruct, serCode, testRunner);
            var runner = asm.GetType("IntRtNs.IntRtRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void Roundtrip_MixedFields_AllValuesPreserved()
        {
            var type = new TypeInfo
            {
                Name = "MixMsg",
                Namespace = "MixNs",
                Fields =
                {
                    new FieldInfo { Name = "Id",    TypeName = "int"    },
                    new FieldInfo { Name = "Value", TypeName = "double" },
                    MakeFixedByteField("Tag", 4)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Mix", "MixNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace MixNs
{
    public unsafe partial struct MixMsg
    {
        public int Id;
        public double Value;
        public unsafe fixed byte Tag[4];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using MixNs;

namespace MixNs
{
    public static class MixRunner
    {
        public static unsafe string Run()
        {
            try
            {
                var input = new MixMsg();
                input.Id = 99;
                input.Value = 3.14;
                {
                    byte* p = input.Tag;
                    p[0] = 0xDE; p[1] = 0xAD; p[2] = 0xBE; p[3] = 0xEF;
                }

                byte[] buffer = new byte[512];
                MixMsg_Native native = default;
                fixed (byte* bufPtr = buffer)
                {
                    int headSize = Unsafe.SizeOf<MixMsg_Native>();
                    var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)bufPtr, headSize);
                    MixMsg.MarshalToNative(in input, ref native, ref arena);
                }

                var output = new MixMsg();
                MixMsg.MarshalFromNative(ref output, in native);

                if (output.Id != 99)      return $""Id: expected 99 got {output.Id}"";
                if (Math.Abs(output.Value - 3.14) > 0.0001) return $""Value: expected 3.14 got {output.Value}"";

                {
                    byte* p = output.Tag;
                    if (p[0] != 0xDE) return $""Tag[0]: expected 0xDE"";
                    if (p[1] != 0xAD) return $""Tag[1]: expected 0xAD"";
                    if (p[2] != 0xBE) return $""Tag[2]: expected 0xBE"";
                    if (p[3] != 0xEF) return $""Tag[3]: expected 0xEF"";
                }

                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}";

            var asm    = CompileToAssembly("MixAsm", userStruct, serCode, testRunner);
            var runner = asm.GetType("MixNs.MixRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void Roundtrip_ZeroBuffer_AllBytesZero()
        {
            var type = new TypeInfo
            {
                Name = "ZeroMsg",
                Namespace = "ZeroNs",
                Fields =
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    MakeFixedByteField("Data", 8)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Zero", "ZeroNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace ZeroNs
{
    public unsafe partial struct ZeroMsg
    {
        public int Id;
        public unsafe fixed byte Data[8];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using ZeroNs;

namespace ZeroNs
{
    public static class ZeroRunner
    {
        public static unsafe string Run()
        {
            try
            {
                // Default-initialized: all bytes should be zero
                var input = new ZeroMsg();
                input.Id = 1;
                // Payload stays zeroed (default)

                byte[] buffer = new byte[512];
                ZeroMsg_Native native = default;
                fixed (byte* bufPtr = buffer)
                {
                    int headSize = Unsafe.SizeOf<ZeroMsg_Native>();
                    var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)bufPtr, headSize);
                    ZeroMsg.MarshalToNative(in input, ref native, ref arena);
                }

                var output = new ZeroMsg();
                ZeroMsg.MarshalFromNative(ref output, in native);

                {
                    byte* p = output.Data;
                    for (int i = 0; i < 8; i++)
                        if (p[i] != 0) return $""Data[{i}] should be 0, got {p[i]}"";
                }

                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}";

            var asm    = CompileToAssembly("ZeroAsm", userStruct, serCode, testRunner);
            var runner = asm.GetType("ZeroNs.ZeroRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 11. View roundtrip – ToManaged from View copies correctly
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ViewRoundtrip_FixedByteBuffer_ToManagedCopiesCorrectly()
        {
            var type = new TypeInfo
            {
                Name = "VrMsg",
                Namespace = "VrNs",
                Fields =
                {
                    new FieldInfo { Name = "Id",      TypeName = "int" },
                    MakeFixedByteField("Payload", 8)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Vr", "VrNs");

            var serCode  = _serEmitter.EmitSerializer(type, registry);
            var viewCode = _viewEmitter.EmitViewStruct(type, registry);

            var userStruct = @"
namespace VrNs
{
    public unsafe partial struct VrMsg
    {
        public int Id;
        public unsafe fixed byte Payload[8];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using VrNs;

namespace VrNs
{
    public static class VrRunner
    {
        public static unsafe string Run()
        {
            try
            {
                // Build native struct directly
                VrMsg_Native native = default;
                native.Id = 55;
                native.Payload[0] = 0xAA;
                native.Payload[1] = 0xBB;
                native.Payload[2] = 0xCC;
                native.Payload[3] = 0xDD;
                native.Payload[4] = 0x01;
                native.Payload[5] = 0x02;
                native.Payload[6] = 0x03;
                native.Payload[7] = 0x04;

                // Create View from pointer and call ToManaged
                var view   = new VrMsgView(&native);
                var managed = view.ToManaged();

                if (managed.Id != 55)
                    return $""Id: expected 55 got {managed.Id}"";

                {
                    byte* p = managed.Payload;
                    byte[] expected = { 0xAA, 0xBB, 0xCC, 0xDD, 0x01, 0x02, 0x03, 0x04 };
                    for (int i = 0; i < 8; i++)
                        if (p[i] != expected[i])
                            return $""Payload[{i}]: expected 0x{expected[i]:X2}, got 0x{p[i]:X2}"";
                }

                // Also test ReadOnlySpan property
                var span = view.Payload;
                for (int i = 0; i < 8; i++)
                    if (span[i] != native.Payload[i])
                        return $""Span[{i}]: mismatch"";

                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}";

            _output.WriteLine("=== Serializer ===");
            _output.WriteLine(serCode);
            _output.WriteLine("=== View ===");
            _output.WriteLine(viewCode);

            var asm    = CompileToAssembly("VrAsm", userStruct, serCode, viewCode, testRunner);
            var runner = asm.GetType("VrNs.VrRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 12. Multiple fixed buffers in same struct
        // ─────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Roundtrip_MultipleFixedBuffers_AllPreserved()
        {
            var type = new TypeInfo
            {
                Name = "MultiMsg",
                Namespace = "MultiNs",
                Fields =
                {
                    new FieldInfo { Name = "Id",   TypeName = "int"   },
                    MakeFixedByteField("TagA",  4),
                    MakeFixedByteField("TagB",  4),
                    MakeFixedIntField ("Nums",  2)
                }
            };

            var registry = new GlobalTypeRegistry();
            registry.RegisterLocal(type, "src.cs", "Multi", "MultiNs");

            var serCode = _serEmitter.EmitSerializer(type, registry);

            var userStruct = @"
namespace MultiNs
{
    public unsafe partial struct MultiMsg
    {
        public int Id;
        public unsafe fixed byte TagA[4];
        public unsafe fixed byte TagB[4];
        public unsafe fixed int  Nums[2];
    }
}";

            var testRunner = @"
using System;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using MultiNs;

namespace MultiNs
{
    public static class MultiRunner
    {
        public static unsafe string Run()
        {
            try
            {
                var input = new MultiMsg();
                input.Id = 11;
                { byte* a = input.TagA; a[0]=1; a[1]=2; a[2]=3; a[3]=4; }
                { byte* b = input.TagB; b[0]=5; b[1]=6; b[2]=7; b[3]=8; }
                { int*  n = input.Nums; n[0]=1000; n[1]=2000; }

                byte[] buffer = new byte[512];
                MultiMsg_Native native = default;
                fixed (byte* bufPtr = buffer)
                {
                    var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)bufPtr, Unsafe.SizeOf<MultiMsg_Native>());
                    MultiMsg.MarshalToNative(in input, ref native, ref arena);
                }

                var output = new MultiMsg();
                MultiMsg.MarshalFromNative(ref output, in native);

                if (output.Id != 11) return $""Id: {output.Id}"";
                {
                    byte* a = output.TagA;
                    if (a[0]!=1||a[1]!=2||a[2]!=3||a[3]!=4) return ""TagA mismatch"";
                }
                {
                    byte* b = output.TagB;
                    if (b[0]!=5||b[1]!=6||b[2]!=7||b[3]!=8) return ""TagB mismatch"";
                }
                {
                    int* n = output.Nums;
                    if (n[0]!=1000||n[1]!=2000) return $""Nums mismatch: {n[0]} {n[1]}"";
                }

                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}";

            var asm    = CompileToAssembly("MultiAsm", userStruct, serCode, testRunner);
            var runner = asm.GetType("MultiNs.MultiRunner")!;
            var result = (string)runner.GetMethod("Run")!.Invoke(null, null)!;

            _output.WriteLine("Result: " + result);
            Assert.Equal("SUCCESS", result);
        }
    }
}
