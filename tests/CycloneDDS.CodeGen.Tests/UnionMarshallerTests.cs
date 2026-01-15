using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System;
using CycloneDDS.CodeGen.Marshalling;

namespace CycloneDDS.CodeGen.Tests;

public class UnionMarshallerTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .First(t => t.Identifier.Text == "TestUnion");
    }

    private Assembly CompileToAssembly(params string[] sources)
    {
        var attributes = @"
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DdsUnionAttribute : Attribute {}
[AttributeUsage(AttributeTargets.Field)]
public class DdsDiscriminatorAttribute : Attribute {}
[AttributeUsage(AttributeTargets.Field)]
public class DdsCaseAttribute : Attribute { public DdsCaseAttribute(int v) {} }
";
        var allSources = sources.Append(attributes).ToArray();
        
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = allSources.Select(s => CSharpSyntaxTree.ParseText(s, options)).ToArray();
        
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),
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
    public void Marshaller_MarshalUnion_OnlyActiveArm()
    {
        var csCode = @"
namespace Test
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
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateUnionMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestUnionMarshaller");
        var unionType = assembly.GetType("Test.TestUnion");
        var nativeType = assembly.GetType("Test.TestUnionNative");

        var marshaller = Activator.CreateInstance(marshallerType);
        var union = Activator.CreateInstance(unionType);
        unionType.GetField("D").SetValue(union, 1);
        unionType.GetField("Value").SetValue(union, 42.5f);

        var native = Activator.CreateInstance(nativeType);

        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { union, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];

        // Verify discriminator and active arm
        Assert.Equal(1, (int)nativeType.GetField("D").GetValue(native));
        Assert.Equal(42.5f, (float)nativeType.GetField("Value").GetValue(native));
    }

    [Fact]
    public void Marshaller_UnmarshalUnion_ReadsCorrectArm()
    {
        var csCode = @"
namespace Test
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
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateUnionMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestUnionMarshaller");
        var nativeType = assembly.GetType("Test.TestUnionNative");

        // Create native with D=2, Count=100
        var native = Activator.CreateInstance(nativeType);
        nativeType.GetField("D").SetValue(native, 2);
        nativeType.GetField("Count").SetValue(native, 100);

        // Unmarshal
        var marshaller = Activator.CreateInstance(marshallerType);
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args = new object[] { native };
        var union = unmarshalMethod.Invoke(marshaller, args);

        var unionType = assembly.GetType("Test.TestUnion");
        Assert.Equal(2, (int)unionType.GetField("D").GetValue(union));
        Assert.Equal(100, (int)unionType.GetField("Count").GetValue(union));
    }

    [Fact]
    public void Marshaller_Union_SwitchesOnDiscriminator()
    {
        var csCode = @"
namespace Test
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
        [DdsCase(3)]
        public byte Flag;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateUnionMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestUnionMarshaller");
        var unionType = assembly.GetType("Test.TestUnion");
        var nativeType = assembly.GetType("Test.TestUnionNative");

        // Test case 3
        var marshaller = Activator.CreateInstance(marshallerType);
        var union = Activator.CreateInstance(unionType);
        unionType.GetField("D").SetValue(union, 3);
        unionType.GetField("Flag").SetValue(union, (byte)255);

        var native = Activator.CreateInstance(nativeType);

        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { union, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];

        Assert.Equal(3, (int)nativeType.GetField("D").GetValue(native));
        Assert.Equal((byte)255, (byte)nativeType.GetField("Flag").GetValue(native));
    }
}
