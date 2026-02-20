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

The Importer automatically translates IDL types to the most efficient C# equivalent supported by the Zero-Alloc binding.

### Primitives

| IDL Type | C# Type | Notes |
| :--- | :--- | :--- |
| `long` | `int` | 32-bit signed |
| `unsigned long` | `uint` | 32-bit unsigned |
| `long long` | `long` | 64-bit signed |
| `float` | `float` | |
| `double` | `double` | |
| `boolean` | `bool` | Mapped to `byte` internally for ABI compatibility |
| `octet` | `byte` | |
| `char` | `byte` | 8-bit characters (ASCII) |

### Collections & Strings

| IDL Definition | Generated C# DSL | Description |
| :--- | :--- | :--- |
| `string name` | `public string Name;` | Unbounded string. |
| `string<32> id` | `[MaxLength(32)]`<br>`public string Id;` | Bounded string. |
| `sequence<double>` | `[DdsManaged]`<br>`public List<double> Data;` | Unbounded sequence. |
| `sequence<long, 10>` | `[MaxLength(10)] [DdsManaged]`<br>`public List<int> Data;` | Bounded sequence. |
| `long matrix[3][4]` | `[ArrayLength(12)] [DdsManaged]`<br>`public int[] Matrix;` | Multi-dimensional arrays are flattened to 1D. |

**Note on Collections:** All collection types (Strings, Lists, Arrays) are marked with `[DdsManaged]` to indicate they reside on the Managed Heap. The runtime Marshaller handles copying these to the native Arena during serialization.

### Complex Types

#### Nested Structs
**IDL:**
```idl
module Geom {
    struct Point { double x; double y; };
    struct Path { sequence<Point> points; };
};
```
**C# Output:**
```csharp
namespace Geom {
    [DdsStruct]
    public partial struct Point { public double X; public double Y; }

    [DdsTopic("Path")]
    public partial struct Path { 
        [DdsManaged] public List<Point> Points; 
    }
}
```

#### Unions
**IDL:**
```idl
union Result switch(long) {
    case 1: long success_value;
    case 2: string error_msg;
};
```
**C# Output:**
```csharp
[DdsUnion]
public partial struct Result {
    [DdsDiscriminator]
    public int _d;

    [DdsCase(1)]
    public int SuccessValue;

    [DdsCase(2)]
    public string ErrorMsg;
}
```

#### Nested Sequences (Aliases)
DDS IDL allows `sequence<sequence<long>>`. The Importer "unrolls" the intermediate typedefs generated by the IDL compiler to provide a clean C# API.

**IDL:** `sequence<sequence<long>> grid;`
**C# Output:** `public List<List<int>> Grid;`

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

### Multi-Dimensional Array Flattening
C# does not support `fixed` multidimensional buffers in the way C does.
*   **Result:** `long x[3][4]` becomes `public int[] x` with `[ArrayLength(12)]`.
*   **Usage:** You must access it as a flat array: `x[row * 4 + col]`.

### "Ref Struct" Errors
The Importer generates **DSL Structs** (standard classes/structs).
The **View Structs** (`ref struct`) are generated later by the `CycloneDDS.CodeGen` Roslyn generator during compilation.
*   **Fix:** Ensure your project references `CycloneDDS.CodeGen`. The View structs are not output to disk; they exist only in memory during the build (or in `obj/` if emitted).