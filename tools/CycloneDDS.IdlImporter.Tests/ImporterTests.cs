using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CycloneDDS.Schema;
using System.Reflection;
using System.Collections.Generic;

namespace CycloneDDS.IdlImporter.Tests
{
    public class ImporterTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceRoot;
        private readonly string _outputRoot;

        public ImporterTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "ImporterTests_" + Guid.NewGuid());
            _sourceRoot = Path.Combine(_testRoot, "src");
            _outputRoot = Path.Combine(_testRoot, "out");

            Directory.CreateDirectory(_sourceRoot);
            Directory.CreateDirectory(_outputRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                try { Directory.Delete(_testRoot, true); } catch { }
            }
        }

        [Fact]
        public void Import_SimpleTree_GeneratesFiles()
        {
            // Setup:
            // src/
            //   Point.idl
            //   Shape.idl -> includes Point.idl
            
            string pointIdl = Path.Combine(_sourceRoot, "Point.idl");
            File.WriteAllText(pointIdl, @"
module Geom {
    struct Point {
        long x;
        long y;
    };
};");

            string shapeIdl = Path.Combine(_sourceRoot, "Shape.idl");
            File.WriteAllText(shapeIdl, @"
#include ""Point.idl""
module Geom {
    struct Shape {
        Geom::Point center;
    };
};");

            // Act
            var importer = new Importer();
            // Point.idl isn't processed unless something includes it or it is the master?
            // Wait, the requirement says "Imports IDL files starting from a master file".
            // If I start from Shape.idl, it should process Point.idl too.
            
            importer.Import(shapeIdl, _sourceRoot, _outputRoot);

            // Assert
            Assert.True(File.Exists(Path.Combine(_outputRoot, "Shape.cs")), "Shape.cs should exist");
            Assert.True(File.Exists(Path.Combine(_outputRoot, "Point.cs")), "Point.cs should exist (recursive dependency)");
            
            string shapeContent = File.ReadAllText(Path.Combine(_outputRoot, "Shape.cs"));
            Assert.Contains("public partial struct Shape", shapeContent);
            Assert.Contains("public Geom.Point center;", shapeContent); // Original case retained + Namespaced type
        }
        
        [Fact]
        public void Import_HandlesSubdirectories()
        {
            // Setup:
            // src/
            //   Main.idl
            //   Math/
            //     Vector.idl
            
            Directory.CreateDirectory(Path.Combine(_sourceRoot, "Math"));
            
            string vectorIdl = Path.Combine(_sourceRoot, "Math", "Vector.idl");
            File.WriteAllText(vectorIdl, @"
module Math {
    struct Vector {
        double val;
    };
};");

            string mainIdl = Path.Combine(_sourceRoot, "Main.idl");
            File.WriteAllText(mainIdl, @"
#include ""Math/Vector.idl""
module App {
    struct AppState {
        Math::Vector vec;
    };
};");

            // Act
            var importer = new Importer();
            importer.Import(mainIdl, _sourceRoot, _outputRoot);

            // Assert
            Assert.True(File.Exists(Path.Combine(_outputRoot, "Main.cs")));
            Assert.True(File.Exists(Path.Combine(_outputRoot, "Math", "Vector.cs")), "Vector.cs should be in subdir");
        }
        
        [Fact]
        public void Import_PreventsCircularLoop()
        {
            // Setup: A <-> B
            string aIdl = Path.Combine(_sourceRoot, "A.idl");
            string bIdl = Path.Combine(_sourceRoot, "B.idl");

            // Add Header Guards to prevent preprocessor infinite loop
            File.WriteAllText(aIdl, @"
#ifndef A_IDL
#define A_IDL
#include ""B.idl""
module Test { struct A { long v; }; };
#endif
");
            File.WriteAllText(bIdl, @"
#ifndef B_IDL
#define B_IDL
#include ""A.idl""
module Test { struct B { long v; }; };
#endif
");

            // Act
            var importer = new Importer();
            
            // This should finish and not hang
            importer.Import(aIdl, _sourceRoot, _outputRoot);

            // Assert
            Assert.True(File.Exists(Path.Combine(_outputRoot, "A.cs")));
            Assert.True(File.Exists(Path.Combine(_outputRoot, "B.cs")));
        }

        [Fact]
        public void Import_GeneratesCompilableCode()
        {
            // Setup complex IDL
            Directory.CreateDirectory(Path.Combine(_sourceRoot, "Complex"));
            
            // 1. Base types
            string basicIdl = Path.Combine(_sourceRoot, "Complex", "Basic.idl");
            File.WriteAllText(basicIdl, @"
module Core {
    struct DateTime {
        long sec;
        unsigned long nanosec;
    };
    
    enum Status {
        OK,
        ERROR,
        UNKNOWN
    };
};");

            // 2. Complex types (Sequences, Arrays, Nested, Optional, ID)
            string complexIdl = Path.Combine(_sourceRoot, "Complex", "Complex.idl");
            File.WriteAllText(complexIdl, @"
#include ""Basic.idl""

module Business {
    @mutable
    struct Record {
        @key @id(1) long id;
        @id(2) string<128> name;
        @optional @id(3) Core::DateTime timestamp;
        @id(4) sequence<Core::Status> history;
        @id(5) long values[10];
        @id(6) Core::Status current_status;
    };

    union Result switch (Core::Status) {
        case Core::OK: Record record;
        case Core::ERROR: string error_msg;
        default: boolean flag;
    };
};");

            // Run Importer
            var importer = new Importer();
            importer.Import(complexIdl, _sourceRoot, _outputRoot);

            // Collect all generated files
            var syntaxTrees = Directory.GetFiles(_outputRoot, "*.cs", SearchOption.AllDirectories)
                .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)))
                .ToList();
                
            Assert.NotEmpty(syntaxTrees);

            // Add references
            // System.Runtime (PrivateCoreLib)
            var systemRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            // System.Runtime.dll
            var runtimePath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll");
            var runtimeRef = MetadataReference.CreateFromFile(runtimePath);
            
            // System.Collections
            var collectionsRef = MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location);
            // System.Runtime.InteropServices (for attributes if needed)
            var interopRef = MetadataReference.CreateFromFile(typeof(System.Runtime.InteropServices.StructLayoutAttribute).Assembly.Location);
            // CycloneDDS.Schema
            var schemaRef = MetadataReference.CreateFromFile(typeof(DdsStructAttribute).Assembly.Location);
            
            // Console (System.Console) might be needed if generated code uses it? No.
            
            var references = new List<MetadataReference> { systemRef, runtimeRef, collectionsRef, interopRef, schemaRef };
            
            // Need to add System.Private.CoreLib explicitly? typeof(object).Assembly.Location usually points to it.

            // Compile
            var compilation = CSharpCompilation.Create("ComplexAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(references)
                .AddSyntaxTrees(syntaxTrees);
                
            var diagnostics = compilation.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            
            if (errors.Any())
            {
                var msgs = string.Join("\n", errors.Select(e => e.ToString()));
                Assert.Fail($"Compilation failed with {errors.Count} errors:\n{msgs}");
            }
        }
    }
}
