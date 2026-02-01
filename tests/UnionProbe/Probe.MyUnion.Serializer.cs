// Manually fixed
#pragma warning disable CS0162, CS0219, CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8618, CS8625
using System;
using CycloneDDS.Core;
using CycloneDDS.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace Probe
{
    public partial struct MyUnion
    {
        public static int GetNativeSize(in MyUnion source)
        {
            return Unsafe.SizeOf<MyUnion_Native>();
        }
        public static int GetNativeHeadSize() 
        {
             return Unsafe.SizeOf<MyUnion_Native>();
        }

        public static unsafe void MarshalToNative(in MyUnion source, IntPtr targetPtr, ref NativeArena arena)
        {
            ref var target = ref Unsafe.AsRef<MyUnion_Native>((void*)targetPtr);
            target = default;
            MarshalToNative(source, ref target, ref arena);
        }

        internal static unsafe void MarshalToNative(in MyUnion source, ref MyUnion_Native target, ref NativeArena arena)
        {
            Console.WriteLine("DEBUG: MyUnion Helper MarshalToNative Called");
            target._d = source._d ? (byte)1 : (byte)0;
            if (source._d)
                target._u.Trueval = source.Vala;
            else
                target._u.Falseval = source.Valb;
        }

        internal static unsafe void MarshalFromNative(IntPtr nativeData, out MyUnion managedData)
        {
            managedData = default;
            MarshalFromNative(ref managedData, in System.Runtime.CompilerServices.Unsafe.AsRef<MyUnion_Native>((void*)nativeData));
        }

        internal static unsafe void MarshalFromNative(ref MyUnion target, in MyUnion_Native source)
        {
            target._d = source._d != 0;
            if (target._d)
                target.Vala = source._u.Trueval;
            else
                target.Valb = source._u.Falseval;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MyUnion_Native
    {
        public System.Byte _d;
        public MyUnion_Union_Native _u;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct MyUnion_Union_Native
    {
        [FieldOffset(0)]
        public System.Byte Trueval;
        [FieldOffset(0)]
        public System.Byte Falseval;
    }
}
