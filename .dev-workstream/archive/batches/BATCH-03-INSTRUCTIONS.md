# BATCH-03: Stage 2 Foundation - Schema Package & CLI Tool Infrastructure

**Batch Number:** BATCH-03  
**Tasks:** FCDC-S006 (Schema Package Migration), FCDC-S007 (CLI Tool Generator Infrastructure)  
**Phase:** Stage 2 - Code Generation (Foundation)  
**Estimated Effort:** 8-10 hours  
**Priority:** CRITICAL (unlocks all code generation)  
**Dependencies:** BATCH-02, BATCH-02.1 (Stage 1 complete)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch establishes the **foundation for Stage 2** by migrating the schema package and setting up the CLI tool infrastructure for code generation.

**Your Mission:** 
1. Migrate the schema attribute package (types developers will use to mark their DDS topics)
2. Set up the CLI tool infrastructure that will parse C# files and generate serialization code

**Critical Context:** We are **NOT using a Roslyn IIncrementalGenerator plugin**. Instead, we use a proven **CLI tool** (`CycloneDDS.CodeGen.exe`) that runs at build time via MSBuild. This approach is easier to debug, deterministic, and leverages existing infrastructure.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Previous Reviews:** 
   - `.dev-workstream/reviews/BATCH-01-REVIEW.md` - Learn from CDR feedback
   - `.dev-workstream/reviews/BATCH-02-REVIEW.md` - Golden Rig validation
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - See FCDC-S006, FCDC-S007
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Sections 5.1 (Packages), 4 (Stage 2)
5. **Architecture Update:** `docs/DESIGN-UPDATES-CLI-TOOL.md` - **CRITICAL - explains CLI tool approach**
6. **Old Implementation:** `old_implem/src/CycloneDDS.Schema/` - Package to migrate
7. **Old CLI Tool:** `old_implem/tools/CycloneDDS.CodeGen/` - Infrastructure to adapt

### Source Code Location

- **New Schema Package:** `Src/CycloneDDS.Schema/` (create from old_implem)
- **New CLI Tool:** `tools/CycloneDDS.CodeGen/` (create from old_implem)
- **Test Projects:** 
  - `tests/CycloneDDS.Schema.Tests/` (schema validation tests)
  - `tests/CycloneDDS.CodeGen.Tests/` (generator infrastructure tests)

### Report Submission

