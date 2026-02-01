using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;
using Probe;

namespace UnionProbeTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Union Probe Starting...");
            
            // 1. Inspect Managed/Native Struct Sizes (What C# thinks)
            InspectCSharpLayout();

            // 2. Perform Roundtrip Probe (What DDS thinks)
            PerformProbe();
        }

        static void InspectCSharpLayout()
        {
            Console.WriteLine("\n--- C# Layout Inspection ---");
            Type nativeType = typeof(UnionProbeStruct).Assembly.GetType("Probe.UnionProbeStruct_Native");
            Type unionNativeType = typeof(UnionProbeStruct).Assembly.GetType("Probe.MyUnion_Native");
            Type unionExplicitType = typeof(UnionProbeStruct).Assembly.GetType("Probe.MyUnion_Union_Native");

            if (nativeType == null || unionNativeType == null)
            {
                Console.WriteLine("Could not find Native types via Reflection.");
                return;
            }

            Console.WriteLine($"SizeOf(UnionProbeStruct_Native): {GetSize(nativeType)}");
            Console.WriteLine($"SizeOf(MyUnion_Native): {GetSize(unionNativeType)}");
            if (unionExplicitType != null)
                Console.WriteLine($"SizeOf(MyUnion_Union_Native): {GetSize(unionExplicitType)}");

            Console.WriteLine($"OffsetOf(_d): {GetOffset(unionNativeType, "_d")}");
            Console.WriteLine($"OffsetOf(_u): {GetOffset(unionNativeType, "_u")}");
            
            Console.WriteLine($"OffsetOf(P1): {GetOffset(nativeType, "P1")}");
            Console.WriteLine($"OffsetOf(U): {GetOffset(nativeType, "U")}");
            Console.WriteLine($"OffsetOf(P2): {GetOffset(nativeType, "P2")}");
        }

        static int GetSize(Type t)
        {
             // For Unsafe.SizeOf<T> via reflection:
             MethodInfo sizeOf = typeof(System.Runtime.CompilerServices.Unsafe).GetMethod("SizeOf");
             MethodInfo generic = sizeOf.MakeGenericMethod(t);
             return (int)generic.Invoke(null, null);
        }
        
        static int GetOffset(Type t, string fieldName)
        {
            return (int)Marshal.OffsetOf(t, fieldName);
        }

        static void PerformProbe()
        {
            Console.WriteLine("\n--- Native DDS Probe ---");
            
            var participant = new DdsParticipant();
            var writer = new DdsWriter<UnionProbeStruct>(participant, "UnionProbeTopic");
            var reader = new DdsReader<UnionProbeStruct>(participant, "UnionProbeTopic");

            // Create sample
            var sample = new UnionProbeStruct
            {
                P1 = 0x11111111,
                P2 = 0x22222222,
                U = new MyUnion
                {
                    _d = true,
                    Vala = 0x42
                }
            };
            // sample.U.TrueVal = 0x42; // Removed redundant line 

            Console.WriteLine("Writing sample...");
            writer.Write(sample);

            Console.WriteLine("Waiting for data...");
            Thread.Sleep(500); 
            
            var handleField = typeof(DdsReader<UnionProbeStruct>).GetField("_readerHandle", BindingFlags.NonPublic | BindingFlags.Instance);
            var handleObj = handleField.GetValue(reader);
            var nativeHandleProp = handleObj.GetType().GetProperty("NativeHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var nativeEntity = (DdsApi.DdsEntity)nativeHandleProp.GetValue(handleObj);
            
            ReadRaw(nativeEntity.Handle);
        }

        static unsafe void ReadRaw(int readerHandle)
        {
            const int MAX_SAMPLES = 1;
            IntPtr[] samples = new IntPtr[MAX_SAMPLES];
            DdsApi.DdsSampleInfo[] infos = new DdsApi.DdsSampleInfo[MAX_SAMPLES];
            
            int count = DdsApi.dds_take(readerHandle, samples, infos, (UIntPtr)MAX_SAMPLES, (uint)MAX_SAMPLES);
            
            if (count > 0)
            {
                Console.WriteLine($"Read {count} sample(s).");
                IntPtr p = samples[0];
                if (infos[0].ValidData != 0)
                {
                     DumpMemory(p, 64);
                }
                else
                {
                    Console.WriteLine("Sample Info indicates Invalid Data.");
                }
            }
            else
            {
                 Console.WriteLine("No data available via raw take.");
            }
        }

        static unsafe void DumpMemory(IntPtr ptr, int size)
        {
            Console.WriteLine($"Memory Dump at 0x{ptr:X}:");
            byte* b = (byte*)ptr;
            for (int i = 0; i < size; i++)
            {
                if (i % 16 == 0) Console.Write($"\n{i:X2}: ");
                Console.Write($"{b[i]:X2} ");
            }
            Console.WriteLine();
        }
    }
}
