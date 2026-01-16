using System;

namespace CycloneDDS.CodeGen
{
    public static class TypeMapper
    {
        public static string GetWriterMethod(string typeName)
        {
            return typeName switch
            {
                "byte" or "Byte" => "WriteUInt8",
                "sbyte" or "SByte" => "WriteInt8",
                "short" or "Int16" => "WriteInt16",
                "ushort" or "UInt16" => "WriteUInt16",
                "int" or "Int32" => "WriteInt32",
                "uint" or "UInt32" => "WriteUInt32",
                "long" or "Int64" => "WriteInt64",
                "ulong" or "UInt64" => "WriteUInt64",
                "float" or "Single" => "WriteFloat",
                "double" or "Double" => "WriteDouble",
                "bool" or "Boolean" => "WriteBool",
                _ => null
            };
        }

        public static string GetSizerMethod(string typeName)
        {
            return GetWriterMethod(typeName);
        }

        public static bool IsPrimitive(string typeName)
        {
             return GetWriterMethod(typeName) != null;
        }
    }
}
