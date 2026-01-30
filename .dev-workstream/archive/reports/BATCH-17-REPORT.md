# BATCH-17 Report: Advanced IDL Generation Control

## Overview
This batch implemented advanced IDL generation control features (FCDC-S025), enabling precise control over IDL file structure, module hierarchy, and cross-assembly type resolution. The goal was to decouple C# namespaces from IDL modules and allow types to be grouped into specific IDL files, facilitating better interoperability with legacy systems and complex project structures.

## Implementation Details

### 1. Attribute-Driven Generation
Three new attributes were introduced and integrated into the `SchemaDiscovery` and `CodeGenerator` pipeline:
- `[DdsIdlFile("FileName")]`: Groups multiple C# types into a single IDL file.
- `[DdsIdlModule("Scope::Name")]`: Overrides the default namespace-based module hierarchy.
- `[DdsIdlMapping(...)]`: Assembly-level attribute for exposing type mapping information to downstream consumers.

### 2. Three-Phase Generation Pipeline
The `CodeGenerator` was refactored into a three-phase process:
1.  **Registry Population**: Scans all local types and registers them in `GlobalTypeRegistry` with their target IDL file and module.
2.  **Dependency Resolution**: 
    - Resolves internal dependencies between local types.
    - Resolves external dependencies by inspecting referenced assemblies for `[DdsIdlMapping]` attributes.
    - Updates `SchemaValidator` to accept valid external types.
3.  **Emission**:
    - Groups types by target IDL file.
    - Detects circular dependencies between IDL files using a DFS algorithm.
    - Generates `#include` directives for dependencies.
    - Emits IDL content with correct module nesting.
    - Emits `CycloneDDS.IdlMap.g.cs` containing metadata for the generated types.

### 3. Circular Dependency Detection
A critical addition was the detection of circular dependencies between IDL files.
- **Algorithm**: Builds a dependency graph where nodes are IDL files and edges represent `#include` relationships. Uses Depth-First Search (DFS) to detect cycles.
- **Behavior**: Throws an `InvalidOperationException` with the exact cycle path (e.g., `FileA.idl -> FileB.idl -> FileA.idl`) before any files are written.
- **Rationale**: IDL compilers generally do not support circular includes without forward declarations (which are limited) or complex preprocessor guards. Detecting this at generation time prevents confusing errors downstream.

## Challenges and Solutions

### 1. Type Name Resolution
**Issue**: `SchemaDiscovery` initially used simple type names or inconsistent formatting, leading to mismatches when validating against `GlobalTypeRegistry` (which uses fully qualified names).
**Solution**: Updated `SchemaDiscovery` to enforce `SymbolDisplayFormat.FullyQualifiedFormat` (specifically `NameAndContainingTypesAndNamespaces`) when extracting field types. This ensures `System.Int32` is consistently used instead of `int`, and custom types are always fully qualified.

### 2. Primitive Type Validation
**Issue**: Switching to fully qualified names caused `SchemaValidator` to reject `System.Int32` as an invalid user type because `TypeMapper.IsPrimitive` only checked for `int`.
**Solution**: Updated `TypeMapper` to explicitly recognize fully qualified system types (`System.Int32`, `System.Boolean`, etc.) as primitives.

### 3. Cross-Assembly Resolution
**Issue**: Resolving types from referenced assemblies required a way to know their IDL mapping without parsing their source code.
**Solution**: Implemented the `[DdsIdlMapping]` assembly attribute. When compiling Assembly A, the generator emits this attribute. When compiling Assembly B (which references A), the generator reads these attributes from A.dll using Roslyn's `GetAttributes()` on the referenced assembly symbol.

## Verification

The implementation is verified by a comprehensive suite of tests in `CrossAssemblyTests.cs` and `IdlGenerationTests.cs`:
- **Circular Dependency**: Verified that `FileA -> FileB -> FileA` cycles are detected and reported with a clear error message.
- **Transitive Dependencies**: Verified that if C depends on B, and B depends on A, the generated IDL for C includes B, and B includes A.
- **Metadata Emission**: Verified that `CycloneDDS.IdlMap.g.cs` is correctly generated with all registered types.
- **Module Hierarchy**: Verified that `[DdsIdlModule]` correctly nests modules and closes braces.
- **Name Collisions**: Verified that multiple types mapping to the same IDL name/scope are detected.

## Status
All requirements for BATCH-17 are met. The solution passes all 111 tests, including the new integration tests for circular dependencies and cross-assembly scenarios.