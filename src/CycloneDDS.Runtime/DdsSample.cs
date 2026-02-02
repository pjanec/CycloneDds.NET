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
                if (!IsValid)
                {
                    throw new InvalidOperationException("Cannot access Data on an invalid sample (ValidData is false).");
                }
                return DdsTypeSupport.FromNative<T>(NativePtr);
            }
        }
    }
}
