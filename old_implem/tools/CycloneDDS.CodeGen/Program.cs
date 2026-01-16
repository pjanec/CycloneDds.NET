using CycloneDDS.CodeGen.OffsetGeneration;
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
            Console.Error.WriteLine("Usage: CycloneDDS.CodeGen <source-directory> OR generate-offsets --source <path> --output <path>");
            return 1;
        }

        if (args[0] == "generate-offsets")
        {
            return RunGenerateOffsets(args);
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

    static int RunGenerateOffsets(string[] args)
    {
        string sourcePath = null;
        string outputPath = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--source" && i + 1 < args.Length)
                sourcePath = args[++i];
            else if (args[i] == "--output" && i + 1 < args.Length)
                outputPath = args[++i];
        }

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(outputPath))
        {
            Console.Error.WriteLine("Usage: generate-offsets --source <cyclone-src> --output <output-file>");
            return 1;
        }

        try
        {
            AbiOffsetGenerator.GenerateFromSource(sourcePath, outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating offsets: {ex.Message}");
            return 1;
        }
    }
}
