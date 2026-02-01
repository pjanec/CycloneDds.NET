using Xunit;
using Xunit.Abstractions;
using CycloneDDS.CodeGen;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace CycloneDDS.CodeGen.Tests
{
    public class MarshallerExecutionTests : CodeGenTestBase
    {
        private readonly ITestOutputHelper _output;

        public MarshallerExecutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void MarshalToNative_PrimitiveType_WritesCorrectly()
        {
            var type = new TypeInfo
            {
                Name = "PrimitivePoint",
                Namespace = "TestMarshal",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "X", TypeName = "int" },
                    new FieldInfo { Name = "Y", TypeName = "double" }
                }
            };
            
            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry(), true);
            
            // Define the struct fields (partial)
            var structDef = @"
namespace TestMarshal
{
    public partial struct PrimitivePoint
    {
        public int X;
        public double Y;
    }
}
";

            // Generate Test Runner in the same assembly to access internal members
            var testRunnerCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using TestMarshal;

namespace TestMarshal
{
    public static class TestRunner
    {
        public static string Run(int x, double y)
        {
            try
            {
                var input = new PrimitivePoint { X = x, Y = y };
                var native = new PrimitivePoint_Native();

                // Allocate buffer
                byte[] buffer = new byte[1024];
                unsafe 
                {
                    fixed (byte* ptr = buffer)
                    {
                        // Calc expected size: int(4) + padding(4) + double(8) = 16
                        // Using ample head size to be safe, but strictly it is 16.
                        int headSize = 16;
                        var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)ptr, headSize);
                        
                        PrimitivePoint.MarshalToNative(in input, ref native, ref arena);
                        
                        if (native.X != x) return $""X mismatch: expected {x}, got {native.X}"";
                        if (Math.Abs(native.Y - y) > 0.0001) return $""Y mismatch: expected {y}, got {native.Y}"";
                    }
                }
                return ""SUCCESS"";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}
";
            var assembly = CompileToAssembly("MarshalTestAssm_Primitives", code, structDef, testRunnerCode);
            var runnerType = assembly.GetType("TestMarshal.TestRunner");
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[]{ 123, 456.789 });
            
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void MarshalToNative_ComplexType_AllocatesInArena()
        {
            var type = new TypeInfo
            {
                Name = "ComplexData",
                Namespace = "TestMarshalComplex",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Name", TypeName = "string" },
                    new FieldInfo { Name = "Numbers", TypeName = "List<int>" }
                }
            };
            
            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry(), true);
            
            var structDef = @"
using System.Collections.Generic;
namespace TestMarshalComplex
{
    public partial struct ComplexData
    {
        public int Id;
        public string Name;
        public List<int> Numbers;
    }
}
";

            var testRunnerCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using TestMarshalComplex;
using System.Collections.Generic;

namespace TestMarshalComplex
{
    public static class ComplexRunner
    {
        public static string Run(int id, string name, int[] numbers)
        {
            try
            {
                var input = new ComplexData 
                { 
                    Id = id, 
                    Name = name, 
                    Numbers = new List<int>(numbers) 
                };
                var native = new ComplexData_Native();

                byte[] buffer = new byte[4096];
                unsafe 
                {
                    fixed (byte* ptr = buffer)
                    {
                        // Head Layout:
                        // int Id (4)
                        // padding (4)
                        // IntPtr Name (8)
                        // DdsSequenceNative Numbers (24)
                        // Total Head Size: 4 + 4 + 8 + 24 = 40.
                        int headSize = 40; 
                        
                        var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)ptr, headSize);
                        
                        ComplexData.MarshalToNative(in input, ref native, ref arena);
                        
                        if (native.Id != id) return ""Id mismatch"";
                        
                        if (native.Name == IntPtr.Zero) return ""Name ptr is null"";
                        string str = Marshal.PtrToStringUTF8(native.Name);
                        if (str != name) return $""Name mismatch: expected '{name}', got '{str}'"";
                        
                        if (native.Numbers.Length != numbers.Length) return $""Length mismatch: {native.Numbers.Length} != {numbers.Length}"";
                        if (native.Numbers.Buffer == IntPtr.Zero) return ""Buffer ptr is null"";
                        
                        int* numPtr = (int*)native.Numbers.Buffer;
                        for(int i=0; i<numbers.Length; i++)
                        {
                            if (numPtr[i] != numbers[i]) return $""Element {i} mismatch: {numPtr[i]} != {numbers[i]}"";
                        }
                    }
                }
                return ""SUCCESS"";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}
";
            var assembly = CompileToAssembly("MarshalTestAssm_Complex", code, structDef, testRunnerCode);
            var runnerType = assembly.GetType("TestMarshalComplex.ComplexRunner");
            
