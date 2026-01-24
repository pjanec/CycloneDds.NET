using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using RoundtripTests;

namespace CycloneDDS.Roundtrip.App;

/// <summary>
/// Generates deterministic test data from integer seeds using reflection.
/// Mirrors the C native implementation's data generation logic.
/// </summary>
internal static class DataGenerator
{
    #region Public API

    /// <summary>
    /// Fill an object with deterministic data based on the seed.
    /// </summary>
    public static void Fill<T>(ref T obj, int seed)
    {
        Console.WriteLine($"[DataGenerator] Fill<{typeof(T).Name}> seed={seed}");
        if (typeof(T) == typeof(AllPrimitives))
        {
            Console.WriteLine("[DataGenerator] Dispatching to FillAllPrimitives");
            var filled = FillAllPrimitives(seed);
            obj = (T)(object)filled;
            return;
        }
        if (typeof(T) == typeof(CompositeKey))
        {
            Console.WriteLine("[DataGenerator] Dispatching to FillCompositeKey");
            obj = (T)(object)FillCompositeKey(seed);
            return;
        }
        if (typeof(T) == typeof(NestedKeyTopic))
        {
            Console.WriteLine("[DataGenerator] Dispatching to FillNestedKeyTopic");
            obj = (T)(object)FillNestedKeyTopic(seed);
            return;
        }

        if (obj == null) 
            throw new ArgumentNullException(nameof(obj));

        Console.WriteLine("[DataGenerator] Fallback to Reflection Fill");

        object boxed = obj;
        FillObject(boxed, seed, depth: 0);
        obj = (T)boxed;
    }

    /// <summary>
    /// Compare two objects for equality (deep comparison).
    /// </summary>
    public static bool AreEqual<T>(T a, T b)
    {
        if (ReferenceEquals(a, b))
            return true;
        
        if (a == null || b == null)
            return false;

        return CompareObjects(a, b, depth: 0);
    }

    #endregion

    #region Fill Logic

    private static void FillObject(object obj, int seed, int depth)
    {
        const int MaxDepth = 10; // Prevent infinite recursion
        if (depth > MaxDepth)
            return;

        Type type = obj.GetType();

        // Get all public instance properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.CanRead)
            .ToArray();

        foreach (var prop in properties)
        {
            // Calculate offset for this property (makes fields unique)
            int offset = CalculateOffset(prop.Name, seed);
            
            object? value = GenerateValue(prop.PropertyType, offset, depth);
            
            if (value != null)
            {
                prop.SetValue(obj, value);
            }
        }
    }

    private static object? GenerateValue(Type type, int seed, int depth)
    {
        // Primitives
        if (type == typeof(bool))
            return (seed % 2) == 1;
        
        if (type == typeof(byte))
            return (byte)(seed % 256);
        
        if (type == typeof(char))
            return (char)('A' + (seed % 26));
        
        if (type == typeof(short))
            return (short)(seed % 10000);
        
        if (type == typeof(ushort))
            return (ushort)(seed % 10000);
        
        if (type == typeof(int))
            return seed;
        
        if (type == typeof(uint))
            return (uint)seed;
        
        if (type == typeof(long))
            return (long)seed * 1000L;
        
        if (type == typeof(ulong))
            return (ulong)seed * 1000UL;
        
        if (type == typeof(float))
            return (float)seed + 0.5f;
        
        if (type == typeof(double))
            return (double)seed + 0.25;

        // String
        if (type == typeof(string))
            return $"Str_{seed}";

        // Array
        if (type.IsArray)
        {
            Type elementType = type.GetElementType()!;
            int rank = type.GetArrayRank();

            if (rank == 1)
            {
                // 1D array
                int length = 5 + (seed % 10); // Variable length based on seed
                Array array = Array.CreateInstance(elementType, length);
                
                for (int i = 0; i < length; i++)
                {
                    object? elem = GenerateValue(elementType, seed + i, depth + 1);
                    array.SetValue(elem, i);
                }
                
                return array;
            }
            else if (rank == 2)
            {
                // 2D array
                int rows = 3;
                int cols = 4;
                Array array = Array.CreateInstance(elementType, rows, cols);
                
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        object? elem = GenerateValue(elementType, seed + i * cols + j, depth + 1);
                        array.SetValue(elem, i, j);
                    }
                }
                
