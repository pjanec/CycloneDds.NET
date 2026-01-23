using System;
using System.Reflection;
using System.Runtime.InteropServices;
using CycloneDDS.Runtime.Tests.KeyedMessages;

public class Program
{
    public static void Main()
    {
        Inspect<SingleKeyMessage>("SingleKeyMessage");
        Inspect<NestedStructKeyMessage>("NestedStructKeyMessage");
        Inspect<ProcessAddress>("ProcessAddress");
    }

    private static void Inspect<T>(string name) where T : struct
    {
        Console.WriteLine($"--- Inspecting {name} ---");
        Console.WriteLine($"SizeOf: {Marshal.SizeOf<T>()}");
        
        var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            var offset = Marshal.OffsetOf<T>(f.Name);
            Console.WriteLine($"Field: {f.Name}, Offset: {offset}, Type: {f.FieldType.Name}");
        }
        
        // Also check Properties backing fields
        Console.WriteLine("-- Backing Fields --");
         var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var p in props)
        {
             // heuristic for backing field
             var bf = typeof(T).GetField($"<{p.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
             if (bf != null)
             {
                 var offset = Marshal.OffsetOf<T>(bf.Name);
                 Console.WriteLine($"Property: {p.Name}, BackingField Offset: {offset}");
             }
        }
    }
}
