# CycloneDDS IDL Import Demo

This example demonstrates how to use the `CycloneDDS.IdlImporter` tool to generate C# bindings from a complex, multi-module IDL project.

## Overview

The example simulates a real-world scenario where data types are split across multiple libraries (and IDL files) with dependencies.

### Directory Structure

- **CommonLib/**: Contains base types (`common_types.idl`) like `Point` and `Color`.
- **AppLib/**: Contains application-specific types (`app_types.idl`) like `ExtendedPoint` and `Result` union.
  - Depends on `CommonLib`.
  - Simulates a layered architecture.
- **IdlImportDemoApp/**: The main C# application that uses both libraries to publish and subscribe data.

### Key Features Demonstrated

1.  **IDL Includes**: `app_types.idl` includes `CommonLib/common_types.idl`.
2.  **Complex Identifiers**: Uses `typedef`, `union`, `enum`, `@key`, `@appendable`, `@final`, and `sequence`.
3.  **Cross-Project References**: generated C# code in `AppLib` correctly references types generated in `CommonLib`.
4.  **Typedef Resolution**: The tool automatically resolves IDL `typedef`s (e.g., `Point2D` -> `Point`) to their underlying C# types.

## How it Works

The process is automated by the `generate_and_run.ps1` script:

1.  **Build the Importer**: Compiles the `CycloneDDS.IdlImporter` tool.
2.  **Import CommonLib**:
    - Runs the importer on `CommonLib/common_types.idl`.
    - Generates C# code in `CommonLib/`.
    - Uses `--source-root` to establish the root context.
3.  **Import AppLib**:
    - Runs the importer on `AppLib/app_types.idl`.
    - Finds the included `common_types.idl` relative to the source root.
    - **Crucially**: The tool detects that `Point` and `Color` are defined in the included file, so it *excludes* them from `AppLib`'s output (preventing duplicates), but simply references the types that `CommonLib` already provides.
4.  **Build & Run**: Compiles the solution and runs the demo application.

## Running the Demo

Prerequisites: .NET 8.0 SDK, Visual Studio 2022 C++ Redistributable (for native DDS libraries).

```powershell
./generate_and_run.ps1
```

Or manually:

```bash
# 1. Build the tool
dotnet build ../../tools/CycloneDDS.IdlImporter/CycloneDDS.IdlImporter.csproj -c Release

# 2. Generate
../../tools/CycloneDDS.IdlImporter/bin/Release/net8.0/CycloneDDS.IdlImporter.exe CommonLib/common_types.idl --source-root . --output-root .
../../tools/CycloneDDS.IdlImporter/bin/Release/net8.0/CycloneDDS.IdlImporter.exe AppLib/app_types.idl --source-root . --output-root .

# 3. Run
dotnet run --project IdlImportDemoApp/IdlImportDemoApp.csproj
```
