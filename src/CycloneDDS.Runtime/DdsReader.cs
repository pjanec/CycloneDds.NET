using System;
using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;
using CycloneDDS.Runtime.Tracking;
using CycloneDDS.Schema;

namespace CycloneDDS.Runtime
{
    public delegate void DeserializeDelegate<TView>(ref CdrReader reader, out TView view);

    public sealed class DdsReader<T, TView> : IDisposable 
        where TView : struct
    {
        // Add field for tracking
        private SenderRegistry? _registry;

        private static readonly byte _encodingKindLE;
        private static readonly byte _encodingKindBE;

        private DdsEntityHandle? _readerHandle;
        private DdsApi.DdsEntity _topicHandle;
        private DdsParticipant? _participant;

        // Async support
        private IntPtr _listener = IntPtr.Zero;
        private GCHandle _paramHandle;
        private volatile TaskCompletionSource<bool>? _waitTaskSource;
        private readonly DdsApi.DdsOnDataAvailable _dataAvailableHandler;
        private readonly DdsApi.DdsOnSubscriptionMatched _subscriptionMatchedHandler;
        private readonly object _listenerLock = new object();
        
        // Filtering
        private volatile Predicate<TView>? _filter;
        
        // Events
        private EventHandler<DdsApi.DdsSubscriptionMatchedStatus>? _subscriptionMatched;
        public event EventHandler<DdsApi.DdsSubscriptionMatchedStatus>? SubscriptionMatched
        {
            add 
            { 
                lock(_listenerLock) {
                    _subscriptionMatched += value; 
                    EnsureListenerAttached(); 
                }
            }
            remove 
            { 
                lock(_listenerLock) {
                    _subscriptionMatched -= value; 
                }
            }
        }
        
        public DdsApi.DdsSubscriptionMatchedStatus CurrentStatus
        {
            get
            {
                if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
                DdsApi.dds_get_subscription_matched_status(_readerHandle.NativeHandle.Handle, out var status);
                return status;
            }
        }

        private delegate void SerializeDelegate(in T sample, ref CdrWriter writer);
        private delegate int GetSerializedSizeDelegate(in T sample, int currentAlignment, CdrEncoding encoding);

        private static readonly DeserializeDelegate<TView>? _deserializer;
        private static readonly SerializeDelegate? _serializer;
        private static readonly GetSerializedSizeDelegate? _sizer;
        
        static DdsReader()
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

            try { 
                _deserializer = CreateDeserializerDelegate(); 
                _sizer = CreateSizerDelegate();
                _serializer = CreateSerializerDelegate(); 
                
                // Verify Struct Size
                uint nativeSize = DdsApi.dds_sample_info_size();
                int managedSize = Marshal.SizeOf<DdsApi.DdsSampleInfo>();
                if (nativeSize != managedSize)
                {
                    Console.WriteLine($"[DdsReader] CRITICAL: DdsSampleInfo size mismatch. Native: {nativeSize}, Managed: {managedSize}");
                    // throw new InvalidOperationException($"DdsSampleInfo size mismatch. Native: {nativeSize}, Managed: {managedSize}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[DdsReader] Initialization failed: {ex}"); throw; }
        }

        public DdsReader(DdsParticipant participant, string topicName, IntPtr qos = default)
        {
            _dataAvailableHandler = OnDataAvailable;
            _subscriptionMatchedHandler = OnSubscriptionMatched;
            if (_deserializer == null) 
                 throw new InvalidOperationException($"Type {typeof(T).Name} missing Deserialize method.");

            _participant = participant;

            // 1. Get or register topic (auto-discovery)
            // Use original QoS to ensure compatibility with existing topics
            // If internal topics (like SenderIdentity) are created, we must match their original QoS.
            DdsApi.DdsEntity topic = participant.GetOrRegisterTopic<T>(topicName, qos);
            _topicHandle = topic;

            // Handle QoS for Reader
            IntPtr readerQos = qos;
            bool qosCreated = false;
            if (readerQos == IntPtr.Zero)
            {
                readerQos = DdsApi.dds_create_qos();
                qosCreated = true;
            }

            try
            {
                // Set Data Representation: XCDR2 and XCDR1
                // Skip for internal SenderIdentity to avoid BadParameter (possible QoS incompatibility)
                if (topicName != "__FcdcSenderIdentity")
                {
                    short[] reps = new short[] { 2, 0 };
                    DdsApi.dds_qset_data_representation(readerQos, (uint)reps.Length, reps);
                }

                // 2. Create Reader
                 var reader = DdsApi.dds_create_reader(participant.NativeEntity, topic, readerQos, IntPtr.Zero);
                 if (!reader.IsValid)
                 {
                      int err = reader.Handle;
                      DdsApi.DdsReturnCode rc = (DdsApi.DdsReturnCode)err;
                      throw new DdsException(rc, $"Failed to create reader for '{topicName}'");
                 }
                 _readerHandle = new DdsEntityHandle(reader);
            }
            finally
            {
                if (qosCreated) DdsApi.dds_delete_qos(readerQos);
            }
        }

        public void SetFilter(Predicate<TView>? filter)
        {
            _filter = filter;
        }

        public ViewScope<TView> Take(int maxSamples = 32)
        {
            return ReadOrTake(maxSamples, 0xFFFFFFFF, true);
        }

        public ViewScope<TView> Read(int maxSamples = 32)
        {
            return ReadOrTake(maxSamples, 0xFFFFFFFF, false);
        }

        public ViewScope<TView> Take(int maxSamples, DdsSampleState sampleState, DdsViewState viewState, DdsInstanceState instanceState)
        {
             return ReadOrTake(maxSamples, (uint)sampleState | (uint)viewState | (uint)instanceState, true);
        }

        public ViewScope<TView> Read(int maxSamples, DdsSampleState sampleState, DdsViewState viewState, DdsInstanceState instanceState)
        {
             return ReadOrTake(maxSamples, (uint)sampleState | (uint)viewState | (uint)instanceState, false);
        }

        private ViewScope<TView> ReadOrTake(int maxSamples, uint mask, bool isTake)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
             
             var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             
             Array.Clear(samples, 0, maxSamples);
             Array.Clear(infos, 0, maxSamples); 
             
             int count;
             if (isTake)
             {
                 count = DdsApi.dds_takecdr(
                     _readerHandle.NativeHandle.Handle,
                     samples,
                     (uint)maxSamples,
                     infos,
                     mask);
             }
             else
             {
                 count = DdsApi.dds_readcdr(
                     _readerHandle.NativeHandle.Handle,
                     samples,
                     (uint)maxSamples,
                     infos,
                     mask);
             }

             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 
                 if (count == (int)DdsApi.DdsReturnCode.NoData)
                 {
                     return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null, _filter, _registry);
                 }
                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_{(isTake ? "take" : "read")}cdr failed: {count}");
             }
             
