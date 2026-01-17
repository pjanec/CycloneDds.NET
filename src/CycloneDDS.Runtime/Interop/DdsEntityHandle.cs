using System;

namespace CycloneDDS.Runtime.Interop
{
    public sealed class DdsEntityHandle : IDisposable
    {
        private DdsApi.DdsEntity _entity;
        private bool _disposed;

        public DdsEntityHandle(DdsApi.DdsEntity entity)
        {
            _entity = entity;
        }

        public DdsApi.DdsEntity NativeHandle => _entity;

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_entity.IsValid)
                {
                    DdsApi.dds_delete(_entity);
                }
                _entity = DdsApi.DdsEntity.Null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~DdsEntityHandle()
        {
            Dispose();
        }
    }
}
