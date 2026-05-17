using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// Tests for the Tier 2 custom formatter (DdsTypeFormat attribute → ToString + GetFormatTokens).
    /// </summary>
    public class SerializerEmitterTests_CustomFormatter : CodeGenTestBase, IDisposable
    {
        private readonly string _tempDir;

        public SerializerEmitterTests_CustomFormatter()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "DdsCodeGenFmt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        // ─────────────────────────────────────────────────────────────────
        // SerializerEmitter: code generation unit tests
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public void EmitCustomFormatter_GeneratesToString_WithFormatStrings()
        {
            var type = new TypeInfo
            {
                Name = "DisEntityId",
                Namespace = "TestNs",
                FormatTemplate = "{Site:D:Number}:{App:D:Number}:{Entity:X8:Keyword}",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Site",   TypeName = "ushort" },
                    new FieldInfo { Name = "App",    TypeName = "ushort" },
                    new FieldInfo { Name = "Entity", TypeName = "uint"   },
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // The generated ToString() should use C# string interpolation with format specifiers.
            Assert.Contains("public override string ToString() => $\"{Site:D}:{App:D}:{Entity:X8}\";", code);
        }

        [Fact]
        public void EmitCustomFormatter_GeneratesGetFormatTokens_WithCorrectTokenTypes()
        {
            var type = new TypeInfo
            {
                Name = "DisEntityId",
                Namespace = "TestNs",
                FormatTemplate = "{Site:D:Number}:{App:D:Number}:{Entity:X8:Keyword}",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Site",   TypeName = "ushort" },
                    new FieldInfo { Name = "App",    TypeName = "ushort" },
                    new FieldInfo { Name = "Entity", TypeName = "uint"   },
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("GetFormatTokens()", code);
            Assert.Contains("TokenType.Number", code);
            Assert.Contains("TokenType.Keyword", code);
            Assert.Contains("TokenType.Punctuation", code);
            Assert.Contains("this.Site.ToString(\"D\")", code);
            Assert.Contains("this.App.ToString(\"D\")", code);
            Assert.Contains("this.Entity.ToString(\"X8\")", code);
        }

        [Fact]
        public void EmitCustomFormatter_EmitsLiteralsAsPunctuation()
        {
            var type = new TypeInfo
            {
                Name = "Vec3",
                Namespace = "TestNs",
                FormatTemplate = "[{X:0.00:Number}, {Y:0.00:Number}, {Z:0.00:Number}]",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "X", TypeName = "float" },
                    new FieldInfo { Name = "Y", TypeName = "float" },
                    new FieldInfo { Name = "Z", TypeName = "float" },
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Opening bracket is punctuation
            Assert.Contains("\"[\"", code);
            Assert.Contains("TokenType.Punctuation", code);
            // Closing bracket is punctuation
            Assert.Contains("\"]\"", code);
        }

        [Fact]
        public void EmitCustomFormatter_FieldWithNoFormatString_UsesToStringWithoutArg()
        {
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Namespace = "TestNs",
                FormatTemplate = "{Value::Number}",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Value", TypeName = "int" },
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Empty format string should call ToString() without arguments
            Assert.Contains("this.Value.ToString()", code);
        }

        [Fact]
        public void EmitCustomFormatter_FieldWithNoTokenType_UsesDefault()
        {
            var type = new TypeInfo
            {
                Name = "TestStruct",
                Namespace = "TestNs",
                FormatTemplate = "{Value:D}",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Value", TypeName = "int" },
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // No explicit TokenType → should use TokenType.Default
            Assert.Contains("TokenType.Default", code);
        }

        [Fact]
        public void EmitCustomFormatter_NotEmitted_WhenFormatTemplateIsNull()
        {
            var type = new TypeInfo
            {
                Name = "Plain",
                Namespace = "TestNs",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" }
                }
                // FormatTemplate intentionally left null
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.DoesNotContain("GetFormatTokens", code);
            Assert.DoesNotContain("override string ToString", code);
        }

        [Fact]
        public void EmitCustomFormatter_IncludesSystemCollectionsGenericUsing()
        {
            var type = new TypeInfo
            {
                Name = "Tagged",
                Namespace = "TestNs",
                FormatTemplate = "{Id:D:Number}",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" }
                }
            };

            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("using System.Collections.Generic;", code);
        }

        // ─────────────────────────────────────────────────────────────────
        // SchemaDiscovery: DdsTypeFormat attribute extraction
        // ─────────────────────────────────────────────────────────────────

        private string CreateFile(string content)
        {
            var path = Path.Combine(_tempDir, "File_" + Guid.NewGuid().ToString("N") + ".cs");
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void SchemaDiscovery_ExtractsFormatTemplate_FromDdsTypeFormat()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsStruct]
    [DdsTypeFormat(""{Site:D:Number}:{App:D:Number}"")]
    public struct EntityId
    {
        public ushort Site;
        public ushort App;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.FirstOrDefault(t => t.Name == "EntityId");
            Assert.NotNull(type);
            Assert.Equal("{Site:D:Number}:{App:D:Number}", type.FormatTemplate);
        }

        [Fact]
        public void SchemaDiscovery_FormatTemplate_IsNull_WhenAttributeAbsent()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsStruct]
    public struct PlainStruct
    {
        public int X;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.FirstOrDefault(t => t.Name == "PlainStruct");
            Assert.NotNull(type);
            Assert.Null(type.FormatTemplate);
        }

        [Fact]
        public void SchemaDiscovery_DdsTypeFormat_WorksOnDdsTopic()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace Test
{
    [DdsTopic(""MyTopic"")]
    [DdsTypeFormat(""{X:0.00:Number}, {Y:0.00:Number}"")]
    public struct PositionTopic
    {
        [DdsKey] public int Id;
        public float X;
        public float Y;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);

            var type = types.FirstOrDefault(t => t.Name == "PositionTopic");
            Assert.NotNull(type);
            Assert.Equal("{X:0.00:Number}, {Y:0.00:Number}", type.FormatTemplate);
        }

        // ─────────────────────────────────────────────────────────────────
        // End-to-end: generated code compiles and runs correctly
        // ─────────────────────────────────────────────────────────────────

        [Fact]
        public void EndToEnd_GeneratedToString_ProducesExpectedOutput()
        {
            // 1. Discover the type info from inline source
            CreateFile(@"
using CycloneDDS.Schema;
namespace E2E
{
    [DdsStruct]
    [DdsTypeFormat(""{Site:D:Number}:{App:D:Number}:{Entity:X8:Keyword}"")]
    public partial struct DisEntityId
    {
        public ushort Site;
        public ushort App;
        public uint Entity;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var typeInfo = types.First(t => t.Name == "DisEntityId");

            // 2. Generate the serializer + formatter code
            var emitter = new SerializerEmitter();
            var generatedCode = emitter.EmitSerializer(typeInfo, new GlobalTypeRegistry());

            // 3. Compile both the original struct + the generated partial together
            var schemaSource = @"
using CycloneDDS.Schema;
namespace E2E
{
    [DdsStruct]
    [DdsTypeFormat(""{Site:D:Number}:{App:D:Number}:{Entity:X8:Keyword}"")]
    public partial struct DisEntityId
    {
        public ushort Site;
        public ushort App;
        public uint Entity;
    }
}";
            var assembly = CompileToAssembly("E2EFormatterTest", schemaSource, generatedCode);

            // 4. Instantiate and set values
            var instance = assembly.CreateInstance("E2E.DisEntityId")!;
            var t = instance.GetType();
            t.GetField("Site")!.SetValue(instance, (ushort)1);
            t.GetField("App")!.SetValue(instance, (ushort)2);
            t.GetField("Entity")!.SetValue(instance, (uint)0x0000ABCD);

            // 5. Verify ToString()
            var toStringResult = instance.ToString();
            Assert.Equal("1:2:0000ABCD", toStringResult);
        }

        [Fact]
        public void EndToEnd_GeneratedGetFormatTokens_YieldsExpectedTokens()
        {
            CreateFile(@"
using CycloneDDS.Schema;
namespace E2E
{
    [DdsStruct]
    [DdsTypeFormat(""{Site:D:Number}:{App:D:Number}"")]
    public partial struct SimpleId
    {
        public ushort Site;
        public ushort App;
    }
}");
            var discovery = new SchemaDiscovery();
            var types = discovery.DiscoverTopics(_tempDir);
            var typeInfo = types.First(t => t.Name == "SimpleId");

            var emitter = new SerializerEmitter();
            var generatedCode = emitter.EmitSerializer(typeInfo, new GlobalTypeRegistry());

            var schemaSource = @"
using CycloneDDS.Schema;
namespace E2E
{
    [DdsStruct]
    [DdsTypeFormat(""{Site:D:Number}:{App:D:Number}"")]
    public partial struct SimpleId
    {
        public ushort Site;
        public ushort App;
    }
}";
            var assembly = CompileToAssembly("E2ETokenTest", schemaSource, generatedCode);

            var instance = assembly.CreateInstance("E2E.SimpleId")!;
            var t = instance.GetType();
            t.GetField("Site")!.SetValue(instance, (ushort)10);
            t.GetField("App")!.SetValue(instance, (ushort)20);

            // Invoke GetFormatTokens() via reflection
            var method = t.GetMethod("GetFormatTokens")!;
            Assert.NotNull(method);

            var tokensObj = method.Invoke(instance, null)!;
            // Materialise to list of (Text, Type)
            var tokens = new List<(string Text, object Type)>();
            foreach (var tok in (System.Collections.IEnumerable)tokensObj)
            {
                var tokType = tok.GetType();
                tokens.Add((
                    (string)tokType.GetProperty("Text")!.GetValue(tok)!,
                    tokType.GetProperty("Type")!.GetValue(tok)!
                ));
            }

            // Expected: "10", ":", "20"
            Assert.Equal(3, tokens.Count);
            Assert.Equal("10", tokens[0].Text);
            Assert.Equal(":", tokens[1].Text);
            Assert.Equal("20", tokens[2].Text);

            // Token type names are from CycloneDDS.Schema.Formatting.TokenType
            Assert.Equal("Number",      tokens[0].Type.ToString());
            Assert.Equal("Punctuation", tokens[1].Type.ToString());
            Assert.Equal("Number",      tokens[2].Type.ToString());
        }
    }
}
