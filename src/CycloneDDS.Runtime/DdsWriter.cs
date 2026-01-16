using System;
using System.Linq;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Schema.Metadata;
using CycloneDDS.Schema.Descriptors;
using CycloneDDS.Runtime.Descriptors;
using CycloneDDS.CodeGen.Runtime;

namespace CycloneDDS.Runtime;

public sealed class DdsWriter<TNative> : IDisposable where TNative : unmanaged
{
    private DdsEntityHandle? _writerHandle;
    private readonly DdsParticipant _participant;
    private readonly TopicMetadata _metadata;
    private NativeDescriptor? _nativeDescriptor; // Added field
    
    public DdsWriter(DdsParticipant participant)
    {
        _participant = participant ?? throw new ArgumentNullException(nameof(participant));
        
        // Auto-discover topic metadata
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
            IntPtr descPtr = IntPtr.Zero;
            if (_metadata.TopicDescriptor != null)
            {
                _nativeDescriptor = new NativeDescriptor(_metadata.TopicDescriptor);
                descPtr = _nativeDescriptor.Ptr;
            }

            topic = DdsApi.dds_create_topic(
                participant.Entity,
                descPtr, // descriptor
                _metadata.TopicName,
                IntPtr.Zero, // QoS
                IntPtr.Zero); // listener
            
            if (!topic.IsValid)
            {
                _nativeDescriptor?.Dispose();
                _nativeDescriptor = null;
                throw new DdsException($"Failed to create topic {_metadata.TopicName}", 
                    DdsReturnCode.Error);
            }
            createdTopic = true;
        }
        
        // Create writer
        var writer = DdsApi.dds_create_writer(
            participant.Entity,
            topic,
            IntPtr.Zero, // QoS
            IntPtr.Zero); // listener
        
        if (!writer.IsValid)
        {
            if (createdTopic) DdsApi.dds_delete(topic);
            throw new DdsException($"Failed to create writer for {_metadata.TopicName}", 
                DdsReturnCode.Error);
        }
        
        _writerHandle = new DdsEntityHandle(writer);
    }
    
    public unsafe void Write(ref TNative sample)
    {
        if (_writerHandle == null)
            throw new ObjectDisposedException(nameof(DdsWriter<TNative>));
        
        fixed (TNative* ptr = &sample)
        {
            var result = DdsApi.dds_write(_writerHandle.Entity, new IntPtr(ptr));
            if (result < 0)
                throw new DdsException("Write failed", (DdsReturnCode)result);
        }
    }
    
    public unsafe bool TryWrite(ref TNative sample)
    {
        if (_writerHandle == null)
            return false;
        
        fixed (TNative* ptr = &sample)
        {
            var result = DdsApi.dds_write(_writerHandle.Entity, new IntPtr(ptr));
            return result >= 0;
        }
    }
    
    public bool IsDisposed => _writerHandle == null;

    public void Dispose()
    {
        _writerHandle?.Dispose();
        _writerHandle = null;
        _nativeDescriptor?.Dispose();
        _nativeDescriptor = null;
    }
}
