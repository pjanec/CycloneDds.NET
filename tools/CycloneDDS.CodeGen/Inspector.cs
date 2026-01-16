using System;
using System.Linq;
using CppAst;

namespace CycloneDDS.CodeGen
{
    public class Inspector
    {
        public static void Inspect()
        {
            var options = new CppParserOptions();
            var compilation = CppParser.Parse("int a = 1 | 2;", options);
            
            Console.WriteLine("Compilation properties:");
            foreach (var prop in typeof(CppCompilation).GetProperties())
            {
                Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name})");
            }
            
            var field = compilation.Fields.FirstOrDefault();
            if (field != null)
            {
                Console.WriteLine($"Field: {field.Name}, Type: {field.Type}");
                var init = field.InitValue;
                if (init != null)
                {
                    Console.WriteLine($"Init Type: {init.GetType().Name}");
                    foreach (var prop in init.GetType().GetProperties())
                    {
                        Console.WriteLine($"  Init.{prop.Name} ({prop.PropertyType.Name})");
                    }
                }
            }
        }
    }
}
