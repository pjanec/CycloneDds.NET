# Fast Cyclone DDS C# Bindings – High-Performance .NET Integration

Fast Cyclone DDS C# Bindings is a .NET library providing seamless integration with the Eclipse Cyclone DDS middleware. It allows you to define DDS topics (data types) directly in C# using a convenient attribute-based DSL, and publish/subscribe to them with zero-copy, zero-allocation performance in mind. The binding wraps the native Cyclone DDS C API (`ddsc.dll`) to ensure 100% on-wire compatibility and interoperability with Cyclone DDS applications in C/C++.

Key features include:

## Idiomatic C# Topic Definitions

Define your DDS data types as standard C# struct or class types, marked with custom attributes like `[DdsTopic]`, `[DdsKey]`, `[DdsStruct]`, etc., instead of using IDL files. The library’s code generator will produce all the necessary serialization/deserialization code behind the scenes.

For example, you can declare:

```csharp
[DdsStruct]
public partial struct Point3D
{
    public double X;
    public double Y;
    public double Z;
}

[DdsTopic("Robot")]
public partial struct RobotState
{
    [DdsKey] public int Id;        // key field for DDS instances
    public Point3D Position;       // nested struct (marked [DdsStruct])
}
```

This will be processed into a fully DDS-compatible topic definition. Attributes exist for all DDS IDL concepts (keys, optional fields, union discriminators/cases, bounded sizes, etc.), closely mirroring IDL annotations in an intuitive C# syntax.

## Wide IDL Feature Support

The binding’s DSL supports primitives (integers, floats, booleans, etc.), enums, structs, unions (using `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]`), arrays (fixed-length, annotated with `[ArrayLength]`), sequences (using .NET `List<T>` or a provided `BoundedSeq<T>` for bounded sequences), and strings.

For strings, you can use unbounded strings (marked with `[DdsManaged]` to allow GC allocation) or high-performance fixed-size strings (`FixedString32`, `FixedString64`, etc.) to avoid allocations. Bounded strings/sequences are supported via `[MaxLength(N)]` attributes or by using the `FixedStringN` types, which store UTF-8 text in a fixed-size struct buffer with no heap allocation on send/receive.

Complex field types must be marked with `[DdsStruct]` or `[DdsTopic]` to be recognized as DDS-serializable; the schema validator will flag any unsupported or unannotated types to ensure your data types are DDS-compliant. Optional fields (for appendable/mutable types) are indicated with `[DdsOptional]`, and mutable/extensible types also support explicit ID annotations via `[DdsId(id)]`.

## QoS and Extensibility Annotations

You can attach a `[DdsQos]` attribute to a topic type to specify default Reliability, Durability, and History QoS policies in code. The library also respects the DDS XTypes extensibility kind: by default it treats types as Appendable (allowing schema evolution), but you can override this with `[DdsExtensibility(DdsExtensibilityKind.Final)]` or Mutable as needed.

Under the hood, it adjusts the data representation to match the chosen extensibility. For example, the writer automatically sets the Data Representation QoS to XCDR2 for Appendable/Mutable types to ensure the serialized form matches Cyclone DDS expectations. XCDR2 imposes standard alignment and is required for full XTypes compatibility.

## High-Performance Zero-Allocation Serialization

A primary goal of this binding is minimal runtime overhead. All serialization logic is generated at build-time in efficient, unsafe C# code. There is no reflection or runtime code generation when sending data.

The generated code marshals struct fields directly into unmanaged memory buffers using pointers and spans, carefully avoiding heap allocations or unnecessary copies. When publishing a sample, the library precomputes the exact size needed, rents a buffer from the .NET array pool, and serializes fields into this buffer using direct memory writes. The buffer is pinned and a pointer is handed directly to the Cyclone DDS writer, allowing Cyclone to copy bytes into its network buffers without intermediate GC objects. The entire write path is designed to create zero garbage, with buffers reused via pooling.

Zero-copy reading is also supported. Subscribers can retrieve loaned sample buffers directly from Cyclone DDS to avoid copy-out. The binding offers a `Read()` / `Take()` API that returns a custom enumerable over incoming samples. This scope holds pointers to internal data and uses a generated unmarshaller to translate data into C# structs on the fly, leveraging Cyclone’s loan/return mechanisms.

