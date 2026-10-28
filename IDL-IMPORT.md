# IDL Import Guide

The **CycloneDDS.IdlImporter** is a CLI tool that converts standard OMG IDL (Interface Definition Language) files into the FastCycloneDDS C# Domain Specific Language (DSL).

It allows you to use existing IDL definitions from other systems (C++, Java, Python) and automatically generate the binary-compatible C# structs required to communicate with them.

## 📋 Prerequisites

1.  **Native IDL Compiler (`idlc`):**
    The importer relies on the official Eclipse Cyclone DDS compiler (`idlc` with our json plugin) to parse IDL files and verify syntax.
    *   **Windows:** Usually found in `%CYCLONEDDS_HOME%\bin\idlc.exe`
    *   **Linux/Mac:** Usually found in `/usr/local/bin/idlc`

2.  **FastCycloneDDS NuGets:**
    The generated code requires `CycloneDDS.Schema` referenced in your project.

---

## 🚀 Basic Usage

The importer can be run either as a single-file CLI tool or as a recursive batch script.

### Using the Single-File CLI

```bash
CycloneDDS.IdlImporter <master-idl> <source-root> <output-root> [options]
```

### Arguments

| Argument | Description |
| :--- | :--- |
| `<master-idl>` | The entry point file (e.g., `main.idl`). This is the file you want to convert. |
| `<source-root>` | The root directory where includes are resolved from. Usually the parent folder of your IDL files. |
| `<output-root>` | The directory where generated `.cs` files will be placed. |

### Options

| Option | Description |
| :--- | :--- |
| `--idlc-path <path>` | Explicit path to the `idlc` executable. If omitted, the tool searches `PATH` and standard install locations. |
| `--idlc-args <args>` | Extra arguments to pass directly to the underlying `idlc` compiler (e.g., `-f keylist -x appendable`). |
| `--verbose` | Enable detailed logging of type mapping and file processing. |

### Example

**Structure:**
```text
/src
  /idl
    shapes.idl
    geometry/point.idl
  /csharp_project
```

**Command:**
```bash
CycloneDDS.IdlImporter src/idl/shapes.idl src/idl src/csharp_project/Generated --idlc-args "-I src/common_idl"
```

### Using the Recursive PowerShell Script

For large workspaces containing multiple independent IDL packages, a helper script is included to automate discovery:

```powershell
.\CycloneDDS.IdlImporter.ps1 -SourceRoot <path> -OutputRoot <path> [-IdlcArgs <args>]
```

