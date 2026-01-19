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
        private DdsApi.DdsEntity _topicHandle;
        private DdsParticipant? _participant;
        private readonly string _topicName;

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

        public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
        {
            if (_sizer == null || _serializer == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} does not exhibit expected DDS generated methods (Serialize, GetSerializedSize).");
            }

            _topicName = topicName;
            _participant = participant;

            // 1. Get or register topic (auto-discovery)
            DdsApi.DdsEntity topic = participant.GetOrRegisterTopic<T>(topicName, qos);
            _topicHandle = topic;

            // 2. Create Writer
            var writer = DdsApi.dds_create_writer(
                participant.NativeEntity,
                topic,
                qos,
                IntPtr.Zero);

            if (!writer.IsValid)
            {
                 throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create writer");
            }
            _writerHandle = new DdsEntityHandle(writer);
        }

        public void WriteViaDdsWrite(in T sample)
        {
             if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
             #pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type ('T')
             unsafe
             {
                 fixed (void* p = &sample)
                 {
                     int ret = DdsApi.dds_write(_writerHandle.NativeHandle.Handle, (IntPtr)p);
                     if (ret < 0) throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_write failed: {ret}");
                 }
             }
             #pragma warning restore CS8500
        }

        private void PerformOperation(in T sample, Func<DdsApi.DdsEntity, IntPtr, int> operation)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
            if (!_topicHandle.IsValid) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            // 1. Get Size (no alloc)
            // Start at offset 4 because we will prepend 4-byte CDR header
            int payloadSize = _sizer!(sample, 4); 
            int totalSize = payloadSize + 4;

            // 2. Rent Buffer (no alloc - pooled)
            byte[] buffer = Arena.Rent(totalSize);
            
            try
            {
                // 3. Serialize (ZERO ALLOC via new Span overload)
                var span = buffer.AsSpan(0, totalSize);
                var cdr = new CdrWriter(span); 
                
                // Write CDR Header (XCDR1 format)
                // CDR Identifier: 0x0001 (LE) or 0x0000 (BE)
                // Options: 0x0000
                if (BitConverter.IsLittleEndian)
                {
                    // Little Endian (x64, ARM64, most platforms)
                    cdr.WriteByte(0x00);
                    cdr.WriteByte(0x01);
                }
                else
                {
                    // Big Endian (rare: PowerPC, SPARC, older MIPS)
                    cdr.WriteByte(0x00);
                    cdr.WriteByte(0x00);
                }
                // NOTE: CdrWriter/CdrReader endianness handling is platform-specific.
                // XCDR1 requires encapsulation identifier to match data endianness.
                // Most .NET platforms are Little Endian (LE).
                cdr.WriteByte(0x00);
                cdr.WriteByte(0x00);
                
                _serializer!(sample, ref cdr);
                cdr.Complete();
                
                // 4. Write to DDS via Serdata
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        IntPtr dataPtr = (IntPtr)p;
                        
                        IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                            _topicHandle,
                            dataPtr,
                            (uint)totalSize);

                        if (serdata == IntPtr.Zero)
                        {
                             throw new DdsException(DdsApi.DdsReturnCode.Error, "dds_create_serdata_from_cdr failed");
                        }
                            
                        // Operation consumes ref
                        int ret = operation(_writerHandle.NativeHandle, serdata);
                        if (ret < 0)
                        {
                            throw new DdsException((DdsApi.DdsReturnCode)ret, $"DDS operation failed: {ret}");
                        }
                    }
                }
            }
            finally
            {
                Arena.Return(buffer);
            }
        }

        public void Write(in T sample)
        {
            PerformOperation(sample, DdsApi.dds_writecdr);
        }

        /// <summary>
        /// Dispose an instance.
        /// Marks the instance as NOT_ALIVE_DISPOSED in the reader.
        /// </summary>
        /// <param name="sample">Sample containing the key to dispose (non-key fields ignored)</param>
        /// <remarks>
        /// For keyed topics only. The key fields identify which instance to dispose.
        /// Non-key fields are serialized but ignored by CycloneDDS.
        /// This operation maintains the zero-allocation guarantee.
        /// </remarks>
        public void DisposeInstance(in T sample)
        {
            PerformOperation(sample, DdsApi.dds_dispose_serdata);
        }

        /// <summary>
        /// Unregister an instance (writer releases ownership).
        /// Notifies readers that this writer will no longer update the instance.
        /// Reader instance state will transition to NOT_ALIVE_NO_WRITERS if no other writers exist.
        /// </summary>
        /// <param name="sample">Sample containing the key to unregister (non-key fields ignored)</param>
        /// <remarks>
        /// Useful for graceful shutdown or ownership transfer scenarios.
        /// For keyed topics only. The key fields identify which instance to unregister.
        /// Non-key fields are serialized but ignored by CycloneDDS.
        /// This operation maintains the zero-allocation guarantee.
        /// </remarks>
        public void UnregisterInstance(in T sample)
        {
            PerformOperation(sample, DdsApi.dds_unregister_serdata);
        }
        
        public void Dispose()
        {
            _writerHandle?.Dispose();
            _writerHandle = null;
            _topicHandle = DdsApi.DdsEntity.Null;
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
