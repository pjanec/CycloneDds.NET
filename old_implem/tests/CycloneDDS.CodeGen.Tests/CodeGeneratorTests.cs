using System;
using System.IO;
using Xunit;
using CycloneDDS.CodeGen;

namespace CycloneDDS.CodeGen.Tests;

public class CodeGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public CodeGeneratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "CodeGenTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public void DiscoversSingleTopicType()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "TestType.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

namespace TestNamespace;

[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public int Id { get; set; }
}
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        
        var generatedFile = Path.Combine(_testDir, "Generated", "TestType.Discovery.g.cs");
        Assert.True(File.Exists(generatedFile), $"Generated file not found: {generatedFile}");
        
        var content = File.ReadAllText(generatedFile);
        Assert.Contains("namespace TestNamespace", content);
        Assert.Contains("partial class TestType", content);
        Assert.Contains("Topic: TestTopic", content);
    }

    [Fact]
    public void DiscoversMultipleTopicTypes()
    {
        // Arrange - create file with 2 topic types
        var sourceFile = Path.Combine(_testDir, "Types.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

[DdsTopic(""Topic1"")]
public partial class Type1 { }

[DdsTopic(""Topic2"")]
public partial class Type2 { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(2, filesGenerated);
        Assert.True(File.Exists(Path.Combine(_testDir, "Generated", "Type1.Discovery.g.cs")));
        Assert.True(File.Exists(Path.Combine(_testDir, "Generated", "Type2.Discovery.g.cs")));
    }

    [Fact]
    public void DiscoversUnionType()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "MyUnion.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

[DdsUnion]
public partial class MyUnion 
{ 
    [DdsDiscriminator]
    public int Discriminator;
}
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        
        var generatedFile = Path.Combine(_testDir, "Generated", "MyUnion.Discovery.g.cs");
        Assert.True(File.Exists(generatedFile));
    }

    [Fact]
    public void HandlesFileScopedNamespace()
    {
        // Arrange - file-scoped namespace (C# 10+)
        var sourceFile = Path.Combine(_testDir, "Modern.cs");
        File.WriteAllText(sourceFile, @"
using CycloneDDS.Schema;

namespace My.Namespace;

[DdsTopic(""ModernTopic"")]
public partial class ModernType { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(1, filesGenerated);
        var content = File.ReadAllText(Path.Combine(_testDir, "Generated", "ModernType.Discovery.g.cs"));
        Assert.Contains("namespace My.Namespace", content);
    }

    [Fact]
    public void NoAttributesGeneratesNothing()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDir, "PlainType.cs");
        File.WriteAllText(sourceFile, @"
public class PlainType { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert
        Assert.Equal(0, filesGenerated);
    }

    [Fact]
    public void IgnoresFalsePositiveAttributes()
    {
        // Arrange - type with similar but not exact attribute name
        var sourceFile = Path.Combine(_testDir, "FalsePositive.cs");
        File.WriteAllText(sourceFile, @"
// This should NOT be discovered (DdsTopicHelper != DdsTopic)
[DdsTopicHelper]
public partial class FalsePositive { }

public class DdsTopicHelperAttribute : System.Attribute { }
");

        // Act
        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(_testDir);

        // Assert - should generate 0 files (after fix to attribute matching)
        Assert.Equal(0, filesGenerated);
    }
}
