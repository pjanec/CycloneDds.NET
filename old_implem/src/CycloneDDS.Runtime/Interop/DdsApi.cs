using System;
using System.Runtime.InteropServices;

namespace CycloneDDS.Runtime.Interop;

/// <summary>
/// P/Invoke declarations for Cyclone DDS C API.
/// </summary>
public static class DdsApi
{
    private const string DdsLib = "ddsc";
    
    // Entity handles
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsEntity
    {
        public IntPtr Handle;
        
        public static readonly DdsEntity Null = new DdsEntity { Handle = IntPtr.Zero };
        public bool IsValid => Handle != IntPtr.Zero;
    }
    
    // Return codes
    public const int DDS_RETCODE_OK = 0;
    public const int DDS_RETCODE_ERROR = -1;
    public const int DDS_RETCODE_TIMEOUT = -2;
    
    // Participant
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_participant(
        uint domain_id,
        IntPtr qos,
        IntPtr listener);
    
    // Topic
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_topic(
        DdsEntity participant,
        IntPtr descriptor,
        [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr qos,
        IntPtr listener);
    
    // Writer
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_writer(
        DdsEntity participant_or_publisher,
        DdsEntity topic,
        IntPtr qos,
        IntPtr listener);
    
    // Reader
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern DdsEntity dds_create_reader(
        DdsEntity participant_or_subscriber,
        DdsEntity topic,
        IntPtr qos,
        IntPtr listener);
    
    // Write
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_write(
        DdsEntity writer,
        IntPtr data);
    
    // Take
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_take(
        DdsEntity reader,
        IntPtr[] samples,
        IntPtr[] info,
        int max_samples,
        uint mask);
    
    // Return loan
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_return_loan(
        DdsEntity reader,
        IntPtr[] samples,
        int count);
    
    // Delete entity
    [DllImport(DdsLib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int dds_delete(DdsEntity entity);
}
