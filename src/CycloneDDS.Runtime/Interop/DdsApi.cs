using System;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime;

namespace CycloneDDS.Runtime.Interop
{
    public static class DdsApi
    {
        private const string DLL_NAME = "ddsc";

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
            IntPtr sertype = dds_get_topic_sertype(topic);
            if (sertype == IntPtr.Zero) return IntPtr.Zero;

            var iov = new ddsrt_iovec_t
            {
                iov_base = data,
                iov_len = (UIntPtr)size
            };
            
            return ddsi_serdata_from_ser_iov(sertype, 2, 1, new[] { iov }, (UIntPtr)size);
        }

        [DllImport(DLL_NAME)]
        public static extern void dds_free(IntPtr ptr);

        [DllImport(DLL_NAME)]
        public static extern int dds_write(
            int writer, // DdsEntity.Handle
            IntPtr data);

        [DllImport(DLL_NAME)]
        public static extern int dds_take(
            int reader, // Changed to int to match others
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz,
            uint maxs);

        // Return loan
        [DllImport(DLL_NAME)]
        public static extern int dds_return_loan(
            DdsEntity reader,
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

        public const uint DDS_DATA_REPRESENTATION_XCDR1 = 0;
        public const uint DDS_DATA_REPRESENTATION_XCDR2 = 1;

        // Cleanup
        [DllImport(DLL_NAME)]
        public static extern int dds_delete(DdsEntity entity);
    }
}
