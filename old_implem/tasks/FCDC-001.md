# Task FCDC-001: Schema Attribute Definitions

**ID:** FCDC-001  
**Title:** Schema Attribute Definitions  
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Phase:** 1 - Foundation & Schema Package  
**Estimated Effort:** 2-3 days  
**Dependencies:** None  

---

## Overview

Implement the core attribute classes that users will apply to their C# schema types to define DDS topics, unions, and field metadata. These attributes form the foundation of the C#-first DSL.

**Design Reference:** [Detailed Design §4 Schema DSL Design](../docs/FCDC-DETAILED-DESIGN.md#4-schema-dsl-design), [§4.4 Required Attributes](../docs/FCDC-DETAILED-DESIGN.md#44-required-attributes)

---

## Objectives

1. Create `CycloneDDS.Schema` project (class library targeting net8.0)
2. Implement all type-level attribute classes
3. Implement all field-level attribute classes
4. Implement union-specific attribute classes
5. Define proper AttributeUsage and validation
6. Add XML documentation for IntelliSense

---

## Acceptance Criteria

- [ ] Type-level attributes (`DdsTopicAttribute`, `DdsQosAttribute`, `DdsUnionAttribute`, `DdsTypeNameAttribute`) are implemented with correct AttributeUsage
- [ ] Field-level attributes (`DdsKeyAttribute`, `DdsBoundAttribute`, `DdsIdAttribute`, `DdsOptionalAttribute`) are implemented
- [ ] Union-specific attributes (`DdsDiscriminatorAttribute`, `DdsCaseAttribute`, `DdsDefaultCaseAttribute`) are implemented
- [ ] All attributes have XML documentation comments explaining usage
- [ ] Attributes have proper validation in constructors (e.g., max bound > 0)
- [ ] `DdsQosAttribute` has all required properties with correct types (Reliability, Durability, HistoryKind, HistoryDepth)
- [ ] Assembly is strongly named (for generator compatibility) ✅ FCDC-001
- [ ] Package metadata (version, author, description) is complete
- [ ] All attributes are sealed and [AttributeUsage] is correctly applied

---

## Implementation Details

### File Structure

```
src/CycloneDDS.Schema/
├── CycloneDDS.Schema.csproj
├── Attributes/
│   ├── TypeLevel/
│   │   ├── DdsTopicAttribute.cs
│   │   ├── DdsQosAttribute.cs
│   │   ├── DdsUnionAttribute.cs
│   │   └── DdsTypeNameAttribute.cs
│   ├── FieldLevel/
│   │   ├── DdsKeyAttribute.cs
│   │   ├── DdsBoundAttribute.cs
│   │   ├── DdsIdAttribute.cs
│   │   └── DdsOptionalAttribute.cs
│   └── UnionSpecific/
│       ├── DdsDiscriminatorAttribute.cs
│       ├── DdsCaseAttribute.cs
│       └── DdsDefaultCaseAttribute.cs
├── Enums/
│   ├── DdsReliability.cs
│   ├── DdsDurability.cs
│   └── DdsHistoryKind.cs
└── README.md
```

### Key Implementation Notes

#### 1. DdsTopicAttribute

```csharp
/// <summary>
/// Marks a type as a DDS topic schema definition and specifies the topic name.
/// This attribute is required for all types that will be used as DDS topics.
/// </summary>
/// <example>
/// [DdsTopic("PoseUpdate")]
/// public partial class PoseUpdate { /* ... */ }
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DdsTopicAttribute : Attribute
{
    /// <summary>
    /// Gets the DDS topic name.
    /// </summary>
    public string TopicName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsTopicAttribute"/> class.
    /// </summary>
    /// <param name="topicName">The DDS topic name (must not be null or whitespace).</param>
    /// <exception cref="ArgumentException">Thrown if topicName is null or whitespace.</exception>
    public DdsTopicAttribute(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name must not be null or whitespace.", nameof(topicName));
        
        TopicName = topicName;
    }
}
```

#### 2. DdsQosAttribute

```csharp
/// <summary>
/// Specifies default QoS (Quality of Service) settings for a DDS topic.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DdsQosAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the reliability policy.
    /// </summary>
    public DdsReliability Reliability { get; set; } = DdsReliability.Reliable;

    /// <summary>
    /// Gets or sets the durability policy.
    /// </summary>
    public DdsDurability Durability { get; set; } = DdsDurability.Volatile;

    /// <summary>
    /// Gets or sets the history kind.
    /// </summary>
    public DdsHistoryKind HistoryKind { get; set; } = DdsHistoryKind.KeepLast;

    /// <summary>
    /// Gets or sets the history depth (only relevant for KeepLast history).
    /// </summary>
    public int HistoryDepth { get; set; } = 1;

    // Constructor validation logic
    public DdsQosAttribute()
    {
        // Validation can be done in a separate validator during generation
    }
}
```

#### 3. DdsUnionAttribute

```csharp
/// <summary>
/// Marks a type as a DDS union schema definition.
/// The type must have exactly one field marked with [DdsDiscriminator]
/// and one or more fields marked with [DdsCase] or [DdsDefaultCase].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DdsUnionAttribute : Attribute
{
}
```

#### 4. DdsCaseAttribute

```csharp
/// <summary>
/// Marks a field as a union arm corresponding to a specific discriminator value.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class DdsCaseAttribute : Attribute
{
    /// <summary>
    /// Gets the discriminator value for this union arm.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsCaseAttribute"/> class.
    /// </summary>
    /// <param name="value">The discriminator value (enum literal or integral value).</param>
    public DdsCaseAttribute(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}
```

### Enum Definitions

```csharp
/// <summary>
/// Specifies the DDS reliability quality of service policy.
/// </summary>
public enum DdsReliability
{
    /// <summary>
    /// Best effort delivery (no retransmissions).
    /// </summary>
    BestEffort = 0,

    /// <summary>
    /// Reliable delivery (retransmissions on packet loss).
    /// </summary>
    Reliable = 1
}

/// <summary>
/// Specifies the DDS durability quality of service policy.
/// </summary>
public enum DdsDurability
{
    /// <summary>
    /// No persistent storage (default).
    /// </summary>
    Volatile = 0,

    /// <summary>
    /// Transient local durability (stored in memory).
    /// </summary>
    TransientLocal = 1,

    /// <summary>
    /// Transient durability (may be stored persistently).
    /// </summary>
    Transient = 2,

    /// <summary>
    /// Persistent durability (stored persistently).
    /// </summary>
    Persistent = 3
}

/// <summary>
/// Specifies the DDS history quality of service policy kind.
/// </summary>
public enum DdsHistoryKind
{
    /// <summary>
    /// Keep last N samples (N specified by HistoryDepth).
    /// </summary>
    KeepLast = 0,

    /// <summary>
    /// Keep all samples (subject to resource limits).
    /// </summary>
    KeepAll = 1
}
```

---

## Testing Requirements

### Unit Tests (FCDC-001-Tests project)

1. **Attribute Construction Tests**
   - Test DdsTopicAttribute with valid topic name
   - Test DdsTopicAttribute throws ArgumentException for null/empty topic name
   - Test DdsCaseAttribute with various discriminator value types

2. **Attribute Usage Tests**
   - Apply attributes to sample types and verify via reflection
   - Verify AllowMultiple = false prevents duplicate attributes
   - Verify attributes can be retrieved correctly

3. **Validation Tests**
   - DdsQosAttribute: HistoryDepth validation (future enhancement)
   - DdsBoundAttribute: Max > 0 validation

---

## Documentation Requirements

- [ ] XML documentation comments on all public types
- [ ] README.md explaining the attribute system
- [ ] Example usage snippets in XML comments
- [ ] NuGet package README

---

## Definition of Done

- All acceptance criteria met
- Unit tests pass with 100% coverage
- XML documentation complete
- Code review approved
- NuGet package builds successfully
- Attributes can be applied to test schema types without errors
