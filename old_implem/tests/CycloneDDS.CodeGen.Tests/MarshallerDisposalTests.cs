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

namespace CycloneDDS.CodeGen.Tests;

public class MarshallerDisposalTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    private Assembly CompileToAssembly(params string[] sources)
    {
        var attributes = @"
using System;
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DdsTopicAttribute : Attribute {}
";
        var allSources = sources.Append(attributes).ToArray();
        
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = allSources.Select(s => CSharpSyntaxTree.ParseText(s, options)).ToArray();
        
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(CycloneDDS.CodeGen.Marshalling.IMarshaller<,>).Assembly.Location)
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
            throw new Exception($"Compilation failed:\n{errors}");
        }
        
        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    [Fact]
    public void Marshaller_Dispose_FreesAllocatedMemory()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int[] Numbers;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
        var topicType = assembly.GetType("Test.TestTopic");
        var nativeType = assembly.GetType("Test.TestTopicNative");

        var marshaller = Activator.CreateInstance(marshallerType);
        var topic = Activator.CreateInstance(topicType);
        topicType.GetField("Numbers").SetValue(topic, new int[] { 1, 2, 3 });

        var native = Activator.CreateInstance(nativeType);

        // Marshal (allocates memory)
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];

        // Verify allocation happened
        var ptr = (IntPtr)nativeType.GetField("Numbers_Ptr").GetValue(native);
        Assert.NotEqual(IntPtr.Zero, ptr);

        // Dispose
        var disposeMethod = marshallerType.GetMethod("Dispose");
        disposeMethod.Invoke(marshaller, null);

        // Verify internal tracking field is cleared
        var trackingField = marshallerType.GetField("_allocatedNumbers_Ptr", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var trackedPtr = (IntPtr)trackingField.GetValue(marshaller);
        Assert.Equal(IntPtr.Zero, trackedPtr);
    }

    [Fact]
    public void Marshaller_MultipleArrays_DisposesAll()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int[] Numbers;
        public float[] Values;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
        var topicType = assembly.GetType("Test.TestTopic");

        var marshaller = Activator.CreateInstance(marshallerType);
        var topic = Activator.CreateInstance(topicType);
        topicType.GetField("Numbers").SetValue(topic, new int[] { 1, 2 });
        topicType.GetField("Values").SetValue(topic, new float[] { 1.5f, 2.5f });

        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);

        // Marshal both arrays
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);

        // Dispose
        var disposeMethod = marshallerType.GetMethod("Dispose");
        disposeMethod.Invoke(marshaller, null);

        // Verify both tracking fields cleared
        var numbersField = marshallerType.GetField("_allocatedNumbers_Ptr",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var valuesField = marshallerType.GetField("_allocatedValues_Ptr",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Equal(IntPtr.Zero, (IntPtr)numbersField.GetValue(marshaller));
        Assert.Equal(IntPtr.Zero, (IntPtr)valuesField.GetValue(marshaller));
    }

    [Fact]
    public void Marshaller_DoubleDispose_Safe()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int[] Numbers;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
        var topicType = assembly.GetType("Test.TestTopic");

        var marshaller = Activator.CreateInstance(marshallerType);
        var topic = Activator.CreateInstance(topicType);
        topicType.GetField("Numbers").SetValue(topic, new int[] { 1, 2, 3 });

        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);

        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);

        // Dispose twice - should not throw
        var disposeMethod = marshallerType.GetMethod("Dispose");
        disposeMethod.Invoke(marshaller, null);
        disposeMethod.Invoke(marshaller, null); // Second dispose should be safe
    }

    [Fact]
    public void Marshaller_WithoutArrays_NoDispose()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int Id;
        public double Value;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");

        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");

        // Marshaller without arrays should NOT have Dispose method
        var disposeMethod = marshallerType.GetMethod("Dispose");
        Assert.Null(disposeMethod);
        
        // Should NOT implement IDisposable
        var interfaces = marshallerType.GetInterfaces();
        Assert.DoesNotContain(typeof(IDisposable), interfaces);
    }
}
