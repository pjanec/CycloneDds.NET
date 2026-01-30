using System;
using System.Runtime.InteropServices;

namespace CsharpToC.Symmetry.Native
{
    /// <summary>
    /// P/Invoke wrapper for the native test library (ddsc_test_lib.dll).
    /// Used for generating golden CDR data from the native CycloneDDS implementation.
    /// </summary>
    public static class NativeWrapper
    {
        private const string DllName = "ddsc_test_lib";

        /// <summary>
        /// Generates a CDR payload for the specified topic using the native serializer.
        /// </summary>
        /// <param name="topicName">Fully qualified topic name (e.g., "AtomicTests::CharTopic")</param>
        /// <param name="seed">Seed value for deterministic data generation</param>
        /// <returns>CDR byte array</returns>
        public static byte[] GeneratePayload(string topicName, int seed)
        {
            IntPtr buffer = IntPtr.Zero;
            try
            {
                int length = Native_GeneratePayload(topicName, seed, out buffer);
                
                if (length <= 0)
                {
                    string errorMsg = "Unknown native error";
                    try {
                        IntPtr errPtr = Native_GetLastError();
                        if (errPtr != IntPtr.Zero) errorMsg = Marshal.PtrToStringAnsi(errPtr);
                    } catch {}
                    
                    throw new InvalidOperationException(
                        $"Native payload generation failed for topic '{topicName}' with seed {seed}. " +
                        $"Returned length: {length}. Error: {errorMsg}");
                }

                byte[] result = new byte[length];
                Marshal.Copy(buffer, result, 0, length);
                return result;
            }
            catch (DllNotFoundException ex)
            {
                throw new InvalidOperationException(
                    $"Native library '{DllName}.dll' not found. " +
                    $"Please ensure the DLL is in the output directory or system PATH.", ex);
            }
            finally
            {
                // Free the native buffer if allocation was successful
                if (buffer != IntPtr.Zero)
                {
                    Native_FreeBuffer(buffer);
                }
            }
        }

        #region P/Invoke Declarations

        /// <summary>
        /// Native function to generate a CDR payload for a test topic.
        /// </summary>
        /// <param name="topicName">Topic name (C string)</param>
        /// <param name="seed">Seed value for data generation</param>
        /// <param name="buffer">Output buffer pointer (allocated by native code)</param>
        /// <returns>Length of the buffer, or negative on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int Native_GeneratePayload(
            string topicName,
            int seed,
            out IntPtr buffer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Native_FreeBuffer(IntPtr buffer);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Native_GetLastError();

        #endregion
    }
}