                return array;
            }
        }

        // List<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            Type elementType = type.GetGenericArguments()[0];
            int count = 3 + (seed % 5); // Variable length
            
            var list = Activator.CreateInstance(type) as System.Collections.IList;
            if (list == null)
                return null;

            for (int i = 0; i < count; i++)
            {
                object? elem = GenerateValue(elementType, seed + i, depth + 1);
                list.Add(elem);
            }
            
            return list;
        }

        // Nullable<T>
        if (Nullable.GetUnderlyingType(type) is Type underlyingType)
        {
            // Make it null 20% of the time
            if (seed % 5 == 0)
                return null;
            
            return GenerateValue(underlyingType, seed, depth);
        }

        // Struct or Class
        if (type.IsClass || type.IsValueType)
        {
            try
            {
                object? instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    FillObject(instance, seed, depth + 1);
                    return instance;
                }
            }
            catch
            {
                // Can't instantiate - skip
            }
        }

        return null;
    }

    #endregion

    #region Compare Logic

    private static bool CompareObjects(object a, object b, int depth)
    {
        const int MaxDepth = 10;
        if (depth > MaxDepth)
            return true; // Assume equal to prevent stack overflow

        Type type = a.GetType();
        
        if (type != b.GetType())
            return false;

        // Primitives and strings use default equality
        if (type.IsPrimitive || type == typeof(string))
            return Equals(a, b);

        // Arrays
        if (type.IsArray)
        {
            Array arrayA = (Array)a;
            Array arrayB = (Array)b;

            if (arrayA.Rank != arrayB.Rank)
                return false;

            for (int dim = 0; dim < arrayA.Rank; dim++)
            {
                if (arrayA.GetLength(dim) != arrayB.GetLength(dim))
                    return false;
            }

            if (arrayA.Rank == 1)
            {
                for (int i = 0; i < arrayA.Length; i++)
                {
                    object? elemA = arrayA.GetValue(i);
                    object? elemB = arrayB.GetValue(i);

                    if (!CompareValues(elemA, elemB, depth + 1))
                        return false;
                }
            }
            else if (arrayA.Rank == 2)
            {
                int rows = arrayA.GetLength(0);
                int cols = arrayA.GetLength(1);

                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        object? elemA = arrayA.GetValue(i, j);
                        object? elemB = arrayB.GetValue(i, j);

                        if (!CompareValues(elemA, elemB, depth + 1))
                            return false;
                    }
                }
            }

            return true;
        }

        // List<T>
        if (a is System.Collections.IList listA && b is System.Collections.IList listB)
        {
            if (listA.Count != listB.Count)
                return false;

            for (int i = 0; i < listA.Count; i++)
            {
                if (!CompareValues(listA[i], listB[i], depth + 1))
                    return false;
            }

            return true;
        }

        // Struct or Class - compare properties
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        foreach (var prop in properties)
        {
            object? valueA = prop.GetValue(a);
            object? valueB = prop.GetValue(b);

            if (!CompareValues(valueA, valueB, depth + 1))
                return false;
        }

        return true;
    }

    private static bool CompareValues(object? a, object? b, int depth)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null)
            return false;

        Type type = a.GetType();

        // Floating-point tolerance
        if (type == typeof(float))
        {
            float fa = (float)a;
            float fb = (float)b;
            return Math.Abs(fa - fb) < 1e-5f;
        }

        if (type == typeof(double))
        {
            double da = (double)a;
            double db = (double)b;
            return Math.Abs(da - db) < 1e-9;
        }

        // Primitives and strings
        if (type.IsPrimitive || type == typeof(string))
            return Equals(a, b);

        // Recursively compare complex types
        return CompareObjects(a, b, depth);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Calculate a unique offset for a property based on its name.
    /// This ensures different fields get different values.
    /// </summary>
    private static int CalculateOffset(string propertyName, int baseSeed)
    {
        int hash = 0;
        foreach (char c in propertyName.ToLowerInvariant())
        {
            hash = (hash * 31 + c) % 10000;
        }
        return baseSeed + hash;
    }

    #endregion

    private static AllPrimitives FillAllPrimitives(int seed)
    {
        var val = new AllPrimitives
        {
            Id = seed,
            Bool_field = (seed % 2) == 0,
            Char_field = (byte)(seed % 256),
            Octet_field = (byte)(seed % 256),
            Short_field = (short)(seed % 10000),
            Ushort_field = (ushort)(seed % 10000),
            Long_field = (int)seed,
            Ulong_field = (uint)seed,
            Llong_field = (long)seed * 1000L,
            Ullong_field = (ulong)seed * 1000UL,
            Float_field = (float)seed + 0.5f,
            Double_field = (double)seed + 0.25
        };
        Console.WriteLine($"[DataGenerator] Generated AllPrimitives: Id={val.Id}, Bool={val.Bool_field}, Char={val.Char_field:X2}");
        return val;
    }

    private static CompositeKey FillCompositeKey(int seed)
    {
        return new CompositeKey
        {
            Region = $"Region_{seed}",
            Zone = (int)((seed + 1) * 31),
            Sector = (short)((seed + 2) * 7),
            Name = $"Name_{seed + 10}",
            Value = (double)((seed + 20) * 3.14159),
            Priority = (Priority)((seed + 3) % 4)
        };
    }

    private static NestedKeyTopic FillNestedKeyTopic(int seed)
    {
        return new NestedKeyTopic
        {
            Location = new Location
            {
                Building = (int)seed,
                Floor = (short)((seed % 10) + 1),
                Room = (int)((seed + 100) * 31)
            },
            Description = $"Room_Desc_{seed}",
            Temperature = (double)((seed + 50) * 0.5),
            Last_updated = new Timestamp
            {
                Seconds = (long)(seed + 1000000),
                Nanoseconds = (uint)((seed * 1000) % 1000000000)
            }
        };
    }
}
