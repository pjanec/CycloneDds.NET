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

        [Fact]
        public void ToManaged_ComplexNested_CopiesCorrectly()
        {
            var innerType = new TypeInfo
            {
                Name = "InnerStruct",
                Namespace = "Test.ToManaged",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Message", TypeName = "string" },
                    new FieldInfo { Name = "Numbers", TypeName = "double[]" }
                }
            };

            var complexType = new TypeInfo
            {
                Name = "ComplexStruct",
                Namespace = "Test.ToManaged",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Single", TypeName = "InnerStruct" },
                    new FieldInfo { Name = "List", TypeName = "InnerStruct[]" }
                }
            };

            var reg = new GlobalTypeRegistry();
            reg.RegisterLocal(innerType, "test.cs", "test", "module");
            reg.RegisterLocal(complexType, "test.cs", "test", "module");

            var viewEmitter = new ViewEmitter();
            var innerViewCode = viewEmitter.EmitViewStruct(innerType, reg);
            var complexViewCode = viewEmitter.EmitViewStruct(complexType, reg);

            var supportCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace Test.ToManaged
{
    [StructLayout(LayoutKind.Sequential)]
    public struct InnerStruct
    {
        public int Id;
        public string Message;
        public System.Collections.Generic.List<double> Numbers;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexStruct
    {
        public InnerStruct Single;
        public System.Collections.Generic.List<InnerStruct> List;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DdsSequence
    {
        public uint Maximum;
        public uint Length;
        public IntPtr Buffer;
        public bool Release;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InnerStruct_Native
    {
        public int Id;
        public IntPtr Message;
        public DdsSequence Numbers;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ComplexStruct_Native
    {
        public InnerStruct_Native Single;
        public DdsSequence List; 
    }

    public class TestRunner
    {
        public static unsafe void Run()
        {
            var native = new ComplexStruct_Native();
            
            // 1. Setup 'Single' (InnerStruct)
            native.Single.Id = 101;
            
            var msgBytes = System.Text.Encoding.UTF8.GetBytes(""NestedMessage\0"");
            var hMsg = Marshal.AllocHGlobal(msgBytes.Length);
            Marshal.Copy(msgBytes, 0, hMsg, msgBytes.Length);
            native.Single.Message = hMsg;

            var numbers = new double[] { 1.1, 2.2, 3.3 };
            var hNumbers = Marshal.AllocHGlobal(numbers.Length * sizeof(double));
            Marshal.Copy(numbers, 0, hNumbers, numbers.Length);
            native.Single.Numbers.Length = (uint)numbers.Length;
            native.Single.Numbers.Buffer = hNumbers;

            // 2. Setup 'List' (Sequence<InnerStruct>)
            var listCount = 2;
            var hList = Marshal.AllocHGlobal(listCount * Marshal.SizeOf<InnerStruct_Native>());
            var nativeListPtr = (InnerStruct_Native*)hList;
            
            // List[0]
            nativeListPtr[0].Id = 201;
            var msg1Bytes = System.Text.Encoding.UTF8.GetBytes(""ListMsg1\0"");
            var hMsg1 = Marshal.AllocHGlobal(msg1Bytes.Length);
            Marshal.Copy(msg1Bytes, 0, hMsg1, msg1Bytes.Length);
            nativeListPtr[0].Message = hMsg1;
            nativeListPtr[0].Numbers.Length = 0;
            nativeListPtr[0].Numbers.Buffer = IntPtr.Zero;

            // List[1]
            nativeListPtr[1].Id = 202;
            var msg2Bytes = System.Text.Encoding.UTF8.GetBytes(""ListMsg2\0"");
            var hMsg2 = Marshal.AllocHGlobal(msg2Bytes.Length);
            Marshal.Copy(msg2Bytes, 0, hMsg2, msg2Bytes.Length);
            nativeListPtr[1].Message = hMsg2;
            
            // List[1].Numbers -> { 9.9 }
            var nums2 = new double[] { 9.9 };
            var hNums2 = Marshal.AllocHGlobal(nums2.Length * sizeof(double));
            Marshal.Copy(nums2, 0, hNums2, nums2.Length);
            nativeListPtr[1].Numbers.Length = (uint)nums2.Length;
            nativeListPtr[1].Numbers.Buffer = hNums2;

            native.List.Length = (uint)listCount;
            native.List.Buffer = hList;

            try 
            {
                unsafe
                {
                    var view = new ComplexStructView(&native);
                    var managed = view.ToManaged();

                    // Assertions
                    if (managed.Single.Id != 101) throw new Exception($""Single.Id mismatch: {managed.Single.Id}"");
                    if (managed.Single.Message != ""NestedMessage"") throw new Exception($""Single.Message mismatch: {managed.Single.Message}"");
                    if (managed.Single.Numbers.Count != 3) throw new Exception($""Single.Numbers.Count mismatch: {managed.Single.Numbers.Count}"");
                    if (Math.Abs(managed.Single.Numbers[1] - 2.2) > 0.001) throw new Exception($""Single.Numbers[1] mismatch: {managed.Single.Numbers[1]}"");

                    if (managed.List.Count != 2) throw new Exception($""List.Count mismatch: {managed.List.Count}"");
                    
                    if (managed.List[0].Id != 201) throw new Exception($""List[0].Id mismatch: {managed.List[0].Id}"");
                    if (managed.List[0].Message != ""ListMsg1"") throw new Exception($""List[0].Message mismatch: {managed.List[0].Message}"");
                    if (managed.List[0].Numbers.Count != 0) throw new Exception($""List[0].Numbers.Count mismatch: {managed.List[0].Numbers.Count}"");

                    if (managed.List[1].Id != 202) throw new Exception($""List[1].Id mismatch: {managed.List[1].Id}"");
                    if (managed.List[1].Numbers.Count != 1) throw new Exception($""List[1].Numbers.Count mismatch: {managed.List[1].Numbers.Count}"");
                    if (Math.Abs(managed.List[1].Numbers[0] - 9.9) > 0.001) throw new Exception($""List[1].Numbers[0] mismatch: {managed.List[1].Numbers[0]}"");
                }
            }
            finally
            {
                 Marshal.FreeHGlobal(hMsg);
                 Marshal.FreeHGlobal(hNumbers);
                 Marshal.FreeHGlobal(hMsg1);
                 Marshal.FreeHGlobal(hMsg2);
                 Marshal.FreeHGlobal(hNums2);
                 Marshal.FreeHGlobal(hList);
            }
        }
    }
}
";
            RunGeneratedCode("Test.ToManaged.TestRunner", "Run", supportCode, innerViewCode, complexViewCode);
        }
    }
}