**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-03-REPORT.md`

**Use template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-03-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (Schema Package):** Migrate → Write tests → **ALL tests pass** ✅
2. **Task 2 (CLI Tool):** Set up → Write tests → **ALL tests pass** ✅

**DO NOT** move to Task 2 until:
- ✅ Task 1 implementation complete
- ✅ Task 1 tests written
- ✅ **ALL tests passing** (including BATCH-01, BATCH-02 tests)

**Why:** Schema package is required by CLI tool. Must be solid before building generator.

---

## Context

**Stage 1 Complete:** CDR serialization primitives proven correct via Golden Rig.

**Stage 2 Goal:** Generate XCDR2-compliant serialization code from C# schema types marked with `[DdsTopic]`.

**This Batch:** Establishes the foundation:
- Schema attributes developers use to define DDS topics
- CLI tool that discovers these types and generates code

**Related Tasks:**
- [FCDC-S006](../docs/SERDATA-TASK-MASTER.md#fcdc-s006-schema-package-migration) - Schema attributes
- [FCDC-S007](../docs/SERDATA-TASK-MASTER.md#fcdc-s007-cli-tool-generator-infrastructure) - CLI tool setup

**Why CLI Tool (Not Roslyn Plugin):**
- Runs only at build time (not on every keystroke)
- Easy to debug (standard console app)
- No "ghost generation" or caching issues
- Proven approach from old implementation

See `docs/DESIGN-UPDATES-CLI-TOOL.md` for detailed rationale.

---

## 🎯 Batch Objectives

**Primary Goal:** Migrate schema package and establish CLI tool infrastructure for code generation.

**Success Metrics:** 
- Schema package compiles, attributes usable
- CLI tool runs successfully, discovers `[DdsTopic]` types
- All tests pass

---

## ✅ Tasks

### Task 1: Schema Package Migration (FCDC-S006)

**Files:** `Src/CycloneDDS.Schema/` (NEW directory, copy from old_implem)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s006-schema-package-migration)

**Description:**  
Migrate the schema attribute package from `old_implem/src/CycloneDDS.Schema/` to the new project. This package provides the attributes developers use to mark their DDS topics.

**Design Reference:** [SERDATA-DESIGN.md §5.1](../docs/SERDATA-DESIGN.md), old_implem/docs/FCDC-DETAILED-DESIGN.md §4.3

**Actions:**

1. **Copy Package:**
   ```bash
   # From: old_implem/src/CycloneDDS.Schema/
   # To: Src/CycloneDDS.Schema/
   ```

2. **Update Project File:**
   - Target: `net8.0`
   - PackageId: `CycloneDDS.Schema`
   - Description: "DDS schema attributes for C# types"

3. **Add New Attribute (if not exists):**
   ```csharp
   [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
   public sealed class DdsManagedAttribute : Attribute
   {
       // Opt-in for managed types (string, List<T>) that allow GC allocations
   }
   ```

**Expected Attributes:**
- `[DdsTopic]` - Marks a type as a DDS topic
- `[DdsKey]` - Marks a field as part of the topic key
- `[DdsQos(...)]` - QoS settings
- `[DdsUnion]` - Marks a union type
- `[DdsDiscriminator]` - Union discriminator field
- `[DdsCase(...)]` - Union case values
- `[DdsOptional]` - Optional (nullable) member
- `[DdsManaged]` - **NEW** - Opt-in for managed types

**Expected Wrapper Types:**
- `FixedString32`, `FixedString64`, `FixedString128`  256` - Fixed-size UTF-8 strings
- `BoundedSeq<T, N>` - Bounded sequence (max length N)

**No Code Changes Needed:** Attributes are compatible as-is.

**Tests Required:** (Create `tests/CycloneDDS.Schema.Tests/`)

**Minimum 8-10 tests:**
1. ✅ `[DdsTopic]` can be applied to struct
2. ✅ `[DdsTopic]` can be applied to class
3. ✅ `[DdsKey]` can be applied to field
4. ✅ `[DdsUnion]` targets are correct
5. ✅ `[DdsManaged]` can be applied to struct/class
6. ✅ FixedString32 has correct size (32 bytes)
7. ✅ FixedString64 has correct size (64 bytes)
8. ✅ BoundedSeq<int, 10> enforces max length
9. ✅ Attribute constructors accept correct parameters
10. ✅ QoS attribute stores reliability/durability settings

**Estimated Time:** 3-4 hours

---

### Task 2: CLI Tool Generator Infrastructure (FCDC-S007)

**Files:** `tools/CycloneDDS.CodeGen/` (NEW directory, adapt from old_implem)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s007-cli-tool-generator-infrastructure)

**Description:**  
Set up the CLI tool infrastructure that will parse C# source files and generate serialization code. This is a **Console Application** (not a Roslyn plugin).

**Design Reference:** [DESIGN-UPDATES-CLI-TOOL.md](../docs/DESIGN-UPDATES-CLI-TOOL.md), [SERDATA-DESIGN.md §4 Stage 2](../docs/SERDATA-DESIGN.md)

**⚠️ CRITICAL: This is a CLI TOOL, NOT a Roslyn IIncrementalGenerator plugin.**

**Why:**
- Runs only at build time (via MSBuild target)
- Easy to debug (standard console app)
- No caching complexity
- Uses `Microsoft.CodeAnalysis` to parse files from disk

**Actions:**

