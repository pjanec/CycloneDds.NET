# IDL Importer - Task Details Document

**Project:** FastCycloneDDS C# Bindings  
**Tool:** CycloneDDS.IdlImporter  
**Version:** 1.0  
**Date:** January 28, 2026

**Related Documents:**
- [Design Document](../../docs/IdlImport-design.md)
- [Task Tracker](./IDLImport-TASK-TRACKER.md)

---

## Overview

This document provides detailed implementation specifications for each task in the IDL Importer tool development. Each task includes:

- **Objective**: What needs to be accomplished
- **Technical Details**: Implementation specifics
- **Dependencies**: Prerequisites and related tasks
- **Success Criteria**: Definition of done (unit test requirements)
- **Reference**: Links to design document sections

---

## Phase 1: Foundation

### IDLIMP-001: Project Setup and Shared Infrastructure

**Objective:** Create the .NET 8 console application project and establish shared code infrastructure with `CycloneDDS.CodeGen`.

**Technical Details:**
1. Create `tools/CycloneDDS.IdlImporter/CycloneDDS.IdlImporter.csproj` (.NET 8 console app)
2. Add project references:
   - `CycloneDDS.Schema` (for attribute types)
   - Create or reference shared library for `IdlcRunner` and `JsonModels`
3. Setup directory structure:
   ```
   tools/CycloneDDS.IdlImporter/
     ├─ CycloneDDS.IdlImporter.csproj
     ├─ Program.cs (CLI entry point)
     └─ README.md (basic usage)
   ```
4. Configure build output and dependencies
5. Add NuGet packages:
   - `System.CommandLine` (for CLI parsing)
   - `System.Text.Json` (for JSON parsing)

**Shared Code Strategy:**
- Option A: Extract `IdlcRunner.cs` and `JsonModels.cs` from CodeGen into `CycloneDDS.Compiler.Common` library
- Option B: Use project reference to CodeGen and access public classes
- Choose based on existing CodeGen architecture

**Dependencies:** None

**Success Criteria:**
- Project builds successfully
- Can reference and instantiate `IdlcRunner` class
- Can deserialize sample `idlc -l json` output using `JsonModels`
- Basic CLI `--help` command displays usage information

**Unit Tests:**
```csharp
[Fact]
public void Project_Builds_Successfully()
{
    // Verify project references resolve
    var idlcRunner = new IdlcRunner();
    Assert.NotNull(idlcRunner);
}

[Fact]
public void CLI_DisplaysHelp_WhenHelpFlagProvided()
{
    var args = new[] { "--help" };
    var exitCode = Program.Main(args);
    Assert.Equal(0, exitCode);
}
```

