using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CycloneDDS.Compiler.Common;
using CycloneDDS.Compiler.Common.IdlJson;

namespace CycloneDDS.IdlImporter;

/// <summary>
/// Core orchestrator for the IDL import process.
/// Manages recursive IDL file processing with folder structure preservation.
/// </summary>
public class Importer
{
    private readonly HashSet<string> _processedFiles = new();
    private readonly Queue<string> _workQueue = new();
    private readonly bool _verbose;
    private readonly string? _idlcPath;
    private readonly string? _idlcExtraArgs;
    
    private string _sourceRoot = string.Empty;
    private string _outputRoot = string.Empty;

    // Cache idlc output: path -> types
    private readonly Dictionary<string, List<JsonTypeDefinition>> _typeCache = new();

    public Importer(bool verbose = false, string? idlcPath = null, string? idlcExtraArgs = null)
    {
        _verbose = verbose;
        _idlcPath = idlcPath;
        _idlcExtraArgs = idlcExtraArgs;
    }

    /// <summary>
    /// Imports IDL files starting from a master file, replicating the directory structure.
    /// </summary>
    /// <param name="masterIdlPath">The entry point IDL (e.g. "src/App/main.idl")</param>
    /// <param name="sourceRoot">The common root for all IDLs (e.g. "src/")</param>
    /// <param name="outputRoot">The root for generated C# files (e.g. "generated/")</param>
    public void Import(string masterIdlPath, string sourceRoot, string outputRoot)
    {
        _sourceRoot = Path.GetFullPath(sourceRoot);
        _outputRoot = Path.GetFullPath(outputRoot);
        
        string fullMasterPath = Path.GetFullPath(masterIdlPath);
        if (!File.Exists(fullMasterPath))
        {
             throw new FileNotFoundException($"Master IDL file not found: {fullMasterPath}");
        }

        Log($"Starting import from: {Path.GetRelativePath(_sourceRoot, fullMasterPath)}");
        
        EnqueueFile(fullMasterPath);

        while (_workQueue.Count > 0)
        {
            string currentFile = _workQueue.Dequeue();
            try
            {
                ProcessSingleFile(currentFile);
            }
            catch (Exception ex)
            {
                Log($"ERROR processing {currentFile}: {ex.Message}");
                // We rethrow to fail the build if import fails
                throw;
            }
        }
    }

    private void ProcessSingleFile(string idlPath)
    {
        Log($"Processing {Path.GetFileName(idlPath)}...");

        // 1. Get types for THIS file (and included ones) - cached
        List<JsonTypeDefinition> allTypes = GetIdlTypes(idlPath);

        // 2. Parse manual includes
        var includes = ParseIncludes(idlPath);
        
        // 3. Identification of types defined in THIS file
        // Logic: Filter out types that stem from included files
        var excludedTypeNames = new HashSet<string>();
        
        foreach (var incPath in includes)
        {
            // Ensure dependency is processed
            EnqueueFile(incPath);

            // Get types for the included file to exclude them here
             var incTypes = GetIdlTypes(incPath);
             foreach(var t in incTypes) 
             {
                 excludedTypeNames.Add(t.Name);
             }
        }
        
        // Filter types: only those NOT in excluded set
        // Refinement: If a type is in the excluded set (circular dependency case), 
        // check if it is arguably defined in the current file via regex heuristic.
        string fileContent = File.ReadAllText(idlPath);
        
        var typesToGenerate = allTypes
            .Where(t => !excludedTypeNames.Contains(t.Name) || IsDefinedInFile(t.Name, fileContent))
            .ToList();

        // 4. Generate C#
        if (typesToGenerate.Any())
        {
            // Mirror output path
            string relativePath = Path.GetRelativePath(_sourceRoot, idlPath);
            string relativeDir = Path.GetDirectoryName(relativePath) ?? string.Empty;
            string outputDir = Path.Combine(_outputRoot, relativeDir);
            Directory.CreateDirectory(outputDir);
                
            string csFileName = Path.GetFileNameWithoutExtension(idlPath) + ".cs";
            string finalCsPath = Path.Combine(outputDir, csFileName);
                
            Log($"Generating {finalCsPath} for {typesToGenerate.Count} unique types");
                
            var typeMapper = new TypeMapper();
            // Register all types (even excluded ones) so we can resolve references to them
            foreach (var t in allTypes)
            {
                typeMapper.RegisterType(t);
            }

            var emitter = new CSharpEmitter(typeMapper);
            emitter.GenerateCSharp(typesToGenerate, Path.GetFileName(idlPath), finalCsPath);
        }
        else
        {
            Log("No unique types to generate for this file.");
        }
    }

