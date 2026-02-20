using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CycloneDDS.Compiler.Common.IdlJson
{
    /// <summary>
    /// Root structure of JSON output from 'idlc -l json'
    /// </summary>
    public class IdlJsonRoot
    {
        [JsonPropertyName("File")]
        public List<JsonFileMeta> File { get; set; } = new();

        [JsonPropertyName("Types")]
        public List<JsonTypeDefinition> Types { get; set; } = new();
    }

    /// <summary>
    /// File metadata - source IDL file information
    /// </summary>
    public class JsonFileMeta
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Members")]
        public List<JsonFileMeta> Members { get; set; } = new();
    }

    /// <summary>
    /// Complete type definition (struct, union, enum, alias)
    /// </summary>
    public class JsonTypeDefinition
    {
        /// <summary>
        /// Fully-qualified type name (e.g., "MyNamespace::MyStruct")
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type kind: "struct", "union", "enum", "alias", "sequence"
        /// </summary>
        [JsonPropertyName("Kind")]
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// For aliases: the target type name
        /// </summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>
        /// Extensibility mode: "final", "appendable", "mutable"
        /// </summary>
        [JsonPropertyName("Extensibility")]
        public string Extensibility { get; set; } = "final";

        /// <summary>
        /// For unions: discriminator type name
        /// </summary>
        [JsonPropertyName("Discriminator")]
        public string? Discriminator { get; set; }

        /// <summary>
        /// Type members (struct fields, union cases, enum values)
        /// </summary>
        [JsonPropertyName("Members")]
        public List<JsonMember> Members { get; set; } = new();

        /// <summary>
        /// For bounded types: size/bound value
        /// </summary>
        [JsonPropertyName("Bound")]
        public int? Bound { get; set; }

        /// <summary>
        /// For arrays/sequences
        /// </summary>
        [JsonPropertyName("CollectionType")]
        public string? CollectionType { get; set; }

        /// <summary>
        /// QoS settings (if topic has #pragma directives)
        /// </summary>
        [JsonPropertyName("QoS")]
        public JsonQoSSettings? QoS { get; set; }

        /// <summary>
        /// CRITICAL: DDS Topic Descriptor with serialization opcodes and key metadata
        /// </summary>
        [JsonPropertyName("TopicDescriptor")]
        public JsonTopicDescriptor? TopicDescriptor { get; set; }
    }

    /// <summary>
    /// Member definition (struct field, union case, enum value)
    /// </summary>
    public class JsonMember
    {
        /// <summary>
        /// Member name
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Member type name
        /// </summary>
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        /// <summary>
        /// Member kind (for nested definitions)
        /// </summary>
        [JsonPropertyName("Kind")]
        public string? Kind { get; set; }

        /// <summary>
        /// Member ID (for @mutable types)
        /// </summary>
        [JsonPropertyName("Id")]
        public int? Id { get; set; }

        /// <summary>
        /// Is this a key field? (@key annotation)
        /// </summary>
        [JsonPropertyName("IsKey")]
        public bool IsKey { get; set; }

        /// <summary>
        /// Is this an optional field? (@optional annotation)
        /// </summary>
        [JsonPropertyName("IsOptional")]
        public bool IsOptional { get; set; }

        /// <summary>
        /// Is this an external field? (@external annotation)
        /// </summary>
        [JsonPropertyName("IsExternal")]
        public bool IsExternal { get; set; }

        /// <summary>
        /// String/sequence bound (for bounded types)
        /// </summary>
        [JsonPropertyName("Bound")]
        public int? Bound { get; set; }

        /// <summary>
        /// Collection type: "array", "sequence"
        /// </summary>
        [JsonPropertyName("CollectionType")]
        public string? CollectionType { get; set; }

        /// <summary>
        /// Array dimensions (for multi-dimensional arrays)
        /// </summary>
        [JsonPropertyName("Dimensions")]
        public List<int>? Dimensions { get; set; }

        /// <summary>
        /// Union case labels (for union members)
        /// </summary>
        [JsonPropertyName("Labels")]
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Enum value (for enum members)
        /// </summary>
        [JsonPropertyName("Value")]
        public long? Value { get; set; }

        /// <summary>
        /// Member Offset
        /// </summary>
        [JsonPropertyName("Offset")]
        public int? Offset { get; set; }

        /// <summary>
        /// Nested members (for complex types)
        /// </summary>
        [JsonPropertyName("Members")]
        public List<JsonMember>? Members { get; set; }
    }

    /// <summary>
    /// Topic QoS settings from #pragma directives
    /// </summary>
    public class JsonQoSSettings
    {
        [JsonPropertyName("Reliability")]
        public string? Reliability { get; set; }

        [JsonPropertyName("Durability")]
        public string? Durability { get; set; }

        [JsonPropertyName("History")]
        public string? History { get; set; }

        [JsonPropertyName("HistoryDepth")]
        public int? HistoryDepth { get; set; }
    }

    /// <summary>
    /// DDS Topic Descriptor - contains all serialization metadata
    /// This is the CRITICAL structure for descriptor generation
    /// </summary>
    public class JsonTopicDescriptor
    {
        /// <summary>
        /// Struct size in bytes (C-ABI layout)
        /// </summary>
        [JsonPropertyName("Size")]
        public uint Size { get; set; }

        /// <summary>
        /// Struct alignment requirement
        /// </summary>
        [JsonPropertyName("Align")]
        public uint Align { get; set; }

        /// <summary>
        /// DDS descriptor flags
        /// </summary>
        [JsonPropertyName("FlagSet")]
        public uint FlagSet { get; set; }

        /// <summary>
        /// Type name for this descriptor
        /// </summary>
        [JsonPropertyName("TypeName")]
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// Key field descriptors with pre-calculated offsets
        /// </summary>
        [JsonPropertyName("Keys")]
        public List<JsonKeyDescriptor> Keys { get; set; } = new();

        /// <summary>
        /// XCDR2 serialization opcodes array
        /// Note: JSON often outputs as signed ints, but they're uint32 bitmasks
        /// Using long[] to safely capture both, cast to uint later
        /// </summary>
        [JsonPropertyName("Ops")]
        public long[] Ops { get; set; } = Array.Empty<long>();
    }

    /// <summary>
    /// Key field descriptor with pre-calculated byte offset
    /// </summary>
    public class JsonKeyDescriptor
    {
        /// <summary>
        /// Key field name
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// CRITICAL: Pre-calculated byte offset in struct
        /// This is computed by Cyclone DDS and guaranteed to be correct
        /// </summary>
        [JsonPropertyName("Offset")]
        public uint Offset { get; set; }

        /// <summary>
        /// Key field order/index
        /// </summary>
        [JsonPropertyName("Order")]
        public uint Order { get; set; }
    }
}
