using System;
using System.Collections.Generic;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Schema;

namespace CycloneDDS.CodeGen.Tests
{
    /// <summary>
    /// Tests for the MarshalFromNative(IntPtr, out T) code-generation path.
    ///
    /// Regression coverage for two defects:
    ///   1. Class types received `managedData = default` (i.e. null) before delegating
    ///      to the ref-based overload, causing a NullReferenceException on first field
    ///      assignment.
    ///   2. No null-pointer guard: a zero IntPtr (ValidData==0 DDS lifecycle samples)
    ///      caused an access violation instead of a clean default return.
    /// </summary>
    public class SerializerEmitterTests_UnmarshalFromNative : CodeGenTestBase
    {
        // -----------------------------------------------------------------------
        // 1. Generated-code inspection tests
        // -----------------------------------------------------------------------

        [Fact]
        public void EmitUnmarshalFromNative_Struct_ContainsNewInstantiation()
        {
            var type = new TypeInfo
            {
                Name = "PointStruct",
                Namespace = "TestUnmarshal",
                IsStruct = true,
                Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // After the fix, both struct and class use `new T()` inside the IntPtr overload.
            Assert.Contains("managedData = new PointStruct();", code);
        }

        [Fact]
        public void EmitUnmarshalFromNative_Class_ContainsNewInstantiation()
        {
            var type = new TypeInfo
            {
                Name = "PointClass",
                Namespace = "TestUnmarshal",
                IsClass = true,
                Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // Must allocate the managed object before delegating population.
            Assert.Contains("managedData = new PointClass();", code);
        }

        [Fact]
        public void EmitUnmarshalFromNative_DoesNotEmitBareDefault_InIntPtrOverload()
        {
            // The old (buggy) template emitted only `managedData = default;` as the
            // sole initialisation, with no subsequent `new T()`.  After the fix, the
            // bare `managedData = default;` must only appear inside the null-guard
            // branch, never as the sole pre-delegation initialization.
            var type = new TypeInfo
            {
                Name = "SimpleClass",
                Namespace = "TestUnmarshal",
                IsClass = true,
                Fields = new List<FieldInfo> { new FieldInfo { Name = "Value", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // The `new T()` assignment must come BEFORE the MarshalFromNative delegation call.
            int newIdx     = code.IndexOf("managedData = new SimpleClass();", StringComparison.Ordinal);
            int delegateIdx = code.IndexOf("MarshalFromNative(ref managedData, in", StringComparison.Ordinal);
            Assert.True(newIdx >= 0, "Expected 'managedData = new SimpleClass();' in generated code.");
            Assert.True(delegateIdx >= 0, "Expected the delegation call in generated code.");
            Assert.True(newIdx < delegateIdx, "'new T()' must appear before the delegation call.");
        }

        [Fact]
        public void EmitUnmarshalFromNative_ContainsIntPtrZeroGuard()
        {
            var type = new TypeInfo
            {
                Name = "GuardedType",
                Namespace = "TestUnmarshal",
                Fields = new List<FieldInfo> { new FieldInfo { Name = "Id", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            Assert.Contains("if (nativeData == IntPtr.Zero)", code);
        }

        [Fact]
        public void EmitUnmarshalFromNative_NullGuard_ReturnsDefault_BeforeInstantiation()
        {
            // Inside the null-guard branch the generated code must assign default and return
            // without ever calling `new T()` (zero-allocation fast path).
            var type = new TypeInfo
            {
                Name = "GuardedType2",
                Namespace = "TestUnmarshal",
                Fields = new List<FieldInfo> { new FieldInfo { Name = "Id", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string code = emitter.EmitSerializer(type, new GlobalTypeRegistry());

            // The `return;` must appear in the code (inside the null branch).
            Assert.Contains("return;", code);

            // `managedData = default;` must come before `managedData = new GuardedType2();`
            int defaultIdx = code.IndexOf("managedData = default;", StringComparison.Ordinal);
            int newIdx     = code.IndexOf("managedData = new GuardedType2();", StringComparison.Ordinal);
            Assert.True(defaultIdx >= 0, "Expected 'managedData = default;' (null-guard branch).");
            Assert.True(newIdx >= 0,     "Expected 'managedData = new GuardedType2();'.");
            Assert.True(defaultIdx < newIdx, "'managedData = default;' must come before 'new T()'.");
        }

        // -----------------------------------------------------------------------
        // 2. Execution tests – struct type regression
        // -----------------------------------------------------------------------

        [Fact]
        public void MarshalFromNativeIntPtr_Struct_RoundTrips_Correctly()
        {
            // Verifies the IntPtr entry-point works correctly for the existing struct
            // path after the refactoring.
            var type = new TypeInfo
            {
                Name = "RtStruct",
                Namespace = "TestRtNative",
                IsStruct = true,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "X", TypeName = "int" },
                    new FieldInfo { Name = "Y", TypeName = "int" }
                }
            };

            var emitter = new SerializerEmitter();
            // generateUsings:false so we control the single set of using directives
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry(), generateUsings: false);

            string typeDef = @"
namespace TestRtNative
{
    public partial struct RtStruct
    {
        public int X;
        public int Y;
    }
}
";
            string runner = @"
namespace TestRtNative
{
    public static class Runner
    {
        public static unsafe string Run(int x, int y)
        {
            try
            {
                var native = new RtStruct_Native { X = x, Y = y };
                RtStruct result;
                RtStruct.MarshalFromNative((IntPtr)(&native), out result);
                if (result.X != x) return $""X mismatch: {result.X} != {x}"";
                if (result.Y != y) return $""Y mismatch: {result.Y} != {y}"";
                return ""SUCCESS"";
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}
";
            string fullCode = @"using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
" + generatedCode + typeDef + runner;

            var assembly = CompileToAssembly("UnmarshalTest_Struct", fullCode);
            var runnerType = assembly.GetType("TestRtNative.Runner");
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[] { 42, 99 });
            Assert.Equal("SUCCESS", result);
        }

        // -----------------------------------------------------------------------
        // 3. Execution tests – class type (the primary regression)
        // -----------------------------------------------------------------------

        [Fact]
        public void MarshalFromNativeIntPtr_Class_DoesNotThrowNullReferenceException()
        {
            // Before the fix: `managedData = default` (null) was passed by-ref into the
            // population overload.  Assigning any field to a null class reference throws
            // NullReferenceException. This test would have FAILED on the old codegen.
            var type = new TypeInfo
            {
                Name = "RtClass",
                Namespace = "TestRtNativeClass",
                IsClass = true,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Score", TypeName = "double" }
                }
            };

            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry(), generateUsings: false);

            string typeDef = @"
namespace TestRtNativeClass
{
    public partial class RtClass
    {
        public int Id;
        public double Score;
    }
}
";
            string runner = @"
namespace TestRtNativeClass
{
    public static class Runner
    {
        public static unsafe string Run(int id, double score)
        {
            try
            {
                var native = new RtClass_Native { Id = id, Score = score };
                RtClass result;
                RtClass.MarshalFromNative((IntPtr)(&native), out result);
                if (result == null) return ""result is null"";
                if (result.Id != id) return $""Id mismatch: {result.Id} != {id}"";
                if (Math.Abs(result.Score - score) > 0.0001) return $""Score mismatch: {result.Score} != {score}"";
                return ""SUCCESS"";
            }
            catch (NullReferenceException nre)
            {
                return ""NullReferenceException: "" + nre.Message;
            }
            catch (Exception ex) { return ex.ToString(); }
        }
    }
}
";
            string fullCode = @"using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
" + generatedCode + typeDef + runner;

            var assembly = CompileToAssembly("UnmarshalTest_Class", fullCode);
            var runnerType = assembly.GetType("TestRtNativeClass.Runner");
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[] { 7, 3.14 });
            Assert.Equal("SUCCESS", result);
        }

        // -----------------------------------------------------------------------
        // 4. Execution tests – IntPtr.Zero guard
        // -----------------------------------------------------------------------

        [Fact]
        public void MarshalFromNativeIntPtr_Struct_NullPointer_ReturnsDefault()
        {
            var type = new TypeInfo
            {
                Name = "NullPtrStruct",
                Namespace = "TestNullPtrStruct",
                IsStruct = true,
                Fields = new List<FieldInfo> { new FieldInfo { Name = "Val", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry(), generateUsings: false);

            string typeDef = @"
namespace TestNullPtrStruct
{
    public partial struct NullPtrStruct { public int Val; }
}
";
            string runner = @"
namespace TestNullPtrStruct
{
    public static class Runner
    {
        public static string Run()
        {
            try
            {
                NullPtrStruct result;
                NullPtrStruct.MarshalFromNative(IntPtr.Zero, out result);
                // For a struct, default is zeroed memory.
                if (result.Val != 0) return $""Expected default (0), got {result.Val}"";
                return ""SUCCESS"";
            }
            catch (Exception ex) { return ""Exception: "" + ex.ToString(); }
        }
    }
}
";
            string fullCode = @"using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
" + generatedCode + typeDef + runner;

            var assembly = CompileToAssembly("UnmarshalTest_NullStruct", fullCode);
            var runnerType = assembly.GetType("TestNullPtrStruct.Runner");
            var result = (string)runnerType.GetMethod("Run").Invoke(null, Array.Empty<object>());
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void MarshalFromNativeIntPtr_Class_NullPointer_ReturnsNull()
        {
            // Before the fix, IntPtr.Zero would be blindly dereferenced, causing an
            // access violation.  After the fix, the method must return null (default
            // for a reference type) without crashing.
            var type = new TypeInfo
            {
                Name = "NullPtrClass",
                Namespace = "TestNullPtrClass",
                IsClass = true,
                Fields = new List<FieldInfo> { new FieldInfo { Name = "Val", TypeName = "int" } }
            };

            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry(), generateUsings: false);

            string typeDef = @"
namespace TestNullPtrClass
{
    public partial class NullPtrClass { public int Val; }
}
";
            string runner = @"
namespace TestNullPtrClass
{
    public static class Runner
    {
        public static string Run()
        {
            try
            {
                NullPtrClass result;
                NullPtrClass.MarshalFromNative(IntPtr.Zero, out result);
                // For a class, default is null.
                if (result != null) return ""Expected null result for IntPtr.Zero"";
                return ""SUCCESS"";
            }
            catch (Exception ex) { return ""Exception: "" + ex.ToString(); }
        }
    }
}
";
            string fullCode = @"using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
" + generatedCode + typeDef + runner;

            var assembly = CompileToAssembly("UnmarshalTest_NullClass", fullCode);
            var runnerType = assembly.GetType("TestNullPtrClass.Runner");
            var result = (string)runnerType.GetMethod("Run").Invoke(null, Array.Empty<object>());
            Assert.Equal("SUCCESS", result);
        }
    }
}
