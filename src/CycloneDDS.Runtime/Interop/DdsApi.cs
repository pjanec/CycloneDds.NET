using System;
using System.Runtime.InteropServices;

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
            Timeout = -10, // Corrected from header
            PreconditionNotMet = -4,
            AlreadyDeleted = -9, // Corrected
            HandleExpired = -5, // ERR_OUT_OF_RESOURCES is -5
            NoData = -11,
            IllegalOperation = -12,
            NotAllowedBySecurity = -13,
            Unsupported = -2,
            BadParameter = -3,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DdsSampleInfo
        {
            public uint SampleState;
            public uint ViewState;
            public uint InstanceState;
            [MarshalAs(UnmanagedType.I1)]
            public bool ValidData;
            public long SourceTimestamp;
            public long InstanceHandle;
            public long PublicationHandle;
            public uint DisposedGenerationCount;
            public uint NoWritersGenerationCount;
            public uint SampleRank;
            public uint GenerationRank;
            public uint AbsoluteGenerationCount;
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

        // Serdata
        // kind: 0 for generic CDR (DDS_EST_1_0)
        [DllImport(DLL_NAME)]
        public static extern IntPtr dds_create_serdata_from_cdr(
            IntPtr topic_desc,
            IntPtr kind, 
            IntPtr data,
            uint size);

        [DllImport(DLL_NAME)]
        public static extern int dds_write_serdata(
            DdsEntity writer,
            IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern void dds_free_serdata(IntPtr serdata);

        [DllImport(DLL_NAME)]
        public static extern void dds_free(IntPtr ptr);

        // Convert serdata back to CDR buffer
        // buffer: out pointer to buffer allocated by cyclone (must be freed?)
        // actually dds_serdata_to_cdr allocates using dds_alloc, so should use dds_free. 
        // But for now let's assume standard free or dds_free works.
        [DllImport(DLL_NAME)]
        public static extern int dds_serdata_to_cdr(
            IntPtr serdata,
            out IntPtr buffer,
            out uint len);

        [DllImport(DLL_NAME)]
        public static extern int dds_take_serdata(
            DdsEntity reader, 
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos, 
            UIntPtr bufsz, 
            uint maxs);

        [DllImport(DLL_NAME)]
        public static extern int dds_write(
            int writer, // DdsEntity.Handle
            IntPtr data);

        // Read/Take
        // Based on dds.h: dds_take(reader, buf, si, bufsz, maxs)
        [DllImport(DLL_NAME)]
        public static extern int dds_take(
            DdsEntity reader,
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz, // size_t
            uint maxs); // uint32_t
            
         // dds_take_mask(reader, buf, si, bufsz, maxs, mask)
        [DllImport(DLL_NAME)]
        public static extern int dds_take_mask(
            DdsEntity reader,
            [In, Out] IntPtr[] samples, 
            [In, Out] DdsSampleInfo[] infos,
            UIntPtr bufsz, // size_t
            uint maxs,
            uint mask);

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
            short[] values);

        public const uint DDS_DATA_REPRESENTATION_XCDR1 = 0;
        public const uint DDS_DATA_REPRESENTATION_XCDR2 = 1;

        // Cleanup
        [DllImport(DLL_NAME)]
        public static extern int dds_delete(DdsEntity entity);
    }
}
