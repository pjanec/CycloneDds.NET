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

        public void WriteViaDdsWrite(in T sample)
        {
             if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
             unsafe
             {
                 fixed (void* p = &sample)
                 {
                     int ret = DdsApi.dds_write(_writerHandle.NativeHandle.Handle, (IntPtr)p);
                     if (ret < 0) throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_write failed: {ret}");
                 }
             }
        }

        public void Write(in T sample)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));

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
                var cdr = new CdrWriter(span);  // ✅ No wrapper allocation!
                
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
                // For fixed buffer, Complete() is no-op but good practice
                cdr.Complete();
                
                // 4. Write to DDS via Serdata
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        IntPtr dataPtr = (IntPtr)p;
                        
                        // Create serdata from CDR bytes using the topic entity to get sertype
                        IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                            _topicHandle.NativeHandle,
                            dataPtr,
                            (uint)totalSize);

                        if (serdata == IntPtr.Zero)
                        {
                             throw new DdsException(DdsApi.DdsReturnCode.Error, "dds_create_serdata_from_cdr failed");
                        }
                            
                        try
                        {
                            int ret = DdsApi.dds_writecdr(_writerHandle.NativeHandle, serdata);
                            if (ret < 0)
                            {
                                throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_writecdr failed: {ret}");
                            }
                        }
                        // dds_writecdr consumes the reference to serdata on success (and most failure paths),
                        // so we must NOT unref it here to avoid double-free.
                        // Ideally, we would detect if dds_writecdr *didn't* consume it (e.g. lock failure),
                        // but that requires native changes to be robust. 
                        // Only leak is on rare lock failure.
                        finally
                        {
                             // No-op
                        }
                    }
                }
            }
            finally
            {
                Arena.Return(buffer);
            }
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