             return new ViewScope<TView>(_readerHandle.NativeHandle, samples, infos, count, _deserializer, _filter, _registry);
        }

        private bool HasData()
        {
            try 
            {
                using var scope = Read(1);
                return scope.Count > 0;
            }
            catch { return false; }
        }

        public async Task<bool> WaitDataAsync(CancellationToken cancellationToken = default)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
             
             EnsureListenerAttached();
             
             var tcs = _waitTaskSource;
             if (tcs == null || tcs.Task.IsCompleted)
             {
                 tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                 _waitTaskSource = tcs;
             }
             
             // Check if data is already available (Race Condition fix)
             // Note: This effectively 'peeks' at the data and may mark it as Read.
             if (HasData()) return true;

             using (cancellationToken.Register(() => tcs.TrySetCanceled()))
             {
                 try
                 {
                    return await tcs.Task;
                 }
                 catch (TaskCanceledException)
                 {
                    if (cancellationToken.IsCancellationRequested) throw;
                    return true;
                 }
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
                 DdsApi.dds_lset_data_available(_listener, _dataAvailableHandler);
                 DdsApi.dds_lset_subscription_matched(_listener, _subscriptionMatchedHandler);
                 
                 if (_readerHandle != null)
                 {
                     DdsApi.dds_reader_set_listener(_readerHandle.NativeHandle, _listener);
                 }
             }
        }
        
        private static void OnSubscriptionMatched(int reader, ref DdsApi.DdsSubscriptionMatchedStatus status, IntPtr arg)
        {
             if (arg == IntPtr.Zero) return;
             try
             {
                 var handle = GCHandle.FromIntPtr(arg);
                 if (handle.IsAllocated && handle.Target is DdsReader<T, TView> self)
                 {
                     self._subscriptionMatched?.Invoke(self, status);
                 }
             }
             catch { }
        }

        private static void OnDataAvailable(int reader, IntPtr arg)
        {
             if (arg == IntPtr.Zero) return;
             try
             {
                 var handle = GCHandle.FromIntPtr(arg);
                 if (handle.IsAllocated && handle.Target is DdsReader<T, TView> self)
                 {
                     self._waitTaskSource?.TrySetResult(true);
                 }
             }
             catch { }
        }

        private TView[] TakeBatch()
        {
            using var scope = Take();
            if (scope.Count == 0) return Array.Empty<TView>();
            var batch = new TView[scope.Count];
            for (int i = 0; i < scope.Count; i++)
            {
                batch[i] = scope[i];
            }
            return batch;
        }

        public async IAsyncEnumerable<TView> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check for data before waiting to handle pre-existing samples
                var batch = TakeBatch();
                if (batch.Length > 0)
                {
                    foreach (var item in batch) yield return item;
                    continue; 
                }

                await WaitDataAsync(cancellationToken);
                
                // After wait, try take again
                batch = TakeBatch();
                 foreach (var item in batch) yield return item;
            }
        }

        public void Dispose()
        {
            if (_listener != IntPtr.Zero)
            {
                // Unset listener from reader first? 
                if (_readerHandle != null)
                {
                     // dds_reader_set_listener(_readerHandle.NativeHandle, IntPtr.Zero); // Optional based on impl
                }
                DdsApi.dds_delete_listener(_listener);
                _listener = IntPtr.Zero;
            }
            if (_paramHandle.IsAllocated) _paramHandle.Free();

            _readerHandle?.Dispose();
            _readerHandle = null;
            _topicHandle = DdsApi.DdsEntity.Null;
            _participant = null;
        }
        
        public DdsInstanceHandle LookupInstance(in T keySample)
        {
            if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));

            // Use XCDR2 for lookup serdata creation
            CdrEncoding encoding = CdrEncoding.Xcdr2;

            // Use _sizer to calculate size. Start at offset 4 for header
            int size = _sizer!(keySample, 4, encoding);
            byte[] buffer = Arena.Rent(size + 4);

            try
            {
                var span = buffer.AsSpan(0, size + 4);
                var cdr = new CdrWriter(span, encoding);
                
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
                            long handle = DdsApi.dds_lookup_instance_serdata(_readerHandle.NativeHandle.Handle, serdata);
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

        public ViewScope<TView> TakeInstance(DdsInstanceHandle handle, int maxSamples = 1)
        {
            return ReadOrTakeInstance(handle, maxSamples, 0xFFFFFFFF, true);
        }

        public ViewScope<TView> ReadInstance(DdsInstanceHandle handle, int maxSamples = 1)
        {
            return ReadOrTakeInstance(handle, maxSamples, 0xFFFFFFFF, false);
        }

        private ViewScope<TView> ReadOrTakeInstance(DdsInstanceHandle handle, int maxSamples, uint mask, bool isTake)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
             
             var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             
             Array.Clear(samples, 0, maxSamples);
             Array.Clear(infos, 0, maxSamples); 
             
             int count;
             if (isTake)
             {
                 count = DdsApi.dds_takecdr_instance(
                     _readerHandle.NativeHandle.Handle,
                     samples,
                     (uint)maxSamples,
                     infos,
                     handle.Value,
                     mask);
             }
             else
             {
                 count = DdsApi.dds_readcdr_instance(
                     _readerHandle.NativeHandle.Handle,
                     samples,
                     (uint)maxSamples,
                     infos,
                     handle.Value,
                     mask);
             }

             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 
                 if (count == (int)DdsApi.DdsReturnCode.BadParameter)
                     throw new ArgumentException("Invalid instance handle");
                 
                 // Handle NoData or other errors by returning empty view
                 // If it's pure error we might want to throw, but standard Read returns empty on NoData
                 if (count == (int)DdsApi.DdsReturnCode.NoData)
                     return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null, _filter, _registry);

                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_{(isTake ? "take" : "read")}cdr_instance failed: {count}");
             }
             
             return new ViewScope<TView>(_readerHandle.NativeHandle, samples, infos, count, _deserializer, _filter, _registry);
        }

        /// <summary>
        /// Enable sender tracking for this reader.
        /// After this, ViewScope.GetSender(index) will return sender information.
        /// </summary>
        public void EnableSenderTracking(SenderRegistry registry)
        {
            _registry = registry;
            this.SubscriptionMatched += OnSenderTrackingSubscriptionMatched;
        }

        private void OnSenderTrackingSubscriptionMatched(object? sender, DdsApi.DdsSubscriptionMatchedStatus e)
        {
            if (e.CurrentCountChange > 0 && _registry != null)
            {
                // New writer(s) connected - register them
                try
                {
                    var handles = GetMatchedPublicationHandles();
                    foreach (var handle in handles)
                    {
                        var writerGuid = GetMatchedPublicationGuid(handle);
                        _registry.RegisterRemoteWriter(handle, writerGuid);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DdsReader] Failed to register matched publications: {ex}");
                }
            }
        }

        private long[] GetMatchedPublicationHandles()
        {
            if (_readerHandle == null) return Array.Empty<long>();

            // Query Cyclone DDS for matched publication count
            var handles = new long[64]; // Reasonable max
            int count = DdsApi.dds_get_matched_publications(
                _readerHandle.NativeHandle.Handle,
                handles,
                (uint)handles.Length);

            if (count < 0) return Array.Empty<long>();
            
            if (count > handles.Length)
            {
                 handles = new long[count];
                 count = DdsApi.dds_get_matched_publications(
                    _readerHandle.NativeHandle.Handle,
                    handles,
                    (uint)handles.Length);
            }
            
            if (count > 0)
            {
                if (count > handles.Length) count = handles.Length;
                var result = new long[count];
                Array.Copy(handles, result, count);
                return result;
            }

            return Array.Empty<long>();
        }

        private DdsGuid GetMatchedPublicationGuid(long publicationHandle)
        {
            if (_readerHandle == null) return default;
            
            IntPtr ptr = DdsApi.dds_get_matched_publication_data(
                _readerHandle.NativeHandle.Handle,
                publicationHandle);
                
            if (ptr != IntPtr.Zero)
            {
                try
                {
                    var data = Marshal.PtrToStructure<DdsApi.DdsBuiltinTopicEndpoint>(ptr);
                    return data.Key;
                }
                finally
                {
                    DdsApi.dds_builtintopic_free_endpoint(ptr);
                }
            }
            else
            {
                 // Console.WriteLine($"[DdsReader] dds_get_matched_publication_data returned NULL.");
            }
            
            return default;
        }


        private static GetSerializedSizeDelegate CreateSizerDelegate()
        {
            var method = typeof(T).GetMethod("GetSerializedSize", new[] { typeof(int), typeof(CdrEncoding) });
            if (method == null) throw new MissingMethodException(typeof(T).Name, "GetSerializedSize(int, CdrEncoding)");

            var dm = new DynamicMethod(
                "GetSerializedSizeThunk",
                typeof(int),
                new[] { typeof(T).MakeByRefType(), typeof(int), typeof(CdrEncoding) },
                typeof(DdsReader<T, TView>).Module);

            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // sample (ref)
            if (!typeof(T).IsValueType)
            {
                 il.Emit(OpCodes.Ldind_Ref); 
            }
            
            il.Emit(OpCodes.Ldarg_1); // offset
            il.Emit(OpCodes.Ldarg_2); // encoding
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
                typeof(DdsReader<T, TView>).Module);

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

        private static DeserializeDelegate<TView> CreateDeserializerDelegate()
        {
             var method = typeof(T).GetMethod("Deserialize", new[] { typeof(CdrReader).MakeByRefType() });
             if (method == null) throw new MissingMethodException(typeof(T).Name, "Deserialize");
             
             var dm = new DynamicMethod("DeserializeThunk", typeof(void), new[] { typeof(CdrReader).MakeByRefType(), typeof(TView).MakeByRefType() }, typeof(DdsReader<T,TView>).Module);
             var il = dm.GetILGenerator();
             // IL Stack: [out view], [ref reader] -> call -> [out view], [result] -> stobj -> []
             il.Emit(OpCodes.Ldarg_1); // out view
             il.Emit(OpCodes.Ldarg_0); // ref reader
             il.Emit(OpCodes.Call, method); // returns TView (stack)
             il.Emit(OpCodes.Stobj, typeof(TView));
             il.Emit(OpCodes.Ret);
             
             return (DeserializeDelegate<TView>)dm.CreateDelegate(typeof(DeserializeDelegate<TView>));
        }
    }

    public ref struct ViewScope<TView> where TView : struct
    {
        private DdsApi.DdsEntity _reader;
        private IntPtr[]? _samples;
        private DdsApi.DdsSampleInfo[]? _infos;
        private int _count;
        private DeserializeDelegate<TView>? _deserializer;
        private Predicate<TView>? _filter;
        private SenderRegistry? _registry;
        
        public ReadOnlySpan<DdsApi.DdsSampleInfo> Infos => _infos != null ? _infos.AsSpan(0, _count) : ReadOnlySpan<DdsApi.DdsSampleInfo>.Empty;

        internal ViewScope(DdsApi.DdsEntity reader, IntPtr[]? samples, DdsApi.DdsSampleInfo[]? infos, int count, DeserializeDelegate<TView>? deserializer, Predicate<TView>? filter, SenderRegistry? registry)
        {
            _reader = reader;
            _samples = samples;
            _infos = infos;
            _count = count;
            _deserializer = deserializer;
            _filter = filter;
            _registry = registry;
        }

        /// <summary>
        /// Get sender identity for the sample at given index.
        /// Returns null if tracking disabled or identity unknown.
        /// </summary>
        public SenderIdentity? GetSender(int index)
        {
            if (_registry == null) return null;
            if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
            if (_infos == null) return null;
            
            long handle = _infos[index].PublicationHandle;
            if (_registry.TryGetIdentity(handle, out var identity))
            {
                return identity;
            }
            return null;
        }
        
        public int Count => _count;

        public Enumerator GetEnumerator() => new Enumerator(this, _filter);

        public ref struct Enumerator
        {
             private ViewScope<TView> _scope;
             private Predicate<TView>? _filter;
             private int _index;
             private TView _current;

             internal Enumerator(ViewScope<TView> scope, Predicate<TView>? filter)
             {
                 _scope = scope;
                 _filter = filter;
                 _index = -1;
                 _current = default;
             }
             
             public bool MoveNext()
             {
                 while (++_index < _scope.Count)
                 {
                     TView item = _scope[_index];
                     if (_filter == null || _filter(item))
                     {
                         _current = item;
                         return true;
                     }
                 }
                 return false;
             }
             
             public TView Current => _current;
        }

        public TView this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                if (_infos == null || _samples == null) throw new ObjectDisposedException("ViewScope");
                
                // If invalid data, return default
                if (_infos[index].ValidData == 0) return default;
                
                IntPtr serdata = _samples[index];
                if (serdata == IntPtr.Zero) return default;

                // Lazy Deserialization from Serdata
                uint size = DdsApi.ddsi_serdata_size(serdata);
                
                if (size == 0) return default;
                
                byte[] buffer = Arena.Rent((int)size);
                
                try
                {
                    unsafe
                    {
                        fixed (byte* p = buffer)
                        {
                            // Extract CDR from serdata
                            DdsApi.ddsi_serdata_to_ser(serdata, UIntPtr.Zero, (UIntPtr)size, (IntPtr)p);
                            
                            // Deserialize
                            var span = new ReadOnlySpan<byte>(p, (int)size);
                            
                            // Check XCDR2 (Byte 1 >= 6). 
                            // Encapsulation Header: Byte 0, Byte 1 (ID), Byte 2, Byte 3 (Options)
                            // ID 0x0006 - 0x000D are XCDR2. 
                            // Byte 1 stores the specific ID value in standard encodings.
                            CdrEncoding encoding = CdrEncoding.Xcdr1;
                            if (size >= 2)
                            {
                                if (p[1] >= 6) encoding = CdrEncoding.Xcdr2;
                            }

                            var reader = new CdrReader(span, encoding);
                            
                            // Cyclone DDS provides the 4-byte encapsulation header in the serdata.
                            // We must skip it so that CdrReader is aligned to the start of the payload
                            // and reads the correct data.
                            if (reader.Remaining >= 4)
                            {
                                // TODO: Verify header if needed.
                                reader.ReadInt32(); // Advance 4 bytes
                            }
                            
                            _deserializer!(ref reader, out TView view);
                            return view;
                        }
                    }
                }
                finally
                {
                    Arena.Return(buffer);
                }
            }
        }
        
        public void Dispose()
        {
            // Release references to serdata
            if (_count > 0 && _samples != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_samples[i] != IntPtr.Zero)
                    {
                        DdsApi.ddsi_serdata_unref(_samples[i]);
                    }
                }
            }
            
            if (_samples != null) ArrayPool<IntPtr>.Shared.Return(_samples);
            if (_infos != null) ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(_infos);
            
            _count = 0;
            _samples = null;
            _infos = null;
            _deserializer = null;
        }
    }
}
