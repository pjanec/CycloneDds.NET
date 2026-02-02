using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace CycloneDDS.IdlImporter;

/// <summary>
/// Entry point for the CycloneDDS IDL Importer tool.
/// Converts IDL files to C# DSL using idlc JSON output.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var masterIdlArg = new Argument<string>(
            name: "master-idl",
            description: "Path to the entry-point IDL file");

        var sourceRootOption = new Option<string?>(
            name: "--source-root",
            description: "Root directory containing all IDL files (default: master-idl directory)");

        var outputRootOption = new Option<string?>(
            name: "--output-root",
            description: "Root directory for generated C# files (default: current directory)");

        var idlcPathOption = new Option<string?>(
            name: "--idlc-path",
            description: "Path to idlc executable (default: auto-detect)");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable detailed logging");

        var rootCommand = new RootCommand("CycloneDDS IDL Importer v1.0")
        {
            masterIdlArg,
            sourceRootOption,
            outputRootOption,
            idlcPathOption,
            verboseOption
        };

        rootCommand.SetHandler(
            async (masterIdl, sourceRoot, outputRoot, idlcPath, verbose) =>
            {
                // Default logic
                if (string.IsNullOrEmpty(sourceRoot))
                {
                    sourceRoot = Path.GetDirectoryName(Path.GetFullPath(masterIdl)) ?? Directory.GetCurrentDirectory();
                }

                if (string.IsNullOrEmpty(outputRoot))
                {
                    outputRoot = Directory.GetCurrentDirectory();
                }

                try
                {
                    await RunImporter(masterIdl, sourceRoot, outputRoot, idlcPath, verbose);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                    
                    if (verbose)
                    {
                        Console.Error.WriteLine(ex.StackTrace);
                    }
                    
                    Environment.Exit(1);
                }
            },
            masterIdlArg, sourceRootOption, outputRootOption, idlcPathOption, verboseOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunImporter(
        string masterIdl,
        string sourceRoot,
        string outputRoot,
        string? idlcPath,
        bool verbose)
    {
        // Validate arguments
        if (!File.Exists(masterIdl))
        {
            throw new FileNotFoundException($"Master IDL file not found: {masterIdl}");
        }

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source root directory not found: {sourceRoot}");
        }

        var fullMasterPath = Path.GetFullPath(masterIdl);
        var fullSourceRoot = Path.GetFullPath(sourceRoot);
        var fullOutputRoot = Path.GetFullPath(outputRoot);

        if (!fullMasterPath.StartsWith(fullSourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Master IDL file must be located within the source root directory");
        }

        Console.WriteLine("CycloneDDS IDL Importer");
        Console.WriteLine("=======================");
        Console.WriteLine($"Master IDL:   {masterIdl}");
        Console.WriteLine($"Source Root:  {sourceRoot}");
        Console.WriteLine($"Output Root:  {outputRoot}");
        if (!string.IsNullOrEmpty(idlcPath))
        {
            Console.WriteLine($"IDLC Path:    {idlcPath}");
        }
        Console.WriteLine();

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(fullOutputRoot);

        // Run the Importer
        try 
        {
            var importer = new Importer(verbose, idlcPath);
            importer.Import(fullMasterPath, fullSourceRoot, fullOutputRoot);
        }
        catch (Exception)
        {
             // Log error but allow Main to catch it for consistent exit codes
             throw;
        }
        
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Import complete");
        Console.ResetColor();

        await Task.CompletedTask;
    }
}
