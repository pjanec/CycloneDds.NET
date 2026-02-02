using Xunit;
using Xunit.Abstractions;
using CycloneDDS.CodeGen;
using CycloneDDS.CodeGen.Emitters;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Reflection;

namespace CycloneDDS.CodeGen.Tests
{
    public class ToManagedTests : CodeGenTestBase
    {
        private readonly ITestOutputHelper _output;

        public ToManagedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private void RunGeneratedCode(string runnerTypeName, string methodName, params string[] codes)
        {
            var asm = CompileToAssembly("TestAssembly_" + Guid.NewGuid().ToString("N"), codes);
            var type = asm.GetType(runnerTypeName);
            if (type == null) throw new Exception($"Runner Type {runnerTypeName} not found");
            var method = type.GetMethod(methodName);
            if (method == null) throw new Exception($"Runner Method {methodName} not found");
            
            try
            {
                method.Invoke(null, null);
            }
            catch(System.Reflection.TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        [Fact]
        public void ToManaged_PrimitivesAndStrings_CopiesCorrectly()
        {
            var type = new TypeInfo
            {
                Name = "PrimitiveTest",
                Namespace = "Test.ToManaged",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "IntVal", TypeName = "int" },
                    new FieldInfo { Name = "DoubleVal", TypeName = "double" },
                    new FieldInfo { Name = "StrVal", TypeName = "string" },
                    new FieldInfo { Name = "OptInt", TypeName = "int?" }
                }
            };
            
            var reg = new GlobalTypeRegistry();
            reg.RegisterLocal(type, "test.cs", "test", "module");

            var viewEmitter = new ViewEmitter();
            var viewCode = viewEmitter.EmitViewStruct(type, reg);

            var supportCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace Test.ToManaged
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PrimitiveTest
    {
        public int IntVal;
        public double DoubleVal;
        public string StrVal;
        public int? OptInt; 
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PrimitiveTest_Native
    {
        public int IntVal;
        public double DoubleVal;
        public IntPtr StrVal;
        public IntPtr OptInt; 
    }

    public class TestRunner
    {
        public static void Run()
        {
            var native = new PrimitiveTest_Native
            {
                IntVal = 42,
                DoubleVal = 3.14
            };
            
            var strBytes = System.Text.Encoding.UTF8.GetBytes(""Hello World\0"");
            var hStr = Marshal.AllocHGlobal(strBytes.Length);
            Marshal.Copy(strBytes, 0, hStr, strBytes.Length);
            native.StrVal = hStr;

            int optVal = 99;
            var hOpt = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(hOpt, optVal);
            native.OptInt = hOpt;

            try
            {
                unsafe
                {
                    var view = new PrimitiveTestView(&native);
                    var managed = view.ToManaged();
                    
                    if (managed.IntVal != 42) throw new Exception($""IntVal mismatch: {managed.IntVal}"");
                    if (Math.Abs(managed.DoubleVal - 3.14) > 0.001) throw new Exception($""DoubleVal mismatch: {managed.DoubleVal}""); 
                    if (managed.StrVal != ""Hello World"") throw new Exception($""StrVal mismatch: '{managed.StrVal}'"");
                    if (managed.OptInt != 99) throw new Exception($""OptInt mismatch: {managed.OptInt}"");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(hStr);
                Marshal.FreeHGlobal(hOpt);
            }
        }
    }
}
";
            RunGeneratedCode("Test.ToManaged.TestRunner", "Run", supportCode, viewCode);
        }

        [Fact]
        public void ToManaged_Alias_CopiesCorrectly()
        {
            var type = new TypeInfo
            {
                Name = "AliasTest",
                Namespace = "Test.ToManaged",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Val", TypeName = "MyInt" }
                }
            };

            var reg = new GlobalTypeRegistry();
            reg.RegisterLocal(type, "test.cs", "test", "module");
            
            reg.RegisterExternal("MyInt", "defs", "mod");
            if (reg.TryGetDefinition("MyInt", out var aliasDef))
            {
                aliasDef.IsAlias = true;
                aliasDef.BaseType = "int";
            }
            
            var viewEmitter = new ViewEmitter();
            var viewCode = viewEmitter.EmitViewStruct(type, reg);
            
            var supportCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace Test.ToManaged
{
    using MyInt = System.Int32;

    [StructLayout(LayoutKind.Sequential)]
    public struct AliasTest
    {
        public MyInt Val;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AliasTest_Native
    {
        public int Val;
    }

    public class TestRunner
    {
        public static void Run()
        {
            var native = new AliasTest_Native { Val = 123 };
            unsafe
            {
                var view = new AliasTestView(&native);
                var managed = view.ToManaged();
                if (managed.Val != 123) throw new Exception($""Val mismatch: {managed.Val}"");
            }
        }
    }
}
";
            RunGeneratedCode("Test.ToManaged.TestRunner", "Run", supportCode, viewCode);
        }

        [Fact]
        public void ToManaged_FixedArray_Flattened_CopiesCorrectly()
        {
            var type = new TypeInfo
            {
                Name = "ArrayTest",
                Namespace = "Test.ToManaged",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Matrix", TypeName = "double[]", 
                        Attributes = new List<AttributeInfo> { 
                            new AttributeInfo { Name = "ArrayLength", Arguments = new List<object>{ 6 } }
                        } 
                    }
                }
            };
            
            var reg = new GlobalTypeRegistry();
            reg.RegisterLocal(type, "test.cs", "test", "module");

            var viewEmitter = new ViewEmitter();
            var viewCode = viewEmitter.EmitViewStruct(type, reg);

            var supportCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace Test.ToManaged
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ArrayTest
    {
        public double[] Matrix;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ArrayTest_Native
    {
        public unsafe fixed double Matrix[6];
    }

    public class TestRunner
    {
        public static void Run()
        {
            var native = new ArrayTest_Native();
            unsafe
            {
                for(int i=0; i<6; i++) native.Matrix[i] = i * 1.1;
                
                var view = new ArrayTestView(&native);
                var managed = view.ToManaged();
                
                if (managed.Matrix.Length != 6) throw new Exception(""Length mismatch"");
                if (Math.Abs(managed.Matrix[5] - 5.5) > 0.001) throw new Exception(""Value mismatch"");
            }
        }
    }
}
";
            RunGeneratedCode("Test.ToManaged.TestRunner", "Run", supportCode, viewCode);
        }
    }
}
