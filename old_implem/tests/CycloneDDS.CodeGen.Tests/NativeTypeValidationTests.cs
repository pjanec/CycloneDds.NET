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

public class NativeTypeValidationTests
{
    private TypeDeclarationSyntax ParseType(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
    }

    [Fact]
    public void GeneratedStruct_SizeMatchesCalculatedLayout()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public byte B;
    public int I;
    public short S;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        // Get expected size from layout calculator
        var calc = new StructLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        // Compile generated code and get actual size
        var actualSize = GetCompiledStructSize(nativeCode, "TestTypeNative");
        
        Assert.Equal(layout.TotalSize, actualSize);
    }
    
    [Fact]
    public void GeneratedStruct_FieldOffsetsMatchLayout()
    {
        var csCode = @"
[DdsTopic(""TestTopic"")]
public partial class TestType
{
    public byte B;
    public long L;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        // Get expected offsets
        var calc = new StructLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        // Compile and get actual offsets
        var offsets = GetCompiledFieldOffsets(nativeCode, "TestTypeNative");
        
        Assert.Equal(layout.Fields[0].Offset, offsets["B"]);
        Assert.Equal(layout.Fields[1].Offset, offsets["L"]);
    }
    
    [Fact]
    public void StructWithPadding_HasExplicitPaddingFields()
    {
        var csCode = @"
[DdsTopic(""PaddingTopic"")]
public partial class PaddingType
{
    public byte B;
    public int I; // Needs 3 bytes padding before
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        Assert.Contains("private fixed byte _padding0[3];", nativeCode);
    }

    [Fact]
    public void GeneratedCode_CompilesWithoutErrors()
    {
        var csCode = @"
[DdsTopic(""CompileTopic"")]
public partial class CompileType
{
    public int A;
    public double B;
    public FixedString32 C;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        var assembly = CompileToAssembly(nativeCode);
        Assert.NotNull(assembly);
    }

    [Fact]
    public void ComplexStruct_AllOffsetsCorrect()
    {
        var csCode = @"
[DdsTopic(""ComplexTopic"")]
public partial class ComplexType
{
    public byte A;
    public double B;
    public short C;
    public FixedString32 D;
}";
        
        var type = ParseType(csCode);
        var emitter = new NativeTypeEmitter();
        var nativeCode = emitter.GenerateNativeStruct(type, "TestNamespace");
        
        var calc = new StructLayoutCalculator();
        var layout = calc.CalculateLayout(type);
        
        var offsets = GetCompiledFieldOffsets(nativeCode, "ComplexTypeNative");
        var actualSize = GetCompiledStructSize(nativeCode, "ComplexTypeNative");
        
        Assert.Equal(layout.TotalSize, actualSize);
        Assert.Equal(layout.Fields[0].Offset, offsets["A"]);
        Assert.Equal(layout.Fields[1].Offset, offsets["B"]);
        Assert.Equal(layout.Fields[2].Offset, offsets["C"]);
        // Fixed buffer fields might be tricky to get offset via Marshal.OffsetOf if they are fixed buffers.
        // Marshal.OffsetOf works for fixed buffers too.
        Assert.Equal(layout.Fields[3].Offset, offsets["D"]); 
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
            // Skip padding fields
            if (field.Name.StartsWith("_padding")) continue;
            
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
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location), // System.Console
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
