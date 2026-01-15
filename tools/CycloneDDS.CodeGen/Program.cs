using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;

namespace CycloneDDS.CodeGen;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: CycloneDDS.CodeGen <source-directory>");
            return 1;
        }

        var sourceDir = args[0];
        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine($"Directory not found: {sourceDir}");
            return 1;
        }

        Console.WriteLine($"[CodeGen] Scanning: {sourceDir}");

        var generator = new CodeGenerator();
        var filesGenerated = generator.Generate(sourceDir);

        if (filesGenerated < 0)
        {
            Console.Error.WriteLine("[CodeGen] Code generation failed due to validation errors");
            return 1;
        }

        Console.WriteLine($"[CodeGen] Generated {filesGenerated} files");
        return 0;
    }
}