**Reference:** [Design: Architecture](#architecture), [Design: File Organization](#file-organization)

---

### IDLIMP-002: IdlcRunner Enhancement for Include Paths

**Objective:** Enhance `IdlcRunner` to support the `-I` include path argument required for resolving cross-folder IDL includes.

**Technical Details:**
1. Extend `IdlcRunner.RunIdlc()` method signature:
   ```csharp
   public IdlcResult RunIdlc(string idlFilePath, string outputDir, string? includePath = null)
   ```
2. Modify command-line argument construction to include `-I "{includePath}"` when provided
3. Ensure proper path escaping for paths with spaces
4. Add validation for include path existence

**Current Implementation (to modify):**
```csharp
// Before
Arguments = $"-l json -o \"{outputDir}\" \"{idlFilePath}\""

// After
string includeArg = string.IsNullOrEmpty(includePath) ? "" : $"-I \"{includePath}\"";
Arguments = $"-l json {includeArg} -o \"{outputDir}\" \"{idlFilePath}\""
```

**Dependencies:** IDLIMP-001

**Success Criteria:**
- Can compile IDL file with includes using source root as include path
- Include path with spaces handled correctly
- Non-existent include path produces clear error
- Existing CodeGen functionality not broken (backward compatible)

**Unit Tests:**
```csharp
[Fact]
public void IdlcRunner_SupportsIncludePath()
{
    var runner = new IdlcRunner();
    var result = runner.RunIdlc("test.idl", "output", includePath: "c:\\includes");
    // Verify -I argument present in command
}

[Fact]
public void IdlcRunner_HandlesPathsWithSpaces()
{
    var runner = new IdlcRunner();
    var result = runner.RunIdlc("test.idl", "output", includePath: "c:\\my includes\\path");
    // Verify proper escaping
}

[Theory]
[InlineData("FolderA/includes.idl", "FolderA", "FolderB/types.idl")]
public void IdlcRunner_ResolvesCrossFolderIncludes(string masterIdl, string includeRoot, string expectedInclude)
{
    // Verify idlc can find includes when -I points to common root
}
```

**Reference:** [Design: Component Design - IdlcRunner](#2-idlcrunner-idl-compilation)

---

### IDLIMP-003: Type Mapper Implementation

**Objective:** Implement the core type mapping logic that translates IDL types from JSON to C# types with appropriate attributes.

**Technical Details:**
1. Create `TypeMapper.cs` class with key methods:
   ```csharp
   public class TypeMapper
   {
       public string MapPrimitive(string idlType);
       public (string CsType, bool IsManaged, int ArrayLen, int Bound) MapMember(JsonMember member);
       public string GetCSharpNamespace(string idlModulePath);
   }
   ```
2. Implement all mappings from [Design: Type Mapping Rules](#type-mapping-rules)
3. Handle special cases:
   - Sequences (bounded/unbounded) → `List<T>`
   - Arrays (fixed/dynamic) → `T[]`
   - Bounded strings → `string` with `[MaxLength(N)]`
   - User-defined types → resolve to C# type name

4. Detect when `[DdsManaged]` attribute is required:
   - Strings (always)
   - Sequences (always)
   - Arrays (always)
   - Structs containing managed members (recursive check - future enhancement)

**Edge Cases to Handle:**
- Nested sequences: `sequence<sequence<long>>` → `List<List<int>>`
- Multi-dimensional arrays (defer to phase 2 if not in test IDL)
- Typedef resolution (idlc resolves to canonical type)

**Dependencies:** IDLIMP-001

**Success Criteria:**
- All primitive types map correctly per design table
- Collection types map with correct attributes
- User-defined types preserve namespace/module hierarchy
- Managed flag set correctly for all collection types

**Unit Tests:**
```csharp
[Theory]
[InlineData("long", "int")]
[InlineData("unsigned long", "uint")]
[InlineData("double", "double")]
[InlineData("boolean", "bool")]
[InlineData("string", "string")]
public void MapPrimitive_CorrectlyMapsBasicTypes(string idlType, string expectedCsType)
{
    var mapper = new TypeMapper();
    var result = mapper.MapPrimitive(idlType);
    Assert.Equal(expectedCsType, result);
}

[Fact]
public void MapMember_UnboundedSequence_ReturnsListWithManagedFlag()
{
    var member = new JsonMember { Type = "long", CollectionType = "sequence" };
    var (csType, isManaged, arrayLen, bound) = new TypeMapper().MapMember(member);
    
    Assert.Equal("List<int>", csType);
    Assert.True(isManaged);
    Assert.Equal(0, bound);
}

[Fact]
public void MapMember_BoundedSequence_ReturnsListWithBound()
{
    var member = new JsonMember { Type = "long", CollectionType = "sequence", Bound = 10 };
    var (csType, isManaged, arrayLen, bound) = new TypeMapper().MapMember(member);
    
    Assert.Equal("List<int>", csType);
    Assert.True(isManaged);
    Assert.Equal(10, bound);
}

[Fact]
public void MapMember_FixedArray_ReturnsArrayWithLength()
{
    var member = new JsonMember 
    { 
        Type = "double", 
        CollectionType = "array", 
        Dimensions = new[] { 5 } 
    };
    var (csType, isManaged, arrayLen, bound) = new TypeMapper().MapMember(member);
    
    Assert.Equal("double[]", csType);
    Assert.True(isManaged);
    Assert.Equal(5, arrayLen);
}
```

**Reference:** [Design: Type Mapping Rules](#type-mapping-rules)

---

## Phase 2: Core Importer Logic

### IDLIMP-004: Importer Core - File Queue and Recursion

**Objective:** Implement the core `Importer` class that orchestrates the recursive IDL processing with proper deduplication.

**Technical Details:**
1. Create `Importer.cs` with main method:
   ```csharp
   public class Importer
   {
       private readonly HashSet<string> _processedFiles = new();
       private readonly Queue<string> _workQueue = new();
       private string _sourceRoot;
       private string _outputRoot;
       
       public void Import(string masterIdlPath, string sourceRoot, string outputRoot);
       private void ProcessSingleFile(string idlPath);
       private void EnqueueFile(string idlPath);
   }
   ```

2. Implement work queue pattern:
   - Normalize paths to prevent duplicates (`/` vs `\`)
   - Track processed files to handle circular includes
   - Calculate relative paths for output structure

3. Path mirroring logic:
   ```csharp
   string relativePath = Path.GetRelativePath(_sourceRoot, currentIdlPath);
   string targetDir = Path.Combine(_outputRoot, Path.GetDirectoryName(relativePath));
   ```

4. Handle edge cases:
   - Master file includes itself (skip)
   - Circular includes: A includes B, B includes A
   - Files outside source root (warn and skip)

**Dependencies:** IDLIMP-001, IDLIMP-002

**Success Criteria:**
- Processes master file and all recursive includes exactly once
- Mirrors folder structure from source to output
- Handles circular includes without infinite loop
- Skips files outside source root with warning

**Unit Tests:**
```csharp
[Fact]
public void Importer_ProcessesMasterAndIncludes()
{
    // Setup temp IDL files: Master.idl includes A.idl and B.idl
    var importer = new Importer();
    importer.Import("Master.idl", "testIdl", "testOut");
    
    // Verify all 3 files processed
    Assert.True(File.Exists("testOut/Master.cs"));
    Assert.True(File.Exists("testOut/A.cs"));
    Assert.True(File.Exists("testOut/B.cs"));
}

[Fact]
public void Importer_HandlesCircularIncludes()
{
    // A.idl includes B.idl, B.idl includes A.idl
    var importer = new Importer();
    importer.Import("A.idl", "testIdl", "testOut");
    
    // Should complete without hanging
    Assert.True(File.Exists("testOut/A.cs"));
    Assert.True(File.Exists("testOut/B.cs"));
}

[Fact]
public void Importer_MirrorsSubfolderStructure()
{
    // Source: testIdl/FolderA/SubA/Type.idl
    // Output: testOut/FolderA/SubA/Type.cs
    var importer = new Importer();
    importer.Import("Master.idl", "testIdl", "testOut");
    
    Assert.True(File.Exists("testOut/FolderA/SubA/Type.cs"));
}
```

**Reference:** [Design: Architecture - Processing Strategy](#processing-strategy), [Design: Component Design - Importer](#1-importer-core-orchestration)

---

### IDLIMP-005: JSON Parsing and File Metadata Extraction

**Objective:** Parse `idlc -l json` output and extract file-to-type mappings from the JSON structure.

**Technical Details:**
1. Reuse or enhance existing `IdlJsonParser`:
   ```csharp
   public class IdlJsonParser
   {
       public IdlJsonRoot ParseJson(string jsonContent);
       public JsonFileMeta? FindFileMeta(List<JsonFileMeta> files, string fileName);
   }
   ```

2. Key extraction logic:
   - Parse `root.Types` array (all type definitions)
   - Parse `root.File` array (file metadata with type lists)
   - Match types to source files using `JsonFileMeta.DefinedTypes`

3. Handle idlc output variations:
   - File names may be absolute or relative paths
   - Normalize and match by filename (not full path)
   - Handle case where `File` metadata is incomplete

4. Dependency extraction:
   - Extract `JsonFileMeta.Dependencies` list
   - Resolve dependency paths relative to source root
   - Convert to absolute paths for enqueueing

**Dependencies:** IDLIMP-001, IDLIMP-004

**Success Criteria:**
- Correctly deserializes all test IDL JSON outputs
- Maps types to their source files accurately
- Extracts dependency list for recursive processing
- Handles missing or incomplete `File` metadata gracefully

**Unit Tests:**
```csharp
[Fact]
public void JsonParser_ParsesAtomicTestsJson()
{
    var json = File.ReadAllText("atomic_tests.json");
    var parser = new IdlJsonParser();
    var root = parser.ParseJson(json);
    
    Assert.NotNull(root);
    Assert.NotEmpty(root.Types);
}

[Fact]
public void JsonParser_FindsFileMetaByFileName()
{
    var fileMetas = new List<JsonFileMeta>
    {
        new() { Name = "C:\\full\\path\\Types.idl", DefinedTypes = new() { "TypeA" } },
        new() { Name = "Other.idl", DefinedTypes = new() { "TypeB" } }
    };
    
    var parser = new IdlJsonParser();
    var result = parser.FindFileMeta(fileMetas, "Types.idl");
    
    Assert.NotNull(result);
    Assert.Contains("TypeA", result.DefinedTypes);
}

[Fact]
public void JsonParser_ExtractsDependencies()
{
    var json = File.ReadAllText("with_includes.json");
    var parser = new IdlJsonParser();
    var root = parser.ParseJson(json);
    
    var fileMeta = root.File[0];
    Assert.NotEmpty(fileMeta.Dependencies);
}
```

**Reference:** [Design: Component Design - IdlJsonParser](#3-idljsonparser-json-deserialization)

---

## Phase 3: C# Code Generation

### IDLIMP-006: CSharpEmitter - Struct and Enum Generation

**Objective:** Implement C# code emission for basic structures and enumerations with proper attributes and namespaces.

**Technical Details:**
1. Create `CSharpEmitter.cs` with core methods:
   ```csharp
   public class CSharpEmitter
   {
       public string GenerateCSharp(List<string> typeNames, string originalIdlFileName);
       private void EmitType(StringBuilder sb, JsonTypeDefinition type, int indentLevel);
       private void EmitStructMembers(StringBuilder sb, JsonTypeDefinition type, int indent);
       private void EmitEnumMembers(StringBuilder sb, JsonTypeDefinition type, int indent);
   }
   ```

2. Code structure:
   ```csharp
   // <auto-generated>
   // Original IDL: {fileName}
   // </auto-generated>
   
   using System;
   using System.Collections.Generic;
   using CycloneDDS.Schema;
   
   namespace {Namespace}
   {
       [DdsExtensibility(...)]
       [DdsTopic|DdsStruct|DdsUnion]
       public partial struct {TypeName}
       {
           // Members
       }
   }
   ```

3. Type-level attribute emission:
   - Extensibility (skip if Appendable - it's default)
   - `[DdsTopic]` if type has keys, `[DdsStruct]` otherwise
   - `[DdsUnion]` for union types

4. Member emission:
   - Field attributes (`[DdsKey]`, `[DdsOptional]`, `[DdsId]`, etc.)
   - Array/string attributes (`[ArrayLength]`, `[MaxLength]`)
   - `[DdsManaged]` for collection types
   - Proper indentation and formatting

**Dependencies:** IDLIMP-003

**Success Criteria:**
- Generates valid C# syntax that compiles
- All attributes placed correctly
- Namespace matches IDL module hierarchy
- Output formatted for readability (consistent indentation)
- Generates `partial struct` for extensibility

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesSimpleStruct()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestModule::SimpleType",
            Kind = "struct",
            Extensibility = "final",
            Members = new()
            {
                new() { Name = "id", Type = "long", IsKey = true },
                new() { Name = "value", Type = "double" }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestModule::SimpleType" }, "test.idl");
    
    Assert.Contains("namespace TestModule", output);
    Assert.Contains("DdsExtensibilityKind.Final", output);
    Assert.Contains("[DdsKey]", output);
    Assert.Contains("public int id;", output);
    Assert.Contains("public double value;", output);
}

[Fact]
public void CSharpEmitter_GeneratesEnum()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestModule::Color",
            Kind = "enum",
            Members = new()
            {
                new() { Name = "RED", Value = 0 },
                new() { Name = "GREEN", Value = 1 },
                new() { Name = "BLUE", Value = 2 }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestModule::Color" }, "test.idl");
    
    Assert.Contains("public enum Color", output);
    Assert.Contains("RED = 0,", output);
    Assert.Contains("GREEN = 1,", output);
}

[Fact]
public void CSharpEmitter_SkipsDefaultExtensibility()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestModule::AppendableType",
            Kind = "struct",
            Extensibility = "appendable", // Default
            Members = new() { new() { Name = "id", Type = "long" } }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestModule::AppendableType" }, "test.idl");
    
    // Should not contain DdsExtensibility attribute (appendable is default)
    Assert.DoesNotContain("[DdsExtensibility", output);
}
```

**Reference:** [Design: Component Design - CSharpEmitter](#4-csharpemitter-code-generation)

---

### IDLIMP-007: CSharpEmitter - Collection Type Support

**Objective:** Extend `CSharpEmitter` to handle sequences, arrays, and bounded strings with proper attributes.

**Technical Details:**
1. Implement collection member emission:
   ```csharp
   private void EmitField(StringBuilder sb, JsonMember member, int indent)
   {
       var (csType, isManaged, arrayLen, bound) = _typeMapper.MapMember(member);
       
       // Emit attributes
       if (member.IsKey) sb.AppendLine($"{GetIndent(indent)}[DdsKey]");
       if (arrayLen > 0) sb.AppendLine($"{GetIndent(indent)}[ArrayLength({arrayLen})]");
       if (bound > 0) sb.AppendLine($"{GetIndent(indent)}[MaxLength({bound})]");
       if (isManaged) sb.AppendLine($"{GetIndent(indent)}[DdsManaged]");
       
       // Emit field
       sb.AppendLine($"{GetIndent(indent)}public {csType} {ToPascalCase(member.Name)};");
   }
   ```

2. Handle complex scenarios:
   - Sequences of primitives: `sequence<long>` → `List<int>`
   - Sequences of user types: `sequence<Point>` → `List<Point>`
   - Bounded sequences: `sequence<long, 10>` → `List<int>` + `[MaxLength(10)]`
   - Fixed arrays: `long arr[5]` → `int[]` + `[ArrayLength(5)]`
   - Bounded strings: `string<32>` → `string` + `[MaxLength(32)]`

3. Nested types:
   - When member type is user-defined, use simple name if in same namespace
   - Use fully qualified name if in different namespace

**Dependencies:** IDLIMP-003, IDLIMP-006

**Success Criteria:**
- All collection types generate correct C# syntax
- Attributes applied in correct order and combination
- Bounded collections include `[MaxLength]` or `[ArrayLength]`
- All collection types marked with `[DdsManaged]`
- Generated code compiles and passes CodeGen validation

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesUnboundedSequence()
{
    var member = new JsonMember 
    { 
        Name = "values", 
        Type = "long", 
        CollectionType = "sequence" 
    };
    
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestType",
            Kind = "struct",
            Members = new() { member }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestType" }, "test.idl");
    
    Assert.Contains("[DdsManaged]", output);
    Assert.Contains("public List<int> Values;", output);
}

[Fact]
public void CSharpEmitter_GeneratesBoundedSequence()
{
    var member = new JsonMember 
    { 
        Name = "values", 
        Type = "long", 
        CollectionType = "sequence",
        Bound = 10
    };
    
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestType",
            Kind = "struct",
            Members = new() { member }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestType" }, "test.idl");
    
    Assert.Contains("[MaxLength(10)]", output);
    Assert.Contains("[DdsManaged]", output);
    Assert.Contains("public List<int> Values;", output);
}

[Fact]
public void CSharpEmitter_GeneratesFixedArray()
{
    var member = new JsonMember 
    { 
        Name = "matrix", 
        Type = "double", 
        CollectionType = "array",
        Dimensions = new[] { 5 }
    };
    
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestType",
            Kind = "struct",
            Members = new() { member }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestType" }, "test.idl");
    
    Assert.Contains("[ArrayLength(5)]", output);
    Assert.Contains("[DdsManaged]", output);
    Assert.Contains("public double[] Matrix;", output);
}
```

**Reference:** [Design: Type Mapping Rules - Collection Types](#collection-types)

---

### IDLIMP-008: CSharpEmitter - Union Type Support

**Objective:** Implement union type generation with discriminator and case label handling.

**Technical Details:**
1. Detect union types:
   ```csharp
   if (type.Kind == "union")
   {
       sb.AppendLine($"{indent}[DdsUnion]");
       sb.AppendLine($"{indent}public partial struct {typeName}");
   }
   ```

2. Emit discriminator field:
   ```csharp
   // First member is discriminator
   var discriminator = type.Members[0];
   sb.AppendLine($"{indent}[DdsDiscriminator]");
   sb.AppendLine($"{indent}public {MapType(discriminator)} _d;");
   ```

3. Emit case members:
   ```csharp
   foreach (var member in type.Members.Skip(1)) // Skip discriminator
   {
       if (member.Labels != null)
       {
           foreach (var label in member.Labels)
           {
               if (label == "default")
                   sb.AppendLine($"{indent}[DdsDefaultCase]");
               else
                   sb.AppendLine($"{indent}[DdsCase({label})]");
           }
       }
       // Emit field...
   }
   ```

4. Handle special cases:
   - Multiple case labels for same field
   - Default case
   - Union with no default case

**Dependencies:** IDLIMP-006

**Success Criteria:**
- Union types emit `[DdsUnion]` attribute
- Discriminator field has `[DdsDiscriminator]` attribute
- Each case has appropriate `[DdsCase(value)]` attributes
- Default case has `[DdsDefaultCase]` attribute
- Generated unions compile and match existing union test patterns

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesUnion()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestUnion",
            Kind = "union",
            Discriminator = "long",
            Members = new()
            {
                new() { Name = "_d", Type = "long" }, // Discriminator
                new() { Name = "intVal", Type = "long", Labels = new() { "0" } },
                new() { Name = "strVal", Type = "string", Labels = new() { "1" } },
                new() { Name = "defVal", Type = "double", Labels = new() { "default" } }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestUnion" }, "test.idl");
    
    Assert.Contains("[DdsUnion]", output);
    Assert.Contains("[DdsDiscriminator]", output);
    Assert.Contains("[DdsCase(0)]", output);
    Assert.Contains("[DdsCase(1)]", output);
    Assert.Contains("[DdsDefaultCase]", output);
}
```

**Reference:** [Design: Type Mapping Rules - User-Defined Types](#user-defined-types), [Design: Type Mapping Rules - Union-Specific Mappings](#union-specific-mappings)

---

## Phase 4: CLI and Integration

### IDLIMP-009: Command-Line Interface Implementation

**Objective:** Implement a user-friendly CLI using `System.CommandLine` for the tool.

**Technical Details:**
1. Implement `Program.cs` with arguments:
   ```csharp
   var masterIdlArg = new Argument<string>("master-idl", "Path to entry-point IDL file");
   var sourceRootArg = new Argument<string>("source-root", "Root directory containing all IDL files");
   var outputRootArg = new Argument<string>("output-root", "Root directory for generated C# files");
   
   var idlcPathOption = new Option<string?>("--idlc-path", "Path to idlc executable");
   var verboseOption = new Option<bool>("--verbose", "Enable detailed logging");
   
   var rootCommand = new RootCommand("CycloneDDS IDL Importer");
   rootCommand.AddArgument(masterIdlArg);
   rootCommand.AddArgument(sourceRootArg);
   rootCommand.AddArgument(outputRootArg);
   rootCommand.AddOption(idlcPathOption);
   rootCommand.AddOption(verboseOption);
   ```

2. Add validation:
   - Master IDL file exists
   - Source root is a directory
   - Master IDL is within source root
   - Output root can be created

3. Implement logging:
   - Normal mode: Show progress (file being processed)
   - Verbose mode: Show details (types found, dependencies, path calculations)
   - Error mode: Show idlc errors and diagnostics

4. Error handling:
   - Catch and display friendly error messages
   - Return proper exit codes (0 = success, 1 = error)

**Dependencies:** IDLIMP-004, IDLIMP-005, IDLIMP-006, IDLIMP-007, IDLIMP-008

**Success Criteria:**
- CLI displays help with `--help`
- Validates arguments and displays clear errors
- Processes IDL files and generates output
- Verbose mode shows detailed information
- Returns proper exit codes for automation

**Unit Tests:**
```csharp
[Fact]
public void CLI_ValidatesArguments()
{
    var args = new[] { "nonexistent.idl", "source", "output" };
    var exitCode = Program.Main(args);
    Assert.NotEqual(0, exitCode);
}

[Fact]
public void CLI_ProcessesValidInput()
{
    // Setup test IDL files
    var args = new[] { "test.idl", "testSource", "testOutput" };
    var exitCode = Program.Main(args);
    Assert.Equal(0, exitCode);
}

[Fact]
public void CLI_VerboseModeShowsDetails()
{
    var output = new StringWriter();
    Console.SetOut(output);
    
    var args = new[] { "test.idl", "testSource", "testOutput", "--verbose" };
    Program.Main(args);
    
    var outputText = output.ToString();
    Assert.Contains("Processing:", outputText);
    Assert.Contains("types found", outputText);
}
```

**Reference:** [Design: Usage Workflow - Command-Line Interface](#command-line-interface)

---

### IDLIMP-010: End-to-End Integration with Existing Test IDL

**Objective:** Validate the tool works end-to-end using `atomic_tests.idl` and compare output to existing `AtomicTestsTypes.cs`.

**Technical Details:**
1. Copy `atomic_tests.idl` to test directory
2. Run importer:
   ```csharp
   importer.Import("atomic_tests.idl", "testIdl", "testOutput");
   ```
3. Compare generated `atomic_tests.cs` structure to `AtomicTestsTypes.cs`:
   - Same types present
   - Same field names and types
   - Same attributes
   - Functional equivalence (not necessarily identical formatting)

4. Build generated C# code:
   - Create test csproj referencing generated files
   - Add CycloneDDS.Schema reference
   - Verify compilation succeeds

5. Run CodeGen on generated C#:
   - Import CycloneDDS.targets
   - Build project
   - Verify derived IDL is generated
   - Compare derived IDL structure to original

**Dependencies:** IDLIMP-009 (all previous tasks)

**Success Criteria:**
- Successfully imports `atomic_tests.idl`
- Generated C# contains all expected types
- Generated C# compiles without errors
- CodeGen produces valid IDL from generated C#
- Derived IDL is functionally equivalent to original (validated via JSON comparison)

**Unit Tests:**
```csharp
[Fact]
public void Integration_ImportsAtomicTestsIdl()
{
    var importer = new Importer();
    importer.Import("atomic_tests.idl", "testIdl", "testOutput");
    
    Assert.True(File.Exists("testOutput/atomic_tests.cs"));
}

[Fact]
public void Integration_GeneratedCodeCompiles()
{
    // Generate C# from atomic_tests.idl
    var importer = new Importer();
    importer.Import("atomic_tests.idl", "testIdl", "testOutput");
    
    // Build test project with generated code
    var result = BuildTestProject("testOutput");
    Assert.True(result.Success);
}

[Fact]
public void Integration_CodeGenProducesEquivalentIdl()
{
    // Generate C# from atomic_tests.idl
    var importer = new Importer();
    importer.Import("atomic_tests.idl", "testIdl", "testOutput");
    
    // Build with CodeGen enabled
    var buildResult = BuildWithCodeGen("testOutput");
    Assert.True(buildResult.Success);
    
    // Compare original and derived IDL JSON
    var originalJson = RunIdlcJson("atomic_tests.idl");
    var derivedJson = RunIdlcJson("testOutput/obj/Generated/atomic_tests.idl");
    
    AssertFunctionallyEquivalent(originalJson, derivedJson);
}
```

**Reference:** [Design: Usage Workflow - End-to-End Workflow](#end-to-end-workflow), [Design: Testing Strategy](#testing-strategy)

---

## Phase 5: Advanced Features

### IDLIMP-011: Nested Struct Support

**Objective:** Handle nested struct definitions where types reference other user-defined types.

**Technical Details:**
1. Type resolution:
   - When member type is user-defined (not primitive), resolve to C# type name
   - Check if type is in same namespace (use simple name) or different namespace (use qualified name)
   
2. Emit nested structs with `[DdsStruct]` attribute
3. Ensure proper ordering in generated file (define dependencies before usage - may require topological sort)

4. Handle scenarios:
   - Struct with nested struct field: `Point2D` contains `Coordinate` struct
   - Struct with sequence of structs: `List<Point2D>`
   - Multi-level nesting: `Box` contains `Corner` which contains `Point`

**Dependencies:** IDLIMP-006, IDLIMP-007

**Success Criteria:**
- Nested struct types emit `[DdsStruct]` attribute
- Field types reference correct C# type names
- Types ordered correctly in output file (dependencies first)
- Sequences of nested types work correctly

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesNestedStruct()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "Point2D",
            Kind = "struct",
            Members = new()
            {
                new() { Name = "x", Type = "double" },
                new() { Name = "y", Type = "double" }
            }
        },
        new()
        {
            Name = "Container",
            Kind = "struct",
            Members = new()
            {
                new() { Name = "id", Type = "long" },
                new() { Name = "point", Type = "Point2D" }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "Point2D", "Container" }, "test.idl");
    
    Assert.Contains("[DdsStruct]", output);
    Assert.Contains("public Point2D point;", output);
}

[Fact]
public void CSharpEmitter_GeneratesSequenceOfStructs()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "Point2D",
            Kind = "struct",
            Members = new() { new() { Name = "x", Type = "double" } }
        },
        new()
        {
            Name = "Path",
            Kind = "struct",
            Members = new()
            {
                new() 
                { 
                    Name = "points", 
                    Type = "Point2D", 
                    CollectionType = "sequence" 
                }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "Point2D", "Path" }, "test.idl");
    
    Assert.Contains("public List<Point2D> Points;", output);
    Assert.Contains("[DdsManaged]", output);
}
```

**Reference:** [Design: Type Mapping Rules - User-Defined Types](#user-defined-types)

---

### IDLIMP-012: Optional Member Support

**Objective:** Handle `@optional` annotation for optional struct members.

**Technical Details:**
1. Detect optional flag in JSON:
   ```csharp
   if (member.IsOptional)
   {
       sb.AppendLine($"{indent}[DdsOptional]");
   }
   ```

2. Optional members requirements:
   - Only valid in Appendable and Mutable extensibility
   - Should be emitted after non-optional members (CodeGen may enforce this)

3. Handle combination with other attributes:
   ```csharp
   [DdsOptional]
   [DdsId(10)]
   [MaxLength(256)]
   [DdsManaged]
   public string? OptionalField;
   ```

**Dependencies:** IDLIMP-006

**Success Criteria:**
- Optional members emit `[DdsOptional]` attribute
- Works correctly with other attributes
- Generated code compiles and validates in CodeGen
- Roundtrip test with optional fields succeeds

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesOptionalMember()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestType",
            Kind = "struct",
            Extensibility = "appendable",
            Members = new()
            {
                new() { Name = "required", Type = "long" },
                new() { Name = "optional", Type = "long", IsOptional = true }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestType" }, "test.idl");
    
    Assert.Contains("[DdsOptional]", output);
    Assert.Contains("public int Optional;", output);
}
```

**Reference:** [Design: Type Mapping Rules - Annotations](#annotations)

---

### IDLIMP-013: Member ID (@id) Support

**Objective:** Handle explicit member ID annotations for Mutable types.

**Technical Details:**
1. Detect member ID in JSON:
   ```csharp
   if (member.Id.HasValue)
   {
       sb.AppendLine($"{indent}[DdsId({member.Id.Value})]");
   }
   ```

2. Member IDs:
   - Only valid in Mutable extensibility
   - Must be unique within type
   - Used for version compatibility

**Dependencies:** IDLIMP-006

**Success Criteria:**
- Member IDs emit `[DdsId(N)]` attribute
- IDs preserved through roundtrip
- Works with Mutable types

**Unit Tests:**
```csharp
[Fact]
public void CSharpEmitter_GeneratesMemberIds()
{
    var types = new List<JsonTypeDefinition>
    {
        new()
        {
            Name = "TestType",
            Kind = "struct",
            Extensibility = "mutable",
            Members = new()
            {
                new() { Name = "field1", Type = "long", Id = 100 },
                new() { Name = "field2", Type = "double", Id = 200 }
            }
        }
    };
    
    var emitter = new CSharpEmitter(types);
    var output = emitter.GenerateCSharp(new[] { "TestType" }, "test.idl");
    
    Assert.Contains("[DdsId(100)]", output);
    Assert.Contains("[DdsId(200)]", output);
}
```

**Reference:** [Design: Type Mapping Rules - Annotations](#annotations)

---

## Phase 6: Testing Infrastructure

### IDLIMP-014: Comprehensive Unit Test Suite

**Objective:** Create a complete unit test suite covering all tool components with high code coverage.

**Technical Details:**
1. Create test project: `CycloneDDS.IdlImporter.Tests.csproj`
2. Test categories:
   - **TypeMapper tests**: All type mappings (30+ tests)
   - **Importer tests**: File processing, recursion, deduplication (15+ tests)
   - **Emitter tests**: Code generation for all type constructs (40+ tests)
   - **CLI tests**: Argument validation, error handling (10+ tests)
   - **Integration tests**: End-to-end workflows (5+ tests)

3. Use test fixtures for common setups:
   ```csharp
   public class TypeMapperFixture
   {
       public TypeMapper Mapper { get; } = new();
       public List<JsonTypeDefinition> SampleTypes { get; } = CreateSampleTypes();
   }
   ```

4. Test all edge cases:
   - Empty files
   - Circular includes
   - Invalid JSON
   - Missing dependencies
   - Name collisions

**Dependencies:** IDLIMP-010

**Success Criteria:**
- Minimum 90% code coverage
- All public APIs have unit tests
- All edge cases covered
- Tests are maintainable and well-documented
- CI/CD pipeline configured to run tests

**Unit Test Categories:**
```csharp
public class TypeMapperTests { /* 30+ tests */ }
public class ImporterCoreTests { /* 15+ tests */ }
public class CSharpEmitterStructTests { /* 20+ tests */ }
public class CSharpEmitterCollectionTests { /* 15+ tests */ }
public class CSharpEmitterUnionTests { /* 10+ tests */ }
public class CLITests { /* 10+ tests */ }
public class IntegrationTests { /* 5+ tests */ }
```

**Reference:** [Design: Testing Strategy](#testing-strategy)

---

### IDLIMP-015: Roundtrip Validation Test Suite

**Objective:** Create roundtrip tests that validate wire compatibility between original and imported types.

**Technical Details:**
1. Test workflow:
   ```
   Original IDL → Import to C# → CodeGen → Derived IDL
   Original IDL → idlc -l json → JSON_A
   Derived IDL → idlc -l json → JSON_B
   Compare JSON_A and JSON_B (structural equivalence)
   ```

2. Semantic comparison logic:
   - Type names may differ (resolved typedefs)
   - Field order must match
   - Field types (wire types) must match
   - Extensibility must match
   - Keys must match (same offsets and types)
   - Serialization opcodes must match

3. Create comparison utility:
   ```csharp
   public class IdlEquivalenceValidator
   {
       public bool AreEquivalent(IdlJsonRoot original, IdlJsonRoot derived);
       public List<string> GetDifferences(IdlJsonRoot original, IdlJsonRoot derived);
   }
   ```

4. Test sets:
   - All atomic test types (primitives, strings, sequences, arrays)
   - Nested structures
   - Unions with all case types
   - Mutable types with member IDs
   - Types with optional members

**Dependencies:** IDLIMP-014

**Success Criteria:**
- All atomic test types roundtrip successfully
- Functional equivalence validation passes
- Serialization opcodes match between original and derived
- Tests run automatically in CI/CD
- Clear diagnostic messages when roundtrip fails

**Unit Tests:**
```csharp
[Theory]
[MemberData(nameof(AtomicTestTypes))]
public void Roundtrip_AtomicType_ProducesFunctionallyEquivalentIdl(string typeName)
{
    // Import atomic_tests.idl → C#
    var importer = new Importer();
    importer.Import("atomic_tests.idl", "testIdl", "testOutput");
    
    // Build with CodeGen → derived IDL
    BuildWithCodeGen("testOutput");
    
    // Compare JSONs
    var originalJson = RunIdlcJson($"atomic_tests.idl");
    var derivedJson = RunIdlcJson($"testOutput/obj/Generated/atomic_tests.idl");
    
    var validator = new IdlEquivalenceValidator();
    var equivalent = validator.AreEquivalent(originalJson, derivedJson);
    
    Assert.True(equivalent, validator.GetDifferences(originalJson, derivedJson));
}

[Fact]
public void Roundtrip_ComplexNestedType_PreservesWireFormat()
{
    // Test doubly nested structures
    var importer = new Importer();
    importer.Import("nested_test.idl", "testIdl", "testOutput");
    
    BuildWithCodeGen("testOutput");
    
    // Compare serialization opcodes
    var originalOps = ExtractOpcodes("nested_test.idl", "ComplexNested");
    var derivedOps = ExtractOpcodes("testOutput/obj/Generated/nested_test.idl", "ComplexNested");
    
    Assert.Equal(originalOps, derivedOps);
}
```

**Reference:** [Design: Testing Strategy - Roundtrip Tests](#roundtrip-tests)

---

## Summary

This task breakdown provides 15 distinct, testable tasks organized into 6 phases:

- **Phase 1 (Foundation)**: 3 tasks - project setup, shared infrastructure
- **Phase 2 (Core Logic)**: 2 tasks - importer orchestration, JSON parsing
- **Phase 3 (Generation)**: 4 tasks - C# code emission for all type constructs
- **Phase 4 (Integration)**: 2 tasks - CLI and end-to-end validation
- **Phase 5 (Advanced)**: 3 tasks - nested types, optional, member IDs
- **Phase 6 (Testing)**: 2 tasks - comprehensive unit and roundtrip tests

Each task is:
- **Self-contained**: Can be implemented independently after dependencies
- **Testable**: Has clear success criteria and unit test requirements
- **Traceable**: References design document sections
- **Estimable**: Scoped for 1-3 days of focused development

**Total Estimated Effort:** 30-45 development days for complete implementation with full test coverage.
