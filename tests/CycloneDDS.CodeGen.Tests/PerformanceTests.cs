using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using CycloneDDS.CodeGen;
using CycloneDDS.Core;
using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices;
using System.Diagnostics;

namespace CycloneDDS.CodeGen.Tests
{
    public class PerformanceTests : CodeGenTestBase
    {
        [Fact]
        public void LargeDataSerialization_PerformanceSanity()
        {
            // 10,000 element sequence
            var type = new TypeInfo { Name = "BigSeq", Namespace = "Perf", Fields = new List<FieldInfo> {
                new FieldInfo { Name = "Items", TypeName = "BoundedSeq<int>" }
            }};
            
            var emitter = new SerializerEmitter();
            var demitter = new DeserializerEmitter();
            
             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace Perf {
  public partial struct BigSeq { public BoundedSeq<int> Items; }
}";
            code += emitter.EmitSerializer(type, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(type, new GlobalTypeRegistry(), false) + "\n" +
                    GenerateTestHelper("Perf", "BigSeq");

            var assembly = CompileToAssembly("PerfSeq", code);
            var t = assembly.GetType("Perf.BigSeq");
            var inst = Activator.CreateInstance(t);
            
            var count = 10000;
            var seq = new BoundedSeq<int>(20000);
            for(int i=0; i<count; i++) seq.Add(i);
            SetField(inst, "Items", seq);
            
            // Debug check
            var fieldVal = (BoundedSeq<int>)GetField(inst, "Items");
            if (fieldVal.Count != count) throw new Exception($"Setup failed: count is {fieldVal.Count}");

            var helper = assembly.GetType("Perf.TestHelper");
            var methodName = "SerializeWithBuffer"; // cache method info
            var method = helper.GetMethod(methodName);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);
            
            // Warmup
            for(int i=0; i<10; i++)
            {
                buffer.Clear();
                method.Invoke(null, new object[] { inst, buffer });
            }
            buffer.Clear();
            
            var sw = Stopwatch.StartNew();
            method.Invoke(null, new object[] { inst, buffer });
            sw.Stop();
            
            if (buffer.WrittenCount < 40000) 
            {
               throw new Exception($"Serialized size is too small: {buffer.WrittenCount}");
            }

            // Correctness
            var result = helper.GetMethod("DeserializeFrombufferToOwned").Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
            var resSeq = (BoundedSeq<int>)GetField(result, "Items");
            Assert.Equal(count, resSeq.Count);
            
            // With optimizations, this should be blazing fast (< 50ms including reflection overhead)
            // 40KB memcpy is microseconds.
            Assert.True(sw.ElapsedMilliseconds < 50, $"Serialization took {sw.ElapsedMilliseconds}ms, expected < 50ms");
        }

        [Fact]
        public void ComplexNestedRoundtrip_Stress()
        {
             // Deeply nested struct with all features.
             // Reuse the code logic from ComplexCombinationTests but loop it.
             // Actually, I'll just create a simpler nested struct to keep setup small but run it 1000 times.
             var inner = new TypeInfo { Name = "Inner", Namespace="Perf", Fields = new List<FieldInfo> { new FieldInfo { Name = "X", TypeName = "int" } } };
             var outer = new TypeInfo { Name = "Outer", Namespace="Perf", Fields = new List<FieldInfo> { new FieldInfo { Name = "In", TypeName = "Inner" } } };
             
             var emitter = new SerializerEmitter();
             var demitter = new DeserializerEmitter();

             string code = @"using CycloneDDS.Schema; using System; using System.Text; using System.Linq; using System.Runtime.InteropServices; using CycloneDDS.Core;
namespace Perf {
  public partial struct Inner { public int X; }
  public partial struct Outer { public Inner In; }
}";
             code += emitter.EmitSerializer(inner, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(inner, new GlobalTypeRegistry(), false) + "\n" +
                     emitter.EmitSerializer(outer, new GlobalTypeRegistry(), false) + "\n" + demitter.EmitDeserializer(outer, new GlobalTypeRegistry(), false) + "\n" +
                     GenerateTestHelper("Perf", "Outer");

             var assembly = CompileToAssembly("PerfStress", code);
             var tOuter = assembly.GetType("Perf.Outer");
             var tInner = assembly.GetType("Perf.Inner");
             
             var inst = Activator.CreateInstance(tOuter);
             var inInst = Activator.CreateInstance(tInner);
             SetField(inInst, "X", 101);
             SetField(inst, "In", inInst);

             var helper = assembly.GetType("Perf.TestHelper");
             var ser = helper.GetMethod("SerializeWithBuffer");
             var deser = helper.GetMethod("DeserializeFrombufferToOwned");
             var buffer = new System.Buffers.ArrayBufferWriter<byte>(65536);

             var sw = Stopwatch.StartNew();
             for(int i=0; i<1000; i++) {
                 buffer.Clear();
                 ser.Invoke(null, new object[] { inst, buffer });
                 var res = deser.Invoke(null, new object[] { (ReadOnlyMemory<byte>)buffer.WrittenMemory });
                 
                 // Light verification
                 var rIn = GetField(res, "In");
                 Assert.Equal(101, GetField(rIn, "X"));
             }
             sw.Stop();
             
             Assert.True(sw.ElapsedMilliseconds < 5000, $"Stress test took too long: {sw.ElapsedMilliseconds}ms");
        }
    }
}

