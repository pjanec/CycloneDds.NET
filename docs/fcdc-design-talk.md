Cyclone DDS csharp bindings design talk

# Original Requirements

I am using cyclone dds, its plain-c implementation. I want to develop csharp bindings for it, with strong emphasis on performance and zero allocation where possible.

I want it for network ingress/egress for my high performance entity component system supporting both unmanaged POD structures as well as managed ones.

I need a way to define the structs/classes (serving as data structures for topics) so I can use custom data types (like Numeric.Quaternion, FixedString32, array of custom structs, bool, DateTime, Guid etc.)

Best if I could define the data structures in csharp, adding some attributes specifying that this structure/class should be used for generating a dds data binding code, also what topic name should it be associated with and what common QoS settings this topic should be using (reliability, durability, history depth). On the member field level a need attributes for marking the key field, for mapping the enum members to a concrete int value.

 I do not need to use those structs directly in my csharp code, i just want to use csharp as data definition language. The “working” csharp structs/classes could be generated (with proper field offset to match the raw data etc.)

Some pre-compiler would takes such csharp structures and generate IDL from them so that the cyclone’s idl compiler can generate the serialization code for desired platform. And the precompiler should also generate the csharp binding code for these structures.

I am also fine with if i define the data structs in IDL (somehow augmented \- like with specially formatted comments or something \- to allow defining the custom data types i mentioned above)

I also need to generate a csharp code/wrappers that internally use plain-c cyclone dds methods like dds\_take, dds\_read, dds\_write and similar supporting the conversion between native raw data and csharp same representations. I also need a wrapper for dds participants. All should support IDisposable and properly deallocate the native counterparts. and can convert between native sample/sampleinfo data and their csharp representation (especially the managed one as the POD are probably exactly same).

I do not need to follow the granularity of native dds calls. Best if the wrapper classes just support abstractions like participant, reader and writer. I need to be able to specify dds partition and dds domain per participant. The reader and writer could take participant as an argument and inherit these settings.

Best if the reader/writer could be a generic class taking the dds-genertaed csharp data struct/class representing a dds sample as a template (generics) parameter. And that based on the sample data type it automatically assigns the QoS setting (internally creating dds publisher/subscriber and assign the QoS, partitions etc.)

Writer must be able to send disposal sample.

I need xtypes 'appendable' (not mutable and not final) support for backward compatibility and I need to support unions (although no idea how to express that in c\#\!) . I need to support variable size data as well (very long strings, sequences etc.)

I also want to support the optional fields and null class references within a topic data type (represented by struct pointers in native plain-c code)

i interact with cyclone-dds entpoints only (just my sw component, all using same cyclone implementation), no interop necessary.

—----------

# Design talk

Below are the viable designs for “Cyclone DDS C core \+ high-perf C\# bindings \+ custom .NET-ish field types \+ (near) zero allocation”. I’ll compare them first, then give the approach I’d pick for your requirements (ECS ingress/egress, POD \+ managed, generic reader/writer, QoS/partition/domain metadata, disposal samples).

---

## **The 3 real approaches**

### **A) IDL-first (IDL is source of truth; C\# generated from IDL)**

**Flow**

1. Write `.idl` (plus your QoS/topic/“custom type” metadata via conventions/comments).  
2. Run Cyclone `idlc` → generates C type support (descriptors \+ (de)serialization metadata).  
3. Your generator reads the IDL AST and emits:  
   * C\# blittable “native” structs that match the C layout.  
   * Optional managed façade types \+ marshallers.

**Pros**

* Highest interoperability / least surprise: DDS is fundamentally IDL-driven.  
* Cyclone’s `idlc` output is the “happy path” for serialization metadata (opcodes) and descriptors. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/idl/compiler.html?utm_source=chatgpt.com))  
* Easiest to support “true DDS types” (bounded strings, sequences, unions, keys).

**Cons**

* You *don’t* get to use C\# as the DSL unless you build a second DSL layer anyway.  
* Expressing .NET-specific types (Guid/DateTime/FixedString32/Numeric.Quaternion) is awkward unless you define explicit IDL representations and conversion rules.

**Best when**: you’re okay authoring IDL and want maximum DDS portability.

---

### **B) C\#-first schema (C\# is the DSL; generate IDL \+ C \+ C\# glue)**

**Flow**

1. You write C\# “schema types” annotated with attributes:  
   * Topic name  
   * Common QoS  
   * Field attributes (key, fixed string length, fixed arrays, etc.)  
   * Custom type mapping hints  
2. A **Roslyn source generator** produces:  
   * `.idl` files  
   * Generated C\# blittable structs (the “working” ones you actually use in perf paths)  
   * Generated marshallers (managed ↔ native layout)  
   * A small metadata table per topic (topic name, QoS defaults, type name, key fields, etc.)  
3. Build runs Cyclone `idlc` on the generated IDL to get Cyclone’s C descriptors/serialization.  
4. Your C\# runtime binds to Cyclone C API (P/Invoke) and uses the `idlc` descriptors to create topics/readers/writers.

**Pros**

* Exactly matches what you asked for: “use C\# as data definition language; generate working structs/classes; then generate IDL so Cyclone generates serialization”.  
* Lets you define a *strict* “DDS-safe subset” of C\# (unmanaged layout \+ explicit rules) while still allowing ergonomic declarations.  
* You can support both:  
  * **POD/blittable fast path** (zero alloc, no per-sample GC work)  
  * **Managed façade path** (conversion code that you control)

**Cons**

* Most engineering up front: you’re building a schema compiler.  
* You must be disciplined about what schema constructs are allowed, otherwise layout/ABI/serialization mismatches will bite.

**Best when**: you want “C\# as DSL” \+ performance \+ control. (This is the one I’d pick for you.)

---

### **C) Serialized-path (CDR) bindings (avoid `idlc` for C structs; move serialization to the edge)**

Cyclone has APIs to read/write serialized samples:

* `dds_readcdr` exposes serialized data via internal serdata structures. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/0.9.1/ddsc_api_docs/function_dds_8h_1aea8f64776b3bc034f8f86b1fe3966b47.html?utm_source=chatgpt.com))  
* `dds_writecdr` writes serialized values provided as serdata. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/0.8.2/ddsc_api_docs/function_dds_8h_1a8a059f2e870ab8cb5ea70aa8025cd693.html?utm_source=chatgpt.com))

**Pros**

* You can treat Cyclone like a transport and keep your own serialization/layout rules.  
* Potentially great for “bridge” scenarios or unknown types.

**Cons**

* You now own CDR serialization \+ alignment \+ endianness \+ XTypes details.  
* Harder to get “DDS-native” behaviors (keying, instance lifecycle, dispose semantics) correct without leaning on `idlc`\-generated type support.  
* You’ll end up rebuilding what `idlc` already gives you, but in C\#.

**Best when**: you want custom wire format control or dynamic/unknown types. For your “ECS hot path” it’s *tempting* but usually ends up being more work and risk than it looks.

---

## **Recommendation for your requirements**

### **Pick Approach B (C\#-first schema \+ `idlc` in the build), with a two-representation model**

You want:

* **Unmanaged POD** samples that can be sent/received with minimal overhead  
* **Managed** samples also supported  
* **Strong emphasis on performance and zero allocation**

The clean way is:

1. **Schema types (C\# DSL)**: what you author  
2. **Native layout types (generated, unmanaged)**: what the DDS reader/writer uses  
3. **Managed façade types (optional, generated)**: what higher layers may use  
4. **Marshallers (generated)**: conversion between (2) and (3), with *caller-controlled memory* so you can be allocation-free in steady state.

---

## **Key design decisions (the ones that make or break perf)**

### **1\) Only allow `unmanaged` for the “native layout” generic parameter**

Your hot-path DDS types should be `where TNative : unmanaged`. That guarantees blittable layout and enables:

* stackalloc / Span-based copies  
* `Unsafe.ReadUnaligned`/`Unsafe.As` style tricks  
* direct pinning for writes (or direct use with loaned buffers)

Managed types become **separate** generic parameters with an explicit marshaller:

* `DdsWriter<TNative>`  
* `DdsWriter<TManaged, TNative>` where marshaller is source-generated

### **2\) Use Cyclone loaning when possible (zero-copy-ish receive \+ send buffers)**

Cyclone supports loaning and returning loans (for reads/takes) and also loan mechanisms for shared memory configurations. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/api/loan.html?utm_source=chatgpt.com))  
If you can model your “native layout” types to match what Cyclone expects, you can:

* take loaned samples and wrap them as `ReadOnlySpan<TNative>` (carefully) until `dds_return_loan`  
* loan a sample/buffer for writing, fill it, then `dds_write` (or `dds_writecdr` for serialized/shared-memory buffer cases) ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/0.10.2/api/loan.html?utm_source=chatgpt.com))

Even if you don’t get perfect zero-copy across all configs, designing around the loan APIs is the best shot at “zero allocation, minimal copy”.

### **3\) Make “custom .NET types” purely schema sugar with explicit DDS storage representations**

For each “nice” .NET type you listed, define an unambiguous DDS storage form:

* `Numeric.Quaternion` → `struct { float x; float y; float z; float w; }`  
* `FixedString32` → `char[32]` **or** `octet[32]` \+ encoding rule (usually UTF-8, NUL padded)  
* `bool` → DDS `boolean` (be careful about C\# `bool` size/layout in structs; prefer `byte` in native layout for explicitness)  
* `DateTime` → `int64` ticks (UTC) or DDS `Time_t` mapping (pick one and standardize)  
* `Guid` → `octet[16]` (little-endian vs RFC4122 byte order: pick one and document)  
* `array of custom structs` → **fixed arrays** map nicely; variable-size sequences require allocation unless you impose max bounds and use pooled buffers.

The important part: your generator must produce **deterministic IDL** and **deterministic native layout**.

### **4\) QoS defaults: encode in metadata \+ apply automatically in constructors**

Your wrapper can keep the public surface very small:

* `Participant(domainId, partition)`  
* `Writer<TNative>(participant)` and `Reader<TNative>(participant)`  
  …and it resolves:  
* topic name (from generated metadata)  
* QoS policies (from generated metadata)  
* creates publisher/subscriber internally and applies partition \+ QoS

Cyclone docs explicitly describe that writers inherit settings from publisher or can set QoS on writer itself. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/about_dds/datawriters.html?utm_source=chatgpt.com))  
So your implementation can choose a consistent rule, e.g.:

* partition on publisher/subscriber  
* reliability/durability/history on reader/writer QoS

### **5\) Disposal samples: use Cyclone’s “write+dispose” API**

Cyclone provides `dds_writedispose(writer, data)` which writes a sample and marks the instance as disposed. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/0.8.2/ddsc_api_docs/function_dds_8h_1ad16fb6bd42165796bd3f88c52a9c23c1.html?utm_source=chatgpt.com))  
Your `Writer<T>` should expose something like:

* `Write(in TNative sample)`  
* `Dispose(in TNative keyFieldsSample)` (or `WriteDispose(in TNative sample)`), depending on your instance/key strategy.

---

## **What the generated C\# should look like (conceptually)**

### **Schema (what you write)**

* Attributes:  
  * `[DdsTopic("Pose", Reliability = Reliable, Durability = Volatile, HistoryDepth = 8)]`  
  * `[DdsKey]` on key fields  
  * `[DdsFixedString(32)]`, `[DdsFixedArray(16)]`, `[DdsTypeAlias("...")]`, etc.

### **Generated native layout (what DDS uses)**

* `[StructLayout(LayoutKind.Sequential, Pack=...)]`  
* only primitives \+ fixed buffers \+ other unmanaged structs  
* no `string`, no `DateTime`, no `Guid` (those are schema sugar), no `bool` unless you lock it to `byte`

