# IDL Generation

CycloneDDS C# bindings automatically generate IDL files from your C# data structures. This allows seamless interoperability with other DDS implementations and provides control over how your types are represented in the DDS ecosystem.

---

## Table of Contents

- [How It Works](#how-it-works)
- [Quick Start](#quick-start)
- [Default Behavior](#default-behavior)
- [Grouping Types in Files](#grouping-types-in-files)
- [Legacy Interoperability](#legacy-interoperability)
- [Cross-Assembly Dependencies](#cross-assembly-dependencies)
- [Advanced Scenarios](#advanced-scenarios)
- [Troubleshooting](#troubleshooting)

---

## How It Works

When you build your project, the CycloneDDS code generator:

1. **Discovers** all DDS types (marked with `[DdsTopic]` or `[DdsStruct]`)
2. **Groups** them by target IDL file (based on C# filename or `[DdsIdlFile]` attribute)
3. **Resolves** dependencies between types and generates `#include` directives
4. **Emits** IDL files to your build output folder (`bin/Debug/net8.0/`)
5. **Embeds** metadata into your DLL for cross-assembly reference

**Key Benefits:**
- ✅ **Zero Configuration:** Works out of the box for 90% of cases
- ✅ **Smart Dependencies:** Automatically generates `#include` statements
- ✅ **Cross-Assembly:** Types from referenced DLLs "just work"
- ✅ **Legacy Friendly:** Override module names for C++ interop

---

## Quick Start

### Basic Topic Definition

```csharp
using CycloneDDS.Schema;

namespace MyApp.Messages
{
    [DdsTopic("SensorData")]
    public partial struct SensorData
    {
        [DdsKey]
        public int SensorId;
        
        public double Temperature;
        public double Humidity;
    }
}
```

**Build Output:** `SensorData.idl` in `bin/Debug/net8.0/`

```idl
// Auto-generated IDL

module MyApp {
    module Messages {
        struct SensorData {
            @key int32 SensorId;
            double Temperature;
            double Humidity;
        };
    };
};
```

**No extra steps needed!** The generator uses your C# filename and namespace to organize the IDL.

---

## Default Behavior

### File Naming

**Rule:** C# filename → IDL filename

| C# File | Generated IDL |
|---------|---------------|
| `Geometry.cs` | `Geometry.idl` |
| `SensorData.cs` | `SensorData.idl` |
| `CommonTypes.cs` | `CommonTypes.idl` |

### Module Hierarchy

**Rule:** C# namespace → IDL modules (dots become `::`)

| C# Namespace | IDL Modules |
|--------------|-------------|
| `MyApp` | `module MyApp { ... }` |
| `Corp.Common.Geo` | `module Corp { module Common { module Geo { ... }}}` |
| `Sensors.Temperature` | `module Sensors { module Temperature { ... }}` |

### Example

**C# Source:** `Data/CommonTypes.cs`
```csharp
namespace Corp.Common
{
    [DdsStruct]
    public partial struct Point3D
    {
        public double X, Y, Z;
    }
}
```

**Generated IDL:** `bin/Debug/net8.0/CommonTypes.idl`
```idl
module Corp {
    module Common {
        struct Point3D {
            double X;
            double Y;
            double Z;
        };
    };
};
```

---

## Grouping Types in Files

Use `[DdsIdlFile]` to group multiple types into a single IDL file.

### Example: Common Definitions

**C# Source:** `Types/BasicTypes.cs`
```csharp
using CycloneDDS.Schema;

namespace MyApp.Core
{
    [DdsStruct]
    [DdsIdlFile("CommonDefs")]  // Override default
    public partial struct Header
    {
        public int Sequence;
        public long Timestamp;
    }
    
    [DdsStruct]
    [DdsIdlFile("CommonDefs")]  // Same file
    public partial struct Footer
    {
        public int Checksum;
    }
}
```

**Generated IDL:** `bin/Debug/net8.0/CommonDefs.idl`
```idl
module MyApp {
    module Core {
        struct Header {
            int32 Sequence;
            int64 Timestamp;
        };
        
        struct Footer {
            int32 Checksum;
        };
    };
};
```

**Result:** Both types in one IDL file instead of two separate files.

---

## Legacy Interoperability

When integrating with existing C++ DDS systems, you may need to match their IDL structure exactly.

### Example: Matching C++ Modules

**Your C# Code:**
```csharp
using CycloneDDS.Schema;

namespace MyModernApp.Internal  // Your C# namespace (organization)
{
    [DdsTopic("SystemState")]
    [DdsIdlFile("LegacyCore")]           // Match legacy filename
    [DdsIdlModule("LegacySys::Core")]    // Match legacy modules
    public partial struct SystemState
    {
        [DdsKey]
        public int SystemId;
        
        public int Status;
        public string Description;
    }
}
```

**Generated IDL:** `bin/Debug/net8.0/LegacyCore.idl`
```idl
module LegacySys {
    module Core {
        struct SystemState {
            @key int32 SystemId;
            int32 Status;
            string Description;
        };
    };
};
```

**Result:** Your C# namespace doesn't have to match the legacy C++ structure. Perfect for gradual migration!

---

## Cross-Assembly Dependencies

Types from referenced assemblies automatically work. No manual configuration needed.

### Scenario: Shared Library

**Assembly A:** `Corp.Common.dll`

**File:** `Geometry.cs`
```csharp
using CycloneDDS.Schema;

namespace Corp.Common.Geometry
{
    [DdsStruct]
    [DdsIdlFile("MathDefs")]
    [DdsIdlModule("Math::Geo")]
    public partial struct Point3D
    {
        public double X, Y, Z;
    }
}
```

**Build Output:**
- `MathDefs.idl` in `bin/Debug/net8.0/`
- Metadata embedded in `Corp.Common.dll`

---

**Assembly B:** `Robot.Control.dll` (references `Corp.Common.dll`)

**File:** `Navigation.cs`
```csharp
using CycloneDDS.Schema;
using Corp.Common.Geometry;  // Import from Assembly A

namespace Robot.Control
{
    [DdsTopic("Trajectory")]
    public partial struct Trajectory
    {
        [DdsKey]
        public int RobotId;
        
        public Point3D StartPoint;               // Type from Assembly A
        public BoundedSeq<Point3D> Waypoints;    // Collection from A
    }
}
```

**Build Output:** `Navigation.idl` in `bin/Debug/net8.0/`
```idl
// Auto-generated IDL

#include "MathDefs.idl"  // ← Automatically added!

module Robot {
    module Control {
        struct Trajectory {
            @key int32 RobotId;
            Math::Geo::Point3D StartPoint;
            sequence<Math::Geo::Point3D> Waypoints;
        };
    };
};
```

**What Happened:**
1. Generator detected `Point3D` is from referenced assembly
2. Queried `Corp.Common.dll` metadata to find IDL file name
3. Generated `#include "MathDefs.idl"`
4. MSBuild copied `MathDefs.idl` from A to B's output folder

**No manual steps!** Just reference the assembly and use the types.

---

## Advanced Scenarios

### Mixing Defaults and Overrides

```csharp
namespace MyApp.Types
{
    // Uses defaults: MyApp::Types module, Types.idl file
    [DdsStruct]
    public partial struct BasicHeader
    {
        public int Sequence;
    }
    
    // Same namespace, but different IDL file
    [DdsStruct]
    [DdsIdlFile("AdvancedTypes")]
    public partial struct AdvancedHeader
    {
        public int Sequence;
        public long Timestamp;
        public string Source;
    }
    
    // Custom module for interop
    [DdsStruct]
    [DdsIdlFile("AdvancedTypes")]       // Grouped together
    [DdsIdlModule("Legacy::Header")]    // Different module hierarchy
    public partial struct LegacyFormat
    {
        public int Version;
    }
}
```

**Result:**
- `Types.idl` contains `BasicHeader` in `module MyApp::Types`
- `AdvancedTypes.idl` contains:
  - `AdvancedHeader` in `module MyApp::Types`
  - `LegacyFormat` in `module Legacy::Header`

### Transitive Dependencies

If your assembly graph is `App → LibB → LibA`, and:
- LibA defines `Point`
- LibB defines `Line { Point start, end; }`
- App defines `Shape { Line[] edges; }`

**Result:**
- App's output folder contains: `LibA.idl`, `LibB.idl`, `Shape.idl`
- `LibB.idl` has `#include "LibA.idl"`
- `Shape.idl` has `#include "LibB.idl"`

Everything is copied automatically. No manual include path configuration!

---

## Troubleshooting

### IDL File Not Generated

**Problem:** No `.idl` file in output folder.

**Check:**
1. Type is marked with `[DdsTopic]` or `[DdsStruct]`
2. Type is `partial struct` or `partial class`
3. Build succeeded (check for code generation errors)

**Example:**
```csharp
// ❌ Won't generate IDL
public struct MyData { }  // Missing [DdsTopic] and not partial

// ✅ Will generate IDL
[DdsTopic("MyData")]
public partial struct MyData { }
```

---

### External Type Not Found

**Error:**
```
Type 'Corp.Common.Point' from 'Corp.Common.dll' is used but has no [DdsIdlMapping].
Ensure the assembly was built with the CycloneDDS code generator.
```

**Solution:** Rebuild the referenced assembly with CycloneDDS tooling.

**Cause:** You're referencing an assembly that:
- Was built without CycloneDDS generator, OR
- Was built with an old version of CycloneDDS that doesn't embed IDL metadata

**Fix:**
1. Rebuild `Corp.Common` project
2. Ensure it references `CycloneDDS.Schema`
3. Ensure build runs code generation

---

### IDL Name Collision

**Error:**
```
IDL name collision detected:
  Type 1: 'MyApp.Data.Point'
  Type 2: 'MyApp.Geometry.Point'
  Both map to: 'Common.idl' → 'module Shared { struct Point }'

Use [DdsIdlModule] on one or both types to create distinct module paths.
```

**Problem:** Two C# types are generating the same IDL structure.

**Solution:** Use `[DdsIdlModule]` to differentiate:

```csharp
namespace MyApp.Data
{
    [DdsStruct]
    [DdsIdlFile("Common")]
    [DdsIdlModule("Shared::Data")]  // ← Differentiate
    public partial struct Point { }
}

namespace MyApp.Geometry
{
    [DdsStruct]
    [DdsIdlFile("Common")]
    [DdsIdlModule("Shared::Geometry")]  // ← Differentiate
    public partial struct Point { }
}
```

**Result:**
```idl
module Shared {
    module Data {
        struct Point { ... };
    };
    module Geometry {
        struct Point { ... };
    };
};
```

---

### Stale IDL Files

**Problem:** Changed `[DdsIdlFile("Old")]` to `[DdsIdlFile("New")]`, but `Old.idl` still exists.

**Solution:** Run **Clean Solution** before rebuilding.

**Why:** The generator creates new files but doesn't delete old ones automatically.

**Workaround:** Add to your `.csproj`:
```xml
<Target Name="CleanGeneratedIdl" BeforeTargets="Clean">
  <ItemGroup>
    <GeneratedIdl Include="$(OutputPath)*.idl" />
  </ItemGroup>
  <Delete Files="@(GeneratedIdl)" />
</Target>
```

---

## Best Practices

### ✅ Do

- **Use Defaults:** Let the generator infer file/module names when possible
- **Group Logically:** Use `[DdsIdlFile]` to group related types
- **Document Overrides:** Add comments explaining why you override modules
- **Rebuild Referenced Assemblies:** After updating CycloneDDS tools

### ❌ Don't

- **Don't use `.idl` extension:** `[DdsIdlFile("Types")]` not `[DdsIdlFile("Types.idl")]`
- **Don't use paths:** `[DdsIdlFile("Types")]` not `[DdsIdlFile("../Types")]`
- **Don't use C# syntax in modules:** `[DdsIdlModule("A::B")]` not `[DdsIdlModule("A.B")]`
- **Don't duplicate IDL identities:** Check for name collisions

---

## Quick Reference

| Attribute | Purpose | Example |
|-----------|---------|---------|
| `[DdsTopic]` | Define a DDS topic | `[DdsTopic("SensorData")]` |
| `[DdsStruct]` | Define a helper struct | `[DdsStruct]` |
| `[DdsIdlFile]` | Override IDL filename | `[DdsIdlFile("CommonTypes")]` |
| `[DdsIdlModule]` | Override module hierarchy | `[DdsIdlModule("Legacy::Core")]` |
| `[DdsKey]` | Mark key field | `[DdsKey] public int Id;` |

---

## Additional Resources

- [Full Design Document](docs/ADVANCED-IDL-GENERATION-DESIGN.md) - Comprehensive technical details
- [Task Specification](docs/SERDATA-TASK-MASTER.md#fcdc-s025) - Implementation requirements
- [CycloneDDS IDL Reference](https://cyclonedds.io/docs/cyclonedds/latest/idl_reference.html) - IDL language specification

---

**Need Help?** If you encounter issues not covered here, please check the design document or open an issue on GitHub.