1. **Create Project:**
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>Exe</OutputType>
       <TargetFramework>net8.0</TargetFramework>
       <PackAsTool>false</PackAsTool>
     </PropertyGroup>
     
     <ItemGroup>
       <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
       <ProjectReference Include="..\..\Src\CycloneDDS.Schema\CycloneDDS.Schema.csproj" />
       <ProjectReference Include="..\..\Src\CycloneDDS.Core\CycloneDDS.Core.csproj" />
     </ItemGroup>
   </Project>
   ```

2. **Implement Program.cs:**
   ```csharp
   using System;
   using System.IO;
   using System.Linq;
   using Microsoft.CodeAnalysis;
   using Microsoft.CodeAnalysis.CSharp;
   
   namespace CycloneDDS.CodeGen
   {
       class Program
       {
           static int Main(string[] args)
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
   ```

3. **Implement SchemaDiscovery.cs:**
   ```csharp
   public class SchemaDiscovery
   {
       public List<TypeInfo> DiscoverTopics(string sourceDirectory)
       {
           // 1. Find all .cs files
           var files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
           
           // 2. Parse into syntax trees
           var syntaxTrees = files.Select(f => 
               CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f)).ToList();
           
           // 3. Create compilation
           var compilation = CSharpCompilation.Create("Discovery")
               .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
               .AddSyntaxTrees(syntaxTrees);
           
           // 4. Find types with [DdsTopic]
           var topics = new List<TypeInfo>();
           foreach (var tree in syntaxTrees)
           {
               var semanticModel = compilation.GetSemanticModel(tree);
               var root = tree.GetRoot();
               
               // Walk syntax tree, find types with [DdsTopic]
               // (Implementation details for next batch - for now just structure)
           }
           
           return topics;
       }
   }
   ```

4. **Implement CodeGenerator.cs (Skeleton):**
   ```csharp
   public class CodeGenerator
   {
       private readonly SchemaDiscovery _discovery = new();
       
       public void Generate(string sourceDir, string outputDir)
       {
           Console.WriteLine($"Discovering topics in: {sourceDir}");
           var topics = _discovery.DiscoverTopics(sourceDir);
           
           Console.WriteLine($"Found {topics.Count} topic(s)");
           
           foreach (var topic in topics)
           {
               Console.WriteLine($"  - {topic.Name}");
               // Code generation in next batch
           }
           
           Console.WriteLine($"Output will go to: {outputDir}");
       }
   }
   ```

5. **Create MSBuild .targets File:**
   ```xml
   <!-- tools/CycloneDDS.CodeGen/CycloneDDS.targets -->
   <Project>
     <Target Name="CycloneDDSCodeGen" BeforeTargets="CoreCompile">
       <PropertyGroup>
         <CodeGenToolPath>$(MSBuildThisFileDirectory)..\tools\net8.0\CycloneDDS.CodeGen.exe</CodeGenToolPath>
         <SourcePath>$(MSBuildProjectDirectory)</SourcePath>
         <OutputPath>$(MSBuildProjectDirectory)\obj\Generated</OutputPath>
       </PropertyGroup>
       
       <Message Text="Running CycloneDDS Code Generator..." Importance="high" />
       <Exec Command="&quot;$(CodeGenToolPath)&quot; &quot;$(SourcePath)&quot; &quot;$(OutputPath)&quot;" />
     </Target>
   </Project>
   ```

**Deliverables:**
- `tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj`
- `tools/CycloneDDS.CodeGen/Program.cs`
- `tools/CycloneDDS.CodeGen/SchemaDiscovery.cs`
- `tools/CycloneDDS.CodeGen/CodeGenerator.cs`
- `tools/CycloneDDS.CodeGen/CycloneDDS.targets` (MSBuild integration)
- `tools/CycloneDDS.CodeGen/TypeInfo.cs` (data model for discovered types)

**Tests Required:** (Create `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 10-12 tests:**
1. ✅ CLI tool accepts source and output directory arguments
2. ✅ CLI tool returns error if arguments missing
3. ✅ SchemaDiscovery finds .cs files in directory
4. ✅ SchemaDiscovery parses files into syntax trees
5. ✅ SchemaDiscovery creates compilation successfully
6. ✅ SchemaDiscovery discovers type with `[DdsTopic]`
7. ✅ SchemaDiscovery ignores type without `[DdsTopic]`
8. ✅ CodeGenerator calls SchemaDiscovery
9. ✅ CodeGenerator reports discovered topic count
10. ✅ Tool runs end-to-end (discovery + skeleton generation)
11. ✅ MSBuild target can be imported (XML validation)
12. ✅ Error handling for invalid source directory

**Quality Standard:**
- **NOT ACCEPTABLE:** Tests that just check "method exists" or "returns non-null"
- **REQUIRED:** Tests that verify actual discovery behavior with real C# code samples

**Example Good Test:**
```csharp
[Fact]
public void Discovers_Type_With_DdsTopic_Attribute()
{
    // Create temp directory with C# file
    string sourceCode = @"
        using CycloneDDS.Schema;
        
        [DdsTopic]
        public struct SensorData
        {
            public int Id;
            public double Value;
        }
    ";
    
    var tempDir = CreateTempFileWithCode(sourceCode);
    
    var discovery = new SchemaDiscovery();
    var topics = discovery.DiscoverTopics(tempDir);
    
    Assert.Single(topics);
    Assert.Equal("SensorData", topics[0].Name);
}
```

**Estimated Time:** 5-6 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 18-22 tests

**Test Distribution:**
- CycloneDDS.Schema.Tests: 8-10 tests
- CycloneDDS.CodeGen.Tests: 10-12 tests

**Test Quality Standards:**

**✅ REQUIRED:**
- Tests verify **actual behavior** (discovery finds types, attributes apply correctly)
- Use real C# code samples for discovery tests
- Verify CLI tool can be invoked programmatically

**❌ NOT ACCEPTABLE:**
- Tests that only check compilation
- Tests that don't verify discovery logic
- Shallow tests (e.g., "tool exists" without running it)

**All tests must pass before submitting report.**

---

## 📊 Report Requirements

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Required Sections:**

1. **Implementation Summary**
   - Tasks completed (FCDC-S006, FCDC-S007)
   - Test counts
   - Any deviations from instructions

2. **Issues Encountered**
   - Problems with old code migration?
   - Roslyn API challenges?
   - MSBuild target integration issues?

3. **Design Decisions**
   - How did you handle file discovery?
   - What TypeInfo structure did you use?
   - Any simplifications or improvements?

4. **Weak Points Spotted**
   - Old code quality issues?
   - Areas needing refactoring later?
   - Documentation gaps?

5. **Next Steps**
   - What's needed for actual code emission (FCDC-S010)?
   - Dependencies on other tasks?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S006** Complete: Schema package migrated, 8-10 tests pass
- ✅ **FCDC-S007** Complete: CLI tool infrastructure set up, 10-12 tests pass
- ✅ All 18-22 tests passing (plus BATCH-01/02 tests still pass)
- ✅ No compiler warnings
- ✅ CLI tool can be invoked: `dotnet run --project tools/CycloneDDS.CodeGen -- <source> <output>`
- ✅ MSBuild .targets file created (will be tested in future batches)
- ✅ Report submitted to `.dev-workstream/reports/BATCH-03-REPORT.md`

**GATE:** CLI tool must successfully discover `[DdsTopic]` types before moving to code emission (BATCH-04).

---

## ⚠️ Common Pitfalls to Avoid

1. **Trying to use Roslyn IIncrementalGenerator:** We use CLI tool, not compiler plugin
   - Correct: Console app with `Microsoft.CodeAnalysis`
   
2. **Not testing actual discovery:** Tests must use real C# code
   - Wrong: "method returns list"
   - Right: "discovers SensorData type from sample code"

3. **Forgetting MSBuild integration:** .targets file is critical
   - Must create `CycloneDDS.targets` for future builds

4. **Over-engineering discovery logic:** Keep it simple for now
   - Just find types with `[DdsTopic]` - detailed analysis in next batch

5. **Not running end-to-end:** Test tool actually works
   - Create temp directory with sample .cs file
   - Run tool, verify it finds the type

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S006, S007
- **Design:** [SERDATA-DESIGN.md](../docs/SERDATA-DESIGN.md) - Section 4, 5.1
- **CLI Tool Rationale:** [DESIGN-UPDATES-CLI-TOOL.md](../docs/DESIGN-UPDATES-CLI-TOOL.md)
- **Old Schema Package:** `old_implem/src/CycloneDDS.Schema/`
- **Old CLI Tool:** `old_implem/tools/CycloneDDS.CodeGen/` (adapt infrastructure)
- **Roslyn Docs:** Microsoft.CodeAnalysis.CSharp API

---

**Next Batch:** BATCH-04 (Schema Validator + IDL Emitter) - Validates discovered types and generates IDL
