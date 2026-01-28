# CycloneDDS IDL Importer

**Status:** 🔵 Under Development (Skeleton Created)  
**Version:** 1.0.0-dev  

A tool for importing existing IDL files into the CycloneDDS C# bindings ecosystem, enabling bidirectional integration between legacy IDL-based DDS systems and modern C# DSL code.

---

## Overview

The IDL Importer converts IDL type definitions to C# DSL (using `CycloneDDS.Schema` attributes) by leveraging the native `idlc` compiler's JSON output format. This enables:

- **Migration** of existing IDL-based projects to C#
- **Interoperability** with legacy DDS systems
- **Automation** of manual translation processes
- **Multi-assembly** support with folder structure preservation

---

## Features

### Completed (Skeleton)
- ✅ Project structure and build configuration
- ✅ CLI framework with argument validation
- ✅ Skeleton classes for core components

### Planned (See Task Tracker)
- 🔵 Recursive IDL file processing
- 🔵 Complete type mapping (primitives, collections, unions)
- 🔵 C# code generation with attributes
- 🔵 Multi-assembly support
- 🔵 Comprehensive test suite with roundtrip validation

---

## Usage

```bash
# Basic usage
CycloneDDS.IdlImporter <master-idl> <source-root> <output-root>

# Example
CycloneDDS.IdlImporter Master.idl ./idl ./generated

# With options
CycloneDDS.IdlImporter Master.idl ./idl ./generated --verbose --idlc-path "C:\cyclone\bin\idlc.exe"
```

### Arguments

- `<master-idl>`: Entry point IDL file
- `<source-root>`: Root directory containing all IDL files
- `<output-root>`: Root directory for generated C# files

### Options

- `--idlc-path <path>`: Path to idlc executable (default: auto-detect)
- `--verbose`: Enable detailed logging
- `--help`: Display help information

---

## Development Status

### Current Phase: Foundation

The project skeleton has been created with the following structure:

```
tools/CycloneDDS.IdlImporter/
├── CycloneDDS.IdlImporter.csproj    # .NET 8 console project
├── Program.cs                        # CLI entry point (complete)
├── Importer.cs                       # Core orchestration (skeleton)
├── CSharpEmitter.cs                  # Code generation (skeleton)
├── TypeMapper.cs                     # Type mapping (skeleton)
├── README.md                         # This file
├── IDLImport-TASK-TRACKER.md        # Task status and phases
└── IDLImport-TASK-DETAILS.md        # Detailed task specifications
```

### Next Steps

1. **IDLIMP-001**: Complete project setup and shared infrastructure
2. **IDLIMP-002**: Enhance IdlcRunner for include paths
3. **IDLIMP-003**: Implement TypeMapper logic

See [IDLImport-TASK-TRACKER.md](./IDLImport-TASK-TRACKER.md) for complete development plan.

---

## Building

```bash
cd tools/CycloneDDS.IdlImporter
dotnet build
```

### Dependencies

- .NET 8 SDK
- `System.CommandLine` (NuGet)
- `System.Text.Json` (NuGet)
- `CycloneDDS.Schema` (project reference)

---

## Testing

*Test project not yet created (IDLIMP-014)*

```bash
# Future: Run tests
dotnet test
```

---

## Documentation

- **[Design Document](../../docs/IdlImport-design.md)**: Architecture and design details
- **[Task Tracker](./IDLImport-TASK-TRACKER.md)**: Development status and phases
- **[Task Details](./IDLImport-TASK-DETAILS.md)**: Detailed task specifications

---

## Related Projects

This tool is part of the FastCycloneDDS C# Bindings ecosystem:

- **CycloneDDS.Schema**: Attribute-based DSL for DDS types
- **CycloneDDS.CodeGen**: Generates serializers from C# DSL
- **CycloneDDS.Runtime**: DDS API implementation

---

## Contributing

This project is under active development. The task tracker provides a structured approach to implementation:

1. Check [IDLImport-TASK-TRACKER.md](./IDLImport-TASK-TRACKER.md) for current status
2. Review [IDLImport-TASK-DETAILS.md](./IDLImport-TASK-DETAILS.md) for task specifications
3. Follow TDD approach (write tests first)
4. Reference [Design Document](../../docs/IdlImport-design.md) for architecture decisions

---

## License

Part of the FastCycloneDDS C# Bindings project.

---

## Changelog

- **2026-01-28**: Initial project skeleton created
  - CLI framework with argument validation
  - Skeleton classes for core components
  - Project structure and documentation
