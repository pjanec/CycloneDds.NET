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
    public class SequenceBoolTests : CodeGenTestBase
    {
        private readonly ITestOutputHelper _output;

        public SequenceBoolTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private void RunGeneratedCode(string runnerTypeName, string methodName, params string[] codes)
        {
            var asm = CompileToAssembly("BoolSeqTest_" + Guid.NewGuid().ToString("N"), codes);
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
        public void ToManaged_BoolSequence_CopiesCorrectly()
        {
            var structType = new TypeInfo
            {
                Name = "BoolSeqStruct",
                Namespace = "Test.SeqBool",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Flags", TypeName = "bool[]" }
                }
            };

            var reg = new GlobalTypeRegistry();
            reg.RegisterLocal(structType, "test.cs", "test", "module");

            var viewEmitter = new ViewEmitter();
            var viewCode = viewEmitter.EmitViewStruct(structType, reg);

            var supportCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using System.Collections.Generic;

namespace Test.SeqBool
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BoolSeqStruct
    {
        public List<bool> Flags;
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
    public struct BoolSeqStruct_Native
    {
        public DdsSequence Flags;
    }

    public class TestRunner
    {
        public static unsafe void Run()
        {
            var native = new BoolSeqStruct_Native();
            
            var flags = new bool[] { true, false, true };
            // Simulate native bool array (byte per bool)
            var bytes = new byte[] { 1, 0, 1 };
            
            var hBuffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, hBuffer, bytes.Length);
            
            native.Flags.Length = (uint)bytes.Length;
            native.Flags.Buffer = hBuffer;

            try 
            {
                unsafe
                {
                    var view = new BoolSeqStructView(&native);
                    var managed = view.ToManaged();

                    if (managed.Flags == null) throw new Exception(""Flags is null"");
                    if (managed.Flags.Count != 3) throw new Exception($""Count mismatch: {managed.Flags.Count}"");
                    if (managed.Flags[0] != true) throw new Exception($""[0] mismatch"");
                    if (managed.Flags[1] != false) throw new Exception($""[1] mismatch"");
                    if (managed.Flags[2] != true) throw new Exception($""[2] mismatch"");
                }
            }
            finally
            {
                 Marshal.FreeHGlobal(hBuffer);
            }
        }
    }
}
";
            RunGeneratedCode("Test.SeqBool.TestRunner", "Run", supportCode, viewCode);
        }
    }
}
