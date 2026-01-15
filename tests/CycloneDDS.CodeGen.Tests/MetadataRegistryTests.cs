using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System;
using CycloneDDS.CodeGen.Emitters;
using CycloneDDS.CodeGen.Marshalling;

namespace CycloneDDS.CodeGen.Tests;

public class MetadataRegistryTests
{
    private TypeDeclarationSyntax ParseType(string code, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == typeName);
    }

    private Assembly CompileToAssembly(params string[] sources)
    {
        var attributes = @"
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DdsTopicAttribute : Attribute { public DdsTopicAttribute(string name = """") {} }
[AttributeUsage(AttributeTargets.Field)]
public class DdsKeyAttribute : Attribute {}
";
        var allSources = sources.Append(attributes).ToArray();
        
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = allSources.Select(s => CSharpSyntaxTree.ParseText(s, options)).ToArray();
        
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Collections.dll")),
            MetadataReference.CreateFromFile(typeof(TopicMetadata).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IMarshaller<,>).Assembly.Location)
        };
        
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));
        
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        
        if (!result.Success)
        {
            var errors = string.Join("\n", result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage()));
            var sourceLog = string.Join("\n--- SOURCE ---\n", sources);
            throw new Exception($"Compilation failed:\n{errors}\n\nSources:\n{sourceLog}");
        }
        
        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    [Fact]
    public void MetadataRegistry_ContainsAllTopics_Runtime()
    {
        var topic1Code = @"
namespace Test
{
    [DdsTopic(""Topic1"")]
    public partial class TestTopic1
    {
        public int A;
    }
}";
        var topic2Code = @"
namespace Test
{
    [DdsTopic(""Topic2"")]
    public partial class TestTopic2
    {
        public int B;
    }
}";
        
        var type1 = ParseType(topic1Code, "TestTopic1");
        var type2 = ParseType(topic2Code, "TestTopic2");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var native1 = nativeEmitter.GenerateNativeStruct(type1, "Test");
        var native2 = nativeEmitter.GenerateNativeStruct(type2, "Test");
        var marsh1 = marshallerEmitter.GenerateMarshaller(type1, "Test");
        var marsh2 = marshallerEmitter.GenerateMarshaller(type2, "Test");
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> { (type1, "Topic1"), (type2, "Topic2") },
            "Test");
        
        var assembly = CompileToAssembly(
            topic1Code, topic2Code,
            native1, native2,
            marsh1, marsh2,
            registry);
        
        var registryType = assembly.GetType("Test.MetadataRegistry");
        var getAllMethod = registryType.GetMethod("GetAllTopics");
        var topics = (System.Collections.IEnumerable)getAllMethod.Invoke(null, null);
        
        Assert.Equal(2, topics.Cast<object>().Count());
    }

    [Fact]
    public void MetadataRegistry_GetMetadata_Runtime()
    {
        var topicCode = @"
namespace Test
{
    [DdsTopic(""TestTopic"")]
    public partial class TestMessage
    {
        public int Id;
    }
}";
        var type = ParseType(topicCode, "TestMessage");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var native = nativeEmitter.GenerateNativeStruct(type, "Test");
        var marshaller = marshallerEmitter.GenerateMarshaller(type, "Test");
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> { (type, "TestTopic") },
            "Test");
        
        var assembly = CompileToAssembly(topicCode, native, marshaller, registry);
        var registryType = assembly.GetType("Test.MetadataRegistry");
        
        var getMethod = registryType.GetMethod("GetMetadata");
        var metadata = getMethod.Invoke(null, new object[] { "TestTopic" });
        
        var metadataType = metadata.GetType();
        Assert.Equal("TestTopic", metadataType.GetProperty("TopicName").GetValue(metadata));
        Assert.Equal("TestMessage", metadataType.GetProperty("TypeName").GetValue(metadata));
    }

    [Fact]
    public void MetadataRegistry_KeyFieldIndices_Runtime()
    {
        var topicCode = @"
namespace Test
{
    [DdsTopic(""TestTopic"")]
    public partial class TestMessage
    {
        [DdsKey]
        public int Id;
        public string Name;
        [DdsKey]
        public int GroupId;
    }
}";
        var type = ParseType(topicCode, "TestMessage");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var native = nativeEmitter.GenerateNativeStruct(type, "Test");
        var marshaller = marshallerEmitter.GenerateMarshaller(type, "Test");
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> { (type, "TestTopic") },
            "Test");
        
        var assembly = CompileToAssembly(topicCode, native, marshaller, registry);
        var registryType = assembly.GetType("Test.MetadataRegistry");
        
        var getMethod = registryType.GetMethod("GetMetadata");
        var metadata = getMethod.Invoke(null, new object[] { "TestTopic" });
        
        var metadataType = metadata.GetType();
        var keyIndices = (int[])metadataType.GetProperty("KeyFieldIndices").GetValue(metadata);
        
        Assert.NotNull(keyIndices);
        Assert.Equal(2, keyIndices.Length);
        Assert.Equal(0, keyIndices[0]);
        Assert.Equal(2, keyIndices[1]);
    }

    [Fact]
    public void MetadataRegistry_TryGetMetadata_Runtime()
    {
        var topicCode = @"
namespace Test
{
    [DdsTopic(""TestTopic"")]
    public partial class TestMessage
    {
        public int Id;
    }
}";
        var type = ParseType(topicCode, "TestMessage");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var native = nativeEmitter.GenerateNativeStruct(type, "Test");
        var marshaller = marshallerEmitter.GenerateMarshaller(type, "Test");
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> { (type, "TestTopic") },
            "Test");
        
        var assembly = CompileToAssembly(topicCode, native, marshaller, registry);
        var registryType = assembly.GetType("Test.MetadataRegistry");
        
        var tryMethod = registryType.GetMethod("TryGetMetadata");
        var args = new object[] { "TestTopic", null };
        var result = (bool)tryMethod.Invoke(null, args);
        
        Assert.True(result);
        Assert.NotNull(args[1]);
        
        args = new object[] { "InvalidTopic", null };
        result = (bool)tryMethod.Invoke(null, args);
        Assert.False(result);
    }

    [Fact]
    public void MetadataRegistry_GetAllTopics_ReturnsAll_Runtime()
    {
        var topic1Code = @"
namespace Test
{
    [DdsTopic(""Topic1"")]
    public partial class TestTopic1
    {
        public int A;
    }
}";
        var topic2Code = @"
namespace Test
{
    [DdsTopic(""Topic2"")]
    public partial class TestTopic2
    {
        public int B;
    }
}";
        var topic3Code = @"
namespace Test
{
    [DdsTopic(""Topic3"")]
    public partial class TestTopic3
    {
        public int C;
    }
}";
        
        var type1 = ParseType(topic1Code, "TestTopic1");
        var type2 = ParseType(topic2Code, "TestTopic2");
        var type3 = ParseType(topic3Code, "TestTopic3");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var natives = new[] {
            nativeEmitter.GenerateNativeStruct(type1, "Test"),
            nativeEmitter.GenerateNativeStruct(type2, "Test"),
            nativeEmitter.GenerateNativeStruct(type3, "Test")
        };
        
        var marshallers = new[] {
            marshallerEmitter.GenerateMarshaller(type1, "Test"),
            marshallerEmitter.GenerateMarshaller(type2, "Test"),
            marshallerEmitter.GenerateMarshaller(type3, "Test")
        };
        
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> {
                (type1, "Topic1"),
                (type2, "Topic2"),
                (type3, "Topic3")
            },
            "Test");
        
        var allSources = new List<string> { topic1Code, topic2Code, topic3Code };
        allSources.AddRange(natives);
        allSources.AddRange(marshallers);
        allSources.Add(registry);
        
        var assembly = CompileToAssembly(allSources.ToArray());
        var registryType = assembly.GetType("Test.MetadataRegistry");
        
        var getAllMethod = registryType.GetMethod("GetAllTopics");
        var topics = (System.Collections.IEnumerable)getAllMethod.Invoke(null, null);
        
        var list = topics.Cast<object>().ToList();
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void MetadataRegistry_InvalidTopic_Runtime()
    {
        var topicCode = @"
namespace Test
{
    [DdsTopic(""ValidTopic"")]
    public partial class TestMessage
    {
        public int Id;
    }
}";
        var type = ParseType(topicCode, "TestMessage");
        
        var nativeEmitter = new NativeTypeEmitter();
        var marshallerEmitter = new MarshallerEmitter();
        var registryEmitter = new MetadataRegistryEmitter();
        
        var native = nativeEmitter.GenerateNativeStruct(type, "Test");
        var marshaller = marshallerEmitter.GenerateMarshaller(type, "Test");
        var registry = registryEmitter.GenerateRegistry(
            new List<(TypeDeclarationSyntax, string)> { (type, "ValidTopic") },
            "Test");
        
        var assembly = CompileToAssembly(topicCode, native, marshaller, registry);
        var registryType = assembly.GetType("Test.MetadataRegistry");
        
        var getMethod = registryType.GetMethod("GetMetadata");
        
        // Attempt to get invalid topic should throw
        Assert.Throws<TargetInvocationException>(() =>
            getMethod.Invoke(null, new object[] { "InvalidTopic" }));
    }
}
