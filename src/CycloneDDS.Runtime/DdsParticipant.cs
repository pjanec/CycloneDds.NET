using System;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public sealed class DdsParticipant : IDisposable
    {
        private DdsEntityHandle? _handle;
        private readonly uint _domainId;
        private bool _disposed;

        public DdsParticipant(uint domainId = 0)
        {
            _domainId = domainId;
            var entity = DdsApi.dds_create_participant(domainId, IntPtr.Zero, IntPtr.Zero);

            if (!entity.IsValid)
            {
                // Retrieve error code from the handle value if it's negative
                int handleVal = entity.Handle;
                if (handleVal < 0)
                {
                    DdsApi.DdsReturnCode err = (DdsApi.DdsReturnCode)handleVal;
                    throw new DdsException(err, "Failed to create participant");
                }
                
                throw new DdsException(DdsApi.DdsReturnCode.Error, "Failed to create participant (Invalid Handle)");
            }
            
            _handle = new DdsEntityHandle(entity);
        }

        public uint DomainId => _domainId;
        
        public bool IsDisposed => _disposed;

        internal DdsApi.DdsEntity NativeEntity
        {
            get
            {
                if (_disposed || _handle == null)
                {
                    throw new ObjectDisposedException(nameof(DdsParticipant));
                }
                return _handle.NativeHandle;
            }
        }
        
        internal DdsEntityHandle HandleWrapper
        {
            get
            {
                if (_disposed || _handle == null)
                {
                    throw new ObjectDisposedException(nameof(DdsParticipant));
                }
                return _handle;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _handle?.Dispose();
                _handle = null;
                _disposed = true;
            }
        }
    }
}
