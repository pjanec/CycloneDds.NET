# BATCH-34: IDL Importer - CLI & Integration

**Batch Number:** BATCH-34
**Tasks:** IDLIMP-009, IDLIMP-010
**Phase:** Phase 4: CLI & Integration
**Estimated Effort:** 6-8 hours
**Priority:** HIGH
**Dependencies:** BATCH-33 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
This batch focuses on making the tool **usable** by end users and integrating it into the build system.
You will replace the stub implementation in `Program.cs` with a real CLI using `System.CommandLine`, and verify the tool works by running a full integration test.

### Required Reading (IN ORDER)
1.  **Task Details:** `tools/CycloneDDS.IdlImporter/IDLImport-TASK-DETAILS.md` (Read IDLIMP-009 and IDLIMP-010)
2.  **Existing Code:** `tools/CycloneDDS.IdlImporter/Program.cs` needs a complete rewrite.

### Source Code Location
-   `tools/CycloneDDS.IdlImporter/Program.cs`
-   `tools/CycloneDDS.IdlImporter/Importer.cs` (minor tweaks may be needed)

### Report Submission
When done, submit your report to: `.dev-workstream/reports/BATCH-34-REPORT.md`

---

## 🎯 Batch Objectives

1.  **CLI:** Implement a robust command-line interface with `System.CommandLine`.
2.  **Arguments:** Support `--source-root`, `--output-root`, `--idlc-path`, and specific IDL files.
3.  **Integration:** Verify the tool can be run from the command line and produces valid output on real filesystem paths.

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

1.  **Task 1 (CLI):** Implement → Write parsing tests → **ALL tests pass** ✅
2.  **Task 2 (Integration):** Run tool on sample IDL folder → Verify output on disk → **ALL tests pass** ✅

---

## ✅ Tasks

### Task 1: CLI Implementation (IDLIMP-009)

**Goal:** Replace the placeholder `Program.cs` with a `System.CommandLine` root command.

**File:** `tools/CycloneDDS.IdlImporter/Program.cs`

**Requirements:**
1.  **Arguments:**
    -   `master-idl` (Required): Path to the entry point IDL file.
2.  **Options:**
    -   `--source-root`: Root directory for IDL includes (default: current dir or master-idl's dir).
    -   `--output-root`: Output directory for C# files (required or default to .).
    -   `--idlc-path`: Override path to `idlc` executable (optional).
    -   `--verbose`: Enable verbose logging.
3.  **Logic:**
    -   Validate input paths.
    -   Instantiate `Importer`.
    -   Call `Importer.Import()`.
    -   Handle exceptions and return non-zero exit code on failure.

**Tests Required:**
-   **NOT** strictly unit tests for `Program.cs` (hard to test Main), but rather ensure the manual execution works.
-   *Optional:* Integration test calling `Program.Main` with args array to verify parsing.

---

### Task 2: Integration Verification (IDLIMP-010)

**Goal:** Prove the tool works in the "Real World".

**Requirements:**
1.  Create a standardized **Integration Test Suite** in `CycloneDDS.IdlImporter.Tests`.
2.  **Scenario:**
    -   Create a complex folder of IDLs on disk (nested folders, cross-includes).
    -   Run the `Importer` (via code, simulating CLI args).
    -   Verify ALL output files exist.
    -   **Compile the Output:** Use `Roslyn` (CSCompiler) or `dotnet build` in the test to ensure the **generated C# code is actually valid**.
        -   *Note:* Just checking file existence isn't enough. We need to know if `csc` accepts it.
        -   You can use `Microsoft.CodeAnalysis.CSharp` to compile the string content in memory during the test to verify syntax.

**Tests Required:**
-   `ImporterTests.Import_GeneratesCompilableCode`:
    -   Generate C# files.
    -   Load them into a C# Compilation.
    -   Add references (CycloneDDS.Schema, System.Runtime).
    -   Assert `compilation.GetDiagnostics().Where(d => d.Severity == Error)` is empty.

---

## 🧪 Testing Requirements

-   **Compilable Output:** The most critical part of this batch is proving that the generated code is syntactically correct.
-   **CLI Usability:** Ensure `--help` prints useful info.

---

## 🎯 Success Criteria

This batch is DONE when:
1.  ✅ `CycloneDDS.IdlImporter.exe` (or `dotnet run`) accepts arguments correctly.
2.  ✅ Integration tests prove that the generated C# code **compiles without errors**.

---

## ⚠️ Common Pitfalls
-   **Compilation References:** When testing compilation, you need to reference the assemblies that define `[DdsStruct]`, `[DdsManaged]`, etc. You might need to reference `CycloneDDS.Schema.dll` in your Roslyn compilation test logic.
-   **Path Defaults:** Ensure reasonable defaults if `source-root` isn't provided (usually `Path.GetDirectoryName(masterIdl)`).
