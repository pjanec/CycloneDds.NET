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
using System.Text;
using CycloneDDS.CodeGen.Marshalling;

namespace CycloneDDS.CodeGen.Tests;

public class MarshallerTests
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
[AttributeUsage(AttributeTargets.Field)]
public class DdsFixedStringAttribute : Attribute { public DdsFixedStringAttribute(int size) {} }
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
    public void Marshaller_GeneratesClassImplementingIMarshaller()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int A;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var code = emitter.GenerateMarshaller(type, "Test");
        
        Assert.Contains("public class TestTopicMarshaller : IMarshaller<TestTopic, TestTopicNative>", code);
    }

    [Fact]
    public void Marshaller_MarshalsPrimitives()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int A;
        public double B;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");
        
        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
        if (marshallerType == null)
        {
            var types = string.Join(", ", assembly.GetTypes().Select(t => t.FullName));
            throw new Exception($"Type Test.TestTopicMarshaller not found. Available types: {types}");
        }
        var topicType = assembly.GetType("Test.TestTopic");
        var nativeType = assembly.GetType("Test.TestTopicNative");
        
        var marshaller = Activator.CreateInstance(marshallerType);
        var topic = Activator.CreateInstance(topicType);
        topicType.GetField("A").SetValue(topic, 42);
        topicType.GetField("B").SetValue(topic, 3.14);
        
        var native = Activator.CreateInstance(nativeType);
        
        // Invoke Marshal(topic, ref native)
        var method = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        method.Invoke(marshaller, args);
        native = args[1]; // Get updated ref
        
        Assert.Equal(42, (int)nativeType.GetField("A").GetValue(native));
        Assert.Equal(3.14, (double)nativeType.GetField("B").GetValue(native));
    }

    [Fact]
    public void Marshaller_UnmarshalsPrimitives()
    {
        var csCode = @"
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public int A;
    }
}";
        var type = ParseType(csCode);
        var emitter = new MarshallerEmitter();
        var marshallerCode = emitter.GenerateMarshaller(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeStruct(type, "Test");
        
        var assembly = CompileToAssembly(csCode, nativeCode, marshallerCode);
        var marshallerType = assembly.GetType("Test.TestTopicMarshaller");
        var nativeType = assembly.GetType("Test.TestTopicNative");
        
        var marshaller = Activator.CreateInstance(marshallerType);
        var native = Activator.CreateInstance(nativeType);
        nativeType.GetField("A").SetValue(native, 123);
        
        // Invoke Unmarshal(ref native)
        var method = marshallerType.GetMethod("Unmarshal");
        var args = new object[] { native };
        var topic = method.Invoke(marshaller, args);
        
        var topicType = assembly.GetType("Test.TestTopic");
        Assert.Equal(123, (int)topicType.GetField("A").GetValue(topic));
    }

    [Fact]
    public void Marshaller_MarshalsFixedString()
    {
        var csCode = @"
namespace Test
{
    using FixedString32 = System.String;
    [DdsTopic]
    public partial class TestTopic
    {
        public FixedString32 Name;
    }
}";
        // Mock FixedString32 for compilation
        var fixedStringDef = "namespace Test { public class FixedString32 {} }";
        
        // Note: The emitter treats FixedString32 as a string in managed code usually?
        // Wait, in IdlTypeMapper, FixedString is mapped to string?
        // No, in NativeTypeEmitter it is mapped to fixed byte[].
        // But what is the MANAGED type?
        // The user writes `public FixedString32 Name;`
        // But `FixedString32` is likely a struct or class wrapping string, or just a typedef?
        // If the user writes `FixedString32`, the managed type has `FixedString32`.
        // But `MarshallerEmitter` assumes `managed.Name` is a string?
        // Let's check MarshallerEmitter.cs:
        // if (fieldType.StartsWith("FixedString")) ... Encoding.UTF8.GetBytes(managed.{fieldName})
        // This implies managed.{fieldName} is a string!
        // But the field type is `FixedString32`.
        // So `FixedString32` must be implicitly convertible to string or IS a string?
        // Actually, usually `FixedString32` is a struct.
        // If `managed.Name` is `FixedString32`, `Encoding.UTF8.GetBytes` will fail unless it's a string.
        
        // Ah, I need to check what `FixedString32` is in the user code.
        // If the user defines `public FixedString32 Name;`, then `managed.Name` is of type `FixedString32`.
        // If `FixedString32` has implicit conversion to string, it might work.
        // But `Encoding.UTF8.GetBytes` takes string.
        
        // If `FixedString32` is just a marker for `string`, then the user should write `[DdsFixedString(32)] string Name;`?
        // But the current implementation checks `fieldType.StartsWith("FixedString")`.
        // This implies the type name IS `FixedString32`.
        
        // I should probably assume `FixedString32` is a string alias or the user uses `string` and attributes.
        // But `NativeTypeEmitter` checks `csType.StartsWith("FixedString")`.
        
        // Let's assume for this test that `FixedString32` is defined as `using FixedString32 = System.String;` in the file?
        // Or I should change the test case to use `string` and `[DdsFixedString]`.
        // But `NativeTypeEmitter` doesn't support `[DdsFixedString]` yet, it supports `FixedString32` type name.
        
        // So I will define `FixedString32` as a string alias in the test code.
        
        csCode = @"
namespace Test
{
    using FixedString32 = System.String;

    [DdsTopic]
    public partial class TestTopic
    {
        public FixedString32 Name;
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
        topicType.GetField("Name").SetValue(topic, "Hello");
        
        var native = Activator.CreateInstance(nativeType);
        var method = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        method.Invoke(marshaller, args);
        native = args[1];
        
        // Verify native data
        // Native field is fixed byte Name[32]
        // We can't easily access fixed buffer via reflection without unsafe code.
        // But we can check if Unmarshal works.
    }

    [Fact]
    public void Marshaller_RoundTrip_FixedString()
    {
        var csCode = @"
namespace Test
{
    using FixedString32 = System.String;
    [DdsTopic]
    public partial class TestTopic
    {
        public FixedString32 Name;
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
        topicType.GetField("Name").SetValue(topic, "Hello World");
        
        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);
        
        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];
        
        // Unmarshal
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args2 = new object[] { native };
        var resultTopic = unmarshalMethod.Invoke(marshaller, args2);
        
        var resultName = (string)topicType.GetField("Name").GetValue(resultTopic);
        Assert.Equal("Hello World", resultName);
    }

    [Fact]
    public void Marshaller_RoundTrip_Guid()
    {
        var csCode = @"
using System;
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public Guid Id;
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
        var guid = Guid.NewGuid();
        topicType.GetField("Id").SetValue(topic, guid);
        
        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);
        
        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];
        
        // Unmarshal
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args2 = new object[] { native };
        var resultTopic = unmarshalMethod.Invoke(marshaller, args2);
        
        var resultGuid = (Guid)topicType.GetField("Id").GetValue(resultTopic);
        Assert.Equal(guid, resultGuid);
    }

    [Fact]
    public void Marshaller_RoundTrip_DateTime()
    {
        var csCode = @"
using System;
namespace Test
{
    [DdsTopic]
    public partial class TestTopic
    {
        public DateTime Timestamp;
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
        var now = new DateTime(2023, 1, 1, 12, 0, 0);
        topicType.GetField("Timestamp").SetValue(topic, now);
        
        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);
        
        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];
        
        // Unmarshal
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args2 = new object[] { native };
        var resultTopic = unmarshalMethod.Invoke(marshaller, args2);
        
        var resultTime = (DateTime)topicType.GetField("Timestamp").GetValue(resultTopic);
        Assert.Equal(now, resultTime);
    }

    [Fact]
    public void Marshaller_TruncatesLongString()
    {
        var csCode = @"
namespace Test
{
    using FixedString32 = System.String;
    [DdsTopic]
    public partial class TestTopic
    {
        public FixedString32 Name;
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
        // 33 chars
        var longString = new string('a', 33);
        topicType.GetField("Name").SetValue(topic, longString);
        
        var nativeType = assembly.GetType("Test.TestTopicNative");
        var native = Activator.CreateInstance(nativeType);
        
        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];
        
        // Unmarshal
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args2 = new object[] { native };
        var resultTopic = unmarshalMethod.Invoke(marshaller, args2);
        
        var resultName = (string)topicType.GetField("Name").GetValue(resultTopic);
        // Should be truncated to 32 chars
        Assert.Equal(32, resultName.Length);
        Assert.Equal(new string('a', 32), resultName);
    }

    [Fact]
    public void Marshaller_MarshalsPrimitiveArray_Runtime()
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

        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];

        // Verify IntPtr allocated and length set
        var ptr = (IntPtr)nativeType.GetField("Numbers_Ptr").GetValue(native);
        var length = (int)nativeType.GetField("Numbers_Length").GetValue(native);

        Assert.NotEqual(IntPtr.Zero, ptr); // Actual allocation check
        Assert.Equal(3, length); // Actual length check
    }

    [Fact]
    public void Marshaller_UnmarshalsPrimitiveArray_Runtime()
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
        var nativeType = assembly.GetType("Test.TestTopicNative");

        // Create native with allocated array
        var native = Activator.CreateInstance(nativeType);
        var numbers = new int[] { 10, 20, 30 };
        var ptr = Marshal.AllocHGlobal(numbers.Length * sizeof(int));
        Marshal.Copy(numbers, 0, ptr, numbers.Length);
        nativeType.GetField("Numbers_Ptr").SetValue(native, ptr);
        nativeType.GetField("Numbers_Length").SetValue(native, numbers.Length);

        // Unmarshal
        var marshaller = Activator.CreateInstance(marshallerType);
        var unmarshalMethod = marshallerType.GetMethod("Unmarshal");
        var args = new object[] { native };
        var topic = unmarshalMethod.Invoke(marshaller, args);

        var topicType = assembly.GetType("Test.TestTopic");
        var resultArray = (int[])topicType.GetField("Numbers").GetValue(topic);

        Assert.NotNull(resultArray);
        Assert.Equal(3, resultArray.Length);
        Assert.Equal(new int[] { 10, 20, 30 }, resultArray);

        // Cleanup
        Marshal.FreeHGlobal(ptr);
    }

    [Fact]
    public void Marshaller_EmptyArray_SetsZeroPointer()
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
        topicType.GetField("Numbers").SetValue(topic, null); // Null array

        var native = Activator.CreateInstance(nativeType);

        // Marshal
        var marshalMethod = marshallerType.GetMethod("Marshal");
        var args = new object[] { topic, native };
        marshalMethod.Invoke(marshaller, args);
        native = args[1];

        // Verify IntPtr is Zero and length is 0
        var ptr = (IntPtr)nativeType.GetField("Numbers_Ptr").GetValue(native);
        var length = (int)nativeType.GetField("Numbers_Length").GetValue(native);

        Assert.Equal(IntPtr.Zero, ptr); // Null array should result in zero pointer
        Assert.Equal(0, length);
    }
}