### **Generated managed façade (optional)**

* properties of `Quaternion`, `Guid`, `DateTime`, etc.  
* not used in the hot path unless you want it

### **Generated marshaller**

* `static void ToNative(in Managed src, out Native dst)`  
* `static void ToManaged(in Native src, ref Managed dst, …buffers…)`  
  Crucially: provide overloads that take caller-provided scratch buffers / pools so conversion is allocation-free in steady state.

---

## **When IDL-first might be better than C\#-first (so you can decide)**

Choose **IDL-first** if any of these are true:

* You must interop with many non-.NET DDS participants and want the IDL as the contract.  
* You need complex IDL constructs (unions, optional members via XTypes, extensive annotations) and don’t want to re-express them in C\# attributes.  
* You want to reuse existing IDL pipelines/tools without maintaining a Roslyn generator.

Otherwise, **C\#-first** is the best match for your stated goals.

---

## **A practical “minimum viable” plan (so it doesn’t balloon)**

1. **Phase 1 (POD only)**  
   * C\# schema subset: structs only, fixed arrays only, fixed strings only, numeric primitives, nested structs.  
   * Generate IDL \+ native C\# structs.  
   * Implement `Participant`, `Reader<TNative>`, `Writer<TNative>`.  
   * Implement read/take into caller-provided buffers; implement loan return.  
   * Implement `WriteDispose` via `dds_writedispose`. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/0.8.2/ddsc_api_docs/function_dds_8h_1ad16fb6bd42165796bd3f88c52a9c23c1.html?utm_source=chatgpt.com))  
2. **Phase 2 (Managed façade \+ marshalling)**  
   * Add generated managed types and marshallers.  
   * Add `Reader<TManaged, TNative>` / `Writer<TManaged, TNative>` wrappers.  
   * Enforce “no allocation” by requiring caller to pass a `Scratch`/pool context for variable/bounded data.  
3. **Phase 3 (Sequences / variable-length)**  
   * Only if needed. This is where “zero allocation” gets tricky.  
   * Prefer bounded sequences and a reusable arena/pool model.

---

## **Bottom line**

Given your constraints (ECS hot path, “C\# as schema”, custom .NET-ish types, desire for IDisposable-based wrappers, generic Reader/Writer with auto QoS/topic binding), the best fit is:

**C\#-first schema \+ Roslyn generator → generate IDL \+ native C\# structs \+ QoS metadata \+ marshallers → run Cyclone `idlc` → P/Invoke Cyclone C API \+ loaning \+ `dds_writedispose`.** ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/idl/compiler.html?utm_source=chatgpt.com))

—  
c\# dsl looks really good, but i need xtypes 'appendable' support and I need to support unions (although no idea how to express that in c\#\!) . I need to support variable size data as well (very long strings, sequences etc.)  
—

If you need **XTypes extensibility (`@appendable`, optionally `@mutable`) \+ unions \+ unbounded strings/sequences**, you can still keep the **C\#-first DSL**—but you have to accept one big reality:

**Variable-size data and unions require an explicit “representation layer” and lifetime management.** You can still make the *steady-state* path allocation-free via pooling/arenas, but “zero allocation always” is not compatible with truly unbounded strings/sequences.

Below is a concrete approach that preserves your “C\# as schema” experience while mapping cleanly to Cyclone’s IDL/XTypes model.

---

## **1\) XTypes extensibility in Cyclone: what you need to emit in IDL**

Cyclone’s IDL compiler supports XTypes extensibility annotations `@final`, `@appendable`, and `@mutable` (with the caveat that `@mutable` support is limited—e.g., Cyclone notes it’s only supported for structs). ([Cyclone DDS](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))  
Also, Cyclone’s default extensibility is `@final` unless you override it. ([Cyclone DDS](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))

### **DSL implication**

Your C\# schema generator must be able to emit:

* `@appendable` on structs/unions (and possibly modules) when requested  
* **Stable member IDs** for appendable/mutable evolution:  
  * In “appendable”, adding new fields at the end is fine; *do not reorder/remove*.  
  * In “mutable”, reordering/removal is possible but then member IDs become critical (often via `@id(...)` / `@autoid(...)` in OMG IDL). Cyclone has a set of supported/unsupported XTypes features; your generator should stay inside what Cyclone supports. ([GitHub](https://github.com/eclipse-cyclonedds/cyclonedds/blob/master/docs/dev/xtypes_relnotes.md?utm_source=chatgpt.com))

**Recommendation:** For your first iteration, target **`@appendable` \+ explicit member IDs (where supported)** and treat `@mutable` as “advanced mode / limited support,” because it’s the easiest way to stay compatible and avoid edge-case surprises.

---

## **2\) Unions: how to express them in C\# DSL (without fighting the language)**

### **The DDS/IDL model**

An IDL union is effectively: `(discriminator, active-case payload)`. Cyclone supports unions in IDL as a first-class construct. ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/idl/about.html?utm_source=chatgpt.com))

### **Best C\#-DSL encoding**

Use a **discriminated union pattern** with attributes, not a C\# `union` (C\# doesn’t have one). Example schema pattern:

* A *discriminator enum* (or primitive)  
* A *partial “union schema” type* with *case members* annotated

Conceptually:

* `[DdsUnion] partial struct Foo`  
* `[DdsDiscriminator] public FooKind Kind;`  
* `[DdsCase(FooKind.A)] public AType A;`  
* `[DdsCase(FooKind.B)] public BType B;`  
* `[DdsDefaultCase] public DefaultType Default;` (optional)

Your generator turns that into IDL:

@appendable union Foo switch(FooKind) {  
  case A: AType a;  
  case B: BType b;  
  default: DefaultType d;  
};

### **Runtime representation choices**

You’ll want **two generated representations** again:

1. **Native union layout** (unmanaged)  
   * `discriminator` \+ a fixed-size payload region (explicit layout) large enough for the largest case  
   * This is fast but requires careful alignment/pack rules.  
2. **Managed union façade** (optional)  
   * ergonomic “oneof-like” API, possibly modeled like a `readonly struct Foo` with `Kind` \+ typed accessors.

This keeps unions fast on ingress/egress and still usable elsewhere.

---

## **3\) Variable-size strings & sequences: you won’t get “always zero alloc,” but you *can* get “zero alloc steady-state”**

### **How Cyclone maps IDL variable-size**

Cyclone’s C mapping uses pointers for unbounded strings and sequences (e.g., `char*` for `string`, and sequences become pointer+length+capacity depending on mapping). ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/idl/about.html?utm_source=chatgpt.com))

That means every sample potentially involves:

* allocating memory for the payload, or  
* reusing memory provided by a pool/arena, and then freeing/returning it properly.

### **The 3 strategies (pick one per field type)**

#### **Strategy 1 — Bound everything**

Use `string<MAX>` and `sequence<T, MAX>` (bounded) wherever possible.

* Best for performance and predictable memory.  
* Easiest to represent as fixed buffers / inline storage in your generated native layout.  
* Still XTypes-friendly.

This is the “ECS-friendly” choice.

#### **Strategy 2 — Two-type model: Keyed header \+ blob topic**

If you truly have “very long strings” / big arrays:

* Publish a small, fixed-size “header” topic (appendable) containing:  
  * `Guid id`, `uint32 length`, `uint32 crc`, etc.  
* Publish the bulk payload on a separate “blob” topic (or chunked).  
  This avoids per-sample huge allocations in the hot path and gives you better control (pooling, chunk reuse, backpressure).

#### **Strategy 3 — Unbounded strings/sequences with pooling**

If you must keep it as a single DDS type:

* Treat `string` / `sequence<T>` as “borrowed memory” in the native layout.  
* Your reader API must provide a **loan/return** or **arena scope**:  
  * `Take(ref Arena arena, Span<TNative> dst, ...)`  
  * The arena owns all allocations for that batch; returning the arena returns memory.  
    This can be effectively zero allocation after warm-up (arena reuses blocks), but not “strictly zero” in the cold path.

---

## **4\) What this means for your generic Reader/Writer design**

### **For POD (fixed-size)**

Keep your original plan:

* `Reader<TNative> where TNative : unmanaged`  
* `Writer<TNative> where TNative : unmanaged`  
* Use loaning where possible.

### **For variable-size / unions / managed façades**

Add an explicit “marshalling context” concept:

* `Reader<TManaged, TNative, TMarshaller>`  
* `Writer<TManaged, TNative, TMarshaller>`

Where:

* `TNative` is the exact Cyclone C layout produced by your generator (may include pointers for strings/sequences).  
* `TMarshaller` is source-generated and *requires* an arena/pool for variable-size fields.

Example usage shape (conceptually):

* `reader.Take(arena, spanOfManaged, out count)`  
* `writer.Write(arena, in managed)`  
  Arena can be per-thread (TLS) or passed explicitly to keep it deterministic.

This is the only scalable way to keep allocations under control with truly variable-size data.

---

## **5\) Appendable \+ evolution rules inside a C\#-first DSL**

If you’re serious about appendable compatibility, the generator should enforce:

* **Append-only** rule for `@appendable` structs: new fields must be added only at the end.  
* Stable field ordering and stable field names (or stable IDs).  
* A “schema version report” at build time (diff old/new schema) to prevent accidental breaking changes.

