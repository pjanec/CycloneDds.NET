using System;
using System.Linq;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.CodeGen.Runtime;

namespace CycloneDDS.Runtime;

public sealed class DdsReader<TNative> : IDisposable where TNative : unmanaged
{
    private DdsEntityHandle? _readerHandle;
    private readonly DdsParticipant _participant;
    private readonly TopicMetadata _metadata;
    
    public DdsReader(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        
        // Auto-discover topic metadata (same as Writer)
        var typeName = typeof(TNative).Name;
        if (typeName.EndsWith("Native"))
            typeName = typeName[..^6]; // Remove "Native" suffix
        
        var allTopics = MetadataRegistry.GetAllTopics();
        _metadata = allTopics.FirstOrDefault(m => m.NativeType == typeof(TNative))
            ?? throw new DdsException($"No topic metadata found for {typeof(TNative).Name}", 
                DdsReturnCode.BadParameter);
        
        // Create topic
        DdsApi.DdsEntity topic;
        bool createdTopic = false;

        if (_metadata.BuiltinTopicHandle != IntPtr.Zero)
        {
            topic = new DdsApi.DdsEntity { Handle = _metadata.BuiltinTopicHandle };
        }
        else
        {
            topic = DdsApi.dds_create_topic(
                participant.Entity,
                IntPtr.Zero, // descriptor
                _metadata.TopicName,
                IntPtr.Zero, // QoS
                IntPtr.Zero); // listener
                
            if (!topic.IsValid)
                throw new DdsException($"Failed to create topic {_metadata.TopicName}", 
                    DdsReturnCode.Error);
            createdTopic = true;
        }
        
        // Create reader
        var reader = DdsApi.dds_create_reader(
            participant.Entity,
            topic,
            IntPtr.Zero, // QoS
            IntPtr.Zero); // listener
            
        if (!reader.IsValid)
        {
            if (createdTopic) DdsApi.dds_delete(topic);
            throw new DdsException($"Failed to create reader for {_metadata.TopicName}", 
                DdsReturnCode.Error);
        }
        
        _readerHandle = new DdsEntityHandle(reader);
    }
    
    public unsafe int Take(Span<TNative> buffer, int maxSamples = 32)
    {
        if (_readerHandle == null)
            throw new ObjectDisposedException(nameof(DdsReader<TNative>));
        
        var samples = new IntPtr[maxSamples];
        var info = new IntPtr[maxSamples];
        
        var count = DdsApi.dds_take(
            _readerHandle.Entity,
            samples,
            info,
            maxSamples,
            0); // mask
        
        if (count < 0)
            throw new DdsException("Take failed", (DdsReturnCode)count);
        
        // Copy to buffer
        for (int i = 0; i < count && i < buffer.Length; i++)
        {
            buffer[i] = Marshal.PtrToStructure<TNative>(samples[i]);
        }
        
        // Return loan
        if (count > 0)
            DdsApi.dds_return_loan(_readerHandle.Entity, samples, count);
        
        return count;
    }
    
    public unsafe bool TryTake(out TNative sample)
    {
        sample = default;
        if (_readerHandle == null)
            return false;
        
        Span<TNative> buffer = stackalloc TNative[1];
        var count = Take(buffer, 1);
        
        if (count > 0)
        {
            sample = buffer[0];
            return true;
        }
        
        return false;
    }
    
    public bool IsDisposed => _readerHandle == null;

    public void Dispose()
    {
        _readerHandle?.Dispose();
        _readerHandle = null;
    }
}