    private bool IsDefinedInFile(string fullTypeName, string fileContent)
    {
        // Simple heuristic: check for "struct SimpleName", "enum SimpleName", "union SimpleName"
        // TODO: This is brittle to comments and namespaced modules, but solves circularity ambiguity for now.
        
        // Strip comments to avoid false positives
        string noComments = Regex.Replace(fileContent, @"//.*", "");
        noComments = Regex.Replace(noComments, @"/\*[\s\S]*?\*/", "");

        string simpleName = fullTypeName;
        int lastSep = fullTypeName.LastIndexOf("::");
        if (lastSep >= 0) simpleName = fullTypeName.Substring(lastSep + 2);
        
        // Regex for definition
        // \b matches word boundary.
        var regex = new Regex($@"\b(struct|enum|union)\s+{Regex.Escape(simpleName)}\b");
        return regex.IsMatch(noComments);
    }

    private List<JsonTypeDefinition> GetIdlTypes(string idlPath)
    {
        if (_typeCache.TryGetValue(idlPath, out var cached))
        {
            return cached;
        }

        var runner = new IdlcRunner 
        { 
            IdlcPathOverride = _idlcPath,
            IdlcExtraArgs = _idlcExtraArgs 
        };
        // Use a simple random temp dir to avoid conflicts
        string tempDir = Path.Combine(Path.GetTempPath(), "CycloneDDS_Import_" + Guid.NewGuid());
        
        try
        {
             // Pass _sourceRoot as include path so included files are found
             var result = runner.RunIdlc(idlPath, tempDir, _sourceRoot);
             
             if (result.ExitCode != 0)
             {
                 throw new InvalidOperationException($"idlc failed for {idlPath}: {result.StandardError}\n{result.StandardOutput}");
             }
             
             // idlc -l json outputs [filename].json in output dir
             // e.g. Point.idl -> Point.json
             string jsonFile = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(idlPath) + ".json");
             
             if (!File.Exists(jsonFile))
             {
                 // Fallback: search for any *.json file
                 var files = Directory.GetFiles(tempDir, "*.json");
                 if (files.Length > 0) jsonFile = files[0];
                 else throw new FileNotFoundException($"Generated JSON not found for {idlPath} in {tempDir}");
             }
             
             var parser = new IdlJsonParser();
             var types = parser.Parse(jsonFile);
             
             var extraScopes = ParseScopesFromIdl(idlPath);
             UnflattenTypes(types, extraScopes);

             _typeCache[idlPath] = types;
             return types;
        }
        catch( Exception ex )
        {
            throw;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore cleanup errors */ }
            }
        }
    }

    private void UnflattenTypes(List<JsonTypeDefinition> types, List<string> extraScopes)
    {
        // 1. Collect scopes from explicitly scoped types (e.g. "Module::Struct")
        var scopes = new HashSet<string>(extraScopes);
        foreach (var t in types)
        {
             if (t.Name.Contains("::"))
             {
                 var parts = t.Name.Split(new[] { "::" }, StringSplitOptions.None);
                 string current = "";
                 for (int i = 0; i < parts.Length - 1; i++) 
                 {
                     if (i > 0) current += "::";
                     current += parts[i];
                     scopes.Add(current);
                 }
             }
        }
        
        // Sort scopes by length descending to match deepest/longest scopes first
        var sortedScopes = scopes.OrderByDescending(s => s.Length).ToList();

        // 2. Build mapping of valid Flattened -> Scoped names
        // e.g. "CommonLib_Point" -> "CommonLib::Point"
        var flattenedToScoped = new Dictionary<string, string>();
        
        // Populate with existing scoped names
        foreach (var t in types)
        {
             if (t.Name.Contains("::"))
             {
                 string flat = t.Name.Replace("::", "_");
                 flattenedToScoped[flat] = t.Name;
             }
        }

        // 3. Fix Type Definitions
        foreach (var t in types)
        {
             string flatName = t.Name.Replace("::", "_");
             
             // Try to find a matching scope prefix
             foreach(var scope in sortedScopes) 
             {
                 string prefix = scope.Replace("::", "_") + "_";
                 
                 // Check if it matches the prefix
                 if (flatName.StartsWith(prefix) && flatName.Length > prefix.Length) 
                 {
                     string rest = flatName.Substring(prefix.Length);
                     // Heuristic: The rest should not start with an underscore implies cleaner match?
                     // actually prefix includes the trailing underscore.
                     
                     string newName = scope + "::" + rest;
                     
                     // Determine if we should apply this new name
                     // We apply if:
                     // 1. The original name was fully flattened (no ::)
                     // 2. The new name has MORE structure (more ::) than the original
                     
                     int oldColons = t.Name.Split(new[] { "::" }, StringSplitOptions.None).Length - 1;
                     int newColons = newName.Split(new[] { "::" }, StringSplitOptions.None).Length - 1;
                     
                     if (newColons > oldColons || !t.Name.Contains("::"))
                     {
                         t.Name = newName;
                         flattenedToScoped[flatName] = newName;
                         break; // match found (longest because sortedScopes is desc)
                     }
                 }
             }
        }

        // 4. Fix References (Type, Discriminator, Members)
        foreach (var t in types)
        {
             if (t.Type != null && flattenedToScoped.TryGetValue(t.Type.Replace("::", "_"), out var scopedAlias)) 
             {
                 t.Type = scopedAlias;
             }
             
             if (t.Discriminator != null && flattenedToScoped.TryGetValue(t.Discriminator.Replace("::", "_"), out var scopedDisc)) 
             {
                 t.Discriminator = scopedDisc;
             }
             
             foreach (var m in t.Members) 
             {
                 FixMemberType(m, flattenedToScoped);
             }

             // Fix Enum Members (CommonLib_RED -> RED)
             if (t.Kind == "enum" && t.Name.Contains("::"))
             {
                 var parts = t.Name.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                 if (parts.Length > 1) 
                 {
                     string parentScope = string.Join("_", parts.Take(parts.Length - 1));
                     string prefix = parentScope + "_";
                     
                     foreach(var m in t.Members)
                     {
                         if (m.Name.StartsWith(prefix))
                         {
                             m.Name = m.Name.Substring(prefix.Length);
                         }
                     }
                 }
             }
        }
    }

    private List<string> ParseScopesFromIdl(string idlPath)
    {
        var scopes = new List<string>();
        if (!File.Exists(idlPath)) return scopes;

        string content = File.ReadAllText(idlPath);
        
        // Remove comments block and line
        string noComments = Regex.Replace(content, @"//.*", "");
        noComments = Regex.Replace(noComments, @"/\*[\s\S]*?\*/", "");

        // Split by whitespace and braces/semicolons to get tokens
        string[] rawTokens = Regex.Split(noComments, @"(\s+|{|}|;)");
        var tokens = rawTokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

        var stack = new Stack<(string Name, int Depth)>();
        int depth = 0;
        
        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i];
            if (t == "{")
            {
                depth++;
            }
            else if (t == "}")
            {
                if (stack.Count > 0 && stack.Peek().Depth == depth)
                {
                    stack.Pop();
                }
                depth--;
            }
            else if (t == "module")
            {
                if (i + 1 < tokens.Length)
                {
                    string name = tokens[i+1];
                    // Check if followed by { (eventually)
                    // We assume valid IDL so next meaningful token is Name, then {
                    // But wait, if tokenizer stripped whitespace, tokens[i+2] MUST be {
                    if (i+2 < tokens.Length && tokens[i+2] == "{")
                    {
                        // Found module Name {
                        // Push (Name, depth + 1) because the scope starts INSIDE the braces
                        stack.Push((name, depth + 1));
                        
                        // Add to Found Scopes
                        var currentPath = string.Join("::", stack.Select(x => x.Name).Reverse());
                        scopes.Add(currentPath);
                        
                        // Skip name token
                        i++; 
                        // Next token is {, handled in next loop iteration which will inc depth
                    }
                }
            }
        }
        
        return scopes;
    }

    private void FixMemberType(JsonMember m, Dictionary<string, string> mapping)
    {
        if (m.Type != null && mapping.TryGetValue(m.Type, out var scoped))
        {
            m.Type = scoped;
        }
        
        if (m.Members != null)
        {
            foreach(var child in m.Members)
            {
                FixMemberType(child, mapping);
            }
        }
    }

    private List<string> ParseIncludes(string filePath)
    {
        var includes = new List<string>();
        if (!File.Exists(filePath)) return includes;

        var lines = File.ReadAllLines(filePath);
        // Regex for #include "..." or #include <...>
        var regex = new Regex(@"^\s*#include\s+[""<](.+)["">]");
        
        string fileDir = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (match.Success)
            {
                string incName = match.Groups[1].Value;
                
                // 1. Adjacent to current file
                string candidate1 = Path.Combine(fileDir, incName);
                if (File.Exists(candidate1)) 
                {
                    includes.Add(Path.GetFullPath(candidate1));
                    continue;
                }
                
                // 2. Relative to SourceRoot
                string candidate2 = Path.Combine(_sourceRoot, incName);
                 if (File.Exists(candidate2)) 
                {
                    includes.Add(Path.GetFullPath(candidate2));
                    continue;
                }
                
                Log($"Warning: Could not resolve include '{incName}' in {filePath}");
            }
        }
        return includes;
    }

    private void EnqueueFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!_processedFiles.Contains(fullPath) && File.Exists(fullPath))
        {
            _processedFiles.Add(fullPath);
            _workQueue.Enqueue(fullPath);
            Log($"Enqueued: {Path.GetFileName(fullPath)}");
        }
    }

    private void Log(string message)
    {
        if (_verbose)
        {
            Console.WriteLine($"[Importer] {message}");
        }
    }
}
