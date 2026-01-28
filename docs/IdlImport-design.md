# IDL Importer Tool - Design Document

**Project:** FastCycloneDDS C# Bindings  
**Tool Name:** CycloneDDS.IdlImporter  
**Version:** 1.0  
**Date:** January 28, 2026  
**Author:** AI-assisted design based on IdlImporter-design-talk.md

---

## Table of Contents

1. [Overview](#overview)
2. [Goals and Objectives](#goals-and-objectives)
3. [Architecture](#architecture)
4. [Component Design](#component-design)
5. [Type Mapping Rules](#type-mapping-rules)
6. [Multi-Assembly Support](#multi-assembly-support)
7. [Information Loss and Compatibility](#information-loss-and-compatibility)
8. [File Organization](#file-organization)
9. [Usage Workflow](#usage-workflow)
10. [Extension Points](#extension-points)

---

## Overview

The **IDL Importer Tool** (`CycloneDDS.IdlImporter`) is a console application that enables bidirectional integration between legacy IDL-based DDS systems and the modern C# DSL ecosystem of FastCycloneDDS C# Bindings.

### Problem Statement

Many DDS projects have existing IDL files representing their data models. Manually translating these to C# DSL is error-prone and time-consuming. The IDL Importer automates this process by:

1. Parsing IDL files using the native `idlc` compiler with `-l json` output
2. Translating the JSON type system metadata into C# DSL classes with appropriate `[Dds*]` attributes
3. Maintaining folder structure for multi-assembly projects
4. Ensuring functional compatibility with the original IDL at the wire protocol level

### Key Principle

The tool creates **functionally compatible** C# DSL code, not byte-for-byte identical IDL. The generated C# DSL, when processed by the existing `CycloneDDS.CodeGen` tool, will produce IDL that is wire-compatible with the original but may differ in syntax (e.g., typedefs are resolved, comments are lost).

---

## Goals and Objectives

### Primary Goals

1. **Automate IDL → C# DSL Translation**: Convert any valid IDL file into equivalent C# DSL code
2. **Preserve Type Semantics**: Ensure all DDS-relevant type information (structure, extensibility, keys, annotations) is maintained
3. **Support Multi-Assembly Projects**: Handle complex project layouts with cross-folder includes and multiple output assemblies
4. **1:1 File Mapping**: Generate one `.cs` file for each `.idl` file, maintaining folder structure
5. **Enable Testability**: Produce code that can be validated using the existing roundtrip test framework

### Secondary Goals

1. **Namespace Preservation**: Map IDL modules to C# namespaces
2. **Include Resolution**: Process all `#include` directives recursively
3. **Annotation Support**: Translate IDL annotations (`@key`, `@optional`, `@id`, extensibility) to C# attributes
4. **Collection Types**: Handle sequences, arrays, bounded/unbounded strings

---

## Architecture

The tool follows a **three-stage pipeline** inspired by the existing CodeGen architecture:

```
Stage 1: IDL Processing        Stage 2: JSON Parsing           Stage 3: C# Emission
┌────────────────────┐        ┌────────────────────┐        ┌──────────────────┐
│  Master.idl        │        │  JSON Type System  │        │  Generated       │
│  ├─ Include A.idl  │──idlc──▶  ├─ Structs       │──parse──▶  ├─ Master.cs   │
│  └─ Include B.idl  │ -l json│  ├─ Unions        │        │  ├─ A.cs         │
│                    │        │  ├─ Enums         │        │  └─ B.cs         │
│  (Recursive crawl) │        │  └─ TopicDesc     │        │                  │
└────────────────────┘        └────────────────────┘        └──────────────────┘
```

### Processing Strategy

The tool uses a **crawler pattern** to process IDL files:

1. **Start**: User provides a master IDL file and source root directory
2. **Process**: Run `idlc -l json` on the master file
3. **Extract**: Parse JSON to identify types defined in the master file
4. **Generate**: Emit C# DSL for those types only
5. **Discover**: Extract dependency list (included files) from JSON
6. **Recurse**: Add discovered files to processing queue
7. **Repeat**: Process each file until queue is empty

This ensures:
- Each file is processed exactly once (tracked via `HashSet<string>`)
- Circular includes are handled gracefully
- Unused types in included files are still generated (complete library)

---

## Component Design

### 1. Importer (Core Orchestration)

**File:** `Importer.cs`  
**Responsibility:** Manages the recursive crawl and coordinates all stages

**Key Features:**
- Maintains work queue and processed files set
- Calculates relative paths for folder structure mirroring
- Coordinates `idlc` execution and JSON parsing
- Delegates C# generation to `CSharpEmitter`

**Critical Logic:**
```csharp
// Path mirroring: Input/FolderA/SubA/Type.idl → Output/FolderA/SubA/Type.cs
string relativePath = Path.GetRelativePath(_sourceRoot, currentIdlPath);
string targetDir = Path.Combine(_outputRoot, Path.GetDirectoryName(relativePath));
```

### 2. IdlcRunner (IDL Compilation)

**File:** `IdlcRunner.cs` (extends existing CodeGen class)  
**Responsibility:** Executes `idlc` with proper arguments

**Enhancement Required:**
- Add support for custom include paths (`-I` flag)
- Pass source root as include path to resolve cross-folder dependencies

**Invocation:**
```bash
idlc -l json -I "SourceRoot" -o "TempDir" "Target.idl"
```

### 3. IdlJsonParser (JSON Deserialization)

**File:** Uses existing `IdlJsonParser.cs` and `JsonModels.cs` from CodeGen  
**Responsibility:** Deserializes `idlc` JSON output into strongly-typed C# objects

**Key Models:**
- `IdlJsonRoot`: Top-level container with `Types` and `File` arrays
- `JsonTypeDefinition`: Represents a struct/union/enum with metadata
- `JsonMember`: Represents a field/member with type info and attributes
- `JsonFileMeta`: Maps types to their source files

### 4. CSharpEmitter (Code Generation)

**File:** `CSharpEmitter.cs`  
**Responsibility:** Translates JSON type definitions into C# DSL syntax

**Key Methods:**

| Method | Purpose |
|--------|---------|
| `GenerateCSharp()` | Entry point: generates complete C# file for a set of types |
| `EmitType()` | Emits a single type (struct/union/enum) with attributes |
| `EmitStructMembers()` | Emits fields for structs/unions with proper attributes |
| `EmitEnumMembers()` | Emits enum values |
| `MapIdlTypeToCSharp()` | Translates IDL types to C# types (core mapping logic) |
| `MapPrimitive()` | Maps IDL primitives (`long` → `int`, `double` → `double`, etc.) |

**Output Structure:**
```csharp
// <auto-generated>
// Original IDL: CommonTypes.idl
// </auto-generated>

using System;
using System.Collections.Generic;
using CycloneDDS.Schema;

namespace MyModule.Common
{
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("SensorData")]
    public partial struct SensorData
    {
        [DdsKey]
        public int SensorId;
        
        [MaxLength(256)]
        [DdsManaged]
        public string Location;
        
        [DdsManaged]
        public List<double> Readings;
    }
}
```

---

## Type Mapping Rules

The `CSharpEmitter` applies these mapping rules to translate IDL types to C#:

### Primitive Types

| IDL Type | C# Type | Notes |
|----------|---------|-------|
| `boolean` | `bool` | 1 byte |
| `char` | `byte` | 8-bit character |
| `octet` | `byte` | Raw byte |
| `short` | `short` | 16-bit signed |
| `unsigned short` | `ushort` | 16-bit unsigned |
| `long` | `int` | 32-bit signed |
| `unsigned long` | `uint` | 32-bit unsigned |
| `long long` | `long` | 64-bit signed |
| `unsigned long long` | `ulong` | 64-bit unsigned |
| `float` | `float` | 32-bit IEEE 754 |
| `double` | `double` | 64-bit IEEE 754 |
| `string` | `string` | UTF-8, requires `[DdsManaged]` |

### Collection Types

| IDL Type | C# Type | Attributes | Notes |
|----------|---------|------------|-------|
| `sequence<T>` | `List<T>` | `[DdsManaged]` | Unbounded sequence |
| `sequence<T, N>` | `List<T>` | `[MaxLength(N)]`, `[DdsManaged]` | Bounded sequence |
| `string<N>` | `string` | `[MaxLength(N)]`, `[DdsManaged]` | Bounded string |
| `T name[N]` | `T[]` | `[ArrayLength(N)]`, `[DdsManaged]` | Fixed-size array |
| `T name[]` | `T[]` | `[DdsManaged]` | Dynamic array (maps to sequence) |

### User-Defined Types

| IDL Construct | C# Construct | Attribute | Notes |
|---------------|--------------|-----------|-------|
| `struct` (with keys) | `public partial struct` | `[DdsTopic("Name")]` | Topic type |
| `struct` (no keys) | `public partial struct` | `[DdsStruct]` | Nested type |
| `union` | `public partial struct` | `[DdsUnion]` | Tagged union |
| `enum` | `public enum` | None | Standard enum |
| Module hierarchy | Namespace | Maps `A::B::C` → `A.B.C` | Nested modules |

### Annotations

| IDL Annotation | C# Attribute | Placement |
|----------------|--------------|-----------|
| `@key` | `[DdsKey]` | Field |
| `@optional` | `[DdsOptional]` | Field |
| `@id(N)` | `[DdsId(N)]` | Field |
| `@external` | `[DdsExternal]` | Field |
| `@final` | `[DdsExtensibility(DdsExtensibilityKind.Final)]` | Type |
| `@appendable` | `[DdsExtensibility(DdsExtensibilityKind.Appendable)]` | Type (default) |
| `@mutable` | `[DdsExtensibility(DdsExtensibilityKind.Mutable)]` | Type |

### Union-Specific Mappings

| IDL Element | C# Representation |
|-------------|-------------------|
| Discriminator field | `[DdsDiscriminator] public <type> _d;` |
| Case label | `[DdsCase(<value>)]` above field |
| Default case | `[DdsDefaultCase]` above field |

---

## Multi-Assembly Support

The tool is designed to support complex multi-assembly project structures common in large DDS systems.

### Scenario: Cross-Folder Includes

**Input Structure:**
```
/SourceIdl
  /Common              ← Assembly A
    Types.idl          (module Common { struct Point {...} })
    /Geometry
      Shapes.idl       (uses Point, module Common::Geometry)
  /Vehicle             ← Assembly B  
    Car.idl            (#include "../Common/Types.idl")
  Master.idl           (#include "Common/Types.idl", #include "Vehicle/Car.idl")
```

**Tool Invocation:**
```bash
IdlImporter.exe "SourceIdl/Master.idl" "SourceIdl" "GeneratedSrc"
```

**Output Structure:**
```
/GeneratedSrc
  /Common
    Types.cs           (namespace Common)
    /Geometry
      Shapes.cs        (namespace Common.Geometry)
  /Vehicle
    Car.cs             (namespace Vehicle)
  Master.cs
```

### Assembly Creation (Manual Step)

After generation, the user creates C# projects:

**`GeneratedSrc/Common/Common.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  
  <!-- SDK automatically includes **/*.cs, so Types.cs and Geometry/Shapes.cs are included -->
  
  <ItemGroup>
    <PackageReference Include="CycloneDDS.Schema" Version="*" />
  </ItemGroup>
  
  <Import Project="../../tools/CycloneDDS.CodeGen/CycloneDDS.targets" />
</Project>
```

**`GeneratedSrc/Vehicle/Vehicle.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="../Common/Common.csproj" />
    <PackageReference Include="CycloneDDS.Schema" Version="*" />
  </ItemGroup>
  
  <Import Project="../../tools/CycloneDDS.CodeGen/CycloneDDS.targets" />
</Project>
```

### How CodeGen Resolves Cross-Assembly References

When `Vehicle.csproj` is built:

1. `CycloneDDS.CodeGen` runs via MSBuild targets
2. It discovers `Car.cs` which references `Common.Point`
3. It inspects `Common.dll` (already built as dependency)
4. It finds `[assembly: DdsIdlMapping("Common.Point", "Types", "Common")]`
5. It generates `Vehicle/Car.idl` with `#include "../Common/Types.idl"`

This creates a functionally equivalent IDL hierarchy to the original.

---

## Information Loss and Compatibility

The transformation `IDL → JSON → C# DSL → IDL` is **functionally compatible** but not syntactically identical.

### Preserved Information

| Feature | Status | Details |
|---------|--------|---------|
| **Struct layouts** | ✅ Preserved | Field order, types, and names maintained |
| **Extensibility** | ✅ Preserved | `@final`, `@appendable`, `@mutable` mapped correctly |
| **Keys** | ✅ Preserved | `@key` → `[DdsKey]` → `@key` |
| **Optional members** | ✅ Preserved | `@optional` roundtrip |
| **Member IDs** | ✅ Preserved | `@id(N)` roundtrip |
| **Unions** | ✅ Preserved | Discriminator and case labels maintained |
| **Enums** | ✅ Preserved | Values and names maintained |
| **Sequences/Arrays** | ✅ Preserved | Bounds and dimensions maintained |
| **String bounds** | ✅ Preserved | `string<32>` → `[MaxLength(32)]` → `string<32>` |
| **Modules** | ✅ Preserved | `A::B::C` → `namespace A.B.C` → `module A { module B { module C` |
| **Includes** | ✅ Preserved | File structure generates correct `#include` directives |

### Lost Information

| Feature | Status | Impact |
|---------|--------|--------|
| **Typedefs** | ❌ Lost | `typedef long MyId;` → resolved to `int` in JSON → `public int MyId;` |
| **Comments** | ❌ Lost | Not present in JSON output |
| **Constants** | ⚠️ Partially Lost | May be present in JSON but not currently handled; values can be hardcoded |
| **Forward declarations** | ❌ Lost | Not needed in C# |
| **IDL pragmas** | ⚠️ Some Lost | Topic-level pragmas (QoS) are preserved; others may be lost |
| **Bitfields** | ⚠️ Special handling needed | Not commonly used; requires extension |

### Wire Compatibility Guarantee

Despite the syntactic differences, the generated code is **wire-compatible** because:

1. **Field offsets**: Preserved by maintaining field order and types
2. **Serialization opcodes**: CodeGen recalculates via `idlc`, ensuring consistency
3. **Key hashes**: Preserved by maintaining key definitions
4. **Extensibility semantics**: Preserved by correct attribute mapping

**Example:**

**Original IDL:**
```idl
typedef long SensorId;

module Sensors {
    @appendable
    struct Reading {
        @key SensorId id;
        double temperature;
    };
};
```

**Generated C# (after import):**
```csharp
namespace Sensors
{
    [DdsExtensibility(DdsExtensibilityKind.Appendable)]
    [DdsTopic("Reading")]
    public partial struct Reading
    {
        [DdsKey]
        public int id;  // typedef resolved to int
        public double temperature;
    }
}
```

**Derived IDL (after CodeGen):**
```idl
module Sensors {
    @appendable
    struct Reading {
        @key long id;  // typedef lost but wire type correct
        double temperature;
    };
};
```

**Wire Format:** Identical for both versions!

---

## File Organization

### Project Structure

```
tools/
  CycloneDDS.IdlImporter/
    CycloneDDS.IdlImporter.csproj       # .NET 8 console app
    Program.cs                           # CLI entry point
    Importer.cs                          # Core orchestration logic
    CSharpEmitter.cs                     # C# code generation
    TypeMapper.cs                        # IDL → C# type mapping
    IdlcRunner.cs                        # Shared with CodeGen (link or move to shared lib)
    JsonModels.cs                        # Shared with CodeGen (link or reference)
    
    Tests/
      CycloneDDS.IdlImporter.Tests.csproj
      ImporterTests.cs                   # Test full workflows
      TypeMapperTests.cs                 # Test individual mappings
      AtomicTests/                       # Use existing atomic_tests.idl
        atomic_tests.idl                 # Copy of roundtrip test IDL
        ExpectedOutput.cs                # Expected C# output (validation)
```

### Shared Code Strategy

Several components are shared with the existing `CycloneDDS.CodeGen`:

1. **IdlcRunner**: Reuse or enhance with include path support
2. **JsonModels**: Reuse JSON deserialization models
3. **IdlJsonParser**: Reuse parser logic

**Options:**
- **Option A (Recommended)**: Extract shared code to `CycloneDDS.Compiler.Common` library
- **Option B**: Use file linking (`.csproj` `<Link>` elements)
- **Option C**: Duplicate code (not recommended for maintenance)

---

## Usage Workflow

### Command-Line Interface

```
CycloneDDS.IdlImporter v1.0

Usage:
  IdlImporter <master-idl> <source-root> <output-root> [options]

Arguments:
  <master-idl>      Path to the entry-point IDL file
  <source-root>     Root directory containing all IDL files
  <output-root>     Root directory for generated C# files

Options:
  --idlc-path <path>        Path to idlc executable (default: auto-detect)
  --namespace <name>        Override root namespace (default: from IDL modules)
  --file-per-type          Generate one C# file per type instead of per IDL file
  --verbose                Enable detailed logging
  --help                   Show this help message

Examples:
  # Basic usage
  IdlImporter Master.idl ./idl ./generated

  # Custom idlc path
  IdlImporter Master.idl ./idl ./generated --idlc-path "C:\cyclone\bin\idlc.exe"

  # Verbose output
  IdlImporter Master.idl ./idl ./generated --verbose
```

### End-to-End Workflow

**Step 1: Prepare Source IDL**
```bash
cd MyProject
ls IdlSources/
  Common/Types.idl
  Vehicle/Car.idl
  Master.idl
```

**Step 2: Run Importer**
```bash
IdlImporter IdlSources/Master.idl IdlSources Generated
```

**Step 3: Review Output**
```bash
ls Generated/
  Common/Types.cs
  Vehicle/Car.cs
  Master.cs
```

**Step 4: Create C# Projects**
- Create `Generated/Common/Common.csproj`
- Create `Generated/Vehicle/Vehicle.csproj` with reference to `Common.csproj`
- Add references to `CycloneDDS.Schema` and import `CycloneDDS.targets`

**Step 5: Build**
```bash
cd Generated/Vehicle
dotnet build
```

This triggers `CycloneDDS.CodeGen`, which generates:
- `obj/Generated/Car.idl`
- `obj/Generated/Car_Descriptor.cs` (serializers)

**Step 6: Use in DDS Application**
```csharp
using CycloneDDS.Runtime;
using Vehicle;

var participant = new DdsParticipant(0);
var writer = new DdsWriter<Car>(participant, "CarTopic");

var car = new Car { Id = 1, Model = "Tesla Model 3" };
writer.Write(ref car);
```

---

## Extension Points

The design includes several extension points for future enhancements:

### 1. Custom Type Mappers

**File:** `TypeMapper.cs` (abstraction layer)

Allow users to provide custom type mappings via configuration:

```json
{
  "typeMapOverrides": {
    "Guid": { "csharpType": "System.Guid", "requiresMarshaling": false },
    "Timestamp": { "csharpType": "System.DateTime", "requiresMarshaling": true }
  }
}
```

### 2. Annotation Handlers

**Interface:** `IAnnotationHandler`

Support for custom IDL annotations:

```csharp
public interface IAnnotationHandler
{
    bool CanHandle(string annotationName);
    string EmitAttribute(JsonAnnotation annotation);
}
```

### 3. Naming Conventions

**File:** `NamingStrategy.cs`

Allow users to customize naming:
- `PascalCase` vs `camelCase` for fields
- Prefix/suffix rules for generated types
- Namespace transformation rules

### 4. Template-Based Emission

Replace hardcoded string building with template engine (e.g., Scriban):

```csharp
var template = Template.Parse(File.ReadAllText("StructTemplate.scriban"));
var output = template.Render(new { Type = typeDefinition });
```

### 5. Incremental Processing

Track file hashes to avoid regenerating unchanged files:

```csharp
var cache = new FileHashCache("importer-cache.json");
if (!cache.HasChanged(idlPath)) {
    Console.WriteLine($"Skipping unchanged file: {idlPath}");
    return;
}
```

---

## Testing Strategy

### Unit Tests

Test individual components in isolation:

- **TypeMapperTests**: Verify all primitive and collection type mappings
- **NamespaceTests**: Verify module → namespace conversion
- **AttributeEmitterTests**: Verify annotation → attribute mapping
- **PathCalculationTests**: Verify relative path calculations

### Integration Tests

Test the full pipeline with known IDL inputs:

- **AtomicTestsRoundtrip**: Import `atomic_tests.idl` → compare output to `AtomicTestsTypes.cs`
- **MultiFileImport**: Test processing of IDL with multiple includes
- **CrossFolderReferences**: Test folder structure preservation

### Validation Tests

Compare generated code semantics:

- Compile generated C# code
- Run `CycloneDDS.CodeGen` on generated code
- Use `idlc` to compile both original and derived IDL
- Compare JSON outputs (should be functionally equivalent)

### Roundtrip Tests

Full workflow validation:

1. Start with known IDL (e.g., `atomic_tests.idl`)
2. Import to C#
3. Build to generate derived IDL
4. Create DDS writers/readers for both original and derived types
5. Send data from original → receive in derived (and vice versa)
6. Verify data integrity

---

## Future Enhancements

### Phase 2 Features

1. **Const Support**: Parse and emit IDL constants as C# `const` fields
2. **Typedef Preservation**: Attempt to preserve typedef names via `using` aliases or custom attributes
3. **Bitmask Support**: Handle `@bit_bound` and bitmask types
4. **Annotation Extensions**: Support custom annotation handlers
5. **Better Error Messages**: Detailed diagnostics for unsupported IDL features

### Phase 3 Features

1. **Bidirectional Sync**: Track changes in C# and optionally update IDL
2. **GUI Tool**: Visual interface for reviewing and customizing imports
3. **Batch Mode**: Process entire directory trees with single command
4. **Documentation Generation**: Generate XML docs from IDL comments (if preserved in future `idlc` versions)

---

## Conclusion

The IDL Importer Tool completes the bidirectional bridge between legacy IDL systems and the modern FastCycloneDDS C# Bindings. By automating the translation process while maintaining wire compatibility, it enables teams to:

- Migrate existing DDS systems to high-performance C# implementations
- Integrate with existing IDL-based ecosystems
- Maintain clean, testable C# code while preserving DDS interoperability

The design prioritizes **correctness** (via round-trip testing), **usability** (via simple CLI), and **maintainability** (via shared components with CodeGen).
