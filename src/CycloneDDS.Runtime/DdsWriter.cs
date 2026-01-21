using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;
using CycloneDDS.Runtime.Tracking;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime
{
    public sealed class DdsWriter<T> : IDisposable
    {
        private static readonly byte _encodingKindLE;
        private static readonly byte _encodingKindBE;

        // Cached delegates to prevent allocation per call
        private static readonly Func<DdsApi.DdsEntity, IntPtr, int> _writeOperation = DdsApi.dds_writecdr;
        private static readonly Func<DdsApi.DdsEntity, IntPtr, int> _disposeOperation = DdsApi.dds_dispose_serdata;
        private static readonly Func<DdsApi.DdsEntity, IntPtr, int> _unregisterOperation = DdsApi.dds_unregister_serdata;


        private DdsEntityHandle? _writerHandle;
        private DdsApi.DdsEntity _topicHandle;
        private DdsParticipant? _participant;
        private readonly string _topicName;

        // Async/Events
        private IntPtr _listener = IntPtr.Zero;
        private GCHandle _paramHandle;
        private readonly object _listenerLock = new object();
        private readonly DdsApi.DdsOnPublicationMatched _publicationMatchedHandler;
        private volatile TaskCompletionSource<bool>? _waitForReaderTaskSource;
        private EventHandler<DdsApi.DdsPublicationMatchedStatus>? _publicationMatched;

        // Delegates for high-performance invocation
        private delegate void SerializeDelegate(in T sample, ref CdrWriter writer);
        private delegate int GetSerializedSizeDelegate(in T sample, int currentAlignment, bool isXcdr2);

        private static readonly SerializeDelegate? _serializer;
        private static readonly SerializeDelegate? _keySerializer;
        private static readonly GetSerializedSizeDelegate? _sizer;

        static DdsWriter()
        {
            var attr = typeof(T).GetCustomAttribute<DdsExtensibilityAttribute>();
            var kind = attr?.Kind ?? DdsExtensibilityKind.Appendable;

            switch (kind)
            {
                case DdsExtensibilityKind.Final:
                    _encodingKindLE = 0x07; // CDR2_LE
                    _encodingKindBE = 0x06; // CDR2_BE
                    break;
                case DdsExtensibilityKind.Mutable:
                    _encodingKindLE = 0x0B; // PL_CDR2_LE
                    _encodingKindBE = 0x0A; // PL_CDR2_BE
                    break;
                case DdsExtensibilityKind.Appendable:
                default:
                    _encodingKindLE = 0x09; // D_CDR2_LE
                    _encodingKindBE = 0x08; // D_CDR2_BE
                    break;
            }

            try
            {
                _sizer = CreateSizerDelegate();
                _serializer = CreateSerializerDelegate();
                _keySerializer = CreateKeySerializerDelegate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DdsWriter<{typeof(T).Name}>] Failed to create delegates: {ex.Message}");
            }
        }

        public DdsWriter(DdsParticipant participant, string topicName, IntPtr qos = default)
        {
            _publicationMatchedHandler = OnPublicationMatched;

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

            // Notify participant (triggers identity publishing if enabled)
            // Skip for the identity writer itself to avoid recursion
            if (typeof(T) != typeof(SenderIdentity))
            {
                _participant.RegisterWriter();
            }
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

        private void PerformOperation(in T sample, Func<DdsApi.DdsEntity, IntPtr, int> operation, int serdataKind = 2)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
            if (!_topicHandle.IsValid) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            // 1. Get Size (no alloc)
            // Start at offset 4 because we will prepend 4-byte CDR header
            // 4 bytes header offset, isXcdr2=true
            int payloadSize = _sizer!(sample, 4, true); 
            int totalSize = payloadSize + 4;

            // 2. Rent Buffer (no alloc - pooled)
            byte[] buffer = Arena.Rent(totalSize);
            
            try
            {
                // 3. Serialize (ZERO ALLOC via new Span overload)
                var span = buffer.AsSpan(0, totalSize);
                // Enable XCDR2 mode in CdrWriter explicitly
                var cdr = new CdrWriter(span, isXcdr2: true); 
                
                // Write CDR Header (XCDR2 format)
                if (BitConverter.IsLittleEndian)
                {
                    // Little Endian
                    cdr.WriteByte(0x00);
                    cdr.WriteByte(_encodingKindLE);
                }
                else
                {
                    // Big Endian
                    cdr.WriteByte(0x00);
                    cdr.WriteByte(_encodingKindBE);
                }
                
                // Options (2 bytes)
                cdr.WriteByte(0x00);
                cdr.WriteByte(0x00);
                
                if (serdataKind == 1 && _keySerializer != null)
                {
                    _keySerializer(sample, ref cdr);
                }
                else
                {
                    _serializer!(sample, ref cdr);
                }
                cdr.Complete();
                
                int actualSize = cdr.Position;

                // 4. Write to DDS via Serdata
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        IntPtr dataPtr = (IntPtr)p;
                        
                        IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                            _topicHandle,
                            dataPtr,
                            (uint)actualSize,
                            serdataKind);

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
            PerformOperation(sample, _writeOperation);
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
            PerformOperation(sample, _disposeOperation, 1); // SDK_KEY
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
            PerformOperation(sample, _unregisterOperation, 1); // SDK_KEY
        }
        
        public event EventHandler<DdsApi.DdsPublicationMatchedStatus>? PublicationMatched
        {
            add 
            {
                lock(_listenerLock) {
                    _publicationMatched += value;
                    EnsureListenerAttached();
                }
            }
            remove 
            {
                lock(_listenerLock) {
                    _publicationMatched -= value;
                }
            }
        }
        
        public DdsApi.DdsPublicationMatchedStatus CurrentStatus 
        {
            get
            {
                 if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
                 DdsApi.dds_get_publication_matched_status(_writerHandle.NativeHandle.Handle, out var status);
                 return status;
            }
        }

        public async Task<bool> WaitForReaderAsync(TimeSpan timeout = default)
        {
            if (CurrentStatus.CurrentCount > 0) return true;
            
            EnsureListenerAttached();
            
             if (CurrentStatus.CurrentCount > 0) return true;
             
             var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
             _waitForReaderTaskSource = tcs;
             
             if (CurrentStatus.CurrentCount > 0) 
             {
                 _waitForReaderTaskSource = null;
                 return true;
             }
             
             using var timeoutCts = new CancellationTokenSource(timeout == default ? TimeSpan.FromMilliseconds(-1) : timeout);
             using (timeoutCts.Token.Register(() => tcs.TrySetResult(false))) 
             {
                  return await tcs.Task;
             }
        }

        private void EnsureListenerAttached()
        {
             if (_listener != IntPtr.Zero) return;
             
             lock (_listenerLock)
             {
                 if (_listener != IntPtr.Zero) return;
                 
                 _paramHandle = GCHandle.Alloc(this);
                 _listener = DdsApi.dds_create_listener(GCHandle.ToIntPtr(_paramHandle));
                 DdsApi.dds_lset_publication_matched(_listener, _publicationMatchedHandler);
                 
                 if (_writerHandle != null)
                 {
                     DdsApi.dds_writer_set_listener(_writerHandle.NativeHandle, _listener);
                 }
             }
        }

        // [MonoPInvokeCallback(typeof(DdsApi.DdsOnPublicationMatched))]
        private static void OnPublicationMatched(int writer, ref DdsApi.DdsPublicationMatchedStatus status, IntPtr arg)
        {
             if (arg == IntPtr.Zero) return;
             try
             {
                 var handle = GCHandle.FromIntPtr(arg);
                 if (handle.IsAllocated && handle.Target is DdsWriter<T> self)
                 {
                     self._publicationMatched?.Invoke(self, status);
                     
                     if (status.CurrentCount > 0)
                     {
                         self._waitForReaderTaskSource?.TrySetResult(true);
                     }
                 }
             }
             catch { }
        }

        public DdsInstanceHandle LookupInstance(in T keySample)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            // Use _sizer to calculate size. Start at offset 4 for header
            int size = _sizer!(keySample, 4, true);
            byte[] buffer = Arena.Rent(size + 4);

            try
            {
                var span = buffer.AsSpan(0, size + 4);
                var cdr = new CdrWriter(span, isXcdr2: true);
                
                // Write Header
                if (BitConverter.IsLittleEndian) { cdr.WriteByte(0x00); cdr.WriteByte(_encodingKindLE); }
                else { cdr.WriteByte(0x00); cdr.WriteByte(_encodingKindBE); }
                cdr.WriteByte(0x00); cdr.WriteByte(0x00);

                _serializer!(keySample, ref cdr);
                
                unsafe
                {
                    fixed (byte* p = buffer)
                    {
                        // 2. Create Serdata (Kind=1 for SDK_KEY)
                        IntPtr serdata = DdsApi.dds_create_serdata_from_cdr(
                            _topicHandle, (IntPtr)p, (uint)(size + 4), 1);
                            
                        if (serdata == IntPtr.Zero) return DdsInstanceHandle.Nil;

                        try
                        {
                            long handle = DdsApi.dds_lookup_instance_serdata(_writerHandle.NativeHandle.Handle, serdata);
                            return new DdsInstanceHandle(handle);
                        }
                        finally
                        {
                            DdsApi.ddsi_serdata_unref(serdata);
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
            if (_writerHandle == null) return;
            
            if (typeof(T) != typeof(SenderIdentity))
            {
                _participant?.UnregisterWriter();
            }

            if (_listener != IntPtr.Zero)
            {
                DdsApi.dds_delete_listener(_listener);
                _listener = IntPtr.Zero;
            }
            if (_paramHandle.IsAllocated) _paramHandle.Free();

            _writerHandle?.Dispose();
            _writerHandle = null;
            _topicHandle = DdsApi.DdsEntity.Null;
            _participant = null;
        }

        // --- Delegate Generators ---
        private static GetSerializedSizeDelegate CreateSizerDelegate()
        {
            var method = typeof(T).GetMethod("GetSerializedSize", new[] { typeof(int), typeof(bool) });
            if (method == null) throw new MissingMethodException(typeof(T).Name, "GetSerializedSize(int, bool)");

            var dm = new DynamicMethod(
                "GetSerializedSizeThunk",
                typeof(int),
                new[] { typeof(T).MakeByRefType(), typeof(int), typeof(bool) },
                typeof(DdsWriter<T>).Module);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // sample (ref)
            if (!typeof(T).IsValueType)
            {
                 il.Emit(OpCodes.Ldind_Ref); 
            }
            
            il.Emit(OpCodes.Ldarg_1); // offset
            il.Emit(OpCodes.Ldarg_2); // isXcdr2
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

        private static SerializeDelegate? CreateKeySerializerDelegate()
        {
            var method = typeof(T).GetMethod("SerializeKey", new[] { typeof(CdrWriter).MakeByRefType() });
            Console.WriteLine($"[DEBUG] Type {typeof(T).Name} SerializeKey found: {method != null}");
            if (method == null) return null;

            var dm = new DynamicMethod(
                "SerializeKeyThunk",
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
