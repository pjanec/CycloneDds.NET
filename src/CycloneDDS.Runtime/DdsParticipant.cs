using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Interop;
using CycloneDDS.Runtime.Tracking;

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

        private SenderIdentityConfig? _identityConfig;
        private DdsWriter<SenderIdentity>? _identityWriter;
        private int _activeWriterCount = 0;
        private readonly object _trackingLock = new();
        internal SenderRegistry? _senderRegistry;

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

                _senderRegistry?.Dispose();
                _identityWriter?.Dispose();

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
                DdsKeyDescriptor[] keys = DdsTypeSupport.GetKeyDescriptors<T>();
                
                // 2. Marshal descriptor to native
                IntPtr descriptorPtr = MarshalDescriptor<T>(ops, keys, DdsTypeSupport.GetTypeName<T>());
                
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
            private IntPtr _keysPtr;
            private IntPtr[] _keyNamePtrs;

            public TopicResource(IntPtr descPtr, IntPtr typeNamePtr, GCHandle opsHandle, IntPtr keysPtr, IntPtr[] keyNamePtrs)
            {
                _descPtr = descPtr;
                _typeNamePtr = typeNamePtr;
                _opsHandle = opsHandle;
                _keysPtr = keysPtr;
                _keyNamePtrs = keyNamePtrs;
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
                if (_keysPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_keysPtr);
                    _keysPtr = IntPtr.Zero;
                }
                if (_keyNamePtrs != null)
                {
                    foreach (var ptr in _keyNamePtrs)
                    {
                        if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr);
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DdsKeyDescriptorNative
        {
            public IntPtr Name;
            public uint Offset;
            public uint Index;
        }

        private static int GetRecursiveOffset(Type type, string keyPath)
        {
            try
            {
                string[] parts = keyPath.Split('.');
                int totalOffset = 0;
                Type currentType = type;

                foreach (var part in parts)
                {
                    // Find the field in the current type
                    var field = currentType.GetField(part, 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.IgnoreCase);

                    if (field == null)
                    {
                         // Try backing field for property? <Name>k__BackingField
                         field = currentType.GetField($"<{part}>k__BackingField", 
                            System.Reflection.BindingFlags.Instance | 
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.IgnoreCase);
                    }

                    if (field == null)
                    {
                        throw new InvalidOperationException($"Could not find field '{part}' in type '{currentType.Name}' while resolving key '{keyPath}'");
                    }

                    // Add the offset of this field within its parent
                    // Note: Marshal.OffsetOf requires exact case match of the field definition
                    totalOffset += Marshal.OffsetOf(currentType, field.Name).ToInt32();
                    
                    // Drill down
                    currentType = field.FieldType;
                }

                return totalOffset;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error calculating recursive offset for {keyPath} in {type.Name}: {ex}");
                throw;
            }
        }

        private static uint GetAlignment(Type type)
        {
            if (type.StructLayoutAttribute != null && type.StructLayoutAttribute.Pack != 0)
                return (uint)type.StructLayoutAttribute.Pack;
            
            return (uint)IntPtr.Size; // Default to machine word size (8 on x64)
        }

        private IntPtr MarshalDescriptor<T>(uint[] ops, DdsKeyDescriptor[] keys, string typeName)
        {
            // Marshal type name
            IntPtr typeNamePtr = Marshal.StringToHGlobalAnsi(typeName);
            
            // Pin ops array
            GCHandle opsHandle = GCHandle.Alloc(ops, GCHandleType.Pinned);
            
            // Handle keys
            IntPtr keysPtr = IntPtr.Zero;
            uint nkeys = 0;
            IntPtr[] keyNamePtrs = null;

            // if (false) // Diagnostic: Disable keys to check for crash
            if (keys != null && keys.Length > 0)
            {
                 int nativeKeySize = Marshal.SizeOf<DdsKeyDescriptorNative>();
                 keysPtr = Marshal.AllocHGlobal(nativeKeySize * keys.Length);
                 
                 keyNamePtrs = new IntPtr[keys.Length];

                 for(int i=0; i<keys.Length; i++)
                 {
                     var nativeKey = new DdsKeyDescriptorNative();
                     nativeKey.Name = Marshal.StringToHGlobalAnsi(keys[i].Name);
                     keyNamePtrs[i] = nativeKey.Name;
                     nativeKey.Index = keys[i].Index;

                     if (keys[i].Offset == 0)
                     {
                         // Use recursive/smart offset calculation for all keys (handles dot notation and case mismatch)
                         nativeKey.Offset = (uint)GetRecursiveOffset(typeof(T), keys[i].Name);
                     }
                     else
                     {
                         nativeKey.Offset = keys[i].Offset;
                     }
                     
                     IntPtr itemPtr = IntPtr.Add(keysPtr, i * nativeKeySize);
                     Marshal.StructureToPtr(nativeKey, itemPtr, false);
                 }
                 
                 nkeys = (uint)keys.Length;
            }
            
            // Create descriptor struct
            var desc = new DdsTopicDescriptor
            {
                m_size = (uint)Marshal.SizeOf<T>(), 
                m_align = GetAlignment(typeof(T)), 
                m_flagset = 0, 
                m_nkeys = nkeys,
                m_typename = typeNamePtr,
                m_keys = keysPtr,
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
            _topicResources.Add(new TopicResource(descPtr, typeNamePtr, opsHandle, keysPtr, keyNamePtrs));

            return descPtr;
        }

        /// <summary>
        /// Enable sender tracking for this participant.
        /// MUST be called before creating any DdsWriter or DdsReader.
        /// </summary>
        /// <param name="config">Configuration with AppDomainId, AppInstanceId</param>
        /// <exception cref="InvalidOperationException">If writers already created</exception>
        public void EnableSenderTracking(SenderIdentityConfig config)
        {
            lock (_trackingLock)
            {
                if (_activeWriterCount > 0)
                    throw new InvalidOperationException("EnableSenderTracking must be called before creating writers");

                _identityConfig = config;
                _senderRegistry = new SenderRegistry(this);
            }
        }

        /// <summary>
        /// Provides access to the sender registry (if tracking enabled).
        /// </summary>
        public SenderRegistry? SenderRegistry => _senderRegistry;

        internal void RegisterWriter()
        {
            lock (_trackingLock)
            {
                _activeWriterCount++;
                if (_identityConfig != null && _activeWriterCount == 1)
                {
                    PublishIdentity();
                }
            }
        }

        internal void UnregisterWriter()
        {
            lock (_trackingLock)
            {
                _activeWriterCount--;
                if (_identityConfig != null && _activeWriterCount == 0 && !_identityConfig.KeepAliveUntilParticipantDispose)
                {
                    DisposeIdentityWriter();
                }
            }
        }

        private void PublishIdentity()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();

            // Get native participant GUID
            DdsApi.dds_get_guid(NativeEntity.Handle, out var myGuid);

            var identity = new SenderIdentity
            {
                ParticipantGuid = myGuid,
                AppDomainId = _identityConfig!.AppDomainId,
                AppInstanceId = _identityConfig.AppInstanceId,
                ProcessId = process.Id,
                ProcessName = _identityConfig.ProcessName ?? process.ProcessName,
                ComputerName = _identityConfig.ComputerName ?? Environment.MachineName
            };

            // QoS: Reliable + TransientLocal
            IntPtr qos = DdsApi.dds_create_qos();
            DdsApi.dds_qset_durability(qos, DdsApi.DDS_DURABILITY_TRANSIENT_LOCAL);
            DdsApi.dds_qset_reliability(qos, DdsApi.DDS_RELIABILITY_RELIABLE, 100_000_000);

            _identityWriter = new DdsWriter<SenderIdentity>(this, "__FcdcSenderIdentity", qos);
            DdsApi.dds_delete_qos(qos);

            _identityWriter.Write(identity);
        }

        private void DisposeIdentityWriter()
        {
            _identityWriter?.Dispose();
            _identityWriter = null;
        }
    }
}
