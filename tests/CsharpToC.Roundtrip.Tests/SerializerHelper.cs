using System;
using System.Reflection;
using System.Reflection.Emit;
using CycloneDDS.Core;
using CycloneDDS.Runtime;

namespace CsharpToC.Roundtrip.Tests
{
    public static class SerializerHelper
    {
        private delegate void SerializeDelegate<T>(in T sample, ref CdrWriter writer);
        private delegate int GetSerializedSizeDelegate<T>(in T sample, int currentAlignment, CdrEncoding encoding);
        
        public static byte[] Serialize<T>(T sample, byte[] header) where T : struct
        {
             return SerializerCache<T>.Serialize(sample, header);
        }

        private static class SerializerCache<T>
        {
            private static readonly SerializeDelegate<T> _serializer;
            private static readonly GetSerializedSizeDelegate<T> _sizer;
            
            static SerializerCache()
            {
                // Init Serializer
                var serMethod = typeof(T).GetMethod("Serialize", new[] { typeof(CdrWriter).MakeByRefType() });
                if (serMethod == null) throw new MissingMethodException(typeof(T).Name, "Serialize");

                var dmSer = new DynamicMethod(
                    "SerializeThunk_" + typeof(T).Name,
                    typeof(void),
                    new[] { typeof(T).MakeByRefType(), typeof(CdrWriter).MakeByRefType() },
                    typeof(T).Module);

                var ilSer = dmSer.GetILGenerator();
                ilSer.Emit(OpCodes.Ldarg_0); 
                if (!typeof(T).IsValueType) ilSer.Emit(OpCodes.Ldind_Ref);
                ilSer.Emit(OpCodes.Ldarg_1); 
                ilSer.Emit(OpCodes.Call, serMethod);
                ilSer.Emit(OpCodes.Ret);

                _serializer = (SerializeDelegate<T>)dmSer.CreateDelegate(typeof(SerializeDelegate<T>));

                // Init Sizer
                var sizerMethod = typeof(T).GetMethod("GetSerializedSize", new[] { typeof(int), typeof(CdrEncoding) });
                if (sizerMethod == null) throw new MissingMethodException(typeof(T).Name, "GetSerializedSize");

                var dmSize = new DynamicMethod(
                    "GetSerializedSizeThunk_" + typeof(T).Name,
                    typeof(int),
                    new[] { typeof(T).MakeByRefType(), typeof(int), typeof(CdrEncoding) },
                    typeof(T).Module);

                var ilSize = dmSize.GetILGenerator();
                ilSize.Emit(OpCodes.Ldarg_0); 
                if (!typeof(T).IsValueType) ilSize.Emit(OpCodes.Ldind_Ref);
                ilSize.Emit(OpCodes.Ldarg_1); 
                ilSize.Emit(OpCodes.Ldarg_2); 
                ilSize.Emit(OpCodes.Call, sizerMethod); 
                ilSize.Emit(OpCodes.Ret);

                _sizer = (GetSerializedSizeDelegate<T>)dmSize.CreateDelegate(typeof(GetSerializedSizeDelegate<T>));
            }

            public static byte[] Serialize(T sample, ReadOnlySpan<byte> header)
            {
                byte encodingKind = header[1];
                CdrEncoding encoding = CdrEncoding.Xcdr1;
                if (encodingKind >= 6) encoding = CdrEncoding.Xcdr2;

                // FIX: Calculate correct origin
                int origin = encoding == CdrEncoding.Xcdr2 ? 0 : 4; 

                // Calculate size (start at offset 4 to account for header)
                int size = _sizer(in sample, 4 + origin, encoding);
                
                byte[] buffer = new byte[size + 4]; 
                var span = new Span<byte>(buffer);
                
                // FIX: Pass origin to Writer
                var writer = new CdrWriter(span, encoding, origin: origin);
                
                // Write Header
                writer.WriteBytes(header);
                
                _serializer(in sample, ref writer);
                
                return buffer;
            }
        }
    }
}
