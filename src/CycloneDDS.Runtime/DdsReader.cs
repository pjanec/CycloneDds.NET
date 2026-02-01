using System;
using System.Buffers;
using System.Reflection;
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
    public sealed class DdsReader<T> : IDisposable 
        where T : struct
    {
        private SenderRegistry? _registry;
        private DdsEntityHandle? _readerHandle;
        private DdsApi.DdsEntity _topicHandle;
        private DdsParticipant? _participant;

        private IntPtr _listener = IntPtr.Zero;
        private GCHandle _paramHandle;
        private volatile TaskCompletionSource<bool>? _waitTaskSource;
        private readonly DdsApi.DdsOnDataAvailable _dataAvailableHandler;
        private readonly DdsApi.DdsOnSubscriptionMatched _subscriptionMatchedHandler;
        private readonly object _listenerLock = new object();
        
        private volatile Predicate<T>? _filter;
        
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
                if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));
                DdsApi.dds_get_subscription_matched_status(_readerHandle.NativeHandle.Handle, out var status);
                return status;
            }
        }

        private static readonly NativeUnmarshalDelegate<T>? _unmarshaller;
        private delegate int GetNativeSizeDelegate(in T sample);
        private delegate void MarshalToNativeDelegate(in T sample, IntPtr target, ref NativeArena arena);

        private static readonly GetNativeSizeDelegate? _nativeSizer;
        private static readonly MarshalToNativeDelegate? _nativeMarshaller;
        private static readonly int _nativeHeadSize;

        static DdsReader()
        {
            try { 
                _unmarshaller = CreateUnmarshallerDelegate();

                var nativeSizeMethod = typeof(T).GetMethod("GetNativeSize", new[] { typeof(T).MakeByRefType() });
                if (nativeSizeMethod != null) _nativeSizer = (GetNativeSizeDelegate)nativeSizeMethod.CreateDelegate(typeof(GetNativeSizeDelegate));

                var toNativeMethod = typeof(T).GetMethod("MarshalToNative", new[] { typeof(T).MakeByRefType(), typeof(IntPtr), typeof(NativeArena).MakeByRefType() });
                if (toNativeMethod != null) _nativeMarshaller = (MarshalToNativeDelegate)toNativeMethod.CreateDelegate(typeof(MarshalToNativeDelegate));
                
                var headSizeMethod = typeof(T).GetMethod("GetNativeHeadSize", BindingFlags.Public | BindingFlags.Static);
                if (headSizeMethod != null) _nativeHeadSize = (int)headSizeMethod.Invoke(null, null);
            }
            catch (Exception ex) { Console.WriteLine($"[DdsReader] Initialization failed: {ex}"); throw; }
        }

        public DdsReader(DdsParticipant participant, string topicName, IntPtr qos = default)
        {
            _dataAvailableHandler = OnDataAvailable;
            _subscriptionMatchedHandler = OnSubscriptionMatched;
            
            if (_unmarshaller == null) 
                 throw new InvalidOperationException($"Type {typeof(T).Name} missing MarshalFromNative method.");

            _participant = participant;

            IntPtr actualQos = qos;
            bool ownQos = false;

            if (actualQos == IntPtr.Zero)
            {
                actualQos = DdsApi.dds_create_qos();
                ownQos = true;
            }

            try
            {
                _topicHandle = participant.GetOrRegisterTopic<T>(topicName, actualQos);

                DdsApi.DdsEntity reader = DdsApi.dds_create_reader(
                    participant.NativeEntity,
                    _topicHandle, 
                    actualQos, 
                    IntPtr.Zero);

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
                if (ownQos) DdsApi.dds_delete_qos(actualQos);
            }
        }

        public void SetFilter(Predicate<T>? filter) => _filter = filter;

        public ViewScope<T> Take(int maxSamples = 32) => ReadOrTake(maxSamples, 0xFFFFFFFF, true);
        public ViewScope<T> Read(int maxSamples = 32) => ReadOrTake(maxSamples, 0xFFFFFFFF, false);

        public ViewScope<T> Take(int maxSamples, DdsSampleState sampleState, DdsViewState viewState, DdsInstanceState instanceState)
             => ReadOrTake(maxSamples, (uint)sampleState | (uint)viewState | (uint)instanceState, true);

        public ViewScope<T> Read(int maxSamples, DdsSampleState sampleState, DdsViewState viewState, DdsInstanceState instanceState)
             => ReadOrTake(maxSamples, (uint)sampleState | (uint)viewState | (uint)instanceState, false);

        private ViewScope<T> ReadOrTake(int maxSamples, uint mask, bool isTake)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));
             
             var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             
             Array.Clear(samples, 0, maxSamples);
             
             int count;
             if (isTake)
                 count = DdsApi.dds_take_mask(_readerHandle.NativeHandle.Handle, samples, infos, (UIntPtr)maxSamples, (uint)maxSamples, mask);
             else
                 count = DdsApi.dds_read_mask(_readerHandle.NativeHandle.Handle, samples, infos, (UIntPtr)maxSamples, (uint)maxSamples, mask);

             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 
                 if (count == (int)DdsApi.DdsReturnCode.NoData)
                 {
                     return new ViewScope<T>(_readerHandle.NativeHandle, null, null, 0, 0, null, _filter, _registry);
                 }
                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_{(isTake ? "take" : "read")} failed: {count}");
             }
             
             return new ViewScope<T>(_readerHandle.NativeHandle, samples, infos, count, maxSamples, _unmarshaller!, _filter, _registry);
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
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));
             
             EnsureListenerAttached();
             
             var tcs = _waitTaskSource;
             if (tcs == null || tcs.Task.IsCompleted)
             {
                 tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                 _waitTaskSource = tcs;
             }
             
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
                 if (handle.IsAllocated && handle.Target is DdsReader<T> self)
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
                 if (handle.IsAllocated && handle.Target is DdsReader<T> self)
                 {
                     self._waitTaskSource?.TrySetResult(true);
                 }
             }
             catch { }
        }

        private T[] TakeBatch()
        {
            using var scope = Take();
            if (scope.Count == 0) return Array.Empty<T>();
            var batch = new T[scope.Count];
            int i = 0;
            foreach (var item in scope)
            {
                batch[i++] = item;
            }
            return batch;
        }

        public async IAsyncEnumerable<T> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = TakeBatch();
                if (batch.Length > 0)
                {
                    foreach (var item in batch) yield return item;
                    continue; 
                }

                await WaitDataAsync(cancellationToken);
                
                batch = TakeBatch();
                 foreach (var item in batch) yield return item;
            }
        }

        public void Dispose()
        {
            if (_listener != IntPtr.Zero)
            {
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
            if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));

            if (_nativeMarshaller != null && _nativeSizer != null)
            {
                int size = _nativeSizer(in keySample);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
                try
                {
                    unsafe
                    {
                        fixed (byte* ptr = buffer)
                        {
                            NativeArena arena = new NativeArena(new Span<byte>(buffer, 0, size), (IntPtr)ptr, _nativeHeadSize);
                            _nativeMarshaller(in keySample, (IntPtr)ptr, ref arena);
                            long handle = DdsApi.dds_lookup_instance(_readerHandle.NativeHandle.Handle, (IntPtr)ptr);
                            return new DdsInstanceHandle(handle);
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            throw new NotSupportedException("Native marshaling delegates missing for this type.");
        }

        public ViewScope<T> TakeInstance(DdsInstanceHandle handle, int maxSamples = 1)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));
             var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             Array.Clear(samples, 0, maxSamples);
             
             int count = DdsApi.dds_take_instance(_readerHandle.NativeHandle.Handle, samples, infos, (UIntPtr)maxSamples, (uint)maxSamples, handle.Value);
             
             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 if (count == (int)DdsApi.DdsReturnCode.NoData) return new ViewScope<T>(_readerHandle.NativeHandle, null, null, 0, 0, null, _filter, _registry);
                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_take_instance failed: {count}");
             }
             return new ViewScope<T>(_readerHandle.NativeHandle, samples, infos, count, maxSamples, _unmarshaller!, _filter, _registry);
        }

        public ViewScope<T> ReadInstance(DdsInstanceHandle handle, int maxSamples = 1)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T>));
             var samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             var infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             Array.Clear(samples, 0, maxSamples);
             
             int count = DdsApi.dds_read_instance(_readerHandle.NativeHandle.Handle, samples, infos, (UIntPtr)maxSamples, (uint)maxSamples, handle.Value);
             
             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 if (count == (int)DdsApi.DdsReturnCode.NoData) return new ViewScope<T>(_readerHandle.NativeHandle, null, null, 0, 0, null, _filter, _registry);
                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_read_instance failed: {count}");
             }
             return new ViewScope<T>(_readerHandle.NativeHandle, samples, infos, count, maxSamples, _unmarshaller!, _filter, _registry);
        }

        public void EnableSenderTracking(SenderRegistry registry)
        {
            _registry = registry;
            this.SubscriptionMatched += OnSenderTrackingSubscriptionMatched;
        }

        private void OnSenderTrackingSubscriptionMatched(object? sender, DdsApi.DdsSubscriptionMatchedStatus e)
        {
            if (e.CurrentCountChange > 0 && _registry != null)
            {
                 var handles = GetMatchedPublicationHandles();
                 foreach (var handle in handles)
                 {
                     var writerGuid = GetMatchedPublicationGuid(handle);
                     _registry.RegisterRemoteWriter(handle, writerGuid);
                 }
            }
        }

        private long[] GetMatchedPublicationHandles()
        {
            if (_readerHandle == null) return Array.Empty<long>();
            var handles = new long[64];
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
            return default;
        }

        private static NativeUnmarshalDelegate<T> CreateUnmarshallerDelegate()
        {
             var method = typeof(T).GetMethod("MarshalFromNative", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(IntPtr), typeof(T).MakeByRefType() }, null);
             if (method == null) return null!;
             return (NativeUnmarshalDelegate<T>)method.CreateDelegate(typeof(NativeUnmarshalDelegate<T>));
        }
    }

    public ref struct ViewScope<T> where T : struct
    {
        private DdsApi.DdsEntity _reader;
        private IntPtr[]? _samples;
        private DdsApi.DdsSampleInfo[]? _infos;
        private int _count;
        private int _loanSize;
        private NativeUnmarshalDelegate<T>? _unmarshaller;
        private Predicate<T>? _filter;
        private SenderRegistry? _registry;
        
        public ReadOnlySpan<DdsApi.DdsSampleInfo> Infos => _infos != null ? _infos.AsSpan(0, _count) : ReadOnlySpan<DdsApi.DdsSampleInfo>.Empty;

        internal ViewScope(DdsApi.DdsEntity reader, IntPtr[]? samples, DdsApi.DdsSampleInfo[]? infos, int count, int loanSize, NativeUnmarshalDelegate<T>? unmarshaller, Predicate<T>? filter, SenderRegistry? registry)
        {
            _reader = reader;
            _samples = samples;
            _infos = infos;
            _count = count;
            _loanSize = loanSize;
            _unmarshaller = unmarshaller;
            _filter = filter;
            _registry = registry;
        }

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
             private ViewScope<T> _scope;
             private Predicate<T>? _filter;
             private int _index;
             private T _current;

             internal Enumerator(ViewScope<T> scope, Predicate<T>? filter)
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
                     if (_scope.HasData(_index, out T item))
                     {
                         if (_filter == null || _filter(item))
                         {
                             _current = item;
                             return true;
                         }
                     }
                 }
                 return false;
             }
             
             public T Current => _current;
        }
        
        internal bool HasData(int index, out T data)
        {
             data = default;
             if (_infos == null || _samples == null) return false; 
             
             if (_infos[index].ValidData == 0) return true; // Return default
             
             _unmarshaller!(_samples[index], out data);
             return true; 
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                if (_infos == null || _samples == null) throw new ObjectDisposedException("ViewScope");
                
                if (_infos[index].ValidData == 0) return default;
                
                _unmarshaller!(_samples[index], out T data);
                return data;
            }
        }
        
        public void Dispose()
        {
            if (_loanSize > 0 && _samples != null)
            {
                // Return LOAN to DDS
                DdsApi.dds_return_loan(_reader.Handle, _samples, _loanSize);
            }
            
            if (_samples != null) ArrayPool<IntPtr>.Shared.Return(_samples);
            if (_infos != null) ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(_infos);
            
            _count = 0;
            _loanSize = 0;
            _samples = null;
            _infos = null;
            _unmarshaller = null;
        }
    }
}
