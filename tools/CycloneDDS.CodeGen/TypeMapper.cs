using System;

namespace CycloneDDS.CodeGen
{
    public static class TypeMapper
    {
        public static string? GetWriterMethod(string typeName)
        {
            return typeName switch
            {
                "byte" or "Byte" or "System.Byte" => "WriteUInt8",
                "sbyte" or "SByte" or "System.SByte" => "WriteInt8",
                "short" or "Int16" or "System.Int16" => "WriteInt16",
                "ushort" or "UInt16" or "System.UInt16" => "WriteUInt16",
                "int" or "Int32" or "System.Int32" => "WriteInt32",
                "uint" or "UInt32" or "System.UInt32" => "WriteUInt32",
                "long" or "Int64" or "System.Int64" => "WriteInt64",
                "ulong" or "UInt64" or "System.UInt64" => "WriteUInt64",
                "float" or "Single" or "System.Single" => "WriteFloat",
                "double" or "Double" or "System.Double" => "WriteDouble",
                "bool" or "Boolean" or "System.Boolean" => "WriteBool",
                // "string" removed to prevent IsPrimitive=true
                "System.Guid" => "WriteGuid",
                "System.DateTime" => "WriteDateTime",
                "System.DateTimeOffset" => "WriteDateTimeOffset",
                "System.TimeSpan" => "WriteTimeSpan",
                "System.Numerics.Vector2" => "WriteVector2",
                "System.Numerics.Vector3" => "WriteVector3",
                "System.Numerics.Vector4" => "WriteVector4",
                "System.Numerics.Quaternion" => "WriteQuaternion",
                "System.Numerics.Matrix4x4" => "WriteMatrix4x4",
                _ => null
            };
        }

        public static string? GetSizerMethod(string typeName)
        {
            return GetWriterMethod(typeName);
        }

        public static bool IsPrimitive(string typeName)
        {
             return GetWriterMethod(typeName) != null;
        }

        public static bool IsBlittable(string typeName)
        {
            return typeName switch
            {
                "byte" or "Byte" or "System.Byte" => true,
                "sbyte" or "SByte" or "System.SByte" => true,
                "short" or "Int16" or "System.Int16" => true,
                "ushort" or "UInt16" or "System.UInt16" => true,
                "int" or "Int32" or "System.Int32" => true,
                "uint" or "UInt32" or "System.UInt32" => true,
                "long" or "Int64" or "System.Int64" => true,
                "ulong" or "UInt64" or "System.UInt64" => true,
                "float" or "Single" or "System.Single" => true,
                "double" or "Double" or "System.Double" => true,
                "System.Guid" => true,
                "System.Numerics.Vector2" => true,
                "System.Numerics.Vector3" => true,
                "System.Numerics.Vector4" => true,
                "System.Numerics.Quaternion" => true,
                "System.Numerics.Matrix4x4" => true,
                _ => false
            };
        }

        public static int GetSize(string typeName)
        {
            return typeName switch
            {
                "byte" or "Byte" or "System.Byte" or "bool" or "Boolean" or "System.Boolean" or "sbyte" or "SByte" or "System.SByte" => 1,
                "short" or "Int16" or "System.Int16" or "ushort" or "UInt16" or "System.UInt16" or "char" or "Char" or "System.Char" => 2,
                "int" or "Int32" or "System.Int32" or "uint" or "UInt32" or "System.UInt32" or "float" or "Single" or "System.Single" => 4,
                "long" or "Int64" or "System.Int64" or "ulong" or "UInt64" or "System.UInt64" or "double" or "Double" or "System.Double" or "System.DateTime" or "System.TimeSpan" => 8,
                "System.Guid" or "System.DateTimeOffset" => 16,
                "System.Numerics.Vector2" => 8,
                "System.Numerics.Vector3" => 12,
                "System.Numerics.Vector4" or "System.Numerics.Quaternion" => 16,
                "System.Numerics.Matrix4x4" => 64,
                _ => 0
            };
        }
    }
}
