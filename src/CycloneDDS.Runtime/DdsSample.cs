using System;
using CycloneDDS.Runtime;
using CycloneDDS.Runtime.Interop;

namespace CycloneDDS.Runtime
{
    public readonly ref struct DdsSample<T>
    {
        public readonly IntPtr NativePtr;
        public readonly DdsApi.DdsSampleInfo Info;

        public DdsSample(IntPtr nativePtr, ref readonly DdsApi.DdsSampleInfo info)
        {
            NativePtr = nativePtr;
            Info = info;
        }

        public bool IsValid => Info.ValidData != 0;

        public T Data
        {
            get
            {
                // Always unmarshal the native memory into a managed object.
                // DDS provides key fields even for metadata-only samples (ValidData == 0),
                // so callers must be able to read key values for lifecycle events.
                return DdsTypeSupport.FromNative<T>(NativePtr);
            }
        }
    }
}