Cyclone’s own material emphasizes `@appendable` as the evolution mechanism (append fields, keep compatibility). ([Cyclone DDS](https://cyclonedds.io/content/blog/xtypes-features.html?utm_source=chatgpt.com))

---

## **6\) Disposal samples still straightforward**

Expose:

* `Writer<T>.Write(in T sample)`  
* `Writer<T>.Dispose(in T keySample)` or `WriteDispose(in T sample)`

Cyclone has an explicit “write-dispose” call (`dds_writedispose`). ([Cyclone DDS](https://cyclonedds.io/docs/cyclonedds/latest/api/dynamic.html?utm_source=chatgpt.com))

---

## **The approach I’d pick for you (given *appendable \+ unions \+ unbounded data*)**

**Keep C\#-first DSL**, but formalize **three “field kinds”** with different codegen \+ runtime semantics:

1. **Inline (fixed-size) fields**: unmanaged, blittable, hot-path friendly  
   * fixed arrays, bounded strings, nested structs, fixed union payload  
2. **Borrowed variable fields**: pointers/lengths in native layout \+ arena ownership  
   * unbounded strings, sequences  
3. **Managed façade fields**: friendly .NET types converted via generated marshaller (still using arena for variable-size)

That yields:

* Very high performance for the ECS path (most data should be (1))  
* Full DDS/XTypes feature coverage for the cases where you truly need (2)  
* A clean way to model unions (discriminator \+ cases)

---

## **Practical “don’t get stuck” constraints**

To avoid a never-ending compiler project:

* Start with `@appendable` only; delay `@mutable` until you’ve validated Cyclone’s supported subset against your needs. ([Cyclone DDS](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))  
* Support unions in schema early (they’re structural and predictable).  
* Treat unbounded strings/sequences as **“requires Arena”** (compile-time enforced).  
* Encourage bounded alternatives and/or the “header \+ blob topic” pattern for huge payloads.

---

I need xtypes 'appendable' (not mutable and not final). I also want to support the null class references within a topic data type (represented by struct pointers in native plain-c code).  
I can use bound types where i know the max size but i need to support also truly unbound variable sizes.  
The pooling strategy for unboud strings/sequences sounds really great to avoid unnecesary allocations.

—-

You can satisfy all of this (appendable XTypes, nullable “class refs”, bounded \+ truly unbounded variable-size, and pooling to avoid allocations) with a C\#-first DSL **as long as you make “presence \+ ownership” explicit in the generated representations**.

Below is the design that fits your constraints and maps cleanly onto Cyclone’s IDL/XTypes \+ C mapping.

---

## **1\) Force `@appendable` everywhere (and never `@final`)**

Cyclone’s `idlc` supports `@final`, `@appendable`, `@mutable`, but **defaults to `@final`** (Cyclone chose this for compatibility with non-XTypes DDS). So your generator must **explicitly emit `@appendable`** on every relevant type (or at least at module scope). ([cyclonedds.io](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))

**Recommendation**

* In generated IDL, put `@appendable` on a top-level module and/or each struct/union:  
  * `@appendable module MyTopics { ... }` and/or `@appendable struct Foo { ... };` ([cyclonedds.io](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))  
* In the C\# DSL, make appendable the *only* allowed extensibility (since you don’t want mutable/final). That simplifies evolution rules.

**Evolution rules you should enforce in the generator**

* “Append-only”: new members may only be added at the end  
* No reordering/removal  
* No type changes for existing members  
* Provide a build-time schema diff warning/error if violated

---

## **2\) Nullable “class references” (native struct pointers) – use XTypes optional members, not raw pointers**

What you want semantically is: “field may be absent (null reference)”.

In DDS-XTypes, the standard way is `@optional` on a member (field presence). Vendors map this to “optional wrapper” types in higher-level languages. ([community.rti.com](https://community.rti.com/static/documentation/connext-dds/current/doc/manuals/connext_dds_professional/extensible_types_guide/extensible_types/Optional_Members.htm?utm_source=chatgpt.com))

Cyclone supports the `@optional` annotation in its IDL feature set (and it has real interoperability implications re: XCDR2—see below). ([GitHub](https://github.com/eclipse-cyclonedds/cyclonedds/issues/2194?utm_source=chatgpt.com))

### **How to express it in your C\# DSL**

Add something like:

* `[DdsOptional]` on a field (or `T?` with an attribute to opt-in, but don’t rely on C\# nullability alone).  
* For “nullable nested struct/class” you generate `@optional <type> field;` in IDL.

### **Why not “pointer fields” in IDL?**

Cyclone supports `@external` to make a non-pointer type a pointer-type in language mappings (i.e., it can affect generated C layout), but that is **not the same** as XTypes presence semantics. ([cyclonedds.io](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))  
If your goal is “null reference inside topic data,” **presence** is what you really mean, not “address of some memory”.

So:

* Use `@optional` to represent “maybe present”.  
* Use `@appendable` for evolution.

### **Interop note you should plan for**

Using `@optional` can require XTypes/XCDR2 compatibility with some peers; Cyclone has an issue describing topic creation refusal unless the remote side uses XCDR2 in at least one compatibility scenario. ([GitHub](https://github.com/eclipse-cyclonedds/cyclonedds/issues/2194?utm_source=chatgpt.com))  
So: if you need to interop with non-XTypes DDS participants, you may need a fallback encoding (below).

### **Fallback encoding if you must support non-XTypes peers**

Represent “nullable ref” as:

* `boolean hasFoo;`  
* `Foo foo;` (still in the type; ignore when `hasFoo == false`)  
  This is portable but less elegant; still appendable-friendly.

---

## **3\) Truly unbounded strings/sequences \+ pooling: the right “ownership model”**

Unbounded `string` and `sequence<T>` in IDL are inherently variable-size. In the classic C mapping, they’re effectively pointer-based (e.g., `char*` for string), and something has to own allocation/free. (General DDS behavior; Cyclone follows OMG IDL syntax and typical mappings.) ([cyclonedds.io](https://cyclonedds.io/docs/cyclonedds/latest/idl/syntax.html?utm_source=chatgpt.com))

### **Your best option: Arena / pool per take-batch**

You already like pooling; make it a first-class runtime concept:

* On **read/take**, you can decode into memory owned by an arena that you reuse.  
* On **write**, you encode from your managed data into an arena-backed native representation, write, then reset arena (or keep for reuse).

This gives you:

* **No per-sample GC allocations** in steady state  
* Deterministic lifetime (“valid until arena reset/return”)

### **Two practical layers (works well with your ECS)**

1. **Fast path** (bounded/inline): no arena needed, fully unmanaged  
2. **Variable path** (unbounded): arena required, explicit lifetime

In API terms, that becomes compile-time enforced:

* `Reader<TNative> where TNative : unmanaged` (bounded/inline)  
* `Reader<TManaged, TNative, TMarshaller>.Take(Arena arena, ...)` (variable / optional / union w/ payload pointers)

---

## **4\) Plug pooling into Cyclone itself where possible (big win)**

Cyclone lets you override its internal allocator via `ddsrt_set_allocator` by providing custom malloc/calloc/realloc/free hooks. ([cyclonedds.io](https://cyclonedds.io/docs/cyclonedds/latest/config/allocation-config.html?utm_source=chatgpt.com))

This is extremely relevant because:

* Even if *your* C\# side is pooling, Cyclone may still allocate for certain internal operations or for constructing samples for variable-size types.  
* A custom allocator can route those allocations into a pool you control (or at least into a high-performance allocator).

**Recommendation**

* Implement a native allocator shim (C) that uses a scalable allocator (mimalloc/jemalloc) *or* your own slab/arena system.  
* Use that as the process allocator for Cyclone via `ddsrt_set_allocator`. ([cyclonedds.io](https://cyclonedds.io/docs/cyclonedds/latest/config/allocation-config.html?utm_source=chatgpt.com))  
  This won’t eliminate all allocations, but it can make them predictable and cheap.

---

## **5\) How to combine appendable \+ optional \+ unbounded safely**

Here’s the consistent ruleset that works:

### **In your C\# DSL, categorize each field as one of:**

* **Inline**: fixed-size (primitives, nested fixed structs, fixed arrays, bounded strings)  
* **Optional**: `@optional` presence (nullable ref semantics)  
* **Variable**: unbounded `string` / `sequence<T>`

### **Generated output**

* **IDL**: emits `@appendable` everywhere and `@optional` where needed. ([cyclonedds.io](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))  
* **Native C\# layout**: for variable fields, use pointer+length (or whatever your generated C layout requires); for optional, include the representation implied by XTypes (presence bit in serialized form; your marshaller will handle it).  
* **Marshaller**: requires an `Arena` for variable/optional content.

### **Reader contract**

* Data with variable/optional content is only valid until:  
  * you return the DDS loan (if you use loaned samples), and/or  
  * you reset/return the arena

Make that explicit in types (e.g., return a `BorrowedSample<T>` struct that implements `Dispose()` to return loan/reset scope).

---

## **6\) Null “class refs” \+ *appendable* evolution**

If you model refs as `@optional`, you get evolution friendliness:

* Old readers can ignore new optional fields appended later  
* New readers can handle missing optional fields from older writers

That’s exactly the sweet spot of `@appendable` \+ `@optional`. ([cyclonedds.io](https://cyclonedds.io/content/guides/supported-idl.html?utm_source=chatgpt.com))

---

## **Recommended end-state architecture (what I’d build)**

1. **Roslyn schema generator**:  
   * Outputs IDL with `@appendable` \+ `@optional` \+ your unions/sequences/strings.  
   * Outputs:  
     * `TNative` unmanaged structs (bounded/inline)  
     * `TNativeVar` native structs for variable-size types (with pointers)  
     * `TManaged` façade (optional)  
     * `TMarshaller` that knows how to use an arena for strings/sequences/optional payloads  
     * topic/QoS metadata table  
2. **Build step** runs Cyclone `idlc` on generated IDL (for C descriptors/opcodes).  
3. **Runtime**:  
   * P/Invoke Cyclone C API  
   * Participant/Reader/Writer abstractions  
   * “Variable” read/write APIs require an `Arena` (pool)  
   * Optionally installs Cyclone custom allocator via `ddsrt_set_allocator` for even tighter control. ([cyclonedds.io](https://cyclonedds.io/docs/cyclonedds/latest/config/allocation-config.html?utm_source=chatgpt.com))

---

i interact with cyclone-dds entpoints only (just my sw component, all using same cyclone implementation), no interop necessary.  
---

Good — if everything on the wire is **Cyclone↔Cyclone only**, you can fully lean into **XTypes/XCDR2** features (appendable \+ optional \+ unions) without carrying “interop baggage”. That simplifies nullable refs and makes your pooling strategy much cleaner.

Here’s the approach I’d take, concretely.

---

## **1\) Make XTypes/XCDR2 the baseline**

* Emit `@appendable` on every generated type (or at module scope \+ per type for clarity). Cyclone’s default is `@final`, so you must emit it explicitly.  
* Use XTypes optional fields (`@optional`) for null “class references” inside topics.  
* Use unions natively in IDL (Cyclone supports them).

Since you’re Cyclone-only, you can standardize on XCDR2; Cyclone’s own discussion of optional members highlights XCDR2 compatibility as the relevant dimension, which you can now assume.

---

## **2\) Nullable “class references”: model as `@optional` nested structs**

This gives you “null reference semantics” *without* inventing pointer tricks.

### **DSL rule**

Any schema member annotated as optional means:

* It may be absent  
* When absent, your managed façade sees `null`  
* When present, it contains a value

### **IDL emission**

@appendable struct Parent {  
  @optional Child child;  
};

### **Generated runtime representation**

You typically generate either:

* a presence flag \+ inlined `Child` (fast, simple, no extra allocations), or  
* a presence bit \+ pointer to arena-owned `Child` (if Child itself contains unbounded fields and you want to avoid deep copies)

Because you don’t need interop, you can pick whichever is best for your performance model. Most people do **presence \+ inlined** for fixed-size child, and **presence \+ arena** for variable-size child.

---

## **3\) Unbounded strings/sequences: adopt an Arena “scope” as a first-class API concept**

Unbounded fields will always be variable-size; the trick is **no GC allocations** and **minimal native allocations**.

### **The winning model**

* All variable-size data is **arena-owned**.  
* Reader `Take`/`Read` returns data that is valid until you dispose the scope.  
* Writer `Write` takes either:  
  * already-arena-backed native value, or  
  * managed value \+ arena to materialize native backing.

This is exactly how you get “zero alloc steady-state” without lying to yourself about unbounded sizes.

### **API shape (conceptual)**

* `using var scope = reader.TakeScope(maxSamples);`  
* `ReadOnlySpan<TNative> samples = scope.Samples;`  
* `ReadOnlySpan<SampleInfo> infos = scope.Infos;`  
* Scope.Dispose returns DDS loans \+ resets the arena.

No per-sample allocations, amortized arena growth only when encountering larger-than-before payloads.

---

## **4\) Use Cyclone’s allocator override to further reduce native churn**

Even with your arena, Cyclone may allocate internally (especially around variable-size handling). Cyclone exposes an allocator override via `ddsrt_set_allocator`.

Since your whole system is Cyclone-only, you can:

* route Cyclone allocations into a high-perf allocator (mimalloc/jemalloc), **or**  
* route them into a pool you control (more effort, but maximum determinism)

This often gives a big “free” win in tail latencies.

---

## **5\) Unions in the DSL: a clean C\# encoding**

Keep it explicit and generator-friendly:

* `[DdsUnion] partial struct Command`  
* `[DdsDiscriminator] public CommandKind Kind;`  
* `[DdsCase(CommandKind.Move)] public MoveCmd Move;`  
* `[DdsCase(CommandKind.Spawn)] public SpawnCmd Spawn;`  
* `[DdsDefaultCase] public NoopCmd Default;`

Generator emits:

@appendable union Command switch(CommandKind) { ... };

Runtime:

* Native union layout: discriminator \+ max-sized payload storage (unmanaged)  
* Optional managed façade: ergonomic typed accessors

Because you control both sides, you can standardize a union layout and keep it fast.

---

## **6\) Practical codegen rules that keep appendable safe**

Because you’re appendable-only, enforce:

* **append-only member additions**  
* **no reordering**  
* **no removal**  
* (optional) emit explicit member IDs for future-proofing if Cyclone supports/needs them for your use cases; otherwise append-order is your contract.

Have the generator emit a “schema digest” and fail the build if a breaking change is detected.

---

## **7\) What I would *standardize* for your system (so it stays coherent)**

### **Field kinds**

1. **Inline fixed**: primitives, fixed arrays, bounded strings, nested fixed structs  
2. **Optional**: `@optional` fields to represent null references  
3. **Variable**: unbounded strings/sequences → always arena-backed

### **Generated representations per topic type**

* `TNative` (unmanaged, used by DDS)  
* `TManaged` (optional, used by app)  
* `TMarshaller` (generated; uses Arena; no hidden allocations)

### **Reader/Writer families**

* `DdsReader<TNative>` / `DdsWriter<TNative>` for pure-inline types  
* `DdsReader<TManaged, TNative, TMarshaller>` / `DdsWriter<...>` for variable/optional-heavy types

And keep QoS/topic metadata auto-bound as you originally wanted.

---

## **8\) Disposal samples**

Keep a single method:

* `WriteDispose(in TNative sample)` (or `DisposeInstance(key)` if you want a key-only path)

Cyclone provides `dds_writedispose`.

---

## **The net: what you get**

* Appendable everywhere (`@appendable`) ✅  
* Null “refs” inside topic types via `@optional` ✅  
* Unions ✅  
* Truly unbounded strings/sequences ✅  
* “Zero alloc steady-state” via Arena scopes \+ (optionally) Cyclone allocator override ✅  
* Still C\#-first DSL ✅

---

sounds really great. pls write a specification for such a csharp binding system.  
—

## **Specification: Cyclone DDS C-core High-Performance C\# Binding System (C\#-first DSL, XTypes `@appendable`, optional refs, unions, unbounded data with pooling)**

### **1\. Purpose**

Provide a high-performance C\# binding layer over **Cyclone DDS C API** (plain C implementation) that supports:

* C\# as a **data definition language** (DSL) for topic types.  
* XTypes **`@appendable`** extensibility (mandatory for all generated types).  
* DDS **unions** (IDL unions) expressed in C\# DSL.  
* **Nullable “class references”** within topic data, modeled via XTypes **`@optional`** members.  
* Both **bounded** and **truly unbounded** strings/sequences.  
* **Pooling / arena allocation** to achieve near-zero allocations in steady state.  
* A minimal runtime API surface: **Participant**, **Reader**, **Writer** with `IDisposable`.  
* Auto-binding of **topic name** and **common QoS defaults** from schema metadata.  
* Disposal samples (`write+dispose` semantics).

### **2\. Non-goals**

* Interoperability with non-Cyclone DDS implementations (system assumes Cyclone↔Cyclone).  
* Support for XTypes `@mutable` or `@final` (not generated).  
* Full DDS API surface exposure (no 1:1 mapping of all native calls).  
* Runtime reflection-driven type discovery (all bindings are source-generated).

---

## **3\. Terminology**

* **Schema type**: user-authored C\# type used as DDS schema input only.  
* **Native type** (`TNative`): generated blittable/unmanaged C\# struct mirroring Cyclone’s C layout and used for `dds_read/dds_take/dds_write`.  
* **Managed type** (`TManaged`): optional generated ergonomic C\# type (nullable refs, `string`, `Guid`, `DateTime`, etc.).  
* **Marshaller**: generated converter between `TManaged` and `TNative` using an **Arena** for variable-sized data.  
* **Arena**: a reusable pooled memory context for variable-sized buffers (native and/or managed backing).  
* **Take scope**: an RAII object that owns DDS loans and Arena lifetime for a batch.

---

## **4\. System architecture**

### **4.1 Build-time pipeline (codegen)**

1. **Roslyn Source Generator (Schema Compiler)** consumes C\# schema types.  
2. Generator emits:  
   * IDL (`.idl`) files with `@appendable`, `@optional`, unions, bounded/unbounded types.  
   * Generated C\#:  
     * `TNative` structs (unmanaged)  
     * optional `TManaged` types  
     * marshallers  
     * per-topic metadata (topic name, QoS defaults, type name, key fields)  
3. Build invokes **Cyclone `idlc`** on generated IDL to produce Cyclone type support artifacts required by the C runtime (type descriptors/ops).  
4. A small native shim library (C) is built that:  
   * links Cyclone  
   * optionally installs a custom allocator  
   * exposes helper functions needed for efficient interop.

### **4.2 Runtime components**

* `DdsParticipant`: wraps a Cyclone domain participant \+ default partition(s).  
* `DdsReader<T...>`: wraps subscriber+reader, uses schema metadata to create topic and QoS.  
* `DdsWriter<T...>`: wraps publisher+writer, uses schema metadata to create topic and QoS.  
* `Arena`: pooled allocator for variable-size payload materialization.  
* `TakeScope<T...>`: owns DDS loans \+ Arena slice.

---

## **5\. C\# Schema DSL**

### **5.1 Allowed schema constructs (v1)**

Schema types are **partial** and annotated. They are not required to be used at runtime.

Supported:

* `struct` and `class` schema declarations (generator decides produced runtime shapes).  
* Primitive fields: `byte/sbyte/short/ushort/int/uint/long/ulong/float/double`  
* `bool` (schema-level; native may represent as `byte` for deterministic layout)  
* Enums with specified underlying type  
* Nested schema types  
* Fixed-size arrays (bounded)  
* Bounded strings (`string<Max>`)  
* Unbounded strings (`string`)  
* Bounded/unbounded sequences  
* Unions  
* Optional members (nullable refs)  
* Keys (`@key`) if needed for instance lifecycle / dispose semantics

Not supported in schema types:

* Arbitrary reference graphs / cyclic references  
* Reflection-only constructs (dynamic, object, generics inside schema except approved containers)  
* Implicit layout without explicit rules

### **5.2 Required type-level attributes**

* `[DdsTopic("TopicName")]`  
* `[DdsAppendable]` (optional in C\#, but generator must enforce appendable in IDL regardless; attribute can exist as documentation \+ validation trigger)  
* `[DdsQos(...)]` for reliability/durability/history depth defaults  
* `[DdsTypeName("…")]` optional override for IDL type name

### **5.3 Field/member attributes**

* `[DdsKey]` key member  
* `[DdsOptional]` marks XTypes optional member (nullable reference)  
* `[DdsBound(Max = N)]` for bounded strings/sequences  
* `[DdsFixedArray(N)]` for fixed arrays  
* `[DdsTypeMap(...)]` for custom .NET types mapping (see §6)  
* `[DdsCase(...)]`, `[DdsDiscriminator]`, `[DdsDefaultCase]` for unions (§7)  
* `[DdsId(n)]` optional explicit member ID (future-proof; if emitted, must remain stable)

### **5.4 Appendable evolution constraints (generator enforced)**

For every `@appendable` struct/union:

* Members may only be **added at the end**  
* No reordering, removal, or type changes of existing members  
* Optional members may be added later, but still appended  
* Generator produces a **schema fingerprint** and fails the build on breaking changes  
* (Optional) Supports explicit member IDs to decouple from order, but IDs must remain stable

---

## **6\. Custom .NET types mapping**

### **6.1 Mapping principle**

Schema types may use user-friendly .NET types, but **wire/storage type is explicit** and deterministic in IDL and `TNative`.

Examples (recommended defaults):

* `Guid` → `octet[16]` (wire) → native fixed buffer 16 bytes  
* `DateTime` → `int64` ticks UTC (wire) → native `long`  
* `Numeric.Quaternion` → `{ float x,y,z,w }` (wire/native)  
* `FixedString32` → bounded string `string<32>` or `octet[32]` (choose one system-wide)

### **6.2 Mapping declaration**

`[DdsTypeMap(Storage = DdsStorage.Guid16)]` etc., or specialized attributes:

* `[DdsGuid]`, `[DdsDateTimeTicks]`, `[DdsFixedString(32)]`

### **6.3 Managed vs native representation**

* `TManaged` uses the .NET type (`Guid`, `DateTime`, `string`, etc.)  
* `TNative` uses the storage representation (fixed buffers or pointer+length for unbounded)

---

## **7\. Union support**

### **7.1 Schema expression**

A union schema is declared with:

* discriminator field annotated `[DdsDiscriminator]`  
* case fields annotated `[DdsCase(value)]`  
* optional default case `[DdsDefaultCase]`

Constraints:

* exactly one discriminator  
* each discriminator value used at most once  
* generator computes max payload size and alignment

### **7.2 Generated IDL**

Generator emits IDL `union ... switch(...) { case ... }` with `@appendable` on union definition.

### **7.3 Generated native representation**

`TNativeUnion` is:

* `discriminator` (enum or integral)  
* `payload` storage as explicit layout union-like region sized to max(case sizes), aligned to strictest case alignment  
* accessor helpers generated (unsafe but allocation-free)

### **7.4 Managed representation (optional)**

`TManagedUnion` exposes:

* `Kind` discriminator  
* typed accessors or `TryGetX(out X)` methods  
* convenience constructors per case

---

## **8\. Optional members (“nullable class references”)**

### **8.1 Semantics**

`[DdsOptional]` means:

* Field may be absent (“null reference”)  
* On the wire: XTypes optional member  
* In `TManaged`: represented as nullable (`Child?` or `Child?`\-like wrapper; for class types, `Child?` reference)  
* In `TNative`: representation depends on whether the optional member contains variable-size fields:  
  * fixed-size optional: presence bit \+ inline value OR presence encoded by XTypes during (de)serialization with generated helpers  
  * variable-size optional: presence \+ arena-owned backing

### **8.2 Lifetime**

Optional members containing variable-size data are valid only within the **TakeScope** / Arena lifetime.

---

## **9\. Variable-size data (unbounded strings/sequences)**

### **9.1 Classification**

* **Bounded**: known max size; can be stored inline or in fixed buffers.  
* **Unbounded**: truly variable; stored as pointer+length in `TNative`, with memory owned by Arena or Cyclone loan.

### **9.2 Arena requirements**

Any topic type containing unbounded members is classified by the generator as **Arena-required**.

Generated API rules:

* `DdsReader<TNative>` is permitted only for **inline-only** `TNative`.  
* `DdsReader<TManaged,...>` / `TakeScope` is required for unbounded types.  
* Writers for unbounded types require either:  
  * caller-provided Arena (managed to native)  
  * direct `TNative` whose buffers are already valid for duration of `dds_write`

### **9.3 Arena interface**

Minimum required features:

* `byte* Alloc(int bytes, int alignment)`  
* `Reset()` (O(1) rewind to reuse allocations)  
* Growth strategy: geometric, with max block size policy  
* Thread-safety: arenas are **not** thread-safe; use per-thread arenas or explicit passing

### **9.4 Managed string/sequence handling**

Marshallers must support:

* Encoding strings (UTF-8 recommended) into Arena buffers  
* Decoding strings into:  
  * either `ReadOnlySpan<byte>`/`Utf8Span`\-like lightweight view (allocation-free), **or**  
  * pooled `string` (allocating) as an opt-in convenience  
* Sequences decoded into:  
  * `ReadOnlySpan<T>` views over Arena memory where possible  
  * or caller-provided buffers

**Policy:** default APIs must be allocation-free; allocating conveniences may exist behind explicit method names (e.g., `ToStringAllocated()`).

---

## **10\. Runtime API surface**

### **10.1 Participant**

public sealed class DdsParticipant : IDisposable  
{  
    public int DomainId { get; }  
    public string\[\] Partitions { get; }  
    public DdsParticipant(int domainId, params string\[\] partitions);  
}

Requirements:

* Creates Cyclone domain participant  
* Stores partition list, applied to publishers/subscribers created by readers/writers  
* Disposes native handle deterministically

### **10.2 Reader / Writer (inline-only native)**

public sealed class DdsWriter\<TNative\> : IDisposable where TNative : unmanaged  
{  
    public void Write(in TNative sample);  
    public void WriteDispose(in TNative sample);     // write+dispose  
}

public sealed class DdsReader\<TNative\> : IDisposable where TNative : unmanaged  
{  
    public int Take(Span\<TNative\> samples, Span\<DdsSampleInfo\> infos);  
    public int Read(Span\<TNative\> samples, Span\<DdsSampleInfo\> infos);  
}

* `Take/Read` fill caller-provided buffers (no allocations)  
* Uses topic/QoS metadata auto-bound from generated per-type registry

### **10.3 Reader / Writer (managed \+ arena, variable-size capable)**

public sealed class DdsWriter\<TManaged, TNative, TMarshaller\> : IDisposable  
{  
    public void Write(in TManaged sample, Arena arena);  
    public void WriteDispose(in TManaged sample, Arena arena);  
}

public sealed class DdsReader\<TManaged, TNative, TMarshaller\> : IDisposable  
{  
    public TakeScope\<TManaged\> Take(Arena arena, int maxSamples);  
}

`TakeScope<TManaged>`:

* Exposes `ReadOnlySpan<TManaged>` (or a struct enumerator) and infos  
* Disposing returns DDS loans and resets/returns Arena allocations for that scope

### **10.4 Topic/QoS auto-binding**

Generator produces a registry:

* `TopicName`  
* `TypeName`  
* QoS defaults (reliability, durability, history kind/depth, resource limits optional)  
* Optional partition override (defaults to participant partitions)

Reader/Writer constructors:

* resolve registry entry from `TNative`/`TManaged` type  
* create topic, publisher/subscriber, and reader/writer with QoS applied

### **10.5 Error handling**

* Native return codes are converted to:  
  * `DdsException` with error code \+ optional message, for programmer errors  
  * or returned as status for expected conditions (timeouts, no data)  
* No exceptions on the hot path by default (configurable):  
  * `TryWrite`, `TryTake` optional variants returning status codes

### **10.6 Threading guarantees**

* Participant is thread-safe for independent reader/writer creation.  
* Reader/Writer instances:  
  * thread-safety is configurable; default: not thread-safe for maximum performance  
  * provide an opt-in locking wrapper if needed  
* Arenas are not thread-safe.

---

## **11\. Native interop layer**

### **11.1 P/Invoke strategy**

* All Cyclone handles are `nint`/`IntPtr` wrapped in safe classes.  
* Use `unsafe` and fixed blocks to avoid copying.  
* Use blittable `TNative` layouts to pass pointers directly.

### **11.2 Optional global allocator override**

Provide an optional initialization call:

public static class CycloneRuntime  
{  
    public static void ConfigureAllocator(ICycloneAllocator allocator);  
}

Implemented by native shim calling Cyclone allocator override APIs.

### **11.3 Loan management**

* For `TakeScope`, runtime uses Cyclone loaning where available; scope disposal must call the appropriate return-loan function.  
* For non-loaned paths, runtime copies into caller-provided spans.

---

## **12\. Disposal samples and instance lifecycle**

* `WriteDispose` emits a write \+ dispose event for the instance.  
* If keys are used:  
  * schema supports `[DdsKey]`  
  * generator can optionally generate `KeyOf(TManaged)` helpers  
* If keys are not used:  
  * `WriteDispose` still exists but semantics depend on DDS behavior for keyless topics; generator warns if dispose is requested for keyless type.

---

## **13\. Performance requirements**

* Inline-only types:  
  * `Take/Read` must be allocation-free and O(n) copy at most (prefer loan)  
  * `Write` must be allocation-free  
* Variable-size types:  
  * steady-state allocation-free from GC perspective  
  * arena growth only when encountering larger-than-ever samples  
  * minimal pinning: use fixed buffers/Arena native memory where possible  
* No reflection at runtime for serialization decisions (registry is generated).

---

## **14\. Testing and validation requirements**

### **14.1 Layout validation**

* Generator emits compile-time asserts (where possible) and runtime debug asserts:  
  * `sizeof(TNative)` expected  
  * `FieldOffset` checks for explicit layout types (unions)  
* Optional integration test that compares IDL-generated C layout expectations with `TNative` (via native shim introspection where feasible).

### **14.2 Schema evolution tests**

* Baseline schema fingerprint stored in repo  
* CI verifies new schema is append-compatible for `@appendable`

### **14.3 Fuzz / stress**

* Randomized variable-size payloads through arena  
* Long-running soak to detect leaks (loan return \+ arena reset correctness)

---

## **15\. Deliverables**

1. `CycloneDDS.Schema` (attributes \+ analyzer utilities)  
2. `CycloneDDS.Generator` (Roslyn source generator \+ optional analyzer for evolution rules)  
3. `CycloneDDS.Runtime` (participant/reader/writer, arena, take scopes, error mapping)  
4. `CycloneDDS.NativeShim` (C shim \+ optional allocator integration)  
5. Build integration:  
   * MSBuild targets to run schema gen → emit IDL → run `idlc` → compile native shim

---

You’re right: **most of those attributes should be unnecessary**. In a Cyclone-only, `@appendable`\-always world, the DSL should feel like “normal C\# types”, with attributes used only when **C\# syntax is ambiguous** or when you need to override defaults.

Here’s how I’d specify the **deduction rules**, and what (few) attributes remain.

---

## **1\) Make `@appendable` implicit and universal**

* Every generated IDL `struct/union` is emitted as `@appendable`.  
* No `[DdsAppendable]` attribute exists (or it’s ignored).  
* The generator enforces append-only evolution by comparing schema fingerprints across builds.

So: **no attribute needed** for appendable.

---

## **2\) Deduce sequences/arrays from the C\# type**

### **Default mapping rules**

| C\# schema member type | IDL emission | Native representation |
| ----- | ----- | ----- |
| `T[]` | `sequence<T>` (unbounded) | pointer \+ len (arena-backed on read/write) |
| `T[N]` (not a C\# type) | n/a | n/a |
| `FixedArray<T, N>` (custom generic) | `T[N]` (fixed array) | inline fixed buffer |
| `List<T>` | `sequence<T>` (unbounded) | pointer \+ len (arena-backed) |
| `ReadOnlySpan<T>` / `Span<T>` | **not allowed** in schema | n/a |

So if you write `float[] BoneWeights`, it is *obviously* `sequence<float>` and unbounded. No `[DdsSequence]` required.

### **Bounded sequence (when you know max)**

C\# doesn’t have a “bounded list” type. So you need either:

* a **wrapper type**: `BoundedSeq<T, Max>` / `FixedCapacityList<T, Max>`  
* or an attribute: `[DdsBound(Max=...)]`

If you dislike attributes, wrappers are better. Example:

public BoundedSeq\<float, \_256\> BoneWeights;

Generator emits:

sequence\<float, 256\> boneWeights;

---

## **3\) Deduce bounded vs unbounded strings**

C\# `string` has no max length, so the natural default is:

* `string Name;` → IDL `string name;` (unbounded)

So `[DdsUnboundedString]` is pointless and should not exist.

For bounded strings, again C\# lacks a built-in type that encodes “max length”, so you need one of:

* wrapper type: `BoundedString<32>` / `FixedString32`  
* or attribute: `[DdsBound(32)]`

Given your performance goals, wrappers are better anyway (they let you control encoding and layout). So:

* `FixedString32 Frame;` → `string<32>` or `octet[32]` (your system-wide choice)  
* `string DebugLabel;` → `string` unbounded

No attribute needed for unbounded; bounded is encoded in the type itself.

---

## **4\) Type maps: deduce by a registry, not per-field attributes**

`Numeric.Quaternion` cannot be represented directly in IDL; you need a mapping. But you don’t need `[DdsTypeMap]` on every field.

Instead, the generator should use a **global type map registry**, configured once:

### **Type map registry (compile-time)**

* `Guid` → `octet[16]`  
* `DateTime` → `int64 ticksUtc`  
* `Numeric.Quaternion` → `struct { float x,y,z,w }` (or typedef)  
* `FixedString32` → bounded string or octet array (depending on your chosen encoding)

Where does that registry live?

* Option A: a **config file** (e.g. `dds_typemap.json`) consumed by the generator

Option B: an assembly-level attribute used once, e.g.  
\[assembly: DdsTypeMap(typeof(Numeric.Quaternion), typeof(QuaternionF32x4))\]

*   
* Option C: conventions \+ built-in defaults for well-known types (Guid/DateTime) and user-extensible registry for custom ones.

Then the field:

public Numeric.Quaternion Rotation;

is mapped automatically without any attribute.

**When do you need a per-field override?**  
Only if the *same CLR type* may map differently in different fields (rare). Example: one `DateTime` as ticks, another as `Time_t`. Then you’d allow an override attribute, but it’s optional.

---

## **5\) Optional (nullable refs): deduce from nullability**

You said you want “null class references inside a topic type”. If you’re Cyclone-only and embracing XTypes, the simplest inference is:

* `SomeClass? x;` or `SomeStruct? x;` → IDL `@optional SomeType x;`

So `[DdsOptional]` can also go away, *provided you have an unambiguous rule*.

### **Recommended rule**

* If schema member type is a **reference type** and is annotated nullable (`T?`) → `@optional`.  
* If schema member type is a **nullable value type** (`T?` where `T: struct`) → `@optional`.

If you want optional on non-nullable members (rare), then an attribute is needed. But default should be inference.

---

## **6\) Unions: you still need *some* structure, but not noisy attributes**

C\# has no union syntax, so you need a representation. Two clean, low-noise options:

### **Option A: “OneOf-style” union wrapper type**

You declare:

public DdsUnion\<MoveCommand, SpawnCommand, NoopCommand\> Cmd;

Generator:

* creates discriminator enum automatically  
* emits IDL union with cases in declaration order  
* generates managed accessors

This is the least attribute-heavy.

### **Option B: Keep your “discriminator \+ cases” partial type**

You need minimal markers to tell the generator “this is a union” and “this is the discriminator”. But you can reduce it to:

* one type-level marker `[DdsUnion]`  
* one discriminator marker `[DdsDiscriminator]`  
  Cases can be inferred by field type \+ naming convention if you want, but usually you still need case value mapping. If you auto-generate a discriminator enum, even that can be inferred.

Given your “deduce from C\#” preference, **Option A is the better fit**.

---

## **7\) What attributes remain (minimal set)**

With good inference \+ wrapper types, you can get down to:

### **Required (because C\# can’t express it otherwise)**

* `[DdsTopic("PoseUpdate")]` — topic name is not derivable from type name reliably (you might want overrides).  
* QoS defaults: either  
  * `[DdsQos(...)]` on the type, **or**  
  * a separate registry/config file mapping topic → QoS.

### **Optional (rare overrides)**

* `[DdsKey]` (unless you decide “any field named Id/EntityId is key”, which I don’t recommend)  
* `[DdsBound(Max=...)]` only if you don’t want bounded wrapper types  
* per-field type-map override (only if needed)

Everything else should be inferred.

---

## **8\) Your example rewritten with “deduce everything” style**

Here’s how the schema could look with near-zero attributes:

\[DdsTopic("PoseUpdate")\]  
\[DdsQos(Reliability \= Reliable, Durability \= Volatile, HistoryDepth \= 8)\]  
public partial class PoseUpdate  
{  
    \[DdsKey\] public Guid EntityId;          // mapped by registry  
    public DateTime TimestampUtc;           // mapped by registry  
    public Numeric.Quaternion Rotation;     // mapped by registry

    public float X, Y, Z;

    public FixedString32 Frame;             // bounded by type

    public EntityMeta? Meta;                // nullable \=\> @optional

    public string DebugLabel;               // string \=\> unbounded  
    public float\[\] BoneWeights;             // array \=\> sequence\<float\>

}

public partial class EntityMeta  
{  
    public int Team;  
    public string Name;                     // unbounded  
}

No `[DdsAppendable]`, no `[DdsSequence]`, no `[DdsUnboundedString]`, no per-field `[DdsTypeMap]`.

---

Makes sense. Unions are the one place where “pure inference from C\# types” tends to get too magical, and you want something explicit and reviewable. Here’s a union design that is **attribute-driven, one arm per line**, with an explicit discriminator, while still keeping everything else mostly inferred.

---

## **Union in the C\# DSL (spec \+ example)**

### **Goals**

* One discriminator field (explicit)  
* One arm per line (explicit)  
* Clear mapping to IDL `union ... switch(...)`  
* Works with `@appendable` everywhere  
* Plays well with managed \+ native generated representations

---

## **1\) DSL shape**

### **Type-level marker**

* `[DdsUnion]` marks the type as an IDL union schema type.

### **Discriminator marker**

* Exactly one field/property must be annotated `[DdsDiscriminator]`.  
* Discriminator type must be:  
  * an enum with explicit underlying integral type, **or**  
  * an integral type (`int`, `uint`, `short`, etc.).  
* Generator emits the discriminator exactly as declared (if enum: IDL `enum`).

### **Arm markers**

* Each arm is a separate field/property annotated `[DdsCase(...)]` or `[DdsDefaultCase]`.  
* Arm type must be a schema-mappable type (struct/class schema type, primitive, bounded/unbounded allowed).  
* `DdsCase` argument is either:  
  * a discriminator enum literal (strongly preferred), or  
  * a numeric literal (if discriminator is integral).

### **Constraints**

* Each discriminator value may appear at most once across arms.  
* At most one `[DdsDefaultCase]`.  
* Arms are emitted into IDL in source order.  
* If a discriminator value is not covered and no default exists, it’s allowed (IDL permits it), but generator may warn.

---

## **2\) Example schema union**

public enum CommandKind : int  
{  
    None  \= 0,  
    Move  \= 1,  
    Spawn \= 2,  
    Despawn \= 3,  
}

\[DdsUnion\]  
public partial class Command  
{  
    \[DdsDiscriminator\]  
    public CommandKind Kind;

    \[DdsCase(CommandKind.Move)\]  
    public MoveCommand Move;

    \[DdsCase(CommandKind.Spawn)\]  
    public SpawnCommand Spawn;

    \[DdsCase(CommandKind.Despawn)\]  
    public DespawnCommand Despawn;

    \[DdsDefaultCase\]  
    public NoopCommand Default;  
}

public partial class MoveCommand  
{  
    public float Dx, Dy, Dz;  
}

public partial class SpawnCommand  
{  
    public Guid PrefabId;   // mapped by global type map  
    public float Px, Py, Pz;  
}

public partial class DespawnCommand  
{  
    public Guid TargetId;  
}

public partial class NoopCommand  
{  
    public int ReasonCode;  
}

No extra appendable attributes; the generator emits `@appendable` by default everywhere.

---

## **3\) Generated IDL for that union**

@appendable enum CommandKind : long {  
  None \= 0,  
  Move \= 1,  
  Spawn \= 2,  
  Despawn \= 3  
};

@appendable struct MoveCommand { float dx; float dy; float dz; };  
@appendable struct SpawnCommand { Guid16 prefabId; float px; float py; float pz; };  
@appendable struct DespawnCommand { Guid16 targetId; };  
@appendable struct NoopCommand { long reasonCode; };

@appendable union Command switch(CommandKind) {  
  case Move:    MoveCommand move;  
  case Spawn:   SpawnCommand spawn;  
  case Despawn: DespawnCommand despawn;  
  default:      NoopCommand  @default; // or \`default\_\` depending on naming rules  
};

(Exact naming escapes are a generator detail.)

---

## **4\) Generated native layout strategy (high-perf, deterministic)**

### **4.1 Discriminator \+ payload region**

Generate `TNative` as:

* `Kind` discriminator stored as `int` (or enum)  
* a payload region sized to the **max of arm native sizes**, aligned to **max alignment** of any arm  
* explicit layout with each arm at the same offset

Conceptually:

\[StructLayout(LayoutKind.Explicit)\]  
public unsafe struct CommandNative  
{  
    \[FieldOffset(0)\]  
    public CommandKind Kind;

    \[FieldOffset(4)\]  
    public MoveCommandNative Move;

    \[FieldOffset(4)\]  
    public SpawnCommandNative Spawn;

    \[FieldOffset(4)\]  
    public DespawnCommandNative Despawn;

    \[FieldOffset(4)\]  
    public NoopCommandNative Default;  
}

Generator responsibilities:

* compute correct payload offset and padding/alignment  
* ensure `sizeof(CommandNative)` is enough for the largest case  
* emit debug assertions in development builds (`sizeof`, `offsetof` equivalents)

### **4.2 Variable-size arms**

Allow union arms that contain unbounded fields (strings/sequences). Then:

* arm native type includes pointer+len members  
* lifetime is bound to arena / take-scope  
* marshaller enforces arena presence

This is compatible with your pooling approach.

---

## **5\) Marshaller rules for unions (managed ↔ native)**

### **Managed → native**

* Writes discriminator first  
* Writes exactly one arm payload corresponding to discriminator  
* Zeroes/ignores other payload overlays (not required by IDL but good hygiene)

### **Native → managed**

* Reads discriminator  
* Materializes only active arm into managed view  
* If the arm contains unbounded fields: managed fields are views into arena-backed memory (or explicit allocated conversions if user requests)

### **Validity window**

* For reads: union arm contents are valid only during take-scope (same as other unbounded data)

---

## **6\) Minimal attribute set overall**

With your “deduce the rest” preference, the typical type needs:

* `[DdsTopic("...")]`  
* `[DdsQos(...)]` (or a central registry)  
* `[DdsKey]` on key members  
* For unions only: `[DdsUnion]`, `[DdsDiscriminator]`, `[DdsCase]`, optional `[DdsDefaultCase]`

Everything else (appendable, sequences, unbounded strings, type maps) is inferred from:

* universal appendable policy  
* CLR type maps (global registry)  
* `string` \=\> unbounded string  
* `T[]`/`List<T>` \=\> sequence  
* `FixedString32`/`BoundedSeq<T,N>` wrappers \=\> bounded

---

If you want, I can extend this example by embedding `Command` inside a `PoseUpdate` topic that also includes `EntityMeta?` optional refs and unbounded fields, and then show the **exact generated native structs** for the whole topic plus the **TakeScope API** surface.

—-  
yes  
—-

Great — here’s the complete, concrete example with:

* a **topic** `PoseUpdate`  
* `@appendable` **implicitly everywhere**  
* **optional** nested reference `EntityMeta?` → IDL `@optional`  
* **unbounded** `string` and `T[]` sequences (arena-backed)  
* **bounded** `FixedString32`  
* **union** `Command` with explicit discriminator \+ one arm per line using attributes  
* minimal attributes outside union: only topic/qos/key

---

## **1\) Schema C\# (what you write)**

using System;  
using Bagira.CycloneDDS.Schema;

public enum CommandKind : int  
{  
    None \= 0,  
    Move \= 1,  
    Spawn \= 2,  
    Despawn \= 3,  
}

\[DdsUnion\]  
public partial class Command  
{  
    \[DdsDiscriminator\]  
    public CommandKind Kind;

    \[DdsCase(CommandKind.Move)\]  
    public MoveCommand Move;

    \[DdsCase(CommandKind.Spawn)\]  
    public SpawnCommand Spawn;

    \[DdsCase(CommandKind.Despawn)\]  
    public DespawnCommand Despawn;

    \[DdsDefaultCase\]  
    public NoopCommand Default;  
}

public partial class MoveCommand  
{  
    public float Dx, Dy, Dz;  
}

public partial class SpawnCommand  
{  
    public Guid PrefabId;     // inferred via global type map  
    public float Px, Py, Pz;  
}

public partial class DespawnCommand  
{  
    public Guid TargetId;     // inferred via global type map  
}

public partial class NoopCommand  
{  
    public int ReasonCode;  
}

public partial class EntityMeta  
{  
    public int Team;  
    public string Name;       // unbounded (inferred)  
}

\[DdsTopic("PoseUpdate")\]  
\[DdsQos(Reliability \= DdsReliability.Reliable,  
       Durability  \= DdsDurability.Volatile,  
       HistoryKind \= DdsHistoryKind.KeepLast,  
       HistoryDepth \= 8)\]  
public partial class PoseUpdate  
{  
    \[DdsKey\] public Guid EntityId;        // key

    public DateTime TimestampUtc;         // mapped to ticks UTC by global rule  
    public Numeric.Quaternion Rotation;   // mapped to float4 struct by global rule

    public float X, Y, Z;

    public FixedString32 Frame;           // bounded by type

    public EntityMeta? Meta;              // nullable \=\> @optional

    public string DebugLabel;             // unbounded (inferred)  
    public float\[\] BoneWeights;           // sequence\<float\> (inferred)

    public Command Cmd;                   // union  
}

### **What’s inferred (no attributes)**

* `@appendable` everywhere  
* `string` → unbounded IDL `string`  
* `float[]` → unbounded IDL `sequence<float>`  
* `EntityMeta?` → `@optional EntityMeta`  
* `Guid`, `DateTime`, `Numeric.Quaternion` → mapped by **global typemap registry**  
* `FixedString32` → bounded string or octet\[32\] based on your system-wide choice

---

## **2\) Generated IDL (what your generator emits)**

@appendable module BagiraTopics  
{  
  typedef octet Guid16\[16\];

  @appendable struct QuaternionF32x4 { float x; float y; float z; float w; };  
  typedef string\<32\> FixedString32; // or octet\[32\] if you choose fixed bytes

  enum CommandKind : long { None=0, Move=1, Spawn=2, Despawn=3 };

  @appendable struct MoveCommand { float dx; float dy; float dz; };  
  @appendable struct SpawnCommand { Guid16 prefabId; float px; float py; float pz; };  
  @appendable struct DespawnCommand { Guid16 targetId; };  
  @appendable struct NoopCommand { long reasonCode; };

  @appendable union Command switch(CommandKind) {  
    case Move:    MoveCommand    move;  
    case Spawn:   SpawnCommand   spawn;  
    case Despawn: DespawnCommand despawn;  
    default:      NoopCommand    @default;  
  };

  @appendable struct EntityMeta {  
    long team;  
    string name; // unbounded  
  };

  @appendable struct PoseUpdate {  
    @key Guid16 entityId;  
    long long timestampUtcTicks;  
    QuaternionF32x4 rotation;

    float x; float y; float z;

    FixedString32 frame;

    @optional EntityMeta meta;

    string debugLabel;              // unbounded  
    sequence\<float\> boneWeights;    // unbounded

    Command cmd;  
  };  
};

---

## **3\) Generated native C\# types (blittable layout used with Cyclone C API)**

### **3.1 Common primitives**

using System.Runtime.InteropServices;

namespace Bagira.CycloneDDS.Generated.Native  
{  
    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct Guid16  
    {  
        public fixed byte Bytes\[16\];  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public struct QuaternionF32x4  
    {  
        public float x, y, z, w;  
    }

    // If you choose string\<32\> encoded as UTF-8 bytes with NUL padding:  
    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct FixedString32Utf8  
    {  
        public fixed byte Bytes\[32\];  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct Utf8StringRef  
    {  
        public byte\* Ptr;  
        public int ByteLen;  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct SeqFloat  
    {  
        public float\* Ptr;  
        public int Len;  
    }  
}

### **3.2 Optional nested struct (nullable ref)**

namespace Bagira.CycloneDDS.Generated.Native  
{  
    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct EntityMetaNative  
    {  
        public int Team;  
        public Utf8StringRef Name; // unbounded \=\> arena-backed  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct OptionalEntityMeta  
    {  
        public byte HasValue;      // 0/1  
        // padding if needed for alignment  
        public EntityMetaNative Value;  
    }  
}

### **3.3 Union native (explicit layout)**

namespace Bagira.CycloneDDS.Generated.Native  
{  
    public enum CommandKind : int { None=0, Move=1, Spawn=2, Despawn=3 }

    \[StructLayout(LayoutKind.Sequential)\]  
    public struct MoveCommandNative { public float Dx, Dy, Dz; }

    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct SpawnCommandNative  
    {  
        public Guid16 PrefabId;  
        public float Px, Py, Pz;  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct DespawnCommandNative  
    {  
        public Guid16 TargetId;  
    }

    \[StructLayout(LayoutKind.Sequential)\]  
    public struct NoopCommandNative { public int ReasonCode; }

    \[StructLayout(LayoutKind.Explicit)\]  
    public unsafe struct CommandNative  
    {  
        \[FieldOffset(0)\] public CommandKind Kind;

        // Payload starts at offset 4\. Generator must ensure correct alignment/padding.  
        \[FieldOffset(4)\] public MoveCommandNative Move;  
        \[FieldOffset(4)\] public SpawnCommandNative Spawn;  
        \[FieldOffset(4)\] public DespawnCommandNative Despawn;  
        \[FieldOffset(4)\] public NoopCommandNative Default;  
    }  
}

### **3.4 Topic native**

namespace Bagira.CycloneDDS.Generated.Native  
{  
    \[StructLayout(LayoutKind.Sequential)\]  
    public unsafe struct PoseUpdateNative  
    {  
        public Guid16 EntityId;  
        public long TimestampUtcTicks;  
        public QuaternionF32x4 Rotation;

        public float X, Y, Z;

        public FixedString32Utf8 Frame;

        public OptionalEntityMeta Meta;

        public Utf8StringRef DebugLabel;  
        public SeqFloat BoneWeights;

        public CommandNative Cmd;  
    }  
}

---

## **4\) Generated managed “view” API (allocation-free by default)**

For variable-size members, the managed sample is typically a **ref struct view** into arena-backed memory:

using System;

namespace Bagira.CycloneDDS.Generated.Managed  
{  
    public readonly ref struct PoseUpdateView  
    {  
        public readonly Guid EntityId;  
        public readonly DateTime TimestampUtc;  
        public readonly Numeric.Quaternion Rotation;  
        public readonly float X, Y, Z;  
        public readonly FixedString32 Frame;

        public readonly EntityMetaView? Meta;            // nullable view

        public readonly ReadOnlySpan\<byte\> DebugLabelUtf8; // allocation-free string view  
        public readonly ReadOnlySpan\<float\> BoneWeights;   // allocation-free sequence view

        public readonly CommandView Cmd;  
    }

    public readonly ref struct EntityMetaView  
    {  
        public readonly int Team;  
        public readonly ReadOnlySpan\<byte\> NameUtf8;  
    }

    public readonly ref struct CommandView  
    {  
        public readonly CommandKind Kind;  
        public readonly MoveCommand Move;     // valid if Kind==Move  
        public readonly SpawnCommand Spawn;   // valid if Kind==Spawn  
        public readonly DespawnCommand Despawn;  
        public readonly NoopCommand Default;  
    }  
}

(You can add convenience methods to decode UTF-8 to `string`, explicitly allocating.)

---

## **5\) Runtime usage (Arena \+ TakeScope)**

### **Write (managed schema → native, arena-backed)**

using Bagira.CycloneDDS.Runtime;  
using Bagira.CycloneDDS.Generated.Native;

var participant \= new DdsParticipant(domainId: 0, partitions: new\[\] { "ecs" });

var writer \= new DdsWriter\<PoseUpdate, PoseUpdateNative, PoseUpdateMarshaller\>(participant);

var arena \= new Arena(initialBytes: 128 \* 1024);

var msg \= new PoseUpdate  
{  
    EntityId \= Guid.NewGuid(),  
    TimestampUtc \= DateTime.UtcNow,  
    Rotation \= new Numeric.Quaternion(0, 0, 0, 1),  
    X \= 1, Y \= 2, Z \= 3,  
    Frame \= FixedString32.From("world"),  
    Meta \= new EntityMeta { Team \= 4, Name \= "alpha" },  
    DebugLabel \= "unbounded string here ...",  
    BoneWeights \= new\[\] { 0.1f, 0.2f, 0.3f },  
    Cmd \= new Command { Kind \= CommandKind.Move, Move \= new MoveCommand { Dx \= 1, Dy \= 0, Dz \= 0 } }  
};

writer.Write(in msg, arena);

### **Take (DDS → native loan → managed view; valid within scope)**

using Bagira.CycloneDDS.Runtime;  
using Bagira.CycloneDDS.Generated.Managed;

var reader \= new DdsReader\<PoseUpdateView, PoseUpdateNative, PoseUpdateMarshaller\>(participant);

var arena \= new Arena(initialBytes: 512 \* 1024);

while (true)  
{  
    using var scope \= reader.Take(arena, maxSamples: 256);

    foreach (ref readonly var s in scope.Samples) // PoseUpdateView  
    {  
        // allocation-free access  
        var label \= s.DebugLabelUtf8;  
        var weights \= s.BoneWeights;

        if (s.Meta is EntityMetaView meta)  
        {  
            var name \= meta.NameUtf8;  
        }

        switch (s.Cmd.Kind)  
        {  
            case CommandKind.Move:  
                // s.Cmd.Move  
                break;  
        }  
    }  
}

---

## **Why this matches your “deduce from C\#” intent**

* No `[DdsAppendable]`, `[DdsSequence]`, `[DdsUnboundedString]`, `[DdsTypeMap]` noise.  
* The only union-specific attributes are exactly where you want explicitness:  
  * discriminator \+ one arm per line.  
* Type mappings are centralized (global registry), not repeated per field.  
* Unbounded/bounded is expressed by the type (`string` vs `FixedString32`) or container (`T[]` vs `BoundedSeq<T,N>`).

If you want, next step is to define the **global typemap registry** format (assembly attributes vs JSON) and the **exact encoding policy** for bounded strings (UTF-8 bytes vs fixed `char`).

—----

Below is a concrete proposal for (A) a **global typemap registry** and (B) a **fixed-string encoding policy**, with two implementation options each. I’ll also call out the key consequence you should be aware of in Cyclone:

**IDL `string<N>` is bounded but still variable-length**, and in C mappings it is typically **pointer-based**, not an inline fixed buffer.  
If you want *true inline fixed storage* (no per-sample allocations), you should represent fixed strings as **`octet[N]` (or `char[N]`)** and define an encoding convention.

---

# **A) Global typemap registry**

## **A1) Assembly-level registration (strongly typed, discoverable, no extra files)**

### **1\) Define a “wire type” (IDL/native) for each custom CLR type**

Example wire types you’ll generate (or ship in a common schema module):

* `Guid` → `Guid16` (`octet[16]`)  
* `DateTime` → `int64` ticks UTC  
* `Numeric.Quaternion` → `QuaternionF32x4` (`float x,y,z,w`)  
* `FixedString32` → `octet[32]` (recommended) or `string<32>` (if you accept pointer/alloc)

### **2\) Register mappings once at assembly level**

Example API (one-time per assembly):

using Bagira.CycloneDDS.Schema;

\[assembly: DdsTypeMap(typeof(Guid),               DdsWire.Guid16)\]  
\[assembly: DdsTypeMap(typeof(DateTime),           DdsWire.Int64TicksUtc)\]  
\[assembly: DdsTypeMap(typeof(Numeric.Quaternion), DdsWire.QuaternionF32x4)\]  
\[assembly: DdsTypeMap(typeof(FixedString32),      DdsWire.FixedUtf8Bytes32)\]

Where `DdsWire.*` are *well-known built-in wire kinds* understood by the generator (not actual CLR types). This avoids requiring you to declare a CLR “wire struct” for everything.

### **3\) Override per field only when needed**

You rarely need overrides, but keep the escape hatch:

public partial class Foo  
{  
    \[DdsTypeOverride(DdsWire.TimeT)\] // if you ever want DDS Time\_t instead of ticks  
    public DateTime Timestamp;  
}

**Pros**

* No JSON, no MSBuild plumbing  
* Strongly typed and refactor-friendly  
* Easy for the generator to read via Roslyn semantic model

**Cons**

* Requires referencing your schema attribute package from the schema assembly

---

# **B) Fixed-string encoding policy**

## **B2) Inline fixed bytes: `octet[32]` (recommended)**

### **IDL**

typedef octet FixedUtf8Bytes32\[32\];

### **Semantics (define this as your system contract)**

* UTF-8 encoded bytes  
* If shorter than 32 bytes: **NUL padding** to 32  
* If longer: either  
  * **reject at marshal time** (recommended), or  
  * truncate (not recommended unless you explicitly want that)  
* Valid UTF-8 required (optional strictness)

### **Native layout**

* Exactly 32 bytes inline inside the topic struct  
* No allocations, no pointers  
* Great for ECS hot path and cache locality

### **Managed-side API**

Provide a small value type:

public readonly struct FixedString32  
{  
    // stores UTF-8 bytes (length \<= 32\)  
    // offers:  
    // \- TryFrom(string, out FixedString32)  
    // \- AsUtf8Span()  
    // \- ToStringAllocated() (explicit allocation)  
}

**Why `octet` instead of `char`?**

* `octet` is explicitly a byte; `char` in IDL is 8-bit character with language mapping quirks. `octet[32]` makes “bytes” unambiguous.

**This is the best match** for your “generated `TNative` with fixed offsets” requirement.

---

# **C) Suggested “built-in wire kinds” (so mapping stays simple)**

To keep the typemap registry ergonomic, define a small set of canonical wire kinds the generator understands:

* `Guid16` → `octet[16]` (define byte order as **RFC4122 byte order** or .NET `Guid.ToByteArray()` order; pick one and lock it)  
* `Int64TicksUtc` → `int64` (UTC ticks)  
* `QuaternionF32x4` → `struct { float x,y,z,w }`  
* `FixedUtf8Bytes32/64/128/...` → `octet[N]`  
* `Utf8String` → IDL `string` (unbounded, arena-backed)  
* `Seq<T>` → IDL `sequence<T>` (unbounded, arena-backed)  
* `BoundedSeq<T,N>` → IDL `sequence<T,N>`

Then your schema stays clean and inference-driven.

---

Based on the cumulative design document, I have analyzed the specification for flaws, potential implementation gaps, and edge cases.

The proposed architecture is **exceptionally strong** for the specific use case (ECS, high-performance, Cyclone-only). It correctly identifies that avoiding GC pressure via arenas and ensuring blittable layouts are the keys to performance.

However, there are specific areas—particularly regarding **memory alignment**, **union safety**, and **arena lifecycle**—where the specification needs tightening to avoid runtime crashes or hard-to-debug data corruption.

### **1\. Critical Technical Flaws & Risks**

#### **A. The "Union Layout" Trap (Alignment & Padding)**

The specification suggests a simple \[FieldOffset\] strategy for unions:

\[FieldOffset(0)\] public CommandKind Kind;

\[FieldOffset(4)\] public MoveCommandNative Move;

**The Flaw:** This assumes the payload *always* starts at byte 4\. This is false if any union arm has an alignment requirement stricter than 4 bytes (e.g., a double or long usually requires 8-byte alignment).

* **Scenario:** If CommandKind is 4 bytes, but MoveCommandNative contains a double, the C compiler (Cyclone) will insert 4 bytes of padding after the enum so the double aligns to 8\.  
* **Result:** The C\# FieldOffset(4) will read 4 bytes of garbage padding and misalign the double, causing performance penalties or data corruption.

**The Fix:** The Generator must implement a **C-compatible alignment calculator**.

* Calculate the MaxAlignment of all union arms.  
* Calculate the natural size of the Discriminator.  
* Determine the Padding required: PayloadOffset \= RoundUp(DiscriminatorSize, MaxAlignment).  
* Generate \[FieldOffset(calculated\_offset)\] dynamically.

#### **B. The octet\[N\] String Encoding Gap**

The design recommends octet\[32\] for FixedString32 to ensure inline storage.

The Flaw: octet is just bytes. If you serialize a string that is not valid UTF-8 (e.g., truncating a multi-byte character halfway), you create a "poisoned" sample.

* While your C\# code might handle it, standard DDS tools (like ddspy or Cyclone's own introspection) might crash or throw errors when trying to interpret that field as a string if you ever change the definition to string\<32\> later or try to debug.

**The Fix:** The FixedString32 wrapper struct must validate UTF-8 consistency on the Write path, or explicitly document that it is a "binary blob" that happens to look like a string.

#### **C. Arena "High Watermark" Retention**

**The Flaw:** The spec mentions "geometric growth" for Arenas. In an ECS, one "heavy frame" (e.g., loading a level) might expand an arena to 100MB. If the arena is thread-local or pooled, that 100MB stays allocated forever, even if subsequent frames only need 10KB.

**The Fix:** Implement an **Arena Reset Policy**.

* arena.Reset() should check if (Capacity \> MaxRetainedCapacity) FreeAndReallocateSmall().  
* Allow the user to configure MaxRetainedBytes to prevent memory bloat over long runtimes.

---

### **2\. Gaps in the Specification**

#### **A. Handling "Default Values" in @appendable**

**The Gap:** When a Reader (v1) reads a sample from a Writer (v2) that has appended a new field, the Reader ignores it. Correct.

* **The Problem:** When a Reader (v2) reads a sample from a Writer (v1) that *misses* the new field, what value does the C\# field get?  
* XTypes dictates it should be the default value. The C\# TNative struct will simply have that memory uninitialized or zeroed depending on how the Marshaller works.

**The Improvement:** The Marshaller must explicitly handle the "missing" case.

* The TakeScope logic needs to check if the generated idlc descriptor flags the field as present.  
* However, since you are using a custom Native layout, you rely on Cyclone to fill the memory. Cyclone generally zeros out missing appendable fields if they are primitive. You need to verify this specific behavior in Cyclone's C mapping to ensure v2 readers don't see garbage data in v1 samples.

#### **B. Thread Safety in ECS Context**

**The Gap:** The spec says "Arenas are not thread-safe," which is correct. However, ECS systems often fan out systems to worker threads.

* If System A (Thread 1\) and System B (Thread 2\) both want to *write* to the same Writer, they cannot share the Writer's internal resources if the Writer attempts to share a scratch arena.

**The Improvement:**

* **Writers should be stateless regarding memory:** The Write method requiring an explicit Arena parameter (as currently designed) is good.  
* **Reader Fan-out:** If multiple threads need to process the *same* TakeScope, they can read ReadOnlySpan safely. But they cannot call Dispose on the scope until all are done. This requires a ref-counted scope or strict ownership rules in the user code.

#### **C. Generic Constraints on unmanaged**

**The Gap:** The spec uses where TNative : unmanaged.

* **Issue:** A struct containing a pointer (e.g., byte\* for unbounded strings) is unmanaged. However, a struct containing a FixedString32 which contains a fixed byte\[32\] is also unmanaged.  
* The generator needs to ensure that TNative is *strictly* unmanaged (no object references). The unmanaged constraint covers most of this, but it's worth noting that unmanaged structs cannot contain auto-implemented properties in some older C\# versions (not an issue in .NET 6/8+).

---

### **3\. Usability Improvements (DX)**

#### **A. The "List vs Sequence" Friction**

**Observation:**

* Schema: public float\[\] Weights;  
* Native: float\* ptr; int len;  
* Managed View: ReadOnlySpan\<float\> Weights;

**The Friction:** If the ECS logic wants to *add* a weight to the list and write it back, they can't mutate the ReadOnlySpan. They have to:

1. Allocate a new C\# array (GC pressure).  
2. Or use a scratch buffer/Arena to build the new list.

**Suggestion:** Provide a helper **ArenaList\<T\>** in the runtime.

* It acts like List\<T\> but stores data inside the Arena.  
* It exposes AsSpan() which can be assigned directly to the Native struct for writing.  
* This closes the loop: Read (Span) $\\to$ Modify (ArenaList) $\\to$ Write (Span), all zero-alloc.

#### **B. Debugging Experience**

The Gap: TNative structs are full of pointers. Debugging them in Visual Studio shows memory addresses, not strings.

Suggestion: Add \[DebuggerDisplay\] attributes to the generated TNative and View types.

* For Utf8StringRef, use a debugger proxy that decodes the string on the fly so the developer sees "Player1" instead of 0x00F1A2.

---

### **4\. Refined Component: The "Alignment Calculator" Logic**

To address Flaw 1.A, here is the logic your Generator needs to implement when emitting the \[FieldOffset\] for the Union Payload:

C\#

// Logic to determine offset of the Union Payload  
int discriminatorSize \= sizeof(int); // or 8 if long  
int maxArmAlignment \= 1;

foreach (var arm in unionArms) {  
    // Recursive function to find strictest alignment in the arm's struct  
    int armAlign \= GetAlignment(arm.Type);   
    if (armAlign \> maxArmAlignment) maxArmAlignment \= armAlign;  
}

// Standard C padding rule  
int payloadOffset \= (discriminatorSize \+ (maxArmAlignment \- 1)) & \~(maxArmAlignment \- 1);

// Emit  
// \[FieldOffset(0)\] public CommandKind Kind;  
// \[FieldOffset(payloadOffset)\] public MoveCommandNative Move;  
// ...

### **5\. Summary of Recommended Actions**

1. **Implement Alignment Calculation:** Do not hardcode offset 4\. Dynamically calculate union payload offsets based on member types.  
2. **Define Arena Trim Policy:** Prevent memory leaks in long-running processes by resetting arenas to a baseline capacity, not their peak capacity.  
3. **Validate UTF-8:** Enforce valid UTF-8 in FixedString32 debug builds to prevent poisoning the topic.  
4. **Add ArenaBuilder Helpers:** Provide a ArenaList\<T\> or ArenaStringBuilder to make modifying/creating variable-length data easier without falling back to GC allocations.  
5. **Verify Evolution Behavior:** Create a specific integration test where a Reader v1 reads a Writer v2 (with appended fields) and asserts the application doesn't crash or see garbage data.

This design is otherwise excellent and fit for purpose. It avoids the bloat of standard DDS bindings while leveraging the specific constraints of your environment.

—-  
is  the design clean about utilizing cyclone dds memory pool allocators? how are we achieving the almost-no-alloc for managed classes?  
—-  
Based on the design document, here is the analysis of how the system handles memory pool integration and achieves "almost-no-alloc" performance.

### **1\. Is the design clean about utilizing Cyclone DDS memory pool allocators?**

**Yes, the design is explicitly "clean" and standard-compliant** regarding Cyclone's memory model. It utilizes two official integration points rather than relying on hacks or "unsafe" assumptions about Cyclone's internals.

* **Zero-Copy Loaning (dds\_take):** The design fundamentally relies on the dds\_take API, which "loans" memory from Cyclone's internal subscriber cache to the application1.  
  * The TakeScope abstraction 2 wraps this loan. It holds the loan active while the application processes the data and automatically calls dds\_return\_loan when the scope is disposed3. This means for read operations, **no memory is allocated by the C\# application**; it just reads purely from Cyclone's internal pointers.  
* **Allocator Injection (ddsrt\_set\_allocator):** The design includes a "Native Shim" layer that calls ddsrt\_set\_allocator4444.  
  * This allows you to force Cyclone to perform its *own* internal allocations (for metadata, dynamic buffers, etc.) using a custom high-performance allocator (like mimalloc or a custom slab allocator)5. This ensures that even "invisible" allocations inside the DDS core follow your memory strategy.

### **2\. How is "almost-no-alloc" achieved for managed classes?**

The design achieves this by completely **bypassing the construction of standard C\# classes** (which are heap-allocated objects) in the hot path. Instead, it uses a "View" pattern backed by **Arenas**.

* **Ref Struct "Views" instead of Objects:**  
  * Instead of deserializing a native sample into a class PoseUpdate, the generated code creates a public readonly ref struct PoseUpdateView6.  
  * Because it is a ref struct, it is **guaranteed to stay on the stack** (zero GC pressure)7.  
  * It contains no data itself; it is merely a facade of pointers (spans) pointing directly into the native memory (either the Cyclone loan or the Arena)8.  
* **Arenas for Variable-Size Data (The "Steady State" Trick):**  
  * **The Problem:** Variable-length data (like strings or lists) usually requires allocating a new array or string object for every sample.  
  * **The Solution:** The design requires passing an Arena object to the Read/Write methods9999.  
    * **On Write:** The marshaller writes the variable data into the Arena's pre-allocated memory block10.  
    * **On Read:** If data needs to be copied or transformed (e.g., handling complex unions or unbounded sequences), it is written into the Arena11.  
  * **Lifecycle:** The Arena is **reset** (rewound) after the batch is processed12. In a steady state (e.g., running a game loop), the Arena grows to the maximum required size once and then simply reuses that same block of memory forever. This results in **zero allocations per frame** after the initial warm-up13.  
* **Span-based Access:**  
  * Strings are exposed as ReadOnlySpan\<byte\> (UTF-8 views) rather than System.String (which would require allocating a new UTF-16 object)14.  
  * Arrays/Sequences are exposed as ReadOnlySpan\<T\>15.

**Summary Diagram of the Memory Flow:**

Fragment kódu

graph LR  
    A\[Cyclone DDS Core\] \-- "Loans Ptr (dds\_take)" \--\> B(Native Struct TNative)  
    B \-- "Pointer Cast (Zero Copy)" \--\> C{Managed View TManaged}  
    C \-- "Reads Fixed Fields" \--\> B  
    C \-- "Reads Variable Fields" \--\> D\[Arena Pool\]  
      
    style C fill:\#d4f1f4,stroke:\#0077b6,stroke-width:2px,stroke-dasharray: 5 5  
    style D fill:\#e2f0cb,stroke:\#82c91e,stroke-width:2px

*The Managed View is a stack-only struct that bridges the Native data and the Arena without creating any persistent objects on the Heap.*
