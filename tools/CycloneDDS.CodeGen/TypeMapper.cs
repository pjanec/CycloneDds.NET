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
                "Guid" or "System.Guid" => "WriteGuid",
                "DateTime" or "System.DateTime" => "WriteDateTime",
                "DateTimeOffset" or "System.DateTimeOffset" => "WriteDateTimeOffset",
                "TimeSpan" or "System.TimeSpan" => "WriteTimeSpan",
                "Vector2" or "System.Numerics.Vector2" => "WriteVector2",
                "Vector3" or "System.Numerics.Vector3" => "WriteVector3",
                "Vector4" or "System.Numerics.Vector4" => "WriteVector4",
                "Quaternion" or "System.Numerics.Quaternion" => "WriteQuaternion",
                "Matrix4x4" or "System.Numerics.Matrix4x4" => "WriteMatrix4x4",
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
                "Guid" or "System.Guid" => true,
                "Vector2" or "System.Numerics.Vector2" => true,
                "Vector3" or "System.Numerics.Vector3" => true,
                "Vector4" or "System.Numerics.Vector4" => true,
                "Quaternion" or "System.Numerics.Quaternion" => true,
                "Matrix4x4" or "System.Numerics.Matrix4x4" => true,
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
                "long" or "Int64" or "System.Int64" or "ulong" or "UInt64" or "System.UInt64" or "double" or "Double" or "System.Double" or "DateTime" or "System.DateTime" or "TimeSpan" or "System.TimeSpan" => 8,
                "Guid" or "System.Guid" or "DateTimeOffset" or "System.DateTimeOffset" => 16,
                "Vector2" or "System.Numerics.Vector2" => 8,
                "Vector3" or "System.Numerics.Vector3" => 12,
                "Vector4" or "System.Numerics.Vector4" or "Quaternion" or "System.Numerics.Quaternion" => 16,
                "Matrix4x4" or "System.Numerics.Matrix4x4" => 64,
                _ => 0
            };
        }
    }
}