When iterating over a scope, the library calls the generated marshal method to parse each sample’s bytes directly into a C# struct instance. Only the fields are copied out. Returning the loan simply informs Cyclone to free its buffers, with no extra copies. This design minimizes copying while presenting a safe, idiomatic iterator interface.

## Full Native Interoperability

This binding directly uses the Cyclone DDS C library for all core functionality, including participants, publishers/subscribers, and discovery. C# objects such as `DdsParticipant`, `DdsWriter<T>`, and `DdsReader<T>` are lightweight wrappers around native entities.

When a `DdsWriter<T>` is created, it registers the topic type with the underlying Cyclone runtime using a native topic descriptor. The memory layout and serialization of C# topic types exactly match Cyclone’s native IDL mapping. The library achieves this by generating topic descriptors identical to those produced by Cyclone’s own IDL compiler.

For each user-defined type, the build process imports or emits equivalent IDL, runs the Cyclone IDL compiler with JSON output, and parses the resulting metadata to obtain the official type opcode sequence, size, alignment, and key offsets. These are embedded into the generated C# type as static tables. At runtime, this metadata is marshaled into Cyclone’s native structures when creating DDS topics.

As a result, Cyclone DDS sees the C# type as if it were defined through its normal IDL pipeline. Samples written in C# are indistinguishable on the wire from samples written in C or C++. Publishers and subscribers can be freely mixed across languages with full interoperability.

## IDL Importer and Code Generation

For existing IDL files, the library provides an IDL Importer tool to generate corresponding C# types. The importer parses IDL definitions, including modules, namespaces, structures, unions, enums, annotations, and constraints, and emits partial C# structs with the appropriate `[Dds*]` attributes.

It preserves the original IDL structure and constraints, including correct type mappings, bounded strings, mutability, custom IDs, and default values. Nested includes are handled, and folder structure is maintained for traceability. After generation, applications can be written in C# as if the types were hand-written, while retaining full compatibility with Cyclone DDS.

## Tested Compatibility

The project includes comprehensive tests to verify that the C# binding’s behavior matches native Cyclone DDS expectations. Generated descriptors are compared against Cyclone’s own to catch discrepancies. During code generation, the IDL compiler output is used as a reference to ensure accuracy.

Runtime tests validate publishing and receiving data both within C# and across language boundaries. Cyclone’s self-describing type APIs are used to register types, so any mismatch would be rejected at topic creation time. Successful topic creation and data exchange, including keys and unions, provide strong proof of wire compatibility.

Unit tests also verify the zero-allocation guarantees by measuring allocations during critical operations.

## Comparison and Benefits

Compared to traditional DDS bindings or naive P/Invoke layers, this approach dramatically reduces overhead. Ahead-of-time code generation enables direct, optimized field access and tightly controlled memory usage. Use of array pooling, spans, and unsafe code avoids allocations and unnecessary copying.

Even strings can be handled in-place using fixed-size buffers, avoiding intermediate objects. The result is C# performance approaching native C, without sacrificing compatibility or DDS features. All Cyclone DDS capabilities, including QoS, data representation, and discovery, are available.


## Summary

Fast Cyclone DDS C# brings the full power of Cyclone DDS to .NET with a focus on speed and convenience. By combining Cyclone’s native IDL processing and networking engine with modern C# techniques such as source generators, unsafe memory access, and pooling, it enables real-time pub/sub applications in C# that meet demanding DDS performance requirements while maintaining wire-level fidelity with existing Cyclone DDS systems.

## ToManaged() and View Recursive Handling

The library supports deep copy of `View` structs to managed C# objects via the `ToManaged()` method. This method recursively converts nested structures and sequences from their native zero-copy representation into standard .NET `List<T>` and object graphs.
*   **Complex Nested Types**: Deep recursion for structs within structs, including sequences of nested types.
*   **Sequences of Booleans**: `bool[]` and `List<bool>` are now fully supported in zero-copy views, with optimized span-based access where possible and correct marshalling to managed lists.
*   **Safety**: The generated code strictly manages `unsafe` contexts, ensuring stability even when dealing with complex pointer arithmetic required for nested sequences.

## Performance Characteristics

Performance verification confirms that the zero-copy paths (using `View` directly) enable extremely high throughput by avoiding memory allocation. The `ToManaged()` fallback provides a safe, convenient API for when data ownership is required, with optimized implementation to minimize overhead during the copy process.

