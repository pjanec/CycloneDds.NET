using Xunit;
using CycloneDDS.CodeGen;
using System.IO;
using System;
using System.Linq;

namespace CycloneDDS.CodeGen.Tests;

public class GeneratorIntegrationTests
{
    [Fact]
    public void Generator_CompleteWorkflow_GeneratesAllFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var topicCode = @"
using CycloneDDS.Core;

namespace TestNs
{
    [DdsTopic(""TestTopic"")]
    public partial class TestMessage
    {
        [DdsKey]
        public int Id;
        public string Data;
    }
}";
            var sourceFile = Path.Combine(tempDir, "Test.cs");
            File.WriteAllText(sourceFile, topicCode);
            
            // Run generator
            var generator = new CodeGenerator();
            var result = generator.Generate(tempDir);
            
            // Verify ALL files created
            var generatedDir = Path.Combine(tempDir, "Generated");
            Assert.True(Directory.Exists(generatedDir), "Generated directory should exist");
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestMessage.idl")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestMessageNative.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestMessageManaged.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestMessageMarshaller.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "MetadataRegistry.g.cs")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void Generator_MultipleTopics_AllGenerated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var topic1Code = @"
namespace TestNs
{
    [DdsTopic(""Topic1"")]
    public partial class Topic1
    {
        public int A;
    }
}";
            var topic2Code = @"
namespace TestNs
{
    [DdsTopic(""Topic2"")]
    public partial class Topic2
    {
        public int B;
    }
}";
            File.WriteAllText(Path.Combine(tempDir, "Topic1.cs"), topic1Code);
            File.WriteAllText(Path.Combine(tempDir, "Topic2.cs"), topic2Code);
            
            var generator = new CodeGenerator();
            generator.Generate(tempDir);
            
            var generatedDir = Path.Combine(tempDir, "Generated");
            Assert.True(File.Exists(Path.Combine(generatedDir, "Topic1.idl")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "Topic2.idl")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "Topic1Native.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "Topic2Native.g.cs")));
            
            // MetadataRegistry should exist and be valid
            var registryFile = Path.Combine(generatedDir, "MetadataRegistry.g.cs");
            Assert.True(File.Exists(registryFile));
            Assert.True(new FileInfo(registryFile).Length > 0); // Not empty
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void Generator_Union_GeneratesAllArtifacts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var unionCode = @"
namespace TestNs
{
    [DdsUnion]
    public partial class TestUnion
    {
        [DdsDiscriminator]
        public int D;
        [DdsCase(1)]
        public float Value;
        [DdsCase(2)]
        public int Count;
    }
}";
            File.WriteAllText(Path.Combine(tempDir, "TestUnion.cs"), unionCode);
            
            var generator = new CodeGenerator();
            generator.Generate(tempDir);
            
            var generatedDir = Path.Combine(tempDir, "Generated");
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestUnion.idl")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestUnionNative.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestUnionManaged.g.cs")));
            Assert.True(File.Exists(Path.Combine(generatedDir, "TestUnionMarshaller.g.cs")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void Generator_KeyFields_TrackedInRegistry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var topicCode = @"
namespace TestNs
{
    [DdsTopic(""KeyedTopic"")]
    public partial class KeyedMessage
    {
        [DdsKey]
        public int PrimaryKey;
        public string Data;
        [DdsKey]
        public int SecondaryKey;
    }
}";
            File.WriteAllText(Path.Combine(tempDir, "KeyedMessage.cs"), topicCode);
            
            var generator = new CodeGenerator();
            generator.Generate(tempDir);
            
            var generatedDir = Path.Combine(tempDir, "Generated");
            var registryFile = Path.Combine(generatedDir, "MetadataRegistry.g.cs");
            Assert.True(File.Exists(registryFile));
            
            var registryContent = File.ReadAllText(registryFile);
            // Should track key field indices 0 and 2
            Assert.Contains("KeyFieldIndices", registryContent);
            Assert.Contains("0, 2", registryContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void Generator_ComplexStruct_AllFieldsGenerated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var topicCode = @"
namespace TestNs
{
    [DdsTopic(""ComplexTopic"")]
    public partial class ComplexMessage
    {
        public int IntField;
        public double DoubleField;
        public bool BoolField;
        public int[] ArrayField;
        public DateTime TimeField;
    }
}";
            File.WriteAllText(Path.Combine(tempDir, "ComplexMessage.cs"), topicCode);
            
            var generator = new CodeGenerator();
            generator.Generate(tempDir);
            
            var generatedDir = Path.Combine(tempDir, "Generated");
            var nativeFile = Path.Combine(generatedDir, "ComplexMessageNative.g.cs");
            Assert.True(File.Exists(nativeFile));
            
            var nativeContent = File.ReadAllText(nativeFile);
            Assert.Contains("IntField", nativeContent);
            Assert.Contains("DoubleField", nativeContent);
            Assert.Contains("BoolField", nativeContent);
            Assert.Contains("ArrayField_Ptr", nativeContent);
            Assert.Contains("ArrayField_Length", nativeContent);
            Assert.Contains("TimeField", nativeContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void Generator_SnapshotTest_IDLStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var topicCode = @"
namespace TestNs
{
    [DdsTopic(""SnapshotTopic"")]
    public partial class SnapshotMessage
    {
        [DdsKey]
        public int Id;
        public string Name;
        public double Value;
    }
}";
            File.WriteAllText(Path.Combine(tempDir, "SnapshotMessage.cs"), topicCode);
            
            var generator = new CodeGenerator();
            generator.Generate(tempDir);
            
            var generatedDir = Path.Combine(tempDir, "Generated");
            var idlFile = Path.Combine(generatedDir, "SnapshotMessage.idl");
            Assert.True(File.Exists(idlFile));
            
            var idlContent = File.ReadAllText(idlFile);
            
            // Verify IDL structure
            Assert.Contains("module TestNs", idlContent);
            Assert.Contains("struct SnapshotMessage", idlContent);
            Assert.Contains("@key", idlContent); // Key annotation
            Assert.Contains("long Id", idlContent);
            Assert.Contains("string Name", idlContent);
            Assert.Contains("double Value", idlContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
