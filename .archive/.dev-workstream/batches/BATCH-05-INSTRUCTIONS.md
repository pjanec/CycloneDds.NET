# BATCH-05: IDL Compiler Integration & Descriptor Parsing

**Batch Number:** BATCH-05  
**Tasks:** FCDC-S008b (IDL Compiler Orchestration), FCDC-S009b (Descriptor Parser)  
**Phase:** Stage 2 - Code Generation (External Tool Integration)  
**Estimated Effort:** 10-12 hours  
**Priority:** CRITICAL (required for serializer generation)  
**Dependencies:** BATCH-04 (IDL generation)

---

## 📋 Onboarding & Workflow

### Developer Instructions

This batch integrates the external `idlc` compiler and implements robust descriptor parsing. You'll invoke `idlc.exe` on generated `.idl` files and extract type metadata using CppAst (libclang).

**Your Mission:** 
1. Implement logic to run external `idlc.exe` compiler process
2. Parse `idlc` output (C descriptor files) using CppAst to extract `m_ops` and `m_keys` metadata

**Critical Context:** We use `CppAst` (libclang) instead of fragile Regex parsing. This ensures reliable extraction regardless of `idlc` output formatting changes.

### Required Reading (IN ORDER)

1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Previous Reviews:** 
   - `.dev-workstream/reviews/BATCH-03-REVIEW.md` - Test quality standards
   - `.dev-workstream/reviews/BATCH-04-REVIEW.md` - Validation & IDL generation
3. **Task Master:** `docs/SERDATA-TASK-MASTER.md` - See FCDC-S008b, FCDC-S009b
4. **Design Document:** `docs/SERDATA-DESIGN.md` - Section 4 (Stage 2), Section 5.1 (Packages)
5. **Architecture Update:** `docs/DESIGN-UPDATES-CLI-TOOL.md` - **CLI tool approach rationale**
6. **Old Implementation:** `old_implem/src/CycloneDDS.Generator/DescriptorExtractor.cs` - Old regex-based parsing (DON'T use this approach)

### Source Code Location

- **CLI Tool:** `tools/CycloneDDS.CodeGen/` (extend from BATCH-04)
- **Test Project:** `tests/CycloneDDS.CodeGen.Tests/` (extend from BATCH-04)

### Report Submission

**⚠️ ⚠️ ⚠️ CRITICAL: REPORT FOLDER LOCATION ⚠️ ⚠️ ⚠️**

**When done, you MUST submit your report to this EXACT location:**  
**`.dev-workstream/reports/BATCH-05-REPORT.md`**

**NOT to:** `reports/`

**Correct folder:** `.dev-workstream/reports`  
**Incorrect folder:** `reports`  

**Double-check before submitting:** Your report goes in **reports/BATCH-05-REPORT.md**

**Use template:**  
`.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-05-QUESTIONS.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (IDL Compiler Orchestration):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (Descriptor Parser):** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to Task 2 until:
- ✅ Task 1 implementation complete
- ✅ Task 1 tests written
- ✅ **ALL tests passing** (including all previous batches)

**Why:** Descriptor parser depends on `idlc` output files.

---

## Context

**BATCH-04 Complete:** Schema validator and IDL generator produce `.idl` files.

**This Batch:** Run external tools and extract metadata:
- Invoke `idlc.exe` on `.idl` files → generates C descriptor code
- Parse C code using CppAst → extract `m_ops` (serialization ops) and `m_keys` (key fields)

**Related Tasks:**
- [FCDC-S008b](../docs/SERDATA-TASK-MASTER.md#fcdc-s008b-idl-compiler-orchestration) - External process management
- [FCDC-S009b](../docs/SERDATA-TASK-MASTER.md#fcdc-s009b-descriptor-parser-cppast-replacement) - Robust C parsing

**Why CppAst (not Regex):**
- `idlc` output formatting can change
- Regex breaks on minor changes (whitespace, etc.)
- CppAst parses C semantic tree - reliable regardless of formatting

---

## 🎯 Batch Objectives

**Primary Goal:** Integrate external `idlc` compiler and extract type descriptors robustly.

**Success Metrics:** 
- CLI tool runs `idlc.exe` successfully, captures output
- Descriptor parser extracts `m_ops` and `m_keys` from C files
- All tests pass

---

## ✅ Tasks

### Task 1: IDL Compiler Orchestration (FCDC-S008b)

**Files:** `tools/CycloneDDS.CodeGen/IdlcRunner.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s008b-idl-compiler-orchestration)

**Description:**  
Implement logic to locate and invoke the external `idlc.exe` compiler that ships with Cyclone DDS.

**Design Reference:** [DESIGN-UPDATES-CLI-TOOL.md](../docs/DESIGN-UPDATES-CLI-TOOL.md), [SERDATA-DESIGN.md §4](../docs/SERDATA-DESIGN.md)

