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
        public void SerializerEmitter_ShortEnum_NativeStructUsesInt()
        {
            // Even for a 16-bit @bit_bound enum, idlc generates a 32-bit int in the native struct.
            // @bit_bound only affects the CDR wire format, not the in-memory ABI layout.
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

            // The ghost native struct must store the enum as int (32-bit) to match idlc ABI.
            Assert.Contains("public int Priority;", code);
            // The marshal code must cast to (int) — always 32-bit.
            Assert.Contains("(int)source.Priority", code);
            // No narrow ushort or byte fields should appear.
            Assert.DoesNotContain("public ushort Priority;", code);
            Assert.DoesNotContain("(ushort)source.Priority", code);
        }

        [Fact]
        public void SerializerEmitter_ByteEnum_NativeStructUsesInt()
        {
            // Even for an 8-bit @bit_bound enum, idlc generates a 32-bit int in the native struct.
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

            // Ghost struct uses int for the native field — not byte.
            Assert.Contains("public int Status;", code);
            // Marshal uses (int) cast.
            Assert.Contains("(int)source.Status", code);
            // No narrow byte field.
            Assert.DoesNotContain("public byte Status;", code);
            Assert.DoesNotContain("(byte)source.Status", code);
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

        // ─────────────────────────────────────────────────────────────────────
        // NativeType alignment tests — all bit-bound sizes must produce 4-byte
        // aligned int fields to match the idlc in-memory ABI.
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_AllBitBounds_NativeStructAlwaysUsesInt(int bitBound)
        {
            var enumType = new TypeInfo { Name = "EVal", Namespace = "T", IsEnum = true, EnumBitBound = bitBound };
            var parent = new TypeInfo
            {
                Name = "S",
                Namespace = "T",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Val", TypeName = "T.EVal", Type = enumType }
                }
            };

            string code = new SerializerEmitter().EmitSerializer(parent, new GlobalTypeRegistry());

            // Regardless of bit bound the native field must be int (32-bit).
            Assert.Contains("public int Val;", code);
            Assert.DoesNotContain("public byte Val;",   code);
            Assert.DoesNotContain("public ushort Val;", code);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_AllBitBounds_MarshalCastIsAlwaysInt(int bitBound)
        {
            var enumType = new TypeInfo { Name = "EVal", Namespace = "T", IsEnum = true, EnumBitBound = bitBound };
            var parent = new TypeInfo
            {
                Name = "S",
                Namespace = "T",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Val", TypeName = "T.EVal", Type = enumType }
                }
            };

            string code = new SerializerEmitter().EmitSerializer(parent, new GlobalTypeRegistry());

            // Marshal path must use (int) cast regardless of declared bit bound.
            Assert.Contains("(int)source.Val", code);
            Assert.DoesNotContain("(byte)source.Val",   code);
            Assert.DoesNotContain("(ushort)source.Val", code);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Union discriminator tests — enum used as discriminator
        // ─────────────────────────────────────────────────────────────────────

        private static TypeInfo BuildUnionWithEnumDiscriminator(int bitBound)
        {
            var discEnum = new TypeInfo { Name = "EDisc", Namespace = "U", IsEnum = true, EnumBitBound = bitBound };

            var union = new TypeInfo
            {
                Name = "MyUnion",
                Namespace = "U",
                IsUnion = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo
                    {
                        Name = "_d",
                        TypeName = "U.EDisc",
                        Type = discEnum,
                        Attributes = new System.Collections.Generic.List<AttributeInfo>
                            { new AttributeInfo { Name = "DdsDiscriminator" } }
                    },
                    new FieldInfo
                    {
                        Name = "IntVal",
                        TypeName = "int",
                        Attributes = new System.Collections.Generic.List<AttributeInfo>
                        {
                            new AttributeInfo
                            {
                                Name = "DdsCase",
                                Arguments = new System.Collections.Generic.List<object> { 0 }
                            }
                        }
                    }
                }
            };
            union.Attributes.Add(new AttributeInfo { Name = "DdsUnion" });
            return union;
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_UnionEnumDiscriminator_NativeDiscFieldIsAlwaysInt(int bitBound)
        {
            // The native _d field in the union ghost struct must always be int regardless of the
            // enum discriminator's bit bound, matching the idlc in-memory ABI.
            var union = BuildUnionWithEnumDiscriminator(bitBound);
            string code = new SerializerEmitter().EmitSerializer(union, new GlobalTypeRegistry());

            Assert.Contains("public int _d;", code);
            Assert.DoesNotContain("public byte _d;",   code);
            Assert.DoesNotContain("public ushort _d;", code);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_UnionEnumDiscriminator_DiscriminatorOffsetIs4(int bitBound)
        {
            // Because _d is always int (4 bytes) the union payload must start at offset 4
            // (or higher if the payload alignment requires it).
            var union = BuildUnionWithEnumDiscriminator(bitBound);
            string code = new SerializerEmitter().EmitSerializer(union, new GlobalTypeRegistry());

            // Verify the discriminator field is at offset 0 and payload at offset 4.
            Assert.Contains("[FieldOffset(0)]", code);
            // The int payload has 4-byte alignment → rounded up offset is 4.
            Assert.Contains("[FieldOffset(4)]", code);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_UnionEnumDiscriminator_MarshalCastIsAlwaysInt(int bitBound)
        {
            // The marshal path for the discriminator must cast to (int), not (byte)/(ushort).
            var union = BuildUnionWithEnumDiscriminator(bitBound);
            string code = new SerializerEmitter().EmitSerializer(union, new GlobalTypeRegistry());

            Assert.Contains("(int)source._d", code);
            Assert.DoesNotContain("(byte)source._d",   code);
            Assert.DoesNotContain("(ushort)source._d", code);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Union arm tests — enum used as a union arm payload field
        // ─────────────────────────────────────────────────────────────────────

        private static TypeInfo BuildUnionWithEnumArm(int bitBound)
        {
            var armEnum = new TypeInfo { Name = "EArm", Namespace = "U", IsEnum = true, EnumBitBound = bitBound };

            var union = new TypeInfo
            {
                Name = "ArmUnion",
                Namespace = "U",
                IsUnion = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo
                    {
                        Name = "_d",
                        TypeName = "int",
                        Attributes = new System.Collections.Generic.List<AttributeInfo>
                            { new AttributeInfo { Name = "DdsDiscriminator" } }
                    },
                    new FieldInfo
                    {
                        Name = "ArmVal",
                        TypeName = "U.EArm",
                        Type = armEnum,
                        Attributes = new System.Collections.Generic.List<AttributeInfo>
                        {
                            new AttributeInfo
                            {
                                Name = "DdsCase",
                                Arguments = new System.Collections.Generic.List<object> { 0 }
                            }
                        }
                    }
                }
            };
            union.Attributes.Add(new AttributeInfo { Name = "DdsUnion" });
            return union;
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_UnionEnumArm_NativeArmFieldIsAlwaysInt(int bitBound)
        {
            // Even when a union arm holds a narrow enum, the native arm field in the ghost struct
            // must be int (32-bit) to match the idlc in-memory ABI.
            var union = BuildUnionWithEnumArm(bitBound);
            string code = new SerializerEmitter().EmitSerializer(union, new GlobalTypeRegistry());

            Assert.Contains("public int ArmVal;", code);
            Assert.DoesNotContain("public byte ArmVal;",   code);
            Assert.DoesNotContain("public ushort ArmVal;", code);
        }

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_UnionEnumArm_MarshalCastIsAlwaysInt(int bitBound)
        {
            var union = BuildUnionWithEnumArm(bitBound);
            string code = new SerializerEmitter().EmitSerializer(union, new GlobalTypeRegistry());

            Assert.Contains("(int)source.ArmVal", code);
            Assert.DoesNotContain("(byte)source.ArmVal",   code);
            Assert.DoesNotContain("(ushort)source.ArmVal", code);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Roundtrip correctness — enum values survive native struct conversion
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(32)]
        public void SerializerEmitter_AllBitBounds_UnmarshalCastDoesNotUseNarrowType(int bitBound)
        {
            // When reading back from the native struct the generated unmarshal code must cast
            // directly through int (not byte/ushort) to avoid sign-extension / truncation bugs.
            var enumType = new TypeInfo { Name = "EVal", Namespace = "T", IsEnum = true, EnumBitBound = bitBound };
            var parent = new TypeInfo
            {
                Name = "S",
                Namespace = "T",
                IsStruct = true,
                Fields = new System.Collections.Generic.List<FieldInfo>
                {
                    new FieldInfo { Name = "Val", TypeName = "T.EVal", Type = enumType }
                }
            };

            string code = new SerializerEmitter().EmitSerializer(parent, new GlobalTypeRegistry());

            // Unmarshal must NOT use narrow intermediate casts.
            Assert.DoesNotContain("(byte)source.Val",   code);
            Assert.DoesNotContain("(ushort)source.Val", code);
            // Should cast directly: (T.EVal)source.Val
            Assert.Contains("(T.EVal)source.Val", code);
        }
    }
}
