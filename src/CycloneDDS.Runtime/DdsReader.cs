using System;
using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using CycloneDDS.Core;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Memory;

namespace CycloneDDS.Runtime
{
    public delegate void DeserializeDelegate<TView>(ref CdrReader reader, out TView view);

    public sealed class DdsReader<T, TView> : IDisposable 
        where TView : struct
    {
        private DdsEntityHandle? _readerHandle;
        private DdsEntityHandle? _topicHandle;
        private DdsParticipant? _participant;
        private readonly IntPtr _topicDescriptor;
        
        private static readonly DeserializeDelegate<TView>? _deserializer;
        
        static DdsReader()
        {
            try { _deserializer = CreateDeserializerDelegate(); }
            catch (Exception ex) { Console.WriteLine($"[DdsReader] Failed delegate: {ex.Message}"); }
        }

        public DdsReader(DdsParticipant participant, string topicName, IntPtr topicDescriptor)
        {
            if (_deserializer == null) 
                 throw new InvalidOperationException($"Type {typeof(T).Name} missing Deserialize method.");

            _participant = participant;
            _topicDescriptor = topicDescriptor;

            // Create Topic
            var topic = DdsApi.dds_create_topic(participant.NativeEntity, topicDescriptor, topicName, IntPtr.Zero, IntPtr.Zero);
            if (!topic.IsValid)
            {
                 int err = topic.Handle;
                 DdsApi.DdsReturnCode rc = (DdsApi.DdsReturnCode)err;
                 throw new DdsException(rc, $"Failed to create topic '{topicName}'");
            }
            _topicHandle = new DdsEntityHandle(topic);

            // Create QoS
            IntPtr qos = DdsApi.dds_create_qos();
            try
            {
                // Create Reader
                var reader = DdsApi.dds_create_reader(participant.NativeEntity, topic, qos, IntPtr.Zero);
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
                DdsApi.dds_delete_qos(qos);
            }
        }

        public ViewScope<TView> Take(int maxSamples = 32)
        {
             if (_readerHandle == null) throw new ObjectDisposedException(nameof(DdsReader<T, TView>));
             
             IntPtr[] samples = ArrayPool<IntPtr>.Shared.Rent(maxSamples);
             DdsApi.DdsSampleInfo[] infos = ArrayPool<DdsApi.DdsSampleInfo>.Shared.Rent(maxSamples);
             
             // Use dds_take_serdata to get opaque handles instead of deserialized samples
             int count = DdsApi.dds_take_serdata(
                 _readerHandle.NativeHandle,
                 samples,
                 infos,
                 (UIntPtr)maxSamples,
                 (uint)maxSamples);
             
             if (count < 0)
             {
                 ArrayPool<IntPtr>.Shared.Return(samples);
                 ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(infos);
                 
                 if (count == (int)DdsApi.DdsReturnCode.NoData)
                     return new ViewScope<TView>(_readerHandle.NativeHandle, null, null, 0, null);

                 throw new DdsException((DdsApi.DdsReturnCode)count, $"dds_take_serdata failed (ReturnCode: {(DdsApi.DdsReturnCode)count})");
             }
             
             return new ViewScope<TView>(_readerHandle.NativeHandle, samples, infos, count, _deserializer);
        }

        public void Dispose()
        {
            _readerHandle?.Dispose();
            _readerHandle = null;
            _topicHandle?.Dispose();
            _topicHandle = null;
            _participant = null;
        }
        
        private static DeserializeDelegate<TView> CreateDeserializerDelegate()
        {
             var method = typeof(T).GetMethod("Deserialize", new[] { typeof(CdrReader).MakeByRefType() });
             if (method == null) throw new MissingMethodException(typeof(T).Name, "Deserialize");
             
             var dm = new DynamicMethod("DeserializeThunk", typeof(void), new[] { typeof(CdrReader).MakeByRefType(), typeof(TView).MakeByRefType() }, typeof(DdsReader<T,TView>).Module);
             var il = dm.GetILGenerator();
             il.Emit(OpCodes.Ldarg_0); // ref reader
             il.Emit(OpCodes.Call, method); // returns TView (stack)
             il.Emit(OpCodes.Ldarg_1); // out view
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
        
        public ReadOnlySpan<DdsApi.DdsSampleInfo> Infos => _infos != null ? _infos.AsSpan(0, _count) : ReadOnlySpan<DdsApi.DdsSampleInfo>.Empty;

        internal ViewScope(DdsApi.DdsEntity reader, IntPtr[]? samples, DdsApi.DdsSampleInfo[]? infos, int count, DeserializeDelegate<TView>? deserializer)
        {
            _reader = reader;
            _samples = samples;
            _infos = infos;
            _count = count;
            _deserializer = deserializer;
        }
        
        public int Count => _count;

        public TView this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new IndexOutOfRangeException();
                if (_infos == null || _samples == null) throw new ObjectDisposedException("ViewScope");
                if (!_infos[index].ValidData || _samples[index] == IntPtr.Zero) return default;
                
                // Convert serdata to CDR buffer (allocates)
                IntPtr serdata = _samples[index];
                IntPtr cdrPtr;
                uint cdrLen;
                
                int rc = DdsApi.dds_serdata_to_cdr(serdata, out cdrPtr, out cdrLen);
                if (rc < 0) throw new DdsException((DdsApi.DdsReturnCode)rc, "dds_serdata_to_cdr failed");
                
                try
                {
                    unsafe
                    {
                        var span = new ReadOnlySpan<byte>((void*)cdrPtr, (int)cdrLen);
                        var reader = new CdrReader(span);
                        _deserializer!(ref reader, out TView view);
                        return view;
                    }
                }
                finally
                {
                    // Free the buffer allocated by dds_serdata_to_cdr
                    DdsApi.dds_free(cdrPtr);
                }
            }
        }
        
        public void Dispose()
        {
            if (_count > 0 && _samples != null)
            {
                DdsApi.dds_return_loan(_reader, _samples, _count);
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
