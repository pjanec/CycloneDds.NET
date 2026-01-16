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

public class UnionManagedViewTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    [Fact]
    public void UnionManagedView_IsRefStruct()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedUnion(type, "TestNamespace");
        
        Assert.Contains("public ref struct TestUnionManaged", managedCode);
    }

    [Fact]
    public void UnionManagedView_HasDiscriminatorProperty()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int Disc;
    [DdsCase(1)]
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedUnion(type, "TestNamespace");
        
        Assert.Contains("public int Disc => _native.Disc;", managedCode);
    }

    [Fact]
    public void UnionManagedView_TryArm_ReturnsNullForWrongDiscriminator()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public float Value;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var code = emitter.GenerateManagedUnion(type, "Test");
        
        // We need native type code to compile
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");

        var assembly = CompileToAssembly(code, nativeCode);
        var nativeType = assembly.GetType("Test.TestUnionNative");
        var managedType = assembly.GetType("Test.TestUnionManaged");
        
        // Create native instance with discriminator 2 (not 1)
        var native = Activator.CreateInstance(nativeType);
        nativeType.GetField("D").SetValue(native, 2);
        
        // Create managed view
        // Managed view is ref struct, so we can't use Activator.CreateInstance or standard reflection easily for constructor
        // But we can use a helper method in the assembly to create it, or just inspect the code logic.
        // Since we can't easily invoke ref struct methods via reflection (it's tricky), 
        // we will rely on compiling a test harness that uses it.
        
        var testHarness = @"
using System;
using Test;

public class Harness
{
    public static float? Test(int disc, float val)
    {
        var native = new TestUnionNative();
        native.D = disc;
        // We can't easily set the union arm value without unsafe code or knowing offset, 
        // but for this test we expect null so value doesn't matter.
        
        var managed = new TestUnionManaged(ref native);
        return managed.TryValue();
    }
}
";
        var harnessAssembly = CompileToAssembly(code, nativeCode, testHarness);
        var harnessType = harnessAssembly.GetType("Harness");
        var testMethod = harnessType.GetMethod("Test");
        
        var result = testMethod.Invoke(null, new object[] { 2, 0.0f });
        Assert.Null(result);
    }

    [Fact]
    public void UnionManagedView_TryArm_ReturnsValueForCorrectDiscriminator()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public float Value;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var code = emitter.GenerateManagedUnion(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");
        
        Console.WriteLine("MANAGED CODE:");
        Console.WriteLine(code);
        Console.WriteLine("NATIVE CODE:");
        Console.WriteLine(nativeCode);

        var testHarness = @"
using System;
using Test;

public class Harness
{
    public static float? Test(int disc, float val)
    {
        var native = new TestUnionNative();
        native.D = disc;
        // Set value - since it's explicit layout, we can set the field directly if we are careful
        // But wait, C# compiler might complain about overlapping fields if not unsafe? 
        // The generated native struct is unsafe.
        native.Value = val;
        
        var managed = new TestUnionManaged(ref native);
        return managed.TryValue();
    }
}
";
        var harnessAssembly = CompileToAssembly(code, nativeCode, testHarness);
        var harnessType = harnessAssembly.GetType("Harness");
        var testMethod = harnessType.GetMethod("Test");
        
        var result = testMethod.Invoke(null, new object[] { 1, 42.5f });
        Assert.Equal(42.5f, (float?)result);
    }

    [Fact]
    public void UnionWithMultipleArms_AllHaveTryMethods()
    {
        var csCode = @"
[DdsUnion]
public partial class MultiUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public int A;
    [DdsCase(2)]
    public float B;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedUnion(type, "TestNamespace");
        
        Assert.Contains("public int? TryA()", managedCode);
        Assert.Contains("public float? TryB()", managedCode);
    }

    [Fact]
    public void UnionManagedView_CompilesWithoutErrors()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var managedCode = emitter.GenerateManagedUnion(type, "TestNamespace");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "TestNamespace");
        
        var assembly = CompileToAssembly(managedCode, nativeCode);
        Assert.NotNull(assembly);
    }

    [Fact]
    public void UnionManagedView_CanAccessNativeData()
    {
        // This is covered by UnionManagedView_TryArm_ReturnsValueForCorrectDiscriminator
        // But let's add a specific one for discriminator access
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public int D;
    [DdsCase(1)]
    public int A;
}";
        
        var type = ParseType(csCode);
        var emitter = new ManagedViewEmitter();
        var code = emitter.GenerateManagedUnion(type, "Test");
        var nativeEmitter = new NativeTypeEmitter();
        var nativeCode = nativeEmitter.GenerateNativeUnion(type, "Test");

        var testHarness = @"
using System;
using Test;

public class Harness
{
    public static int GetDisc(int disc)
    {
        var native = new TestUnionNative();
        native.D = disc;
        var managed = new TestUnionManaged(ref native);
        return managed.D;
    }
}
";
        var harnessAssembly = CompileToAssembly(code, nativeCode, testHarness);
        var harnessType = harnessAssembly.GetType("Harness");
        var testMethod = harnessType.GetMethod("GetDisc");
        
        var result = testMethod.Invoke(null, new object[] { 123 });
        Assert.Equal(123, (int)result);
    }

    private Assembly CompileToAssembly(params string[] sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s, options)).ToArray();
        
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"))
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
}
