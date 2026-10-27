using System;
using System.Linq;
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
        private readonly DdsApi.DdsOnLivelinessLost _livelinessLostHandler;
        private readonly DdsApi.DdsOnOfferedDeadlineMissed _offeredDeadlineMissedHandler;
        private EventHandler<DdsApi.DdsLivelinessLostStatus>? _livelinessLost;
        private EventHandler<DdsApi.DdsOfferedDeadlineMissedStatus>? _offeredDeadlineMissed;

        // Native Marshaling Delegates
        private delegate int GetNativeSizeDelegate(in T sample);
        private delegate void MarshalToNativeDelegate(in T sample, IntPtr target, ref NativeArena arena);

        private static readonly GetNativeSizeDelegate? _nativeSizer;
        private static readonly MarshalToNativeDelegate? _nativeMarshaller;
        private static readonly GetNativeSizeDelegate? _keyNativeSizer;
        private static readonly MarshalToNativeDelegate? _keyNativeMarshaller;

        private static readonly int _nativeHeadSize;
        private static readonly int _keyNativeHeadSize;

        private static readonly DdsExtensibilityKind _extensibilityKind;

        static DdsWriter()
        {
            var attr = typeof(T).GetCustomAttribute<DdsExtensibilityAttribute>();
            _extensibilityKind = attr?.Kind ?? DdsExtensibilityKind.Appendable;

            try
            {
                // Native Marshaling
                _nativeSizer = CreateNativeSizerDelegate("GetNativeSize");
                _nativeMarshaller = CreateNativeMarshallerDelegate("MarshalToNative");
                var headSizeMethod = typeof(T).GetMethod("GetNativeHeadSize", BindingFlags.Public | BindingFlags.Static);
                if (headSizeMethod != null) _nativeHeadSize = (int)(headSizeMethod.Invoke(null, null) ?? 0);
                
                // Native Key Marshaling
                _keyNativeSizer = CreateNativeSizerDelegate("GetKeyNativeSize");
                _keyNativeMarshaller = CreateNativeMarshallerDelegate("MarshalKeyToNative");
                var keyHeadSizeMethod = typeof(T).GetMethod("GetKeyNativeHeadSize", BindingFlags.Public | BindingFlags.Static);
                if (keyHeadSizeMethod != null) _keyNativeHeadSize = (int)(keyHeadSizeMethod.Invoke(null, null) ?? 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DdsWriter<{typeof(T).Name}>] Failed to create delegates: {ex.Message}");
            }
        }

        public DdsWriter(DdsParticipant participant, string? topicName = null, IntPtr qos = default, string? partition = null)
            : this(participant, topicName, qos, partition, null)
        {
        }

        /// <summary>
        /// Creates a writer whose QoS comes from <see cref="DdsQosAttribute"/> on <typeparamref name="T"/>,
        /// overridden by <paramref name="options"/> (e.g. the ownership strength).
        /// </summary>
        public DdsWriter(DdsParticipant participant, string? topicName, DdsWriterOptions options, string? partition = null)
            : this(participant, topicName, IntPtr.Zero, partition, options ?? throw new ArgumentNullException(nameof(options)))
        {
        }

        private DdsWriter(DdsParticipant participant, string? topicName, IntPtr qos, string? partition, DdsWriterOptions? options)
        {
            topicName ??= GetTopicNameFromAttribute();
            _participant = participant;
            _topicName = topicName!;
            _publicationMatchedHandler = OnPublicationMatched;
            _livelinessLostHandler = OnLivelinessLost;
            _offeredDeadlineMissedHandler = OnOfferedDeadlineMissed;

            if (_nativeSizer == null || _nativeMarshaller == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).Name} does not exhibit expected DDS generated native methods (GetNativeSize, MarshalToNative).");
            }


            // QoS Setup
            IntPtr actualQos = qos;
            bool ownQos = false;

            if (actualQos == IntPtr.Zero)
            {
                actualQos = DdsQosBuilder.Create(typeof(T), options);
                ownQos = true;
            }

            try
            {
                string? activePartition = partition ?? participant.DefaultPartition;
                if (!string.IsNullOrEmpty(activePartition))
                {
                    DdsApi.dds_qset_partition(actualQos, 1, new[] { activePartition });
                }

                // 1. Get or register topic (auto-discovery) - Use modified QoS
                _topicHandle = participant.GetOrRegisterTopic<T>(topicName, actualQos);

                DdsApi.DdsEntity writer = default;

                writer = DdsApi.dds_create_writer(
                    participant.NativeEntity,
                    _topicHandle,
                    actualQos,
                    IntPtr.Zero);

                if (!writer.IsValid) 
                    throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create writer");
                
                _writerHandle = new DdsEntityHandle(writer);
            }
            finally
            {
                if (ownQos) DdsApi.dds_delete_qos(actualQos);
            }
            
            // Notify participant (triggers identity publishing if enabled)
            // Skip for the identity writer itself to avoid recursion
            if (typeof(T) != typeof(SenderIdentity))
            {
                _participant.RegisterWriter();
            }
        }

        private static string GetTopicNameFromAttribute() => DdsTopicName.Of<T>();

        public void Write(in T sample)
        {
            if (_nativeSizer != null && _nativeMarshaller != null)
            {
                PerformNativeOperation(sample, DdsApi.dds_write, false);
            }
            else
            {
                 throw new InvalidOperationException("Native delegates missing.");
            }
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
            if (_keyNativeSizer != null && _keyNativeMarshaller != null)
            {
                PerformNativeOperation(sample, DdsApi.dds_dispose, true);
            }
            else
            {
                 throw new InvalidOperationException("Native Key delegates missing.");
            }
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
            if (_keyNativeSizer != null && _keyNativeMarshaller != null)
            {
                PerformNativeOperation(sample, DdsApi.dds_unregister_instance, true);
            }
            else
            {
                 throw new InvalidOperationException("Native Key delegates missing.");
            }
        }

        private void PerformNativeOperation(in T sample, Func<DdsApi.DdsEntity, IntPtr, int> operation, bool isKey)
        {
             if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
             
             var sizer = isKey ? _keyNativeSizer : _nativeSizer;
             var marshaller = isKey ? _keyNativeMarshaller : _nativeMarshaller;
             var headSize = isKey ? _keyNativeHeadSize : _nativeHeadSize;
             
             // Safety check - should be guaranteed by caller
             if (sizer == null || marshaller == null) return; 

             int totalSize = sizer(sample);
             byte[] buffer = Arena.Rent(totalSize);
             
             try
             {
                 unsafe
                 {
                     fixed (byte* p = buffer)
                     {
                         IntPtr ptr = (IntPtr)p;
                         
                         if (headSize == 0) headSize = totalSize;
                         
                         var span = buffer.AsSpan(0, totalSize);
                         var arena = new NativeArena(span, ptr, headSize);
                         
                         marshaller(sample, ptr, ref arena);
                         
                         int ret = operation(_writerHandle.NativeHandle, ptr);
                         if (ret < 0) throw new DdsException((DdsApi.DdsReturnCode)ret, $"Native operation failed: {ret}");
                     }
                 }
             }
             finally
             {
                 Arena.Return(buffer);
             }
        }

        
        /// <summary>
        /// Blocks until every matched reliable reader has acknowledged everything this writer
        /// has written so far (writes, disposes and unregisters), or until <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait; <see cref="Timeout.InfiniteTimeSpan"/> (or any negative value) waits forever.</param>
        /// <returns><c>true</c> when all data was acknowledged; <c>false</c> on timeout.</returns>
        /// <exception cref="DdsException">The native call failed for any reason other than a timeout.</exception>
        public bool WaitForAcks(TimeSpan timeout)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            int ret = DdsApi.dds_wait_for_acks(_writerHandle.NativeHandle.Handle, DdsQosBuilder.ToDuration(timeout));
            if (ret == (int)DdsApi.DdsReturnCode.Timeout) return false;
            if (ret < 0) throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_wait_for_acks failed: {ret}");
            return true;
        }

        /// <summary>
        /// Asserts that this writer is alive. Needed with <see cref="DdsLivelinessKind.ManualByTopic"/>
        /// (and <see cref="DdsLivelinessKind.ManualByParticipant"/>) liveliness when the writer has
        /// nothing to write within its lease; any write asserts liveliness too.
        /// </summary>
        public void AssertLiveliness()
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
            int ret = DdsApi.dds_assert_liveliness(_writerHandle.NativeHandle.Handle);
            if (ret < 0) throw new DdsException((DdsApi.DdsReturnCode)ret, $"dds_assert_liveliness failed: {ret}");
        }

        /// <summary>Raised when this writer failed to assert liveliness within its lease.</summary>
        public event EventHandler<DdsApi.DdsLivelinessLostStatus>? LivelinessLost
        {
            add { lock (_listenerLock) { _livelinessLost += value; EnsureListenerAttached(); } }
            remove { lock (_listenerLock) { _livelinessLost -= value; } }
        }

        /// <summary>Raised when this writer did not update an instance within its deadline.</summary>
        public event EventHandler<DdsApi.DdsOfferedDeadlineMissedStatus>? OfferedDeadlineMissed
        {
            add { lock (_listenerLock) { _offeredDeadlineMissed += value; EnsureListenerAttached(); } }
            remove { lock (_listenerLock) { _offeredDeadlineMissed -= value; } }
        }

        /// <summary>Cumulative liveliness-lost status of this writer.</summary>
        public DdsApi.DdsLivelinessLostStatus LivelinessLostStatus
        {
            get
            {
                if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
                DdsApi.dds_get_liveliness_lost_status(_writerHandle.NativeHandle.Handle, out var status);
                return status;
            }
        }

        /// <summary>Cumulative offered-deadline-missed status of this writer.</summary>
        public DdsApi.DdsOfferedDeadlineMissedStatus OfferedDeadlineMissedStatus
        {
            get
            {
                if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));
                DdsApi.dds_get_offered_deadline_missed_status(_writerHandle.NativeHandle.Handle, out var status);
                return status;
            }
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
                 DdsApi.dds_lset_liveliness_lost(_listener, _livelinessLostHandler);
                 DdsApi.dds_lset_offered_deadline_missed(_listener, _offeredDeadlineMissedHandler);
                 
                 if (_writerHandle != null)
                 {
                     DdsApi.dds_writer_set_listener(_writerHandle.NativeHandle, _listener);
                 }
             }
        }

        // [MonoPInvokeCallback(typeof(DdsApi.DdsOnPublicationMatched))]
        private static void OnPublicationMatched(int writer, DdsApi.DdsPublicationMatchedStatus status, IntPtr arg)
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

        private static void OnLivelinessLost(int writer, DdsApi.DdsLivelinessLostStatus status, IntPtr arg)
        {
             if (arg == IntPtr.Zero) return;
             try
             {
                 var handle = GCHandle.FromIntPtr(arg);
                 if (handle.IsAllocated && handle.Target is DdsWriter<T> self)
                     self._livelinessLost?.Invoke(self, status);
             }
             catch { }
        }

        private static void OnOfferedDeadlineMissed(int writer, DdsApi.DdsOfferedDeadlineMissedStatus status, IntPtr arg)
        {
             if (arg == IntPtr.Zero) return;
             try
             {
                 var handle = GCHandle.FromIntPtr(arg);
                 if (handle.IsAllocated && handle.Target is DdsWriter<T> self)
                     self._offeredDeadlineMissed?.Invoke(self, status);
             }
             catch { }
        }

        public DdsInstanceHandle LookupInstance(in T keySample)
        {
            if (_writerHandle == null) throw new ObjectDisposedException(nameof(DdsWriter<T>));

            if (_keyNativeSizer != null && _keyNativeMarshaller != null)
            {
                 int size = _keyNativeSizer(keySample);
                 byte[] buffer = Arena.Rent(size);
                 try
                 {
                     unsafe
                     {
                         fixed (byte* p = buffer)
                         {
                             int headSize = _keyNativeHeadSize;
                             if (headSize == 0) headSize = size;
                             
                             IntPtr ptr = (IntPtr)p;
                             var span = buffer.AsSpan(0, size);
                             var arena = new NativeArena(span, ptr, headSize);
                             
                             _keyNativeMarshaller(keySample, ptr, ref arena);
                             
                             long handle = DdsApi.dds_lookup_instance(_writerHandle.NativeHandle.Handle, ptr);
                             return new DdsInstanceHandle(handle);
                         }
                     }
                 }
                 finally
                 {
                     Arena.Return(buffer);
                 }
            }
            throw new InvalidOperationException("Native Key delegates missing.");
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
        
        private static GetNativeSizeDelegate? CreateNativeSizerDelegate(string methodName)
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(T).MakeByRefType() }, null);
            if (method == null) return null;
            
            return (GetNativeSizeDelegate)Delegate.CreateDelegate(typeof(GetNativeSizeDelegate), method);
        }

        private static MarshalToNativeDelegate? CreateNativeMarshallerDelegate(string methodName)
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(T).MakeByRefType(), typeof(IntPtr), typeof(NativeArena).MakeByRefType() }, null);
            if (method == null) return null;

            return (MarshalToNativeDelegate)Delegate.CreateDelegate(typeof(MarshalToNativeDelegate), method);
        }
    }

}
