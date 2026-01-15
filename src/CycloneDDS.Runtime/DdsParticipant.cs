using System;
using System.Collections.Generic;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime;

public sealed class DdsParticipant : IDisposable
{
    private DdsEntityHandle? _handle;
    private readonly uint _domainId;
    private readonly string[] _partitions;
    
    public DdsParticipant(uint domainId = 0, params string[] partitions)
    {
        _domainId = domainId;
        _partitions = partitions ?? Array.Empty<string>();
        
        // Create participant (QoS=null, listener=null for now)
        var entity = DdsApi.dds_create_participant(domainId, IntPtr.Zero, IntPtr.Zero);
        
        if (!entity.IsValid)
            throw new DdsException("Failed to create DDS participant", DdsReturnCode.Error);
        
        _handle = new DdsEntityHandle(entity);
    }
    
    public uint DomainId => _domainId;
    public IReadOnlyList<string> Partitions => _partitions;
    public bool IsDisposed => _handle == null;
    
    internal DdsApi.DdsEntity Entity
    {
        get
        {
            if (_handle == null)
                throw new ObjectDisposedException(nameof(DdsParticipant));
            return _handle.Entity;
        }
    }
    
    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }
}
