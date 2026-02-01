using System;
using System.Buffers;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public delegate void NativeUnmarshalDelegate<T>(IntPtr nativeData, out T managedData);

    public readonly ref struct DdsSample<T>
    {
        public readonly T Data;
        public readonly DdsApi.DdsSampleInfo Info;

        public DdsSample(T data, DdsApi.DdsSampleInfo info)
        {
            Data = data;
            Info = info;
        }
    }

    public ref struct DdsLoan<T>
    {
        private readonly DdsEntityHandle _reader;
        private readonly IntPtr[] _samples; // Rented from ArrayPool
        private readonly DdsApi.DdsSampleInfo[] _infos; // Rented from ArrayPool
        private readonly int _length;
        private readonly NativeUnmarshalDelegate<T> _unmarshaller;
        private bool _disposed;

        public DdsLoan(
            DdsEntityHandle reader, 
            IntPtr[] samples, 
            DdsApi.DdsSampleInfo[] infos, 
            int length, 
            NativeUnmarshalDelegate<T> unmarshaller)
        {
            _reader = reader;
            _samples = samples;
            _infos = infos;
            _length = length;
            _unmarshaller = unmarshaller;
            _disposed = false;
        }

        public int Length => _length;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_length > 0)
            {
                // Return loan to DDS
                // We pass the array of pointers (samples) that was filled by dds_take
                DdsApi.dds_return_loan(_reader.NativeHandle.Handle, _samples, _length);
            }
            
            // Return arrays to pool
            ArrayPool<IntPtr>.Shared.Return(_samples);
            ArrayPool<DdsApi.DdsSampleInfo>.Shared.Return(_infos);
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public ref struct Enumerator
        {
            private readonly DdsLoan<T> _loan;
            private int _index;
            private DdsSample<T> _current;

            public Enumerator(DdsLoan<T> loan)
            {
                _loan = loan;
                _index = -1;
                _current = default;
            }

            public bool MoveNext()
            {
                _index++;
                if (_index >= _loan._length) return false;

                var info = _loan._infos[_index];
                T data = default;
                
                if (info.ValidData != 0)
                {
                    // Marshall from native
                    _loan._unmarshaller(_loan._samples[_index], out data);
                }
                
                _current = new DdsSample<T>(data, info);
                return true;
            }

            public DdsSample<T> Current => _current;
        }
    }
}
