using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Buffers;
using System.Runtime.InteropServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;

namespace CycloneDDS.Runtime
{
    public sealed class DdsWriter<T> : IDisposable
    {
        private DdsEntityHandle? _writerHandle;
        private DdsEntityHandle? _topicHandle;
        private DdsParticipant? _participant;
        private readonly string _topicName;
        private readonly IntPtr _topicDescriptor;

        // Delegates for high-performance invocation
        private delegate void SerializeDelegate(in T sample, ref CdrWriter writer);
        private delegate int GetSerializedSizeDelegate(in T sample, int currentAlignment);

        private static readonly SerializeDelegate? _serializer;
        private static readonly GetSerializedSizeDelegate? _sizer;

        static DdsWriter()
        {
            try
            {
                _sizer = CreateSizerDelegate();
                _serializer = CreateSerializerDelegate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DdsWriter<{typeof(T).Name}>] Failed to create delegates: {ex.Message}");
            }
        }

        public DdsWriter(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
        {
            if (_sizer == null || _serializer == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} does not exhibit expected DDS generated methods (Serialize, GetSerializedSize).");
            }

            _topicName = topicName;
            _participant = participant;
            _topicDescriptor = topicDescriptor;

            // 1. Create Topic
            var topic = DdsApi.dds_create_topic(
                participant.NativeEntity,
                topicDescriptor,
                topicName,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!topic.IsValid)
            {
                 throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create topic");
            }
            _topicHandle = new DdsEntityHandle(topic);

            // 2. Create Writer
            var writer = DdsApi.dds_create_writer(
                participant.NativeEntity,
                topic,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!writer.IsValid)
            {
                 throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create writer");
            }
            _writerHandle = new DdsEntityHandle(writer);
        }

        public void Write(in T sample)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            // 1. Get Size (no alloc)
            int size = _sizer!(sample, 0); 

            // 2. Rent Buffer (no alloc - pooled)
            byte[] buffer = Arena.Rent(size);
            
            try
            {
                // 3. Serialize (ZERO ALLOC via new Span overload)
                var span = buffer.AsSpan(0, size);
                var cdr = new CdrWriter(span);  // ✅ No wrapper allocation!
                
                _serializer!(sample, ref cdr);
                cdr.Complete();
                
                // 4. Write to DDS via Serdata
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        IntPtr dataPtr = (IntPtr)p;
                        
                        // Create serdata from CDR bytes
                        IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                            _topicDescriptor,
                            IntPtr.Zero, // Kind 0 (DDS_EST_1_0)
                            dataPtr,
                            (uint)size);
                            
                        if (serdata == IntPtr.Zero)
                            throw new DdsException(DdsApi.DdsReturnCode.Error, "dds_create_serdata_from_cdr failed");
                            
                        try
                        {
                            int ret = DdsApi.dds_write_serdata(_writerHandle.NativeHandle, serdata);
                            if (ret < 0)
                            {
                                throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_write_serdata failed: {ret}");
                            }
                        }
                        finally
                        {
                            // If create_serdata succeeds, we must release it? 
                            // Or does dds_write_serdata take ownership?
                            // Architect says: "4. Release our ref (Cyclone holds its own ref now if sending async)"
                            DdsApi.dds_free_serdata(serdata);
                        }
                    }
                }
            }
            finally
            {
                Arena.Return(buffer);
            }
        }


        private void WriteFallback(in T sample)
        {
            // Fallback to dds_write is currently crashing due to likely struct layout mismatches
            // with the provided native library version.
            // Disable to prevent process crash.
            Console.WriteLine("Warning: Write operation skipped (Native API missing and fallback unstable).");
        }
        
        public void Dispose()
        {
            _writerHandle?.Dispose();
            _writerHandle = null;
            _topicHandle?.Dispose();
            _topicHandle = null;
            _participant = null;
        }

        // --- Delegate Generators ---
        private static GetSerializedSizeDelegate CreateSizerDelegate()
        {
            var method = typeof(T).GetMethod("GetSerializedSize", new[] { typeof(int) });
            if (method == null) throw new MissingMethodException(typeof(T).Name, "GetSerializedSize");

            var dm = new DynamicMethod(
                "GetSerializedSizeThunk",
                typeof(int),
                new[] { typeof(T).MakeByRefType(), typeof(int) },
                typeof(DdsWriter<T>).Module);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // sample (ref)
            if (!typeof(T).IsValueType)
            {
                 il.Emit(OpCodes.Ldind_Ref); 
            }
            
            il.Emit(OpCodes.Ldarg_1); // offset
            il.Emit(OpCodes.Call, method); 
            il.Emit(OpCodes.Ret);

            return (GetSerializedSizeDelegate)dm.CreateDelegate(typeof(GetSerializedSizeDelegate));
        }

         private static SerializeDelegate CreateSerializerDelegate()
        {
            var method = typeof(T).GetMethod("Serialize", new[] { typeof(CdrWriter).MakeByRefType() });
            if (method == null) throw new MissingMethodException(typeof(T).Name, "Serialize");

            var dm = new DynamicMethod(
                "SerializeThunk",
                typeof(void),
                new[] { typeof(T).MakeByRefType(), typeof(CdrWriter).MakeByRefType() },
                typeof(DdsWriter<T>).Module);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // sample (ref)
            if (!typeof(T).IsValueType)
            {
                il.Emit(OpCodes.Ldind_Ref);
            }
            il.Emit(OpCodes.Ldarg_1); // writer (ref)
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);

            return (SerializeDelegate)dm.CreateDelegate(typeof(SerializeDelegate));
        }
    }

}
