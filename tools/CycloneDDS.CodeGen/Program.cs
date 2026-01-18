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
            //DumpCppAst.Dump();
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: CycloneDDS.CodeGen <source-directory> <output-directory>");
                return 1;
            }
            
            string sourceDir = args[0];
            string outputDir = args[1];
            
            string[] references = null;
            if (args.Length > 2)
            {
                var allRefs = string.Join(";", args.Skip(2));
                references = allRefs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
            
            try
            {
                var generator = new CodeGenerator();
                generator.Generate(sourceDir, outputDir, references);
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
