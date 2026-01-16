# CycloneDDS.CodeGen - CLI Code Generator

## Purpose

Standalone CLI tool that discovers DDS schema types (marked with `[DdsTopic]`, `[DdsUnion]`, etc.) and generates supporting code.

## Architecture Decision: Why CLI Tool?

**Original Approach:** Roslyn `IIncrementalGenerator` (BATCH-02)  
**Problem:** Complex caching behavior, difficult to debug, regeneration issues  
**Solution:** CLI tool with explicit file I/O

## Circular Dependency Resolution

**Issue:** The tool needs to reference schema attributes (`[DdsTopic]`) from `CycloneDDS.Schema`, but `CycloneDDS.Schema` needs to build *before* the tool runs.

**Solution:** 
1. Tool uses **syntax-only analysis** (no semantic model required)
2. Attribute names matched as strings (`"DdsTopic"`) 
3. No `ProjectReference` to `CycloneDDS.Schema` in tool project
4. Enums copied locally to `Models/Enums.cs` to avoid dependency

This allows the tool to run before `CycloneDDS.Schema` compiles.

## Usage

**Manual Run:**
```bash
dotnet run --project tools/CycloneDDS.CodeGen/CycloneDDS.CodeGen.csproj -- src/CycloneDDS.Schema
```

**MSBuild Integration:**
```bash
dotnet build src/CycloneDDS.Schema /p:RunCodeGen=true
```

## Generated Files

- `Generated/{TypeName}.Discovery.g.cs` - Placeholder partial class
- Future batches will extend this to generate native types, marshallers, etc.

## Testing

Run tests:
```bash
dotnet test tests/CycloneDDS.CodeGen.Tests
```

## Future Work

- FCDC-006: Add semantic validation (duplicate topic names, invalid types)
- FCDC-007+: Generate IDL, native types, marshallers
