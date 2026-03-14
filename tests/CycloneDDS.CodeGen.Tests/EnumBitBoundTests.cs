using System.Collections.Generic;
using System.IO;
using System;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// ME1-T01: Tests for @bit_bound IDL annotation and narrow serializer widths for typed enums.
    /// </summary>
    public class EnumBitBoundTests : IDisposable
    {
        private readonly string _tempDir;

        public EnumBitBoundTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "CG_BB_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private string CreateTempFile(string content)
        {
            var path = Path.Combine(_tempDir, "Src_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TypeInfo model — EnumBitBound property
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void TypeInfo_EnumBitBound_DefaultIs32()
        {
            var type = new TypeInfo { Name = "EFoo", IsEnum = true };
            Assert.Equal(32, type.EnumBitBound);
        }

        [Fact]
        public void TypeInfo_EnumBitBound_CanBeSet()
        {
            var type = new TypeInfo { Name = "EByte", IsEnum = true, EnumBitBound = 8 };
            Assert.Equal(8, type.EnumBitBound);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SchemaDiscovery — reads underlying enum type via Roslyn
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SchemaDiscovery_ByteBackedEnum_EnumBitBound8()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Me1
{
    public enum EStatus : byte { Active, Idle }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var t = Assert.Single(types, x => x.Name == "EStatus");
            Assert.Equal(8, t.EnumBitBound);
        }

        [Fact]
        public void SchemaDiscovery_ShortBackedEnum_EnumBitBound16()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Me1
{
    public enum EPriority : short { Low, Medium, High }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var t = Assert.Single(types, x => x.Name == "EPriority");
            Assert.Equal(16, t.EnumBitBound);
        }

        [Fact]
        public void SchemaDiscovery_DefaultEnum_EnumBitBound32()
        {
            CreateTempFile(@"
using CycloneDDS.Schema;
namespace Me1
{
    public enum EKind { Alpha, Beta }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var t = Assert.Single(types, x => x.Name == "EKind");
            Assert.Equal(32, t.EnumBitBound);
        }

        // ─────────────────────────────────────────────────────────────────────
        // IdlEmitter — @bit_bound annotation
        // Success condition 1: @bit_bound(8) emitted for byte-backed enum
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void IdlEmitter_ByteEnum_EmitsBitBound8()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo
            {
                Name = "EStatus",
                Namespace = "Me1",
                IsEnum = true,
                EnumBitBound = 8,
                EnumMembers = new System.Collections.Generic.List<string> { "Active", "Idle" }
            };
            registry.RegisterLocal(type, "src.cs", "File1", "Me1");

            var outputDir = Path.Combine(_tempDir, "idl_out");
            Directory.CreateDirectory(outputDir);
            var emitter = new IdlEmitter();
            emitter.EmitIdlFiles(registry, outputDir);

            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));

            // Structural assertion: @bit_bound(8) must appear immediately before the enum declaration
            var lines = content.Replace("\r", "").Split('\n');
            int enumIdx = System.Array.FindIndex(lines, l => l.Contains("enum EStatus"));
            Assert.True(enumIdx > 0, "enum EStatus not found in IDL output");
            // The line before the enum declaration (or one of the preceding lines after any modules) should have @bit_bound(8)
            bool foundBitBound8 = false;
            for (int i = enumIdx - 1; i >= 0 && i >= enumIdx - 5; i--)
            {
                if (lines[i].Contains("@bit_bound(8)")) { foundBitBound8 = true; break; }
            }
            Assert.True(foundBitBound8, $"Expected @bit_bound(8) before enum EStatus. IDL:\n{content}");
        }

        [Fact]
        public void IdlEmitter_ShortEnum_EmitsBitBound16()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo
            {
                Name = "EPriority",
                Namespace = "Me1",
                IsEnum = true,
                EnumBitBound = 16,
                EnumMembers = new System.Collections.Generic.List<string> { "Low", "High" }
            };
            registry.RegisterLocal(type, "src.cs", "File1", "Me1");

            var outputDir = Path.Combine(_tempDir, "idl_short");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);
            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));

            Assert.Contains("@bit_bound(16)", content);
            Assert.Contains("enum EPriority", content);
        }

        // Success condition 2: no @bit_bound for 32-bit default enums
        [Fact]
        public void IdlEmitter_DefaultEnum_NoBitBoundAnnotation()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo
            {
                Name = "EKind",
                Namespace = "Me1",
                IsEnum = true,
                EnumBitBound = 32,      // default
                EnumMembers = new System.Collections.Generic.List<string> { "Alpha", "Beta" }
            };
            registry.RegisterLocal(type, "src.cs", "File1", "Me1");

            var outputDir = Path.Combine(_tempDir, "idl_def");
            Directory.CreateDirectory(outputDir);
            new IdlEmitter().EmitIdlFiles(registry, outputDir);
            var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));

            Assert.DoesNotContain("@bit_bound", content);
            Assert.Contains("enum EKind", content);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SerializerEmitter — narrow casts in marshal / unmarshal
        // Success condition 3: ushort cast for short-backed enum field
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SerializerEmitter_ShortEnum_NativeStructUsesUshort()
        {
            // Ghost struct for a struct using a short-backed enum field should have `public ushort`.
            var enumType = new TypeInfo { Name = "EPriority", Namespace = "Me1", IsEnum = true, EnumBitBound = 16 };
            var parent = new TypeInfo
            {
                Name = "Packet",
                Namespace = "Me1",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Priority", TypeName = "Me1.EPriority", Type = enumType }
                }
            };

            var registry = new GlobalTypeRegistry();
            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(parent, registry);

            // The ghost native struct must store the enum as ushort (16-bit).
            Assert.Contains("public ushort Priority;", code);
            // The marshal code must cast to (ushort) — the "narrow write".
            Assert.Contains("(ushort)source.Priority", code);
            // The unmarshal code must cast back through (ushort) before re-casting to the enum type.
            Assert.Contains("(ushort)source.Priority", code);
            // Ensure no 32-bit int cast appears for this field.
            Assert.DoesNotContain("(int)source.Priority", code);
        }

        [Fact]
        public void SerializerEmitter_ByteEnum_NativeStructUsesByte()
        {
            var enumType = new TypeInfo { Name = "EStatus", Namespace = "Me1", IsEnum = true, EnumBitBound = 8 };
            var parent = new TypeInfo
            {
                Name = "Msg",
                Namespace = "Me1",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Status", TypeName = "Me1.EStatus", Type = enumType }
                }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(parent, new GlobalTypeRegistry());

            // Ghost struct uses byte for the native field.
            Assert.Contains("public byte Status;", code);
            // Marshal uses (byte) cast — the "narrow write".
            Assert.Contains("(byte)source.Status", code);
        }

        [Fact]
        public void SerializerEmitter_DefaultEnum_NativeStructUsesInt()
        {
            var enumType = new TypeInfo { Name = "EKind", Namespace = "Me1", IsEnum = true, EnumBitBound = 32 };
            var parent = new TypeInfo
            {
                Name = "Record",
                Namespace = "Me1",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Kind", TypeName = "Me1.EKind", Type = enumType }
                }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(parent, new GlobalTypeRegistry());

            // Ghost struct uses int for default 32-bit enum.
            Assert.Contains("public int Kind;", code);
            // Marshal uses (int) cast.
            Assert.Contains("(int)source.Kind", code);
            // No ushort or byte involved.
            Assert.DoesNotContain("(ushort)source.Kind", code);
            Assert.DoesNotContain("(byte)source.Kind", code);
        }
    }
}
