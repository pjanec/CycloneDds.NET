using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using System.Collections.Generic;

namespace CycloneDDS.CodeGen.Tests
{
    public class GeneratorIntegrationTests : CodeGenTestBase, IDisposable
    {
        private readonly string _tempDir;
        
        public GeneratorIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch {}
        }

        private string[] RunGenerator(string sourceCode)
        {
            // Write source file
            var srcDir = Path.Combine(_tempDir, "src");
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "Test.cs"), sourceCode);
            
            var outDir = Path.Combine(_tempDir, "out");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            
            // Run Generator
            var generator = new CodeGenerator();
            generator.Generate(srcDir, outDir);
            
            // Read all generated files
            var files = Directory.GetFiles(outDir, "*.cs");
            return files.Select(f => File.ReadAllText(f)).ToArray();
        }
        
        [Fact]
        public void CodeGen_NestedStruct_Compiles()
        {
            string source = @"
                using CycloneDDS.Schema;
                
                namespace Test {
                    [DdsStruct]
                    public partial struct Point3D
                    {
                        public double X;
                        public double Y;
                        public double Z;
                    }
                    
                    [DdsTopic(""Robot"")]
                    public partial struct RobotState
                    {
                        [DdsKey] public int Id;
                        public Point3D Position;
                    }
                }
            ";
            
            var generatedCode = RunGenerator(source);
            var allSources = new[] { source }.Concat(generatedCode).ToArray();
            
            var assembly = CompileToAssembly("NestedStructAssembly", allSources);
            Assert.NotNull(assembly);
        }

        [Fact]
        public void Roundtrip_NestedStruct_Preserves()
        {
            var source = @"
                using CycloneDDS.Schema;
                
                namespace Test {
                    [DdsStruct]
                    public partial struct Point3D { public double X, Y, Z; }
                    
                    [DdsTopic(""Robot"")]
                    public partial struct RobotState {
                        [DdsKey] public int Id;
                        public Point3D Position;
                    }
                }
            ";
            
            var generatedCode = RunGenerator(source);
            var allSources = new[] { source }.Concat(generatedCode).ToArray();
            var assembly = CompileToAssembly("RoundtripAssembly", allSources);
            
            // Create instance
            var robotType = assembly.GetType("Test.RobotState");
            var pointType = assembly.GetType("Test.Point3D");
            
            var robot = Activator.CreateInstance(robotType);
            SetField(robot, "Id", 42);
            
            var point = Activator.CreateInstance(pointType);
            SetField(point, "X", 10.0);
            SetField(point, "Y", 20.0);
            SetField(point, "Z", 30.0);
            
            SetField(robot, "Position", point);
            
            // Serialize
            // Using SerializerEmitter generated method: public static void Serialize(in RobotState data, ref CdrWriter writer)
            // But getting 'in' parameter via reflection is tricky or needs wrapper.
            // Or use the instance method: public void Serialize(ref CdrWriter writer) (if generated as partial)
            
            // I'll use the helper method pattern from CodeGenTestBase if I append it, but I didn't append it here.
            // Let's create a wrapper using dynamic or specific helpers.
            // Or just verify that CodeGen generating partial methods so I can call `Serialize` on instance.
            // "public void Serialize(ref CdrWriter writer)"
            
            // Serialization
            var buffer = new byte[1024];
            var writer = new CycloneDDS.Core.CdrWriter(buffer);
            
            // Use TestHelper approach
            var testHelperCode = GenerateTestHelper("Test", "RobotState");
            var allSourcesWithHelper = new[] { source }.Concat(generatedCode).Concat(new[] { testHelperCode }).ToArray();
            var assemblyWithHelper = CompileToAssembly("RoundtripHelperAssembly", allSourcesWithHelper);
            
            var helperType = assemblyWithHelper.GetType("Test.TestHelper");
            
            // Re-create instance in new assembly context
            robotType = assemblyWithHelper.GetType("Test.RobotState");
            pointType = assemblyWithHelper.GetType("Test.Point3D");
             
            robot = Activator.CreateInstance(robotType);
            SetField(robot, "Id", 42);
            point = Activator.CreateInstance(pointType);
            SetField(point, "X", 10.0);
            SetField(point, "Y", 20.0);
            SetField(point, "Z", 30.0);
            SetField(robot, "Position", point);

            var vectorBuffer = new System.Buffers.ArrayBufferWriter<byte>();
            
            var serializeMethod = helperType.GetMethod("SerializeWithBuffer");
            serializeMethod.Invoke(null, new object[] { robot, vectorBuffer });
            
            var bytes = vectorBuffer.WrittenMemory;
            
            // Deserialize
            var deserializeMethod = helperType.GetMethod("DeserializeFromBuffer");
            var deserialized = deserializeMethod.Invoke(null, new object[] { bytes });
            
            var viewId = (int)GetField(deserialized, "Id");
            var viewPos = GetField(deserialized, "Position");
            var viewX = (double)GetField(viewPos, "X");
            var viewY = (double)GetField(viewPos, "Y");
            var viewZ = (double)GetField(viewPos, "Z");
             
            Assert.Equal(42, viewId);
            Assert.Equal(10.0, viewX);
            Assert.Equal(20.0, viewY);
            Assert.Equal(30.0, viewZ);
        }
    }
}

