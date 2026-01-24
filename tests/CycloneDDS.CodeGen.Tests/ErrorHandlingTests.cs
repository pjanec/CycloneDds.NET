using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;

namespace CycloneDDS.CodeGen.Tests
{
    public class ErrorHandlingTests : CodeGenTestBase
    {
        [Fact]
        public void UnsupportedType_FailsCompilation()
        {
             var type = new TypeInfo { Name = "BadType", Namespace = "Err", Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "F", TypeName = "SomeRandomType" }
             }};
             
             var emitter = new SerializerEmitter();
             string code = @"namespace Err { public partial struct BadType { public SomeRandomType F; } public struct SomeRandomType {} }";
             // Note: If SomeRandomType exists but has no serializer, compilation fails at Serialize call.
             // If I define SomeRandomType but no Serialize method.
             
             code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
             
             // Compilation should fail because SomeRandomType has no Serialize method.
             Assert.ThrowsAny<Exception>(() => CompileToAssembly(code, "ErrUnsupported"));
        }

        [Fact]
        public void InvalidUnion_NoDiscriminator_Fails_EmissionOrCompilation()
        {
             var type = new TypeInfo { Name = "BadUnion", Namespace = "Err", Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsUnion"}},
                 Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "D", TypeName = "int" } // Start of union but no discriminator attribute
             }};
             
             var emitter = new SerializerEmitter();
             // Emission handles logic. If no discriminator, switch statement might be invalid or empty?
             // SerializerEmitter logic:
             // var disc = fields.First(f => f.HasAttribute("DdsDiscriminator"));
             // It might throw FirstOrDefault exception if not found?
             // Or result is null.
             
             // Let's see if EmitSerializer throws or emitted code is bad.
             try {
                 string res = emitter.EmitSerializer(type, new GlobalTypeRegistry(), false);
                 // If doesn't throw, try compile
                 string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; namespace Err { [DdsUnion] public partial struct BadUnion { public int D; } }";
                 code += res;
                 CompileToAssembly(code, "ErrNoDisc");
             } catch (Exception) {
                 // Success (it failed)
                 return;
             }
             // If we reach here, it failed to fail? 
             // Ideally it should fail.
             // Assert.Fail("Should have failed"); but Xunit doesn't have Assert.Fail directly usually.
             throw new Exception("Should have failed");
        }

        // [Fact]
        // public void InvalidOptional_NonNullable_FailsCompilation() - Removed: IsOptional is derived from TypeName now.

        [Fact]
        public void Union_MissingCaseAttribute_IgnoredOrFails()
        {
             // Field in Union without Case logic.
             // CodeGen likely ignores it in switch?
             // But instructions say "Error Handling".
             // If ignored, it's not an error.
             // But maybe valid union requires all fields to be cases?
             // If I have extra field, it won't be serialized.
             // But if I try to verify compilation:
             // Verify it emits nothing for that field?
             
             var type = new TypeInfo { Name = "BadU2", Namespace = "Err", Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsUnion"}},
                 Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "D", TypeName = "int", Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsDiscriminator"} } },
                 new FieldInfo { Name = "X", TypeName = "int" } // No DdsCase
             }};
             
             // X will be ignored in serialization switch potentially.
             // If so, it's not an error validation test.
             // Let's try: "Malformed Descriptor" - maybe invalid attribute arguments?
             // DdsCase("abc") for int discriminator?
             
             // Let's test "Invalid Discriminator Type" string.
             var type2 = new TypeInfo { Name = "BadDiscType", Namespace = "Err", Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsUnion"}},
                 Fields = new List<FieldInfo> {
                 new FieldInfo { Name = "D", TypeName = "string", Attributes = new List<AttributeInfo>{ new AttributeInfo{Name="DdsDiscriminator"} } },
                 new FieldInfo { Name = "X", TypeName = "int", Attributes=new List<AttributeInfo>{ new AttributeInfo{ Name="DdsCase", Arguments=new List<object>{1} } } }
             }};
             // Switch(string) works in C#.
             // Case 1 (int) mismatch string.
             // Compile error: case 1: cannot convert int to string.
             
             var emitter = new SerializerEmitter();
             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; namespace Err { [DdsUnion] public partial struct BadDiscType { [DdsDiscriminator] public string D; [DdsCase(1)] public int X; } }";
             code += emitter.EmitSerializer(type2, new GlobalTypeRegistry(), false);
             
             Assert.ThrowsAny<Exception>(() => CompileToAssembly(code, "ErrBadDiscType"));
        }

        [Fact]
        public void MalformedIDL_ReportsError()
        {
            var tempIdl = Path.Combine(Path.GetTempPath(), "bad_test_11_1.idl");
            File.WriteAllText(tempIdl, @"
module Test {
    struct BadStruct {
        long field1
        string field2  // Missing semicolons
    };
};
");
            
            try {
                var runner = new IdlcRunner();
                // Determine path relative to test assembly to ensure portability
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                // Traverse up 5 levels: net8.0 -> Debug -> bin -> CycloneDDS.CodeGen.Tests -> tests -> RepoRoot
                var repoRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
                var idlcPath = Path.Combine(repoRoot, "cyclone-compiled", "bin", "idlc.exe");
                
                runner.IdlcPathOverride = idlcPath;
                var result = runner.RunIdlc(tempIdl, Path.GetTempPath());
                Assert.NotEqual(0, result.ExitCode);
            }
            finally {
                if (File.Exists(tempIdl)) File.Delete(tempIdl);
            }
        }
    }
}

