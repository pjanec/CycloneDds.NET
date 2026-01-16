# Design Document Updates - CLI Tool Architecture

**Date:** 2026-01-16  
**Source:** design-talk.md lines 3092-3333

## Summary

Updated SERDATA-DESIGN.md and SERDATA-TASK-MASTER.md to reflect the **proven CLI Tool architecture** instead of Roslyn IIncrementalGenerator plugin, based on user's successful experience with the old implementation.

---

## Key Changes Made

### 1. SERDATA-DESIGN.md

#### Architecture Diagram (§3.1)
- ✅ Changed "Roslyn Source Generator" → "CLI Code Generator (Build Tool)"

#### Stage 2 Title and Description (§4)
- ✅ Changed "Source Generator Core" → "CLI Code Generator Core"
- ✅ Removed "Roslyn `IIncrementalGenerator`"
- ✅ Added deliverables:
  - Console Application (net8.0)
  - Uses `Microsoft.CodeAnalysis` to parse files from disk
  - Runs via MSBuild Target (not compiler plugin)
  - `IdlcRunner` (orchestrates idlc.exe)
  - `DescriptorParser` (uses CppAst, not regex)

#### Package List (§5.1)
- ✅ Renamed `CycloneDDS.Generator` → `CycloneDDS.CodeGen`
- ✅ Changed target from `netstandard2.0` → `net8.0 (Exe)`
- ✅ Added components:
  - `SerializerEmitter`
  - `ViewEmitter`
  - `IdlEmitter`
  - `DescriptorParser` (CppAst-based)
  - `IdlcRunner`
  - `SchemaValidator`

---

### 2. SERDATA-TASK-MASTER.md

#### Overview
- ✅ Updated task count: 28 → **32 tasks**
- ✅ Updated effort estimate: 85-110 days → **92-120 days**

#### FCDC-S007: Generator Infrastructure
**Changed from:**
- "Set up Roslyn `IIncrementalGenerator` infrastructure"

**Changed to:**
- "CLI Tool Generator Infrastructure"
- **CRITICAL: We use a CLI TOOL, NOT a Roslyn plugin**
- Emphasizes:
  - Runs only at build time (via MSBuild)
  - Easy to debug (standard console app)
  - No caching complexity or "ghost generation"
  - Uses `Microsoft.CodeAnalysis` to parse files from disk

**Deliverables changed:**
- `  - Source: tools/CycloneDDS.CodeGen/` (not Src/CycloneDDS.Generator/)
- Added `CycloneDDS.targets` for MSBuild integration

#### New Task: FCDC-S008b - IDL Compiler Orchestration
**Status:** Not Started  
**Effort:** 2 days  
**Priority:** High

**Purpose:**  
Manage external `idlc.exe` execution from CLI tool

**Responsibilities:**
1. Locate `idlc` (env vars, NuGet tools, configured path)
2. Execute `idlc -l c` on generated .idl files
3. Capture stdout/stderr → pipe to MSBuild logging
4. Manage temporary .c/.h files

**Deliverables:**
- `tools/CycloneDDS.CodeGen/IdlcRunner.cs`
- Integration tests

#### FCDC-S009: IDL Emitter
- ✅ Renamed to "IDL Text Emitter (Discovery Only)" to clarify it generates .idl text

#### New Task: FCDC-S009b - Descriptor Parser (CppAst Replacement)
**Status:** Not Started  
**Effort:** 3-4 days  
**Priority:** High

**Purpose:**  
Replace fragile regex-based descriptor extraction with robust CppAst parsing

**Why CppAst?**
- Regex fails if idlc changes whitespace/indentation/macros
- CppAst parses actual C semantic tree → formatting-independent

**Requirements:**
1. Parse `.c` file from idlc
2. Locate `dds_topic_descriptor_t` struct initializer
3. Extract `m_ops` array (flatten macros to raw integers)
4. Extract `m_keys` array
5. Generate C# byte array for TypeSupport class

**Deliverables:**
- `tools/CycloneDDS.CodeGen/DescriptorExtraction/DescriptorParser.cs`
- Unit tests with real idlc output

#### FCDC-S029: NuGet Packaging
**Changed package list:**
- ✅ Removed `CycloneDDS.Generator` (source generator)
- ✅ Added `CycloneDDS.CodeGen` (CLI Tool - tools folder packaging)
  - Must include `.targets` file
  - MSBuild task to invoke exe
  
**Updated validation:**
- "Verify code generation runs on build (via MSBuild target)"

---

## Rationale (from design-talk.md §3092-3157)

### Why CLI Tool Instead of Roslyn Plugin?

**Old Painful Way (Roslyn Plugin):**
- ❌ Runs on every keystroke
- ❌ Requires complex `IEquatable` caching to prevent constant regeneration
- ❌ Hard to debug (attach to Visual Studio)
- ❌ "Ghost generation" issues

**User's Proven Way (CLI Tool):**
- ✅ Runs only when you build (via MSBuild target)
- ✅ Reads `.cs` files from disk using `Microsoft.CodeAnalysis`
- ✅ Writes `.Serialization.g.cs` files to disk
- ✅ **Deterministic and easy to debug**
- ✅ User has successful experience with this approach

### Integration

The `SerializerEmitter` logic is **identical** - it's just the delivery mechanism that changes:

**Logic:** Same - `SerializerEmitter` writes C# strings
**Trigger:**
- ❌ Doc said: Compiler event (`IIncrementalGenerator`)
- ✅ User wants: Build event (`Exec Command="CycloneDDS.CodeGen.exe ..."`)

---

## Missing Tasks Now Added

The design-talk.md identified two critical gaps:

### 1. IDL Compiler Orchestration (FCDC-S008b)
Without this, the build process wouldn't actually invoke `idlc` to generate descriptors.

### 2. Robust Descriptor Extraction (FCDC-S009b)
Without this, topic registration would rely on fragile regex parsing that breaks with idlc formatting changes.

---

## Summary

All design documents now correctly specify:
1. **CLI Tool** (not Roslyn plugin)
2. **Build-time execution** (not compile-time)
3. **CppAst-based descriptor parsing** (not regex)
4. **Complete IDL toolchain** (generate .idl → run idlc → parse .c → extract descriptor)

The implementation approach is now aligned with the user's proven architecture from the old implementation.
