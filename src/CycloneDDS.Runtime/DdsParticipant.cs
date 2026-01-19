using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public sealed class DdsParticipant : IDisposable
    {
        private DdsEntityHandle? _handle;
        private readonly uint _domainId;
        private bool _disposed;
        
        private readonly Dictionary<string, DdsApi.DdsEntity> _topicCache = new();
        private readonly object _topicLock = new();
        // Track unmanaged resources for topics so we can free them on Dispose
        private readonly List<IDisposable> _topicResources = new();

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
                lock (_topicLock)
                {
                    // Delete all cached topics
                    foreach (var topic in _topicCache.Values)
                    {
                        DdsApi.dds_delete(topic);
                    }
                    _topicCache.Clear();

                    // Free unmanaged resources
                    foreach (var resource in _topicResources)
                    {
                        resource.Dispose();
                    }
                    _topicResources.Clear();
                }

                _handle?.Dispose();
                _handle = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// Get or register a topic for type T.
        /// Thread-safe. Returns cached topic if already created for this name.
        /// </summary>
        internal DdsApi.DdsEntity GetOrRegisterTopic<T>(string topicName, IntPtr qos = default)
        {
            lock (_topicLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DdsParticipant));

                // Check cache first
                if (_topicCache.TryGetValue(topicName, out var existing))
                {
                    return existing;
                }
                
                // 1. Get descriptor ops from static method (via reflection)
                uint[] ops = DdsTypeSupport.GetDescriptorOps<T>();
                
                // 2. Marshal descriptor to native
                IntPtr descriptorPtr = MarshalDescriptor<T>(ops, DdsTypeSupport.GetTypeName<T>());
                
                // 3. Create native topic
                DdsApi.DdsEntity topic = DdsApi.dds_create_topic(
                    NativeEntity,
                    descriptorPtr,
                    topicName,
                    qos,
                    IntPtr.Zero);
                
                if (!topic.IsValid)
                {
                    throw new DdsException(DdsApi.DdsReturnCode.Error, 
                        $"Failed to create topic '{topicName}' for type '{DdsTypeSupport.GetTypeName<T>()}'");
                }
                
                // 4. Cache and return
                _topicCache[topicName] = topic;
                return topic;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DdsTopicDescriptor
        {
            public uint m_size;
            public uint m_align;
            public uint m_flagset;
            public uint m_nkeys;
            public IntPtr m_typename; // char*
            public IntPtr m_keys;     // dds_key_descriptor_t*
            public uint m_nops;
            public IntPtr m_ops;      // uint32_t*
            public IntPtr m_meta;     // char*
            public DdsTypeMetaSer type_information;
            public DdsTypeMetaSer type_mapping;
            public uint restrict_data_representation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DdsTypeMetaSer
        {
            public IntPtr data;
            public uint sz;
        }

        private class TopicResource : IDisposable
        {
            private IntPtr _descPtr;
            private IntPtr _typeNamePtr;
            private GCHandle _opsHandle;

            public TopicResource(IntPtr descPtr, IntPtr typeNamePtr, GCHandle opsHandle)
            {
                _descPtr = descPtr;
                _typeNamePtr = typeNamePtr;
                _opsHandle = opsHandle;
            }

            public void Dispose()
            {
                if (_descPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_descPtr);
                    _descPtr = IntPtr.Zero;
                }
                if (_typeNamePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_typeNamePtr);
                    _typeNamePtr = IntPtr.Zero;
                }
                if (_opsHandle.IsAllocated)
                {
                    _opsHandle.Free();
                }
            }
        }

        private IntPtr MarshalDescriptor<T>(uint[] ops, string typeName)
        {
            // Marshal type name
            IntPtr typeNamePtr = Marshal.StringToHGlobalAnsi(typeName);
            
            // Pin ops array
            GCHandle opsHandle = GCHandle.Alloc(ops, GCHandleType.Pinned);
            
            // Create descriptor struct
            var desc = new DdsTopicDescriptor
            {
                m_size = (uint)Marshal.SizeOf<T>(), 
                m_align = 4, 
                m_flagset = 0, 
                m_nkeys = 0,
                m_typename = typeNamePtr,
                m_keys = IntPtr.Zero,
                m_nops = (uint)ops.Length,
                m_ops = opsHandle.AddrOfPinnedObject(),
                m_meta = IntPtr.Zero,
                type_information = new DdsTypeMetaSer { data = IntPtr.Zero, sz = 0 },
                type_mapping = new DdsTypeMetaSer { data = IntPtr.Zero, sz = 0 },
                restrict_data_representation = 0
            };

            // Alloc descriptor memory
            IntPtr descPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DdsTopicDescriptor>());
            Marshal.StructureToPtr(desc, descPtr, false);

            // Track resources for cleanup
            _topicResources.Add(new TopicResource(descPtr, typeNamePtr, opsHandle));

            return descPtr;
        }
    }
}
