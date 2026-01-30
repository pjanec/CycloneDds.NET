using System;
using System.Runtime.InteropServices;

namespace CsharpToC.Roundtrip.Tests
{
    internal static class NativeMethods
    {
        private const string DllName = "ddsc_test_lib";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Native_Init(uint domain_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Native_Cleanup();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Native_SendWithSeed([MarshalAs(UnmanagedType.LPStr)] string handler_name, int seed);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Native_ExpectWithSeed([MarshalAs(UnmanagedType.LPStr)] string handler_name, int seed, int timeout_ms);
        
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Native_GetLastError();
    }
}
