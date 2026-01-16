using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using CycloneDDS.CodeGen.Emitters;
using CycloneDDS.CodeGen.Layout;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System;

namespace CycloneDDS.CodeGen.Tests;

public class UnionNativeTypeValidationTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    [Fact]
    public void Union_HasExplicitLayout()
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
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeUnion(type, "TestNamespace");
        
        Assert.Contains("[StructLayout(LayoutKind.Explicit)]", nativeCode);
    }

    [Fact]
    public void Union_DiscriminatorAtOffset0()
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
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeUnion(type, "TestNamespace");
        
        var offsets = GetCompiledFieldOffsets(nativeCode, "TestUnionNative");
        Assert.Equal(0, offsets["D"]);
    }

    [Fact]
    public void Union_PayloadAtCorrectOffset()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public byte D; // 1 byte
    [DdsCase(1)]
    public long A; // 8 bytes, align 8
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeUnion(type, "TestNamespace");
        
        // Payload offset should be 8 (AlignUp(1, 8))
        var offsets = GetCompiledFieldOffsets(nativeCode, "TestUnionNative");
        Assert.Equal(8, offsets["A"]);
    }

    [Fact]
    public void Union_SizeMatchesCalculatedLayout()
    {
        var csCode = @"
[DdsUnion]
public partial class TestUnion
{
    [DdsDiscriminator]
    public byte D;
    [DdsCase(1)]
    public long A;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeUnion(type, "TestNamespace");
        
        var calc = new UnionLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        var actualSize = GetCompiledStructSize(nativeCode, "TestUnionNative");
        Assert.Equal(layout.TotalSize, actualSize);
    }

    [Fact]
    public void UnionWithMultipleArms_AllAtPayloadOffset()
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
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeUnion(type, "TestNamespace");
        
        var offsets = GetCompiledFieldOffsets(nativeCode, "MultiUnionNative");
        var payloadOffset = offsets["A"];
        
        Assert.Equal(payloadOffset, offsets["B"]);
    }

    private int GetCompiledStructSize(string code, string typeName)
    {
        var assembly = CompileToAssembly(code);
        var type = assembly.GetType($"TestNamespace.{typeName}");
        return Marshal.SizeOf(type);
    }
    
    private Dictionary<string, int> GetCompiledFieldOffsets(string code, string typeName)
    {
        var assembly = CompileToAssembly(code);
        var type = assembly.GetType($"TestNamespace.{typeName}");
        
        var offsets = new Dictionary<string, int>();
        foreach (var field in type.GetFields())
        {
            var offset = Marshal.OffsetOf(type, field.Name).ToInt32();
            offsets[field.Name] = offset;
        }
        return offsets;
    }
    
    private Assembly CompileToAssembly(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(StructLayoutAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"))
        };
        
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
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
}