**Responsibilities:**

#### 1. Locate `idlc.exe`

```csharp
public class IdlcRunner
{
    public string FindIdlc()
    {
        // Check environment variable
        string cycloneHome = Environment.GetEnvironmentVariable("CYCLONEDDS_HOME");
        if (!string.IsNullOrEmpty(cycloneHome))
        {
            string path = Path.Combine(cycloneHome, "bin", "idlc.exe");
            if (File.Exists(path))
                return path;
        }
        
        // Check PATH
        string pathEnv = Environment.GetEnvironmentVariable("PATH");
        foreach (var dir in pathEnv.Split(';'))
        {
            string path = Path.Combine(dir, "idlc.exe");
            if (File.Exists(path))
                return path;
        }
        
        throw new FileNotFoundException("idlc.exe not found. Set CYCLONEDDS_HOME or add to PATH.");
    }
}
```

#### 2. Execute `idlc` Process

```csharp
public class IdlcRunner
{
    public IdlcResult RunIdlc(string idlFilePath, string outputDir)
    {
        string idlcPath = FindIdlc();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = idlcPath,
            Arguments = $"-l c -o \"{outputDir}\" \"{idlFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        var process = Process.Start(startInfo);
        
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        
        process.WaitForExit();
        
        return new IdlcResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            GeneratedFiles = FindGeneratedFiles(outputDir, idlFilePath)
        };
    }
    
    private string[] FindGeneratedFiles(string outputDir, string idlFile)
    {
        // idlc generates: <basename>.c and <basename>.h
        string baseName = Path.GetFileNameWithoutExtension(idlFile);
        return new[]
        {
            Path.Combine(outputDir, baseName + ".c"),
            Path.Combine(outputDir, baseName + ".h")
        };
    }
}
```

#### 3. Pipe Output to MSBuild Logging

When called from MSBuild (future integration):

```csharp
public void RunIdlcWithLogging(string idlFile, string outputDir, ILogger logger)
{
    var result = RunIdlc(idlFile, outputDir);
    
    if (!string.IsNullOrEmpty(result.StandardOutput))
        logger.LogMessage(MessageImportance.Normal, result.StandardOutput);
    
    if (!string.IsNullOrEmpty(result.StandardError))
        logger.LogError(result.StandardError);
    
    if (result.ExitCode != 0)
        throw new Exception($"idlc failed with exit code {result.ExitCode}");
}
```

**Error Handling:**
- `idlc` not found → clear error message
- `idlc` fails → include stderr in exception
- Generated files missing → verify output files exist

**Deliverables:**
- `tools/CycloneDDS.CodeGen/IdlcRunner.cs`
- `tools/CycloneDDS.CodeGen/IdlcResult.cs`
- Integration into `CodeGenerator.cs` (call IdlcRunner after IDL emission)

