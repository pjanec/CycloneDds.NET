using System;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Interop
{
    public static class DdsApi
    {
        public const string DLL_NAME = "ddsc";

        // Basic types
        [StructLayout(LayoutKind.Sequential)]
        public struct DdsEntity
        {
            public int Handle;
            public bool IsValid => Handle > 0;
            
            public static readonly DdsEntity Null = new DdsEntity { Handle = 0 };
            
            public override string ToString() => $"DdsEntity(0x{Handle:x})";
        }

        public enum DdsReturnCode : int
        {
            Ok = 0,
            Error = -1,
            Timeout = -10,
            PreconditionNotMet = -4,
            AlreadyDeleted = -9,
            HandleExpired = -5,
            NoData = -11,
            IllegalOperation = -12,
            NotAllowedBySecurity = -13,
            Unsupported = -2,
            BadParameter = -3,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsSampleInfo
        {
            public DdsSampleState SampleState;
            public DdsViewState ViewState;
            public DdsInstanceState InstanceState;
            public byte ValidData; 
            private byte _pad1;
            private byte _pad2;
            private byte _pad3;
            public long SourceTimestamp;
            public long InstanceHandle;
            public long PublicationHandle;
            public uint DisposedGenerationCount;
            public uint NoWritersGenerationCount;
            public uint SampleRank;
            public uint GenerationRank;
            public uint AbsoluteGenerationCount;
            private uint _pad4;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ddsrt_iovec_t
        {
            public UIntPtr iov_len; // Was uint, must be size_t (UIntPtr)
            public IntPtr iov_base;
        }

        // Listener Delegate
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnDataAvailable(int reader, IntPtr arg);
        
        // Listener
        [DllImport(DLL_NAME)]
        public static extern IntPtr dds_create_listener(IntPtr arg);

        [DllImport(DLL_NAME)]
        public static extern void dds_delete_listener(IntPtr listener);

        [DllImport(DLL_NAME)]
        public static extern void dds_lset_data_available(IntPtr listener, DdsOnDataAvailable callback);

        [DllImport(DLL_NAME, EntryPoint = "dds_set_listener")]
        public static extern int dds_reader_set_listener(DdsEntity reader, IntPtr listener);

        [DllImport(DLL_NAME, EntryPoint = "dds_set_listener")]
        public static extern int dds_writer_set_listener(DdsEntity writer, IntPtr listener);
        
        // Participant
        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_participant(
            uint domain_id,
            IntPtr qos,
            IntPtr listener);

        // Topic
        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_topic(
            DdsEntity participant,
            IntPtr desc,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            IntPtr qos,
            IntPtr listener);

        // Writer
        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_writer(
            DdsEntity participant_or_publisher,
            DdsEntity topic,
            IntPtr qos,
            IntPtr listener);
        
        // Reader
        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_reader(
            DdsEntity participant_or_subscriber,
            DdsEntity topic,
            IntPtr qos,
            IntPtr listener);

        // Serdata APIs
        [DllImport(DLL_NAME)]
        public static extern IntPtr dds_get_topic_sertype(DdsEntity topic);

        [DllImport(DLL_NAME, EntryPoint = "dds_serdata_from_ser_iov")]
        public static extern IntPtr ddsi_serdata_from_ser_iov(
            IntPtr sertype,
            int kind, // 2 = SDK_DATA
            uint niov,
            [In] ddsrt_iovec_t[] iov,
            UIntPtr size);

        [DllImport(DLL_NAME)]
        public static extern int dds_writecdr(
            DdsEntity writer,
            IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern int dds_dispose_serdata(
            DdsEntity writer,
            IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern int dds_unregister_serdata(
            DdsEntity writer,
            IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern int dds_write(
            DdsEntity writer,
            IntPtr data);

        [DllImport(DLL_NAME)]
        public static extern int dds_dispose(
            DdsEntity writer,
            IntPtr data);

        [DllImport(DLL_NAME)]
        public static extern int dds_unregister_instance(
            DdsEntity writer,
            IntPtr data);


        [DllImport(DLL_NAME)]
        public static extern int dds_readcdr(
            int reader, // Changed from DdsEntity to int
            [In, Out] IntPtr[] samples, 
            uint maxs,
            [In, Out] DdsSampleInfo[] infos, 
            uint mask);

        [DllImport(DLL_NAME)]
        public static extern int dds_takecdr(
            int reader, // Changed from DdsEntity to int
            [In, Out] IntPtr[] samples, 
            uint maxs,
            [In, Out] DdsSampleInfo[] infos, 
            uint mask);

        [DllImport(DLL_NAME)]
        public static extern long dds_lookup_instance_serdata(int entity, IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern long dds_lookup_instance(int reader, IntPtr key);

        [DllImport(DLL_NAME)]
        public static extern int dds_takecdr_instance(
            int reader,
            [In, Out] IntPtr[] samples, 
            uint maxs,
            [In, Out] DdsSampleInfo[] infos, 
            long handle,
            uint mask);

        [DllImport(DLL_NAME)]
        public static extern int dds_readcdr_instance(
            int reader,
            [In, Out] IntPtr[] samples, 
            uint maxs,
            [In, Out] DdsSampleInfo[] infos, 
            long handle,
            uint mask);

        [DllImport(DLL_NAME, EntryPoint = "dds_takecdr")]
        public static extern int dds_takecdr_raw(
            int reader, 
            IntPtr samples, 
            uint maxs,
            IntPtr infos, 
            uint mask);

        [DllImport(DLL_NAME, EntryPoint = "dds_takecdr")]
        public static extern unsafe int dds_takecdr_ptr(
            int reader, // Changed from DdsEntity to int
            IntPtr* samples, 
            uint maxs,
            DdsSampleInfo* infos, 
            uint mask);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int DdsReadWithCollectorDelegate(
            IntPtr arg,
            IntPtr sampleInfo, // const dds_sample_info_t *
            IntPtr sertype,    // const struct ddsi_sertype *
            IntPtr serdata);   // struct ddsi_serdata *

        [DllImport(DLL_NAME)]
        public static extern int dds_take_with_collector(
            int reader,
            uint maxs,
            long handle, // dds_instance_handle_t
            uint mask,
            DdsReadWithCollectorDelegate collect_sample,
            IntPtr collect_sample_arg);

        [DllImport(DLL_NAME, EntryPoint = "dds_sample_info_size")]
        public static extern uint dds_sample_info_size();

        [DllImport(DLL_NAME, EntryPoint = "dds_serdata_ref")]
        public static extern IntPtr ddsi_serdata_ref(IntPtr serdata);

        [DllImport(DLL_NAME, EntryPoint = "dds_serdata_unref")]
        public static extern void ddsi_serdata_unref(IntPtr serdata);

        [DllImport(DLL_NAME, EntryPoint = "dds_serdata_size")]
        public static extern uint ddsi_serdata_size(IntPtr serdata);

        [DllImport(DLL_NAME, EntryPoint = "dds_serdata_to_ser")]
        public static extern void ddsi_serdata_to_ser(IntPtr serdata, UIntPtr off, UIntPtr sz, IntPtr buf);

        // Opaque struct for type safety in unsafe code
        public struct struct_ddsi_serdata { }

        // Helper to match user expectation
        public static IntPtr dds_create_serdata_from_cdr(DdsEntity topic, IntPtr data, uint size)
        {
            return dds_create_serdata_from_cdr(topic, data, size, 2); // Default to SDK_DATA
        }

        public static IntPtr dds_create_serdata_from_cdr(DdsEntity topic, IntPtr data, uint size, int kind)
        {
            IntPtr sertype = dds_get_topic_sertype(topic);
            if (sertype == IntPtr.Zero) return IntPtr.Zero;

            var iov = new ddsrt_iovec_t
            {
                iov_base = data,
                iov_len = (UIntPtr)size
            };
            
            return ddsi_serdata_from_ser_iov(sertype, kind, 1, new[] { iov }, (UIntPtr)size);
        }

        [DllImport(DLL_NAME)]
        public static extern void dds_free(IntPtr ptr);

        [DllImport(DLL_NAME)]
        public static extern int dds_write(
            int writer, // DdsEntity.Handle
            IntPtr data);

        [DllImport(DLL_NAME)]
        public static extern int dds_read(
            int reader,
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs);

        [DllImport(DLL_NAME)]
        public static extern int dds_take(
            int reader, // Changed to int to match others
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs);
            
        [DllImport(DLL_NAME)]
        public static extern int dds_read_mask(
            int reader,
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs,
            uint mask);

        // dds_lookup_instance already defined above

        [DllImport(DLL_NAME)]
        public static extern int dds_read_instance(
            int reader,
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs,
            long handle);

        [DllImport(DLL_NAME)]
        public static extern int dds_take_instance(
            int reader, 
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs,
            long handle);

        [DllImport(DLL_NAME)]
        public static extern int dds_take_mask(
            int reader, 
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs,
            uint mask);

        // Return loan
        [DllImport(DLL_NAME)]
        public static extern int dds_return_loan(
            int reader,
            [In, Out] IntPtr[] samples,
            int count);

        // QoS Management
        [DllImport(DLL_NAME)]
        public static extern IntPtr dds_create_qos();

        [DllImport(DLL_NAME)]
        public static extern void dds_delete_qos(IntPtr qos);

        // Data Representation QoS
        [DllImport(DLL_NAME)]
        public static extern void dds_qset_data_representation(
            IntPtr qos,
            uint n,
            [In] short[] values);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_history(IntPtr qos, int kind, int depth);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_resource_limits(IntPtr qos, int max_samples, int max_instances, int max_samples_per_instance);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_durability(IntPtr qos, int kind);

        public const int DDS_DURABILITY_VOLATILE = 0;
        public const int DDS_DURABILITY_TRANSIENT_LOCAL = 1;

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_reliability(IntPtr qos, int kind, long max_blocking_time);
        
        public const int DDS_RELIABILITY_BEST_EFFORT = 0;
        public const int DDS_RELIABILITY_RELIABLE = 1;

        // Writer Data Lifecycle QoS: when autodispose is true (the DDS default), unregistering
        // an instance also disposes it.
        [DllImport(DLL_NAME)]
        public static extern void dds_qset_writer_data_lifecycle(IntPtr qos, [MarshalAs(UnmanagedType.I1)] bool autodispose);

        // Blocks until all matched reliable readers acknowledged everything written so far.
        [DllImport(DLL_NAME)]
        public static extern int dds_wait_for_acks(int writer, long timeout);

        // Ownership / Liveliness / Deadline QoS
        [DllImport(DLL_NAME)]
        public static extern void dds_qset_ownership(IntPtr qos, int kind);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_ownership_strength(IntPtr qos, int value);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_liveliness(IntPtr qos, int kind, long lease_duration);

        [DllImport(DLL_NAME)]
        public static extern void dds_qset_deadline(IntPtr qos, long deadline);

        public const int DDS_OWNERSHIP_SHARED = 0;
        public const int DDS_OWNERSHIP_EXCLUSIVE = 1;

        public const int DDS_LIVELINESS_AUTOMATIC = 0;
        public const int DDS_LIVELINESS_MANUAL_BY_PARTICIPANT = 1;
        public const int DDS_LIVELINESS_MANUAL_BY_TOPIC = 2;

        // Asserts liveliness of a writer (or of a participant, for MANUAL_BY_PARTICIPANT).
        [DllImport(DLL_NAME)]
        public static extern int dds_assert_liveliness(int entity);

        // Partition QoS
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dds_qset_partition(
            IntPtr qos,
            uint n,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)]
            string[] parts);

        public const int DDS_HISTORY_KEEP_LAST = 0;
        public const int DDS_HISTORY_KEEP_ALL = 1;

        // Status Structs
        [StructLayout(LayoutKind.Sequential)]
        public struct DdsPublicationMatchedStatus
        {
            public uint TotalCount;
            public int TotalCountChange;
            public uint CurrentCount;
            public int CurrentCountChange;
            public long LastSubscriptionHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsSubscriptionMatchedStatus
        {
            public uint TotalCount;
            public int TotalCountChange;
            public uint CurrentCount;
            public int CurrentCountChange;
            public long LastPublicationHandle;
        }

        // NOTE: status is passed BY VALUE, matching the C signature
        //   void (*)(dds_entity_t, const dds_..._matched_status_t status, void *arg)
        // These 24-byte structs are >16 bytes, so the Windows x64 ABI passes them by an
        // implicit pointer (which a `ref` parameter happened to match) while the Linux
        // System V ABI passes them by value on the stack. Declaring the parameter `ref`
        // therefore worked only on Windows; on Linux it shifted `arg` into the wrong
        // register, so GCHandle.FromIntPtr(arg) threw and the callback was silently
        // dropped. Declaring it by value lets the .NET marshaller emit the correct
        // per-platform ABI on both.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnPublicationMatched(int writer, DdsPublicationMatchedStatus status, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnSubscriptionMatched(int reader, DdsSubscriptionMatchedStatus status, IntPtr arg);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_publication_matched(IntPtr listener, DdsOnPublicationMatched callback);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_subscription_matched(IntPtr listener, DdsOnSubscriptionMatched callback);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_publication_matched_status(int writer, out DdsPublicationMatchedStatus status);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_subscription_matched_status(int reader, out DdsSubscriptionMatchedStatus status);

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsLivelinessChangedStatus
        {
            public uint AliveCount;
            public uint NotAliveCount;
            public int AliveCountChange;
            public int NotAliveCountChange;
            public long LastPublicationHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsLivelinessLostStatus
        {
            public uint TotalCount;
            public int TotalCountChange;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsRequestedDeadlineMissedStatus
        {
            public uint TotalCount;
            public int TotalCountChange;
            public long LastInstanceHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsOfferedDeadlineMissedStatus
        {
            public uint TotalCount;
            public int TotalCountChange;
            public long LastInstanceHandle;
        }

        // Status passed BY VALUE, as for the matched-status callbacks above.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnLivelinessChanged(int reader, DdsLivelinessChangedStatus status, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnLivelinessLost(int writer, DdsLivelinessLostStatus status, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnRequestedDeadlineMissed(int reader, DdsRequestedDeadlineMissedStatus status, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DdsOnOfferedDeadlineMissed(int writer, DdsOfferedDeadlineMissedStatus status, IntPtr arg);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_liveliness_changed(IntPtr listener, DdsOnLivelinessChanged callback);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_liveliness_lost(IntPtr listener, DdsOnLivelinessLost callback);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_requested_deadline_missed(IntPtr listener, DdsOnRequestedDeadlineMissed callback);

        [DllImport(DLL_NAME)]
        public extern static void dds_lset_offered_deadline_missed(IntPtr listener, DdsOnOfferedDeadlineMissed callback);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_liveliness_changed_status(int reader, out DdsLivelinessChangedStatus status);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_liveliness_lost_status(int writer, out DdsLivelinessLostStatus status);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_requested_deadline_missed_status(int reader, out DdsRequestedDeadlineMissedStatus status);

        [DllImport(DLL_NAME)]
        public extern static int dds_get_offered_deadline_missed_status(int writer, out DdsOfferedDeadlineMissedStatus status);
        
        [DllImport(DLL_NAME)]
        public extern static int dds_get_status_changes(int entity, out uint status);

        /// <summary>
        /// Get the GUID of a DDS entity (participant, reader, writer).
        /// </summary>
        /// <param name="entity">Entity handle</param>
        /// <param name="guid">Output GUID</param>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DLL_NAME)]
        public static extern int dds_get_guid(int entity, out DdsGuid guid);

        /// <summary>
        /// Get list of publication handles currently matched to a reader.
        /// </summary>
        /// <param name="reader">Reader entity</param>
        /// <param name="publication_handles">Output array of handles</param>
        /// <param name="max_handles">Size of array</param>
        /// <returns>Number of handles returned, or negative error code</returns>
        [DllImport(DLL_NAME)]
        public static extern int dds_get_matched_publications(
            int reader,
            [In, Out] long[] publication_handles,
            uint max_handles);

        /// <summary>
        /// Native struct for dds_get_matched_publication_data.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DdsBuiltinTopicEndpoint
        {
            public DdsGuid Key;
            public DdsGuid ParticipantKey;
            public long ParticipantInstanceHandle;
            public IntPtr TopicName;
            public IntPtr TypeName;
            public IntPtr Qos;
        }

        /// <summary>
        /// Get detailed information about a matched publication.
        /// Caller must free returned pointer using dds_builtintopic_free_endpoint.
        /// </summary>
        /// <param name="reader">Reader entity</param>
        /// <param name="publication_handle">Handle of matched publication</param>
        /// <returns>Pointer to DdsBuiltinTopicEndpoint, or IntPtr.Zero on failure</returns>
        [DllImport(DLL_NAME)]
        public static extern IntPtr dds_get_matched_publication_data(
            int reader,
            long publication_handle); // Takes 2 args only!

        [DllImport(DLL_NAME)]
        public static extern void dds_builtintopic_free_endpoint(IntPtr endpoint);

        public const uint DDS_DATA_AVAILABLE_STATUS = (1u << 8);
        public const uint DDS_PUBLICATION_MATCHED_STATUS = (1u << 11);
        public const uint DDS_SUBSCRIPTION_MATCHED_STATUS = (1u << 12);
        
        // WaitSet & Conditions
        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_waitset(DdsEntity participant);

        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_readcondition(DdsEntity reader, uint mask);

        [DllImport(DLL_NAME)]
        public static extern DdsEntity dds_create_guardcondition(DdsEntity participant);

        // dds_set_guardcondition(entity, triggered=true) is the correct API in this build
        [DllImport(DLL_NAME)]
        public static extern int dds_set_guardcondition(DdsEntity guardcond, [MarshalAs(UnmanagedType.I1)] bool triggered);

        [DllImport(DLL_NAME)]
        public static extern int dds_waitset_attach(DdsEntity waitset, DdsEntity entity, IntPtr attach_arg);

        [DllImport(DLL_NAME)]
        public static extern int dds_waitset_detach(DdsEntity waitset, DdsEntity entity);

        [DllImport(DLL_NAME)]
        public static extern int dds_waitset_wait(
            DdsEntity waitset,
            [Out] IntPtr[] active_entities,
            UIntPtr n_entities,
            long reltimeout);

        public const long DDS_INFINITY = 0x7FFFFFFFFFFFFFFF;

        // Cleanup
        [DllImport(DLL_NAME)]
        public static extern int dds_delete(DdsEntity entity);
    }

    /// <summary>
    /// Represents a 16-byte DDS GUID (Globally Unique Identifier).
    /// Maps to native dds_guid_t (uint8_t v[16]).
    /// Used for O(1) participant/writer correlation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [CycloneDDS.Schema.DdsStruct]
    public partial struct DdsGuid : IEquatable<DdsGuid>
    {
        /// <summary>
        /// High 64 bits (first 8 bytes of v[16]).
        /// </summary>
        public long High;

        /// <summary>
        /// Low 64 bits (last 8 bytes of v[16]).
        /// </summary>
        public long Low;

        public bool Equals(DdsGuid other) => High == other.High && Low == other.Low;

        // Helper to convert Native DdsGuid -> System.Guid
        public unsafe System.Guid ToManaged()
        {
            fixed (void* ptr = &this)
            {
                return new System.Guid(new ReadOnlySpan<byte>(ptr, 16));
            }
        }

        // Helper to write System.Guid -> Native DdsGuid
        public static DdsGuid FromManaged(System.Guid guid)
        {
            DdsGuid native = default;
            unsafe
            {
                guid.TryWriteBytes(new Span<byte>(&native, 16));
            }
            return native;
        }

        public static unsafe implicit operator System.Guid(DdsGuid ddsGuid)
        {
            return ddsGuid.ToManaged();
        }
        public override bool Equals(object? obj) => obj is DdsGuid other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(High, Low);
        public override string ToString() => $"{High:X16}{Low:X16}";

        public static bool operator ==(DdsGuid left, DdsGuid right) => left.Equals(right);
        public static bool operator !=(DdsGuid left, DdsGuid right) => !left.Equals(right);
    }
}
