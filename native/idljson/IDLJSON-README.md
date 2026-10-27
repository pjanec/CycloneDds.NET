# IDLJSON Plugin - Comprehensive Documentation

**Version:** 1.0  
**Author:** Fast Cyclone DDS C# Bindings Project  
**Date:** January 23, 2026

---

## Table of Contents

1. [Overview](#overview)
2. [What is the IDLJSON Plugin?](#what-is-the-idljson-plugin)
3. [How to Use the Plugin](#how-to-use-the-plugin)
4. [JSON Output Format](#json-output-format)
5. [Complete Type Examples](#complete-type-examples)
6. [Understanding #pragma topic](#understanding-pragma-topic)
7. [Use Cases](#use-cases)
8. [Technical Details](#technical-details)

---

## Overview

The **IDLJSON Plugin** is a compiler plugin for the Cyclone DDS IDL compiler (`idlc`) that generates structured JSON metadata from IDL (Interface Definition Language) files. Instead of only generating C code, this plugin extracts complete type information, memory layout, serialization descriptors, and QoS settings into a JSON format that is easily consumable by code generators in other languages (especially C#).

### Key Features

- ✅ **Complete Type Metadata**: Extracts structs, unions, enums, bitmasks, typedefs with full annotations
- ✅ **Memory Layout Information**: Computed struct sizes, member offsets, and alignment requirements (C-ABI compliant)
- ✅ **Topic Descriptors**: DDS serialization opcodes, key definitions, and flagsets
- ✅ **QoS Settings**: Reliability, durability, and history settings from IDL pragmas
- ✅ **Annotations Support**: @key, @optional, @external, @id, extensibility annotations
- ✅ **Collections**: Arrays, sequences (bounded/unbounded), strings (bounded/unbounded)

---

## What is the IDLJSON Plugin?

The IDLJSON plugin transforms IDL type definitions into a structured JSON representation. It is designed to enable **zero-copy, zero-allocation serialization** in C# by providing all the metadata needed to generate efficient serializers without parsing C code.

### Problem It Solves

**Traditional Workflow:**
```
IDL → idlc → C Header → Manual Parsing → C# Code
```
Issues: Error-prone, fragile, requires C parser, loses metadata

**IDLJSON Workflow:**
```
IDL → idlc -l json → JSON Metadata → C# Code Generator → Optimized C# Code
```
Benefits: Reliable, complete metadata, extensible, maintainable

### Output

The plugin generates a **`.json`** file containing:

1. **Type Definitions**: All user-defined types with their complete structure
2. **Memory Layout**: Byte offsets, sizes, and alignments for all members
3. **Serialization Opcodes**: DDS XCDR2 serialization instructions
4. **Key Information**: Which fields are part of the topic key
5. **QoS Settings**: Reliability, durability, history, and depth settings

---

## How to Use the Plugin

### Prerequisites

- Cyclone DDS with `idlc` compiler installed
- IDLJSON plugin library (`cycloneddsidljson.dll` on Windows, `libcycloneddsidljson.so` on Linux)

### Command Line Usage

```bash
# Basic usage - generates JSON output
idlc -l json MyTypes.idl

# Output files generated:
# - MyTypes.json     (JSON metadata - primary output)
```

### Integration Example

```bash
# In a build system (CMake, MSBuild, etc.)
cd my_idl_folder
idlc -l json *.idl

# Now use the JSON files for code generation
csharp-generator MyTypes.json --output ./Generated/MyTypes.cs
```

### Plugin Options

The plugin inherits standard `idlc` options and adds JSON-specific behavior:

```bash
# Generate with specific output directory
idlc -l json -o output_dir MyTypes.idl

# The JSON file will be created at: output_dir/MyTypes.json
```

---

## JSON Output Format

### Simplified schema
``` json
\{
  "File": \[ ... \],
  "Types": \[
    \{
      "Name": "string",
      "Kind": "struct|union|enum|alias|sequence",
      "Type": "string",
      "Extensibility": "mutable|appendable|final",
      "Discriminator": "string",
      "IsKey": true,
      "Id": 123,
      "IsOptional": true,
      "IsExternal": true,
      "Bound": 123,
      "CollectionType": "array|sequence",
      "Size": 123,
      "Labels": \[ "string", ... \],
      "Value": 123,
      "QoS": \{
        "Reliability": "string",
        "Durability": "string",
        "History": "string",
        "HistoryDepth": 123
      \},
      "TopicDescriptor": \{
        "Size": 123,
        "Align": 123,
        "FlagSet": 123,
        "TypeName": "string",
        "Keys": \[ \{ "Name": "string", "Offset": 123, "Order": 123 \} \],
        "Ops": \[ 1, 2, 3 \]
      \},
      "Members": \[ \{ ... recursive ... \} \],
      "\_eof": 0
    \}
  \]
\}
```


Note: The `_eof` field is a just **trailing comma workaround**. It ensures the JSON is always syntactically valid without needing to buffer or backtrack. It serves no semantic purpose for the consumer.



### Overall Structure

The generated JSON file has two top-level sections:

```json
{
  "File": [ /* Source file metadata */ ],
  "Types": [ /* All type definitions */ ]
}
```

### File Section

Contains source file information and include dependencies:

```json
"File": [
  {
    "Name": "MyTypes.idl",
    "Members": [
      { "Name": "CommonTypes.idl" },
      { "Name": "BaseStructs.idl" }
    ],
    "_eof": 0
  }
]
```

### Types Section

Contains all user-defined types (structs, unions, enums, typedefs):

```json
"Types": [
  { /* Struct definition */ },
  { /* Enum definition */ },
  { /* Union definition */ },
  ...
]
```

---

## Complete Type Examples

### Example 1: Basic Struct with Primitives

**IDL:**
```idl
struct SensorData {
    long sensor_id;
    float temperature;
    double pressure;
    boolean active;
    char status;
    octet raw_data;
};
```

**JSON Output:**
```json
{
  "Name": "SensorData",
  "Kind": "struct",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "sensor_id",
      "Type": "long",
      "_eof": 0
    },
    {
      "Name": "temperature",
      "Type": "float",
      "_eof": 0
    },
    {
      "Name": "pressure",
      "Type": "double",
      "_eof": 0
    },
    {
      "Name": "active",
      "Type": "boolean",
      "_eof": 0
    },
    {
      "Name": "status",
      "Type": "char",
      "_eof": 0
    },
    {
      "Name": "raw_data",
      "Type": "octet",
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Primitive Type Reference:**
- `boolean` → 1 byte
- `char`, `octet` → 1 byte
- `short`, `unsigned short` → 2 bytes
- `long`, `unsigned long`, `float` → 4 bytes
- `long long`, `unsigned long long`, `double` → 8 bytes
- `long double` → 16 bytes (platform-dependent)
- `string` → 8 bytes (pointer)
- `wstring` → 8 bytes (wide char pointer)

### Example 2: Struct with Keys and Topic Descriptor

**IDL:**
```idl
@topic
struct Vehicle {
    @key unsigned long vehicle_id;
    string<64> model;
    double speed;
    double fuel_level;
};
#pragma topic reliable transient_local keep_last 5
```

**JSON Output:**
```json
{
  "Name": "Vehicle",
  "Kind": "struct",
  "Extensibility": "final",
  "QoS": {
    "Reliability": "reliable",
    "Durability": "transient_local",
    "History": "keep_last",
    "HistoryDepth": 5
  },
  "TopicDescriptor": {
    "Size": 88,
    "Align": 8,
    "FlagSet": 0,
    "TypeName": "Vehicle",
    "Keys": [
      { "Name": "vehicle_id", "Offset": 0, "Order": 0 }
    ],
    "Ops": [
      251658244, 1, 196609, 0, 196609, 4, 196609, 8, 
      196609, 72, 0
    ]
  },
  "Members": [
    {
      "Name": "vehicle_id",
      "Type": "unsigned long",
      "IsKey": true,
      "_eof": 0
    },
    {
      "Name": "model",
      "Type": "string",
      "Bound": 64,
      "_eof": 0
    },
    {
      "Name": "speed",
      "Type": "double",
      "_eof": 0
    },
    {
      "Name": "fuel_level",
      "Type": "double",
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- `@key` annotation marks `vehicle_id` as a key field
- `TopicDescriptor` contains serialization opcodes for DDS
- `Keys` array lists all key fields with their byte offsets
- `Ops` array contains XCDR2 serialization instructions
- `string<64>` has a `Bound` of 64 characters

### Example 3: Enum with Explicit Values

**IDL:**
```idl
@bit_bound(16)
enum ErrorCode { 
    NO_ERROR = 0,
    WARNING = 100,
    @value(1000) CRITICAL_ERROR,
    FATAL_ERROR = 9999
};
```

**JSON Output:**
```json
{
  "Name": "ErrorCode",
  "Kind": "enum",
  "Bound": 16,
  "Members": [
    { "Name": "NO_ERROR", "Value": 0, "_eof": 0 },
    { "Name": "WARNING", "Value": 100, "_eof": 0 },
    { "Name": "CRITICAL_ERROR", "Value": 1000, "_eof": 0 },
    { "Name": "FATAL_ERROR", "Value": 9999, "_eof": 0 }
  ],
  "_eof": 0
}
```

**Key Points:**
- `@bit_bound(16)` indicates this enum uses 16-bit storage
- Default `Bound` is 32 if not specified
- Explicit `@value()` annotations preserved
- Sequential values auto-increment (WARNING follows NO_ERROR + 1, then explicit 1000, then FATAL_ERROR is explicit)

### Example 4: Union with Discriminator

**IDL:**
```idl
enum MessageType { TEXT, IMAGE, VIDEO };

@appendable
union MessagePayload switch (MessageType) {
    case TEXT:
        string<1024> text_content;
    case IMAGE:
        sequence<octet> image_data;
    case VIDEO:
        sequence<octet> video_data;
};
```

**JSON Output:**
```json
{
  "Name": "MessagePayload",
  "Kind": "union",
  "Discriminator": "MessageType",
  "Extensibility": "appendable",
  "Members": [
    {
      "Name": "text_content",
      "Type": "string",
      "Bound": 1024,
      "Labels": ["0"],
      "_eof": 0
    },
    {
      "Name": "image_data",
      "Type": "sequence",
      "Labels": ["1"],
      "_eof": 0
    },
    {
      "Name": "video_data",
      "Type": "sequence",
      "Labels": ["2"],
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- `Discriminator` field specifies the switch type (`MessageType` enum)
- `Labels` array contains the case values (as strings)
- Enum case labels converted to numeric strings ("0", "1", "2")
- Union members overlay the same memory (offsets would be identical)

### Example 5: Union with Default Case

**IDL:**
```idl
union DataValue switch (long) {
    case 1:
    case 2:
        long integer_value;
    case 3:
        double double_value;
    default:
        string string_value;
};
```

**JSON Output:**
```json
{
  "Name": "DataValue",
  "Kind": "union",
  "Discriminator": "long",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "integer_value",
      "Type": "long",
      "Labels": ["1", "2"],
      "_eof": 0
    },
    {
      "Name": "double_value",
      "Type": "double",
      "Labels": ["3"],
      "_eof": 0
    },
    {
      "Name": "string_value",
      "Type": "string",
      "Labels": ["default"],
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- Multiple case labels combined into a single `Labels` array
- `default` case represented by the string "default"
- Discriminator type is a primitive (`long` instead of enum)

### Example 6: Arrays (Fixed-Size Collections)

**IDL:**
```idl
struct Matrix3x3 {
    double elements[3][3];
    long row_labels[3];
};
```

**JSON Output:**
```json
{
  "Name": "Matrix3x3",
  "Kind": "struct",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "elements",
      "Type": "double",
      "CollectionType": "array",
      "Size": 9,
      "_eof": 0
    },
    {
      "Name": "row_labels",
      "Type": "long",
      "CollectionType": "array",
      "Size": 3,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- Multi-dimensional arrays flattened into total element count
- `elements[3][3]` becomes `Size: 9`
- `CollectionType: "array"` distinguishes from sequences
- Array memory is inline (not pointer-based)

### Example 7: Sequences (Dynamic Collections)

**IDL:**
```idl
struct DataSet {
    sequence<float> unbounded_floats;
    sequence<long, 100> bounded_longs;
    sequence<string<64>> string_list;
};
```

**JSON Output:**
```json
{
  "Name": "DataSet",
  "Kind": "struct",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "unbounded_floats",
      "Type": "sequence",
      "Kind": "sequence",
      "_eof": 0
    },
    {
      "Name": "bounded_longs",
      "Type": "sequence",
      "Kind": "sequence",
      "Bound": 100,
      "_eof": 0
    },
    {
      "Name": "string_list",
      "Type": "sequence",
      "Kind": "sequence",
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- Unbounded sequences have no `Bound` field
- Bounded sequences include `Bound` (maximum capacity)
- DDS sequences are represented as: `{ uint32_t _maximum; uint32_t _length; T* _buffer; bool _release; }`
- Size in memory: 24 bytes (on 64-bit systems)

### Example 8: Strings (Bounded and Unbounded)

**IDL:**
```idl
struct TextMessage {
    string unbounded_text;
    string<256> bounded_text;
    wstring wide_text;
    string<64> fixed_array[5];
};
```

**JSON Output:**
```json
{
  "Name": "TextMessage",
  "Kind": "struct",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "unbounded_text",
      "Type": "string",
      "_eof": 0
    },
    {
      "Name": "bounded_text",
      "Type": "string",
      "Bound": 256,
      "_eof": 0
    },
    {
      "Name": "wide_text",
      "Type": "wstring",
      "_eof": 0
    },
    {
      "Name": "fixed_array",
      "Type": "string",
      "Bound": 64,
      "CollectionType": "array",
      "Size": 5,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- Unbounded strings: `char*` pointer (8 bytes)
- Bounded strings: `char[N+1]` inline array
- Wide strings: `wchar_t*` pointer
- Arrays of strings combine array and bound metadata

### Example 9: Optional Fields

**IDL:**
```idl
@appendable
struct DeviceConfig {
    @optional long timeout;
    @optional string<128> custom_name;
    @optional sequence<float> calibration_data;
};
```

**JSON Output:**
```json
{
  "Name": "DeviceConfig",
  "Kind": "struct",
  "Extensibility": "appendable",
  "Members": [
    {
      "Name": "timeout",
      "Type": "long",
      "IsOptional": true,
      "_eof": 0
    },
    {
      "Name": "custom_name",
      "Type": "string",
      "Bound": 128,
      "IsOptional": true,
      "_eof": 0
    },
    {
      "Name": "calibration_data",
      "Type": "sequence",
      "Kind": "sequence",
      "IsOptional": true,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- `@optional` annotation sets `IsOptional: true`
- Optional fields are represented as pointers in C (e.g., `long* timeout`)
- Allows fields to be absent in serialized data
- Requires `@appendable` or `@mutable` extensibility

### Example 10: External Fields (Heap-Allocated)

**IDL:**
```idl
struct LargeData {
    @external sequence<double> big_dataset;
    @external string description;
};
```

**JSON Output:**
```json
{
  "Name": "LargeData",
  "Kind": "struct",
  "Extensibility": "final",
  "Members": [
    {
      "Name": "big_dataset",
      "Type": "sequence",
      "Kind": "sequence",
      "IsExternal": true,
      "_eof": 0
    },
    {
      "Name": "description",
      "Type": "string",
      "IsExternal": true,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- `@external` annotation sets `IsExternal: true`
- External fields use heap allocation (pointer to data)
- Reduces struct size when fields are large or optional

### Example 11: Member IDs (Mutable Types)

**IDL:**
```idl
@mutable
struct VersionedData {
    @id(1) long version;
    @id(10) string<128> data_v1;
    @id(20) sequence<octet> data_v2;
};
```

**JSON Output:**
```json
{
  "Name": "VersionedData",
  "Kind": "struct",
  "Extensibility": "mutable",
  "Members": [
    {
      "Name": "version",
      "Type": "long",
      "Id": 1,
      "_eof": 0
    },
    {
      "Name": "data_v1",
      "Type": "string",
      "Bound": 128,
      "Id": 10,
      "_eof": 0
    },
    {
      "Name": "data_v2",
      "Type": "sequence",
      "Kind": "sequence",
      "Id": 20,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- `@id(N)` annotation assigns explicit member IDs
- Member IDs enable field reordering in `@mutable` types
- Used for schema evolution and versioning
- `Id` field only present when explicitly annotated

### Example 12: Nested Structs

**IDL:**
```idl
@nested
struct Point3D {
    double x;
    double y;
    double z;
};

@nested
struct Color {
    octet red;
    octet green;
    octet blue;
};

struct PointCloud {
    sequence<Point3D> points;
    sequence<Color> colors;
    long point_count;
};
```

**JSON Output:**
```json
[
  {
    "Name": "Point3D",
    "Kind": "struct",
    "Extensibility": "final",
    "Members": [
      { "Name": "x", "Type": "double", "_eof": 0 },
      { "Name": "y", "Type": "double", "_eof": 0 },
      { "Name": "z", "Type": "double", "_eof": 0 }
    ],
    "_eof": 0
  },
  {
    "Name": "Color",
    "Kind": "struct",
    "Extensibility": "final",
    "Members": [
      { "Name": "red", "Type": "octet", "_eof": 0 },
      { "Name": "green", "Type": "octet", "_eof": 0 },
      { "Name": "blue", "Type": "octet", "_eof": 0 }
    ],
    "_eof": 0
  },
  {
    "Name": "PointCloud",
    "Kind": "struct",
    "Extensibility": "final",
    "Members": [
      {
        "Name": "points",
        "Type": "sequence",
        "Kind": "sequence",
        "_eof": 0
      },
      {
        "Name": "colors",
        "Type": "sequence",
        "Kind": "sequence",
        "_eof": 0
      },
      {
        "Name": "point_count",
        "Type": "long",
        "_eof": 0
      }
    ],
    "_eof": 0
  }
]
```

**Key Points:**
- `@nested` structs are not DDS topics (no TopicDescriptor)
- All types appear in the `Types` array
- Nested types can be referenced by name
- Type lookup resolves dependencies

### Example 13: Typedef Aliases

**IDL:**
```idl
typedef long Timestamp;
typedef string<256> Name;
typedef sequence<float> FloatArray;
typedef Timestamp TimestampArray[10];
```

**JSON Output:**
```json
[
  {
    "Name": "Timestamp",
    "Kind": "alias",
    "Type": "long",
    "_eof": 0
  },
  {
    "Name": "Name",
    "Kind": "alias",
    "Type": "string",
    "Bound": 256,
    "_eof": 0
  },
  {
    "Name": "FloatArray",
    "Kind": "alias",
    "Type": "sequence",
    "_eof": 0
  },
  {
    "Name": "TimestampArray",
    "Kind": "alias",
    "Type": "Timestamp",
    "CollectionType": "array",
    "Size": 10,
    "_eof": 0
  }
]
```

**Key Points:**
- `Kind: "alias"` indicates a typedef
- `Type` field references the aliased type
- Arrays of typedefs preserve both type and array metadata

### Example 14: Extensibility Modes

**IDL:**
```idl
@final
struct FinalStruct {
    long value;
};

@appendable
struct AppendableStruct {
    long value;
};

@mutable
struct MutableStruct {
    @id(1) long value;
};
```

**JSON Output:**
```json
[
  {
    "Name": "FinalStruct",
    "Kind": "struct",
    "Extensibility": "final",
    ...
  },
  {
    "Name": "AppendableStruct",
    "Kind": "struct",
    "Extensibility": "appendable",
    ...
  },
  {
    "Name": "MutableStruct",
    "Kind": "struct",
    "Extensibility": "mutable",
    ...
  }
]
```

**Extensibility Reference:**
- **final**: No evolution allowed (fastest serialization)
- **appendable**: Can add new fields at end (compatible evolution)
- **mutable**: Can reorder/modify fields using @id (full evolution)

### Example 15: Complex Nested Keys

**IDL:**
```idl
@nested
struct Address {
    unsigned long station_id;
    unsigned long process_id;
};

@topic
struct ProcessData {
    @key unsigned long instance_id;
    @key Address process_addr;
    string<256> status;
};
```

**JSON Output:**
```json
{
  "Name": "ProcessData",
  "Kind": "struct",
  "Extensibility": "final",
  "TopicDescriptor": {
    "Size": 272,
    "Align": 8,
    "FlagSet": 0,
    "TypeName": "ProcessData",
    "Keys": [
      { "Name": "instance_id", "Offset": 0, "Order": 0 },
      { "Name": "process_addr.station_id", "Offset": 4, "Order": 1 },
      { "Name": "process_addr.process_id", "Offset": 8, "Order": 2 }
    ],
    "Ops": [ /* ... */ ]
  },
  "Members": [
    {
      "Name": "instance_id",
      "Type": "unsigned long",
      "IsKey": true,
      "_eof": 0
    },
    {
      "Name": "process_addr",
      "Type": "Address",
      "IsKey": true,
      "_eof": 0
    },
    {
      "Name": "status",
      "Type": "string",
      "Bound": 256,
      "_eof": 0
    }
  ],
  "_eof": 0
}
```

**Key Points:**
- Nested struct keys are flattened: `process_addr.station_id`
- Dot-notation indicates member path traversal
- Each primitive field in the key gets its own entry with computed offset
- Offsets calculated recursively through struct hierarchy

---

## Understanding #pragma topic

### Critical Concept: #pragma topic Relates to the Struct ABOVE It

**⚠️ IMPORTANT:** The `#pragma topic` directive applies to the **struct definition that appears IMMEDIATELY ABOVE it** in the IDL file.

### How It Works

The plugin scans the source buffer **after** processing a struct definition to find any `#pragma topic` directives that follow the struct's closing brace. This design matches the natural reading order of IDL files where pragmas appear after the entity they describe.

### Syntax

```idl
#pragma topic <reliability> <durability> <history> <depth>
```

**Parameters:**
- **reliability**: `reliable` | `best_effort`
- **durability**: `volatile` | `transient_local` | `transient` | `persistent`
- **history**: `keep_last` | `keep_all`
- **depth**: Integer value (history depth, e.g., `1`, `10`, `100`)

### Example 1: Basic Topic QoS

```idl
struct Temperature {
    unsigned long sensor_id;
    @key float value;
    long long timestamp;
};
#pragma topic reliable transient_local keep_last 10
```

**Explanation:**
- The `#pragma topic` applies to the `Temperature` struct above it
- Sets reliability to "reliable"
- Sets durability to "transient_local" (late joiners get last sample)
- Sets history to "keep_last" with depth of 10 samples

**Generated JSON (excerpt):**
```json
{
  "Name": "Temperature",
  "Kind": "struct",
  "QoS": {
    "Reliability": "reliable",
    "Durability": "transient_local",
    "History": "keep_last",
    "HistoryDepth": 10
  },
  ...
}
```

### Example 2: Multiple Topics with Different QoS

```idl
// High-priority critical data
struct AlarmEvent {
    @key unsigned long alarm_id;
    string<256> message;
    unsigned long severity;
};
#pragma topic reliable persistent keep_all 0

// Low-priority sensor data
struct SensorReading {
    @key unsigned long sensor_id;
    float temperature;
    float humidity;
};
#pragma topic best_effort volatile keep_last 1

// Configuration data
struct SystemConfig {
    @key string<64> config_key;
    string<1024> config_value;
};
#pragma topic reliable transient_local keep_last 5
```

**Explanation:**
- **AlarmEvent**: Reliable, persistent, keep all history (critical events must not be lost)
- **SensorReading**: Best effort, volatile, keep last 1 (high-frequency data, only latest matters)
- **SystemConfig**: Reliable, transient_local, keep last 5 (configuration should be available for late joiners)

### Example 3: Struct Without QoS Pragma

```idl
@nested
struct Position {
    double x;
    double y;
    double z;
};
// No #pragma topic - this is a nested type, not a topic
```

**Explanation:**
- No `#pragma topic` means no QoS metadata in JSON
- The `@nested` annotation indicates this is not a DDS topic, just a nested struct
- Default QoS will be used if this is ever used as a topic

### Common Patterns

#### Pattern 1: State Data (Commands, Configuration)
```idl
struct RobotCommand {
    @key unsigned long command_id;
    string<128> command_type;
    sequence<octet> payload;
};
#pragma topic reliable transient_local keep_last 1
```
Use Case: Commands that must be delivered reliably, late joiners get current state

#### Pattern 2: Event Streams (Alarms, Logs)
```idl
struct LogEntry {
    @key long long sequence_num;
    string<256> message;
    unsigned long level;
};
#pragma topic reliable persistent keep_all 0
```
Use Case: Event history preserved permanently, all events delivered

#### Pattern 3: High-Frequency Telemetry
```idl
struct PositionUpdate {
    @key unsigned long vehicle_id;
    double latitude;
    double longitude;
    double altitude;
};
#pragma topic best_effort volatile keep_last 1
```
Use Case: High-rate updates where only latest value matters, no guarantees needed

---

## Use Cases

### Use Case 1: C# Code Generation

**Scenario:** Generate zero-allocation C# serializers for DDS topics

**Workflow:**
1. IDL files define DDS types → `idlc -l json *.idl`
2. JSON metadata consumed by C# code generator
3. Generate optimized C# classes with:
   - Fixed memory layouts matching C structures
   - Direct memory access (unsafe code)
   - Pre-computed offsets for serialization
   - Key extraction using descriptor offsets

**Benefits:**
- No runtime parsing or reflection
- Type-safe API
- Performance equivalent to C/C++

### Use Case 2: Language Interoperability

**Scenario:** Generate bindings for Python, Rust, Java, etc.

**Workflow:**
1. Single JSON metadata file consumed by multiple generators
2. Each generator produces idiomatic code for its language
3. All languages share same DDS wire format

**Benefits:**
- Consistent type definitions across languages
- Single source of truth (JSON metadata)
- Easy to add new language bindings

### Use Case 3: Runtime Type Introspection

**Scenario:** Build diagnostic tools that inspect DDS topics

**Workflow:**
1. Load JSON metadata at runtime
2. Use descriptors to decode serialized samples
3. Display fields, values, keys without prior knowledge

**Benefits:**
- Generic tools work with any DDS type
- No recompilation needed for new types
- Perfect for debugging and monitoring

### Use Case 4: Schema Evolution Validation

**Scenario:** Verify type compatibility between versions

**Workflow:**
1. Compare JSON metadata from old and new IDL versions
2. Validate extensibility rules (final/appendable/mutable)
3. Check member ID consistency for mutable types
4. Verify key compatibility

**Benefits:**
- Prevent breaking changes
- Automate compatibility testing
- Document API evolution

### Use Case 5: Performance Analysis

**Scenario:** Optimize struct layouts for cache efficiency

**Workflow:**
1. Analyze `Size`, `Align`, and member `offset` values
2. Identify padding/waste
3. Reorder fields (in mutable types) for better packing
4. Re-generate JSON and verify improvements

**Benefits:**
- Data-driven layout optimization
- Reduce memory footprint
- Improve cache locality

---

## Technical Details

### Memory Layout Calculation

The plugin implements **C-ABI structure packing rules** for x64 platforms:

1. **Alignment**: Each field aligns to its natural boundary (min of size and 8)
2. **Padding**: Insert padding bytes to satisfy alignment
3. **Struct Size**: Round up to largest member alignment

**Example:**
```idl
struct Example {
    char a;       // offset: 0, size: 1, align: 1
    // padding: 3 bytes
    long b;       // offset: 4, size: 4, align: 4
    double c;     // offset: 8, size: 8, align: 8
    short d;      // offset: 16, size: 2, align: 2
    // padding: 6 bytes
};
// Total size: 24 (rounded to align: 8)
```

### Topic Descriptor Opcodes

The `Ops` array contains DDS XCDR2 serialization instructions. These are low-level bytecode instructions used by DDS to serialize/deserialize topics.

**Common Opcodes:**
- `DDS_OP_ADR`: Field address offset
- `DDS_OP_TYPE`: Field type information
- `DDS_OP_KOF`: Key offset definition
- `DDS_OP_PLM`: Parameter list member (for mutable)
- `DDS_OP_RTS`: Return from struct

**Example Interpretation:**
```json
"Ops": [251658244, 2, 196611, 0, 196611, 4, ...]
```
- Instruction count and flags
- Type information for each field
- Offset values for each field
- Key definitions and order

### Type Resolution

The plugin resolves type names across:
- **Primitive types**: Built-in types (long, float, etc.)
- **User types**: Types defined in same or included files
- **Scoped names**: Module-qualified names (MyModule::MyStruct)
- **C-mangled names**: Underscore-separated names (MyModule_MyStruct)

Type lookup uses both IDL scoped names and C mangled names for robustness.

### QoS Pragma Parsing

The plugin scans the source buffer **line-by-line** after each struct definition:
1. Locate struct end line number from AST
2. Scan subsequent lines for `#pragma topic`
3. Tokenize pragma values (space-separated)
4. Parse and assign to QoS struct

**Implementation:**
```c
#pragma topic reliable transient_local keep_last 10
// Parsed as: ["reliable", "transient_local", "keep_last", "10"]
```

Each token is pattern-matched to determine its QoS category.

### Limitations

1. **Platform-Specific Layouts**: Assumes 64-bit x64 C-ABI (can be extended)
2. **Pragma Scope**: `#pragma topic` must appear **immediately after** struct
3. **Nested Topics**: Only top-level structs can be topics (enforced by `@nested`)
4. **Opcode Interpretation**: Requires understanding of DDS internal opcodes

---

## Conclusion

The IDLJSON plugin provides a **complete, machine-readable representation** of IDL type definitions, enabling robust code generation for any programming language. Its structured JSON output includes all metadata necessary for:

- ✅ Type-safe code generation
- ✅ Memory-efficient serialization
- ✅ Schema evolution
- ✅ Runtime introspection
- ✅ Language interoperability

**Next Steps:**
- Review the JSON output for your IDL files
- Integrate the plugin into your build system
- Develop code generators consuming the JSON metadata
- Leverage QoS pragmas for DDS topic configuration

---

**End of IDLJSON Plugin Documentation**
