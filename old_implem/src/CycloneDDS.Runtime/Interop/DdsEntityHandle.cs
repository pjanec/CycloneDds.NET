using System;

namespace CycloneDDS.Runtime.Interop;

/// <summary>
/// RAII wrapper for DDS entity handles.
/// </summary>
public sealed class DdsEntityHandle : IDisposable
{
    private DdsApi.DdsEntity _entity;
    private bool _disposed = false;
    
    public DdsEntityHandle(DdsApi.DdsEntity entity)
    {
        _entity = entity;
    }
    
    public DdsApi.DdsEntity Entity => _entity;
    public bool IsValid => _entity.IsValid && !_disposed;
    
    public void Dispose()
    {
        if (!_disposed && _entity.IsValid)
        {
            DdsApi.dds_delete(_entity);
            _entity = DdsApi.DdsEntity.Null;
            _disposed = true;
        }
    }
}