            var numbers = new int[] { 10, 20, 30, 40, 50 };
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[]{ 99, "Hello World", numbers });
            
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void MarshalKeyToNative_OnlyKeyFieldsMarshaled()
        {
            var type = new TypeInfo
            {
                Name = "KeyedData",
                Namespace = "TestMarshalKeys",
                Fields = new List<FieldInfo>
                {
                    new FieldInfo 
                    { 
                        Name = "Id", 
                        TypeName = "int", 
                        Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsKey" } } 
                    },
                    new FieldInfo 
                    { 
                        Name = "Name", 
                        TypeName = "string" 
                    }, // Not a key
                    new FieldInfo 
                    { 
                        Name = "Category", 
                        TypeName = "string", 
                        Attributes = new List<AttributeInfo>{ new AttributeInfo { Name = "DdsKey" } } 
                    }
                }
            };
            
            var emitter = new SerializerEmitter();
            var code = emitter.EmitSerializer(type, new GlobalTypeRegistry(), true);
            
            var structDef = @"
namespace TestMarshalKeys
{
    public partial struct KeyedData
    {
        public int Id;
        public string Name;
        public string Category;
    }
}
";

            var testRunnerCode = @"
using System;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using TestMarshalKeys;

namespace TestMarshalKeys
{
    public static class KeyRunner
    {
        public static string Run(int id, string name, string category)
        {
            try
            {
                var input = new KeyedData 
                { 
                    Id = id, 
                    Name = name, 
                    Category = category 
                };
                var native = new KeyedData_Native();

                byte[] buffer = new byte[1024];
                unsafe 
                {
                    fixed (byte* ptr = buffer)
                    {
                        // Head: int(4) + padding(4) + IntPtr(8) + IntPtr(8) = 24.
                        int headSize = 24;
                        var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)ptr, headSize);
                        
                        KeyedData.MarshalKeyToNative(in input, ref native, ref arena);
                        
                        // Verify KEYS are marshaled
                        if (native.Id != id) return ""Id (Key) mismatch"";
                        if (native.Category == IntPtr.Zero) return ""Category (Key) ptr needs to be set"";
                        string catStr = Marshal.PtrToStringUTF8(native.Category);
                        if (catStr != category) return $""Category mismatch: {catStr} != {category}"";
                        
                        // Verify NON-KEYS are NOT marshaled (should be null/0)
                        if (native.Name != IntPtr.Zero) return ""Name (Non-Key) should be null"";
                    }
                }
                return ""SUCCESS"";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}
";
            var assembly = CompileToAssembly("MarshalTestAssm_Keys", code, structDef, testRunnerCode);
            var runnerType = assembly.GetType("TestMarshalKeys.KeyRunner");
            
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[]{ 101, "ShouldNotPublish", "CategoryA" });
            
            Assert.Equal("SUCCESS", result);
        }

        [Fact]
        public void MarshalFromNative_RoundTrip()
        {
             var type = new TypeInfo
            {
                Name = "RoundTripData",
                Namespace = "TestRoundTrip",
                Extensibility = CycloneDDS.Schema.DdsExtensibilityKind.Final,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Name = "Id", TypeName = "int" },
                    new FieldInfo { Name = "Message", TypeName = "string" },
                    new FieldInfo { Name = "Numbers", TypeName = "BoundedSeq<double>" }
                }
            };
            
            var emitter = new SerializerEmitter();
            string generatedCode = emitter.EmitSerializer(type, new GlobalTypeRegistry());
             
            string structDef = @"
namespace TestRoundTrip
{
    public partial struct RoundTripData
    {
        public int Id;
        public string Message;
        public BoundedSeq<double> Numbers;
    }
}
";
            string testRunnerCode = @"
namespace TestRoundTrip
{
    public static class Runner
    {
        public static unsafe string Run(int id, string msg, double[] nums)
        {
             byte[] buffer = new byte[4096];
             fixed (byte* ptr = buffer)
             {
                 int headSize = Unsafe.SizeOf<RoundTripData_Native>();
                 var arena = new NativeArena(new Span<byte>(buffer), (IntPtr)ptr, headSize);
                 
                 // Create Source
                 var source = new RoundTripData();
                 source.Id = id;
                 source.Message = msg;
                 source.Numbers = new CycloneDDS.Schema.BoundedSeq<double>(nums.Length);
                 foreach(var n in nums) source.Numbers.Add(n);
                 
                 // To Native
                 var native = new RoundTripData_Native();
                 RoundTripData.MarshalToNative(in source, ref native, ref arena);
                 
                 // From Native
                 var target = new RoundTripData();
                 RoundTripData.MarshalFromNative(ref target, in native);
                 
                 // Verify
                 if (target.Id != source.Id) return ""Id Mismatch: "" + target.Id;
                 if (target.Message != source.Message) return ""Message Mismatch: "" + target.Message;
                 if (target.Numbers.Count != source.Numbers.Count) return ""Count Mismatch"";
                 for(int i=0; i<target.Numbers.Count; i++)
                 {
                     if (System.Math.Abs(target.Numbers[i] - source.Numbers[i]) > 0.00001) return ""Item Mismatch at "" + i;
                 }
                 
                 return ""SUCCESS"";
             }
        }
    }
}
";
            string code = @"using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices; 
using CycloneDDS.Core; 
using CycloneDDS.Runtime;
using CycloneDDS.Schema;
" + generatedCode + "\n" + structDef + "\n" + testRunnerCode;

            var assembly = CompileToAssembly("MarshalTestAssm_RoundTrip", code);
            var runnerType = assembly.GetType("TestRoundTrip.Runner");
            
            var result = (string)runnerType.GetMethod("Run").Invoke(null, new object[]{ 12345, "Hello World from Native", new double[] { 1.1, 2.2, 3.3 } });
            
            Assert.Equal("SUCCESS", result);
        }
    }
}
