using System;
using System.Collections.Generic;
using System.Reflection;

namespace CsharpToC.Symmetry.Infrastructure
{
    /// <summary>
    /// Generates deterministic test data based on a seed value.
    /// Algorithm matches the native C implementation for consistency.
    /// </summary>
    public static class DataGenerator
    {
        /// <summary>
        /// Creates an instance of type T with fields populated based on the seed.
        /// </summary>
        /// <typeparam name="T">Type to instantiate and populate</typeparam>
        /// <param name="seed">Seed value for deterministic generation</param>
        /// <returns>Populated instance</returns>
        public static T Create<T>(int seed) where T : new()
        {
            var instance = new T();
            PopulateObject(instance, seed, fieldIndex: 0);
            return instance;
        }

        /// <summary>
        /// Populates an existing object's fields based on seed and field index.
        /// </summary>
        private static int PopulateObject(object obj, int seed, int fieldIndex)
        {
            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            int currentIndex = fieldIndex;

            // Populate fields
            foreach (var field in fields)
            {
                object? value = GenerateValue(field.FieldType, seed, currentIndex);
                field.SetValue(obj, value);
                currentIndex++;
            }

            // Populate properties (if they have setters)
            foreach (var prop in properties)
            {
                if (prop.CanWrite)
                {
                    object? value = GenerateValue(prop.PropertyType, seed, currentIndex);
                    prop.SetValue(obj, value);
                    currentIndex++;
                }
            }

            return currentIndex;
        }

        /// <summary>
        /// Generates a value for a specific type based on seed and field index.
        /// Algorithm matches native implementation.
        /// </summary>
        private static object? GenerateValue(Type type, int seed, int index)
        {
            // Primitives
            if (type == typeof(bool))
                return ((seed + index) % 2) == 0;
            
            if (type == typeof(byte))
                return (byte)((seed + index) % 256);
            
            if (type == typeof(sbyte))
                return (sbyte)((seed + index) % 128);
            
            if (type == typeof(char))
                return (char)((seed + index) % 128); // ASCII range
            
            if (type == typeof(short))
                return (short)(seed + index);
            
            if (type == typeof(ushort))
                return (ushort)(seed + index);
            
            if (type == typeof(int))
                return seed + index;
            
            if (type == typeof(uint))
                return (uint)(seed + index);
            
            if (type == typeof(long))
                return (long)(seed + index);
            
            if (type == typeof(ulong))
                return (ulong)(seed + index);
            
            if (type == typeof(float))
                return (float)(seed + index) + 0.5f;
            
            if (type == typeof(double))
                return (double)(seed + index) + 0.5;

            // Strings
            if (type == typeof(string))
                return $"Str_{seed}_{index}";

            // Enums
            if (type.IsEnum)
            {
                var enumValues = Enum.GetValues(type);
                int enumIndex = (seed + index) % enumValues.Length;
                return enumValues.GetValue(enumIndex);
            }

            // Arrays
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                int length = (Math.Abs(seed) % 5) + 1; // 1-5 elements
                var array = Array.CreateInstance(elementType, length);
                
                for (int i = 0; i < length; i++)
                {
                    var element = GenerateValue(elementType, seed, index + i);
                    array.SetValue(element, i);
                }
                
                return array;
            }

            // Generic collections (List<T>, etc.)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add")!;
                
                int length = (Math.Abs(seed) % 5) + 1;
                for (int i = 0; i < length; i++)
                {
                    var element = GenerateValue(elementType, seed, index + i);
                    addMethod.Invoke(list, new[] { element });
                }
                
                return list;
            }

            // Complex types (structs/classes)
            if (type.IsClass || type.IsValueType)
            {
                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance != null)
                    {
                        PopulateObject(instance, seed, index);
                        return instance;
                    }
                }
                catch
                {
                    // Type can't be instantiated, return null
                }
            }

            return null;
        }
    }
}
