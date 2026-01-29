using System;
using System.IO;
using System.Linq;
using Xunit;

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
            Assert.Contains("public Geom.Point Center;", shapeContent); // PascalCase + Namespaced type
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
    }
}