**Tests Required:** (Add to `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 8-10 tests:**
1. ✅ FindIdlc locates `idlc.exe` from CYCLONEDDS_HOME
2. ✅ FindIdlc locates `idlc.exe` from PATH
3. ✅ FindIdlc throws if `idlc.exe` not found
4. ✅ RunIdlc executes process successfully
5. ✅ RunIdlc captures stdout
6. ✅ RunIdlc captures stderr
7. ✅ RunIdlc reports exit code
8. ✅ RunIdlc finds generated .c and .h files
9. ✅ RunIdlc throws if `idlc` returns non-zero exit code
10. ✅ RunIdlc works with temp directory and actual `.idl` file

**Quality Standard:**
- Tests must use **actual temp files** and run real process (if `idlc` available)
- **OR** use mock process for CI environments without `idlc`
- Tests verify **actual exit codes, stdout, stderr**

**Example Good Test:**
```csharp
[Fact]
public void RunIdlc_GeneratesOutputFiles()
{
    // Create temp .idl file
    string idlContent = @"
        @appendable
        struct TestData {
            int32 id;
        };
    ";
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(tempDir);
    
    string idlFile = Path.Combine(tempDir, "TestData.idl");
    File.WriteAllText(idlFile, idlContent);
    
    var runner = new IdlcRunner();
    var result = runner.RunIdlc(idlFile, tempDir);
    
    Assert.Equal(0, result.ExitCode);
    Assert.True(File.Exists(Path.Combine(tempDir, "TestData.c")));
    Assert.True(File.Exists(Path.Combine(tempDir, "TestData.h")));
}
```

**Estimated Time:** 4-5 hours

---

### Task 2: Descriptor Parser with CppAst (FCDC-S009b)

**Files:** `tools/CycloneDDS.CodeGen/DescriptorParser.cs` (NEW)  
**Task Definition:** See [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md#fcdc-s009b-descriptor-parser-cppast-replacement)

**Description:**  
Parse `idlc`-generated C files using `CppAst` (libclang) to extract descriptor metadata (`m_ops` and `m_keys`).

**Design Reference:** [DESIGN-UPDATES-CLI-TOOL.md](../docs/DESIGN-UPDATES-CLI-TOOL.md), design-talk.md §2233-2256

**⚠️ CRITICAL: Use CppAst, NOT Regex**

**Why:** `idlc` output format can change. CppAst parses C semantic tree - reliable regardless of whitespace/formatting.

**Add NuGet Package:**
```xml
<PackageReference Include="CppAst" Version="0.7.0" />
```

**Implementation:**

#### 1. Parse C File with CppAst

```csharp
using CppAst;

public class DescriptorParser
{
    public DescriptorMetadata ParseDescriptor(string cFilePath)
    {
        var options = new CppParserOptions
        {
            ParseMacros = true,
            ParseComments = false
        };
        
        var compilation = CppParser.ParseFile(cFilePath, options);
        
        if (compilation.HasErrors)
        {
            var errors = string.Join("\n", compilation.Diagnostics.Messages);
            throw new Exception($"CppAst parsing failed: {errors}");
        }
        
        return ExtractMetadata(compilation);
    }
}
```

#### 2. Extract `m_ops` Array

`idlc` generates descriptor like this:

```c
static const uint32_t TestData_ops[] = {
    DDS_OP_ADR | DDS_OP_TYPE_4BY, offsetof(TestData, id),
    DDS_OP_RTS
};
```

**Extract with CppAst:**

```csharp
private DescriptorMetadata ExtractMetadata(CppCompilation compilation)
{
    DescriptorMetadata metadata = new();
    
    foreach (var variable in compilation.Variables)
    {
        if (variable.Name.EndsWith("_ops") && variable.Type is CppArrayType arrayType)
        {
            metadata.OpsArrayName = variable.Name;
            metadata.OpsValues = ExtractArrayInitializer(variable.InitValue);
        }
        
        if (variable.Name.EndsWith("_keys") && variable.Type is CppArrayType)
        {
            metadata.KeysArrayName = variable.Name;
            metadata.KeysValues = ExtractArrayInitializer(variable.InitValue);
        }
    }
    
    return metadata;
}

private uint[] ExtractArrayInitializer(CppExpression initExpr)
{
    // Parse initializer list: { value1, value2, ... }
    // CppAst provides structured access to initializer expressions
    
    if (initExpr is CppInitListExpression initList)
    {
        var values = new List<uint>();
        foreach (var item in initList.Arguments)
        {
            // Handle DDS_OP_ADR | DDS_OP_TYPE_4BY expressions
            // Extract numeric values
            values.Add(EvaluateExpression(item));
        }
        return values.ToArray();
    }
    
    throw new Exception("Unexpected initializer format");
}
```

#### 3. Extract Type Name

From descriptor name `TestData_ops`, extract type name `TestData`.

```csharp
private string ExtractTypeName(string descriptorName)
{
    // TestData_ops → TestData
    // TestData_keys → TestData
    return descriptorName.Replace("_ops", "").Replace("_keys", "");
}
```

**Deliverables:**
- `tools/CycloneDDS.CodeGen/DescriptorParser.cs`
- `tools/CycloneDDS.CodeGen/DescriptorMetadata.cs`
- Integration into `CodeGenerator.cs` (parse idlc output after compilation)

**Tests Required:** (Add to `tests/CycloneDDS.CodeGen.Tests/`)

**Minimum 10-12 tests:**
1. ✅ ParseDescriptor parses simple descriptor successfully
2. ✅ Extracts `m_ops` array name
3. ✅ Extracts `m_ops` array values
4. ✅ Extracts `m_keys` array name
5. ✅ Extracts `m_keys` array values
6. ✅ Handles descriptor without keys (empty array)
7. ✅ Extracts type name from descriptor (`TestData_ops` → `TestData`)
8. ✅ Handles multiple ops values (nested structs)
9. ✅ Handles complex ops expressions (DDS_OP_ADR | DDS_OP_TYPE_8BY)
10. ✅ Throws clear error if parsing fails
11. ✅ Handles file with multiple descriptors (if applicable)
12. ✅ End-to-end: Real `idlc` output → parsed metadata

**Quality Standard:**
- Tests must use **real C code samples** (actual `idlc` output format)
- **OR** create synthetic C files matching `idlc` format
- Tests verify **actual parsed values**, not just "method returns object"

**Example Good Test:**
```csharp
[Fact]
public void ParseDescriptor_ExtractsOpsArray()
{
    string cCode = @"
        #include <stdint.h>
        static const uint32_t TestData_ops[] = {
            0x40000004,  // DDS_OP_ADR | DDS_OP_TYPE_4BY
            0x00000000,  // offset
            0x00000001   // DDS_OP_RTS
        };
    ";
    
    var tempFile = CreateTempFile("test.c", cCode);
    
    var parser = new DescriptorParser();
    var metadata = parser.ParseDescriptor(tempFile);
    
    Assert.Equal("TestData_ops", metadata.OpsArrayName);
    Assert.Equal(3, metadata.OpsValues.Length);
    Assert.Equal(0x40000004u, metadata.OpsValues[0]);
}
```

**Estimated Time:** 6-7 hours

---

## 🧪 Testing Requirements

**Minimum Total Tests:** 18-22 new tests

**Test Distribution:**
- IdlcRunner tests: 8-10 tests
- DescriptorParser tests: 10-12 tests

**Test Quality Standards:**

**✅ REQUIRED:**
- IdlcRunner tests use **actual temp files** and real process (if `idlc` available)
- DescriptorParser tests use **real C code** (actual `idlc` output format)
- Tests verify **actual behavior** (exit codes, parsed values), not just compilation

**❌ NOT ACCEPTABLE:**
- Shallow tests that don't run actual process or parse actual C
- Tests that assume `idlc` format without validation
- Tests that don't verify error handling

**All tests must pass before submitting report.**

---

## 📊 Report Requirements

**⚠️ ⚠️ ⚠️ REMINDER: SUBMIT REPORT TO CORRECT FOLDER ⚠️ ⚠️ ⚠️**

**Report Location:** `.dev-workstream/reports/BATCH-05-REPORT.md`

**NOT:** `.dev-workstream/reviews/` (that's for reviews!)

Use template: `.dev-workstream/templates/BATCH-REPORT-TEMPLATE.md`

**Required Sections:**

1. **Implementation Summary**
   - Tasks completed (FCDC-S008b, FCDC-S009b)
   - Test counts
   - Any deviations from instructions

2. **Issues Encountered**
   - `idlc` location challenges?
   - CppAst parsing difficulties?
   - Process execution issues?

3. **Design Decisions**
   - How did you handle `idlc` not found?
   - What CppAst API did you use?
   - How did you extract ops/keys values?

4. **Weak Points Spotted**
   - `idlc` output format assumptions?
   - CppAst API limitations?
   - Error handling gaps?

5. **Next Steps**
   - What's needed for serializer generation (FCDC-S010)?
   - Dependencies identified?

---

## 🎯 Success Criteria

This batch is DONE when:

- ✅ **FCDC-S008b** Complete: IDL Compiler Orchestration, 8-10 tests pass
- ✅ **FCDC-S009b** Complete: Descriptor Parser, 10-12 tests pass
- ✅ All 112-116 tests passing (94 existing + 18-22 new)
- ✅ No compiler warnings
- ✅ CLI tool runs `idlc.exe` successfully
- ✅ Descriptor parser extracts `m_ops` and `m_keys` from C files
- ✅ **Report submitted to `.dev-workstream/reports/BATCH-05-REPORT.md` (NOT reviews!)**

**GATE:** Descriptor metadata extraction working before moving to serializer generation (BATCH-06).

---

## ⚠️ Common Pitfalls to Avoid

1. **Using Regex instead of CppAst:** Must use libclang-based parsing
   - Wrong: Regex matching `m_ops\[\] = \{(.+)\}`
   - Right: CppAst variable extraction with type analysis

2. **Not handling `idlc` not found:** Must give clear error message
   - Include instructions: "Set CYCLONEDDS_HOME or add to PATH"

3. **Assuming `idlc` format:** Test with actual `idlc` output
   - Don't fabricate expected format - use real examples

4. **Not testing process execution:** Must verify actual process runs
   - Tests should create temp `.idl` file and run `idlc`

5. **Testing without `idlc` available:** Handle CI scenarios
   - Use conditional tests or mocks where `idlc` not installed

6. **WRONG REPORT FOLDER:** Remember: `reports/`, NOT `reviews/`!

---

## 📚 Reference Materials

- **Task Master:** [SERDATA-TASK-MASTER.md](../docs/SERDATA-TASK-MASTER.md) - FCDC-S008b, S009b
- **Design:** [SERDATA-DESIGN.md](../docs/SERDATA-DESIGN.md) - Section 4, 5.1
- **CLI Tool Rationale:** [DESIGN-UPDATES-CLI-TOOL.md](../docs/DESIGN-UPDATES-CLI-TOOL.md)
- **CppAst Documentation:** https://github.com/xoofx/CppAst
- **Cyclone DDS `idlc`:** Cyclone DDS installation includes `idlc` executable

---

**Next Batch:** BATCH-06 (Serializer Code Emitter - Fixed Types) - Generate actual C# serialization code
