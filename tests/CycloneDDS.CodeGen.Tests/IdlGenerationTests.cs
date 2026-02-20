using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests
{
    public class IdlGenerationTests
    {
        // --- Validation Tests ---

        [Fact]
        public void ValidateIdlFile_WithExtension_ThrowsError()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlFile", 
                Arguments = new List<object> { "MyFile.idl" } 
            });

            var ex = Assert.Throws<ArgumentException>(() => discovery.GetIdlFileName(type, "Source.cs"));
            Assert.Contains("contains extension", ex.Message);
        }

        [Fact]
        public void ValidateIdlFile_WithPath_ThrowsError()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlFile", 
                Arguments = new List<object> { "folder/MyFile" } 
            });

            var ex = Assert.Throws<ArgumentException>(() => discovery.GetIdlFileName(type, "Source.cs"));
            Assert.Contains("path separators", ex.Message);
        }

        [Fact]
        public void ValidateIdlModule_WithDots_ThrowsError()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlModule", 
                Arguments = new List<object> { "My.Module" } 
            });

            var ex = Assert.Throws<ArgumentException>(() => discovery.GetIdlModule(type));
            Assert.Contains("contains '.'", ex.Message);
        }

        [Fact]
        public void ValidateIdlModule_InvalidIdentifier_ThrowsError()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlModule", 
                Arguments = new List<object> { "My::Invalid-Module" } 
            });

            var ex = Assert.Throws<ArgumentException>(() => discovery.GetIdlModule(type));
            Assert.Contains("invalid identifier", ex.Message);
        }

        // --- Discovery Tests ---

        [Fact]
        public void Discovery_DefaultIdlFile_UsesSourceFileName()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            
            var fileName = discovery.GetIdlFileName(type, "path/to/MyType.cs");
            Assert.Equal("MyType", fileName);
        }

        [Fact]
        public void Discovery_CustomIdlFile_UsesAttribute()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlFile", 
                Arguments = new List<object> { "CustomFile" } 
            });
            
            var fileName = discovery.GetIdlFileName(type, "MyType.cs");
            Assert.Equal("CustomFile", fileName);
        }

        [Fact]
        public void Discovery_DefaultModule_UsesNamespace()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct", Namespace = "Corp.Common" };
            
            var module = discovery.GetIdlModule(type);
            Assert.Equal("Corp::Common", module);
        }

        [Fact]
        public void Discovery_CustomModule_UsesAttribute()
        {
            var discovery = new SchemaDiscovery();
            var type = new TypeInfo { Name = "MyStruct", Namespace = "Corp.Common" };
            type.Attributes.Add(new AttributeInfo { 
                Name = "DdsIdlModule", 
                Arguments = new List<object> { "Legacy::Mod" } 
            });
            
            var module = discovery.GetIdlModule(type);
            Assert.Equal("Legacy::Mod", module);
        }

        // --- Registry Tests ---

        [Fact]
        public void Registry_LocalType_StoresCorrectMapping()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "Point", Namespace = "My.Geo" };
            
            registry.RegisterLocal(type, "Point.cs", "Geometry", "My::Geo");
            
            Assert.True(registry.TryGetDefinition("My.Geo.Point", out var def));
            Assert.Equal("Geometry", def.TargetIdlFile);
            Assert.Equal("My::Geo", def.TargetModule);
            Assert.False(def.IsExternal);
            Assert.Equal("Point.cs", def.SourceFile);
        }

        [Fact]
        public void Registry_ExternalType_ResolvedViaMetadata()
        {
            var registry = new GlobalTypeRegistry();
            
            registry.RegisterExternal("External.Lib.Point", "CommonParams", "Ext::Lib");
            
            Assert.True(registry.TryGetDefinition("External.Lib.Point", out var def));
            Assert.Equal("CommonParams", def.TargetIdlFile);
            Assert.Equal("Ext::Lib", def.TargetModule);
            Assert.True(def.IsExternal);
        }

        [Fact]
        public void Registry_IdlCollision_DetectedAndReported()
        {
            var registry = new GlobalTypeRegistry();
            var type1 = new TypeInfo { Name = "Point", Namespace = "A" };
            var type2 = new TypeInfo { Name = "Point", Namespace = "B" }; // Different C# type

            // Map both to same IDL file/module/name
            registry.RegisterLocal(type1, "A.cs", "Common", "Shared");
            
            var ex = Assert.Throws<InvalidOperationException>(() => 
                registry.RegisterLocal(type2, "B.cs", "Common", "Shared"));
                
            Assert.Contains("collision", ex.Message.ToLower());
        }

        // --- Emission Tests ---

        [Fact]
        public void EmitIdl_MultipleModules_NestedCorrectly()
        {
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "MyStruct", Namespace = "A.B" };
            registry.RegisterLocal(type, "src.cs", "File1", "A::B");
            
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try {
                var emitter = new IdlEmitter();
                emitter.EmitIdlFiles(registry, outputDir);
                
                var content = File.ReadAllText(Path.Combine(outputDir, "File1.idl"));
                // Expect: module A { module B { struct MyStruct ... } };
                // Using regex or simple check
                Assert.Matches(@"module A\s*\{\s*module B", content.Replace("\r", "").Replace("\n", ""));
            } finally {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void EmitIdl_Dependencies_IncludesFirst()
        {
            var registry = new GlobalTypeRegistry();
            
            // Type A depends on B
            var typeB = new TypeInfo { Name = "StructB", Namespace = "Lib" };
            registry.RegisterLocal(typeB, "B.cs", "FileB", "Lib");
            
            var typeA = new TypeInfo { Name = "StructA", Namespace = "App" };
            typeA.Fields.Add(new FieldInfo { Name = "field", TypeName = "Lib.StructB" }); 
            
            registry.RegisterLocal(typeA, "A.cs", "FileA", "App");
            
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try {
                var emitter = new IdlEmitter();
                emitter.EmitIdlFiles(registry, outputDir);
                
                var content = File.ReadAllText(Path.Combine(outputDir, "FileA.idl"));
                Assert.Contains("#include \"FileB.idl\"", content);
                
                var lines = File.ReadAllLines(Path.Combine(outputDir, "FileA.idl"));
                var includeIdx = Array.FindIndex(lines, l => l.Contains("#include"));
                var moduleIdx = Array.FindIndex(lines, l => l.Contains("module"));
                Assert.True(includeIdx < moduleIdx, "Includes should come before module definition");
            } finally {
                 if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void EmitMetadata_AllTypes_Recorded()
        {
            var registry = new GlobalTypeRegistry();
            
            // Register 3 types
            registry.RegisterLocal(new TypeInfo { Name = "Point", Namespace = "Geo" }, "P.cs", "Common", "Geo");
            registry.RegisterLocal(new TypeInfo { Name = "Vector", Namespace = "Geo" }, "V.cs", "Common", "Geo");
            registry.RegisterLocal(new TypeInfo { Name = "Matrix", Namespace = "Math" }, "M.cs", "MathDefs", "Math");
            
            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            
            try {
                // We need to invoke CodeGenerator.EmitAssemblyMetadata, but it's private/internal or part of Generate method.
                // CodeGenerator.cs is not easily unit-testable for this specific method without running full generation or making it public.
                // However, we can use CodeGenerator instance if we can inject registry? 
                // CodeGenerator builds its own registry.
                
                // Alternative: Use CodeGenerator in a way that populates registry.
                // Or check if we can test the logic that generates the metadata string.
                
                // Since this is a unit test for "Emission", and CodeGenerator handles metadata emission...
                // I'll use the full CodeGenerator flow with a mock file system or just real files.
                // Actually, I can use the same pattern as CrossAssemblyTests but simpler (just generate, don't compile).
                
                // Create source files
                var sourceDir = Path.Combine(outputDir, "src");
                Directory.CreateDirectory(sourceDir);
                
                File.WriteAllText(Path.Combine(sourceDir, "Types.cs"), @"
using CycloneDDS.Schema;
namespace Geo {
    [DdsIdlFile(""Common"")]
    [DdsStruct]
    public struct Point { public int x; }

    [DdsIdlFile(""Common"")]
    [DdsStruct]
    public struct Vector { public int y; }
}
namespace Math {
    [DdsIdlFile(""MathDefs"")]
    [DdsStruct]
    public struct Matrix { public int z; }
}
");
                var genDir = Path.Combine(outputDir, "gen");
                var generator = new CodeGenerator();
                generator.Generate(sourceDir, genDir);
                
                var metaPath = Path.Combine(genDir, "CycloneDDS.IdlMap.g.cs");
                Assert.True(File.Exists(metaPath));
                var content = File.ReadAllText(metaPath);
                
                // Verify content
                Assert.Contains("[assembly: DdsIdlMapping(\"Geo.Point\", \"Common\", \"Geo\")]", content);
                Assert.Contains("[assembly: DdsIdlMapping(\"Geo.Vector\", \"Common\", \"Geo\")]", content);
                Assert.Contains("[assembly: DdsIdlMapping(\"Math.Matrix\", \"MathDefs\", \"Math\")]", content);
                
                // Verify count
                var count = content.Split(new[] { "[assembly: DdsIdlMapping" }, StringSplitOptions.None).Length - 1;
                Assert.Equal(3, count);
                
            } finally {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void EmitIdl_StructFieldNames_RetainOriginalCase()
        {
            // C# fields may use any casing; the emitted IDL must preserve it exactly
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "CaseStruct", Namespace = "Ns" };
            type.Fields.Add(new FieldInfo { Name = "camelCaseField",   TypeName = "int" });
            type.Fields.Add(new FieldInfo { Name = "SCREAMING_SNAKE",  TypeName = "int" });
            type.Fields.Add(new FieldInfo { Name = "PascalAlready",    TypeName = "int" });
            type.Fields.Add(new FieldInfo { Name = "lower_snake_case", TypeName = "int" });
            registry.RegisterLocal(type, "src.cs", "CaseFile", "Ns");

            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try
            {
                new IdlEmitter().EmitIdlFiles(registry, outputDir);
                var content = File.ReadAllText(Path.Combine(outputDir, "CaseFile.idl"));

                // Each name must appear verbatim – no camelCase lowercasing
                Assert.Contains("camelCaseField",   content);
                Assert.Contains("SCREAMING_SNAKE",  content);
                Assert.Contains("PascalAlready",    content);
                Assert.Contains("lower_snake_case", content);

                // The first character of PascalAlready must NOT be lowercased
                Assert.DoesNotContain("pascalAlready", content);
                // SCREAMING_SNAKE must not have its first char lowercased
                Assert.DoesNotContain("sCREAMING_SNAKE", content);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }

        [Fact]
        public void EmitIdl_UnionBranchNames_RetainOriginalCase()
        {
            // Union branch member names must also retain original casing
            var registry = new GlobalTypeRegistry();
            var type = new TypeInfo { Name = "CaseUnion", Namespace = "Ns" };
            type.Attributes.Add(new AttributeInfo { Name = "DdsUnion" });

            var disc = new FieldInfo { Name = "_d", TypeName = "int" };
            disc.Attributes.Add(new AttributeInfo { Name = "DdsDiscriminator" });
            type.Fields.Add(disc);

            var caseField = new FieldInfo { Name = "MyValue", TypeName = "int" };
            var caseAttr = new AttributeInfo { Name = "DdsCase" };
            caseAttr.Arguments.Add(0);
            caseField.Attributes.Add(caseAttr);
            type.Fields.Add(caseField);

            var defaultField = new FieldInfo { Name = "OtherValue", TypeName = "int" };
            defaultField.Attributes.Add(new AttributeInfo { Name = "DdsDefaultCase" });
            type.Fields.Add(defaultField);

            registry.RegisterLocal(type, "src.cs", "UnionFile", "Ns");

            var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            try
            {
                new IdlEmitter().EmitIdlFiles(registry, outputDir);
                var content = File.ReadAllText(Path.Combine(outputDir, "UnionFile.idl"));

                Assert.Contains("MyValue",    content);
                Assert.Contains("OtherValue", content);

                // Must NOT be lowercased by ToCamelCase
                Assert.DoesNotContain("myValue",    content);
                Assert.DoesNotContain("otherValue", content);
            }
            finally
            {
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            }
        }    }
}