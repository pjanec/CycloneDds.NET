using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CycloneDDS.CodeGen
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: CycloneDDS.CodeGen <source-directory> <output-directory>");
                return 1;
            }
            
            string sourceDir = args[0];
            string outputDir = args[1];
            
            try
            {
                var generator = new CodeGenerator();
                generator.Generate(sourceDir, outputDir);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }
    }
}