**Behavior:**
1. Scans the `SourceRoot` recursively.
2. When a folder with `.idl` files is found, it automatically identifies the "master" IDL (either the only IDL present, or the one matching the folder's name).
3. Invokes the `CycloneDDS.IdlImporter.exe` on that master IDL.
4. Stops recursing into subdirectories of that folder (treating it as a self-contained package root).

**Example:**
```powershell
.\CycloneDDS.IdlImporter.ps1 -SourceRoot "D:\WORK\MyProject\Idl" -OutputRoot "D:\WORK\MyProject\Generated" -IdlcArgs "-DVERSION=2"
```

---

## 🧠 Principles of Operation

The Importer uses a robust, two-stage process to guarantee correctness:

1.  **JSON Intermediate Representation:**
    Instead of writing a fragile regex parser, the tool invokes `idlc -l json`. This asks the native Cyclone DDS compiler to parse the IDL, resolve all C-preprocessor directives (`#include`, `#define`), and output a standardized JSON syntax tree.

2.  **Semantic Mapping:**
    The Importer reads the JSON and maps IDL concepts to C# concepts using the `CycloneDDS.Schema` attributes. It handles:
    *   **Modules** $\rightarrow$ C# Namespaces
    *   **Structs** $\rightarrow$ C# Partial Structs
    *   **Unions** $\rightarrow$ C# Structs with `[DdsUnion]`
    *   **Annotations** $\rightarrow$ C# Attributes (`@key` $\rightarrow$ `[DdsKey]`)

3.  **Recursive Processing:**
    The tool detects `#include` dependencies. It calculates the relative path of the included file and mirrors that folder structure in the output directory, ensuring your C# namespaces match your IDL module hierarchy.

---

## 🗺️ Type Mapping Reference

The generated C# reproduces the IDL exactly: the code generator turns it back into IDL with the same
wire format and the same type identity (XTypes type information), so the C# application interoperates
with peers built from the original IDL. A construct that cannot be reproduced exactly is not imported
(see [Not supported](#-not-supported)).

### Primitives

| IDL Type | C# Type | Notes |
| :--- | :--- | :--- |
| `boolean` | `bool` | |
| `octet` | `byte` | |
| `uint8` | `[DdsUInt8] byte` | `uint8` and `octet` are different types to XTypes |
| `char` | `[DdsChar] byte` | One byte, as in C (a C# `char` is two bytes) |
| `int8` | `sbyte` | |
| `short` / `int16` | `short` | |
| `unsigned short` / `uint16` | `ushort` | |
| `long` / `int32` | `int` | |
| `unsigned long` / `uint32` | `uint` | |
| `long long` / `int64` | `long` | |
| `unsigned long long` / `uint64` | `ulong` | |
| `float` / `double` | `float` / `double` | |

### Strings, Sequences, Arrays

| IDL Definition | Generated C# | Notes |
| :--- | :--- | :--- |
| `string name` | `[DdsManaged] public string name;` | |
| `string<32> id` | `[MaxLength(32)] [DdsManaged] public string id;` | |
| `sequence<double> data` | `[DdsManaged] public List<double> data;` | Elements: primitives, enums, structs, unions, unbounded strings |
| `sequence<long, 10> data` | `[DdsBound(10)] [DdsManaged] public List<int> data;` | |
| `long arr[4]` | `public fixed int arr[4];` | Arrays of primitives are `fixed` buffers (the struct is `unsafe`) |
| `double m[2][3]` | `[ArrayLength(2, 3)] public fixed double m[6];` | Row-major: `m[i][j]` is `m[i * 3 + j]` |

### Enums

`@bit_bound(8)`, `(16)` and `(32)` (the default) give `enum E : byte`, `: short` and `: int`. Values (`@value`) are kept.

### Typedefs

Typedefs are resolved to the type they name (`typedef sequence<long, 5> LongSeq; LongSeq s;` gives
`[DdsBound(5)] List<int> s`, `typedef long Row[4]; Row grid[3];` gives `int grid[3][4]`). The wire format is
unchanged; the type information names the resolved type instead of the alias, which Cyclone DDS treats
as assignable, so the types still match.

### Annotations

`@key`, `@optional` (value types become `T?`), `@id`, `@final` / `@appendable` / `@mutable` are kept.
A struct with a `@key` member becomes a `[DdsTopic]`, other structs `[DdsStruct]`.

### Unions

```idl
union Result switch (long) {
    case 1: long success_value;
    case 2: case 3: string error_msg;
    default: double other;
};
```
```csharp
[DdsExtensibility(DdsExtensibilityKind.Final)]
[DdsUnion]
public partial struct Result
{
    [DdsDiscriminator] public int _d;
    [DdsCase(1)] public int success_value;
    [DdsCase(2, 3)] [DdsManaged] public string error_msg;
    [DdsDefaultCase] public double other;
}
```
Discriminators can be integers, `boolean` (`[DdsCase(true)]`), `char` (`[DdsChar] byte`, labels as numbers) or enums.

### ❌ Not supported

The import stops and lists every member that uses one of these (nothing is written):

| IDL | What to do instead |
| :--- | :--- |
| `wchar`, `wstring` | `char` / `string` (UTF-8) |
| bitmask | an integer of the bitmask's size, with the bit values as constants |
| struct inheritance (`struct B : A`) | copy the base members into the struct, or make the base a member |
| `sequence<sequence<T>>` | wrap the inner sequence in a struct: `struct Row { sequence<T> items; }; sequence<Row>` |
| `sequence<string<N>>` | `sequence<string>` (the sequence itself may stay bounded) |
| arrays of strings, structs, enums or sequences | a sequence |
| `@optional` arrays, `@external` members | a plain member |
| `@bit_bound` other than 8, 16, 32 on an enum | one of those |

`long double` is rejected by `idlc` itself.

---

## 🏗️ Build Integration

You can integrate the importer directly into your `.csproj` to automatically regenerate C# code whenever your `.idl` files change.

Add this target to your project file:

```xml
<Target Name="ImportIdl" BeforeTargets="BeforeBuild" Inputs="@(IdlFiles)" Outputs="@(IdlFiles->'Generated\%(Filename).cs')">
    <Message Text="Importing IDL..." Importance="high" />
    <Exec Command="CycloneDDS.IdlImporter %(IdlFiles.Identity) $(ProjectDir)idl $(ProjectDir)Generated" />
</Target>
```

---

## ❓ Troubleshooting

### "idlc not found"
The tool attempts to locate `idlc` in:
1.  Current directory
2.  `CYCLONEDDS_HOME` environment variable
3.  System `PATH`
4.  Standard installation paths (`C:\cyclonedds\bin`, `/usr/local/bin`)

**Fix:** Install Cyclone DDS or use the `--idlc-path` option.

### "Type not found" in Generated Code
If you see errors about missing types in the generated C#, ensure that:
1.  You are importing the **Master IDL** file that includes the others.
2.  The `<source-root>` argument correctly points to the base folder where `#include` paths resolve.

### "cannot represent with the same wire format and type identity"
The IDL uses a construct from [Not supported](#-not-supported); the message names the member and what to use instead.

### "Ref Struct" Errors
The Importer generates **DSL Structs** (standard classes/structs).
The **View Structs** (`ref struct`) are generated later by the `CycloneDDS.CodeGen` Roslyn generator during compilation.
*   **Fix:** Ensure your project references `CycloneDDS.CodeGen`. The View structs are not output to disk; they exist only in memory during the build (or in `obj/` if emitted).