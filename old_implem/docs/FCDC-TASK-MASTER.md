# FastCycloneDDS C# Bindings - Task Master List

**Version:** 1.0  
**Date:** 2026-01-14  
**Status:** Planning Phase

This document provides the master list of implementation tasks for the FastCycloneDDS C# Bindings project. Each task is defined with a unique ID and references sections of the [Detailed Design Document](./FCDC-DETAILED-DESIGN.md) to avoid duplication.

---

## Task Status Legend

- 🔴 **Not Started** - Task has not begun
- 🟡 **In Progress** - Task is actively being worked on
- 🟢 **Completed** - Task is finished and tested
- 🔵 **Blocked** - Task is blocked by dependencies

---

## Phase 1: Foundation & Schema Package

### FCDC-001: Schema Attribute Definitions
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2-3 days  
**Dependencies:** None  
**Design Reference:** §4 Schema DSL Design, §4.4 Required Attributes

**Description:**  
Implement the attribute classes that users will use to annotate their schema types. This includes type-level attributes ([DdsTopic], [DdsQos], [DdsUnion]) and field-level attributes ([DdsKey], [DdsDiscriminator], [DdsCase], [DdsDefaultCase], [DdsBound], [DdsId], [DdsOptional]).

**Detailed Task File:** [tasks/FCDC-001.md](../tasks/FCDC-001.md)

---

### FCDC-002: Schema Wrapper Types
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** None  
**Design Reference:** §4.2 Type Mapping Rules, §8.2 Fixed Buffers

**Description:**  
Implement wrapper types for bounded and specialized data: FixedString32/64/128, BoundedSeq<T,N>, and utility methods for encoding/decoding. Include UTF-8 validation for FixedString types.

**Detailed Task File:** [tasks/FCDC-002.md](../tasks/FCDC-002.md)

---

### FCDC-003: Global Type Map Registry
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2 days  
**Dependencies:** FCDC-001  
**Design Reference:** §4.3 Global Type Map Registry, §8.1 Three-Type Model

**Description:**  
Implement assembly-level [DdsTypeMap] attribute and DdsWire enumeration for standard type mappings (Guid16, Int64TicksUtc, QuaternionF32x4, FixedUtf8BytesN). Define the contract for how schemas reference custom type mappings.

**Detailed Task File:** [tasks/FCDC-003.md](../tasks/FCDC-003.md)

---

### FCDC-004: QoS and Error Type Definitions
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 1-2 days  
**Dependencies:** None  
**Design Reference:** §4.4 Required Attributes, §11.2 Error Handling

**Description:**  
Implement QoS enumerations (DdsReliability, DdsDurability, DdsHistoryKind) and error types (DdsException, DdsReturnCode, DdsSampleInfo). These are used by attributes and runtime components.

**Detailed Task File:** [tasks/FCDC-004.md](../tasks/FCDC-004.md)

---

## Phase 2: Roslyn Source Generator

### FCDC-005: Generator Infrastructure
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-5 days  
**Dependencies:** FCDC-001, FCDC-003  
**Design Reference:** §5.1 Roslyn Source Generator Flow

**Description:**  
Set up the Roslyn IIncrementalGenerator infrastructure. Implement the discovery phase to find types with [DdsTopic], [DdsUnion], and global type mappings. Establish diagnostic reporting and source generation context.

**Detailed Task File:** [tasks/FCDC-005.md](../tasks/FCDC-005.md)

---

### FCDC-006: Schema Validation Logic
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-005  
**Design Reference:** §5.1 Phase 2 Validation, §5.4 Schema Evolution Validation

**Description:**  
Implement validation rules for appendable evolution (append-only, no reordering, no removal, no type changes). Implement schema fingerprinting and breaking change detection. Generate detailed diffs for build output.

**Detailed Task File:** [tasks/FCDC-006.md](../tasks/FCDC-006.md)

---

### FCDC-007: IDL Code Emitter
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-005, FCDC-006  
**Design Reference:** §5.1 Phase 3 IDL Generation, §4.2 Type Mapping Rules

**Description:**  
Implement IDL code generation from validated C# schemas. Emit @appendable modules, typedefs, enums, structs, unions with correct @key and @optional annotations. Map C# types to IDL types according to global type map.

**Detailed Task File:** [tasks/FCDC-007.md](../tasks/FCDC-007.md)

---

### FCDC-008: Alignment and Layout Calculator
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-005  
**Design Reference:** §5.3 Alignment and Padding Calculation, Design Talk §2279-2301

**Description:**  
Implement C-compatible alignment calculation for structs and unions. Calculate padding for union payload offsets based on maximum arm alignment. Generate correct [FieldOffset] attributes. Include debug assertions for sizeof/offsetof validation.

**Detailed Task File:** [tasks/FCDC-008.md](../tasks/FCDC-008.md)

---

### FCDC-009: Native Type Code Emitter
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 6-8 days  
**Dependencies:** FCDC-008  
**Design Reference:** §5.1 Phase 4 Native Type Generation, §8 Type System

**Description:**  
Generate TNative blittable structs with correct [StructLayout], fixed buffers for bounded data, explicit layout for unions, and pointer+length for unbounded data. Ensure unmanaged constraint satisfaction.

**Detailed Task File:** [tasks/FCDC-009.md](../tasks/FCDC-009.md)

---

### FCDC-010: Managed View Type Code Emitter
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-009  
**Design Reference:** §5.1 Phase 5 Managed Type Generation, §8.1 Three-Type Model

**Description:**  
Generate TManaged ref struct views that wrap native types. Emit ReadOnlySpan<byte> for strings, ReadOnlySpan<T> for sequences, nullable ref structs for optional values, and ergonomic union views with safe accessors.

**Detailed Task File:** [tasks/FCDC-010.md](../tasks/FCDC-010.md)

---

### FCDC-011: Marshaller Code Emitter
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 7-10 days  
**Dependencies:** FCDC-009, FCDC-010  
**Design Reference:** §5.1 Phase 6 Marshaller Generation, §6.4 IMarshaller Interface

**Description:**  
Generate marshaller code for converting between managed and native representations. Implement UTF-8 encoding/decoding, optional presence handling, union active arm switching, and arena-backed allocations for variable-size data.

**Detailed Task File:** [tasks/FCDC-011.md](../tasks/FCDC-011.md)

---

### FCDC-012: Metadata Registry Code Emitter
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-005  
**Design Reference:** §5.1 Phase 7 Metadata Registry, §6.2-6.5 Topic/QoS Auto-Binding

**Description:**  
Generate metadata registry code that maps topic types to topic names, QoS defaults, and key field indices. Provide lookup APIs for runtime components to auto-configure readers/writers.

**Detailed Task File:** [tasks/FCDC-012.md](../tasks/FCDC-012.md)

---

### FCDC-013: Generator Testing Suite
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 5-6 days  
**Dependencies:** FCDC-007 through FCDC-012  
**Design Reference:** §12.1 Unit Tests - Schema Generator Tests

**Description:**  
Create comprehensive unit tests for the generator: correct IDL emission, alignment calculation, schema evolution detection, error reporting, marshaller round-trips, and UTF-8 handling. Use snapshot testing for generated code.

**Detailed Task File:** [tasks/FCDC-013.md](../tasks/FCDC-013.md)

---

## Phase 3: Runtime Components

### FCDC-014: Arena Memory Manager
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** None  
**Design Reference:** §7.1 Arena Design, Design Talk §2203-2210

**Description:**  
Implement the Arena class with bump-pointer allocation, geometric growth, reset/rewind, and trim policy (MaxRetainedCapacity). Ensure deterministic disposal and efficient reuse. Include debug tracking for memory leaks.

**Detailed Task File:** [tasks/FCDC-014.md](../tasks/FCDC-014.md)

---

### FCDC-015: P/Invoke Declarations
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-004  
**Design Reference:** §11.1 P/Invoke Strategy

**Description:**  
Define P/Invoke declarations for essential Cyclone DDS C API functions: dds_create_participant, dds_create_topic, dds_create_writer, dds_create_reader, dds_write, dds_writedispose, dds_take, dds_return_loan. Wrap handles in IntPtr.

**Detailed Task File:** [tasks/FCDC-015.md](../tasks/FCDC-015.md)

---

### FCDC-016: DdsParticipant Implementation
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-015  
**Design Reference:** §6.1 DdsParticipant

**Description:**  
Implement DdsParticipant wrapper class. Create Cyclone domain participant, store partition configuration, implement IDisposable with deterministic native handle cleanup. Handle QoS creation and participant deletion.

**Detailed Task File:** [tasks/FCDC-016.md](../tasks/FCDC-016.md)

---

### FCDC-017: DdsWriter<TNative> (Inline-Only)
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-016, FCDC-012  
**Design Reference:** §6.2 DdsWriter<TNative>, §11.2 Error Handling

**Description:**  
Implement DdsWriter<TNative> for inline-only types. Auto-discover topic metadata from registry. Create DDS publisher, topic, and writer. Implement Write(), WriteDispose(), and TryWrite() methods. Handle error mapping to DdsException/status codes.

**Detailed Task File:** [tasks/FCDC-017.md](../tasks/FCDC-017.md)

---

### FCDC-018: DdsReader<TNative> (Inline-Only)
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-016, FCDC-012  
**Design Reference:** §6.3 DdsReader<TNative>, §7.2 Zero-Copy Read Strategy

**Description:**  
Implement DdsReader<TNative> for inline-only types. Auto-discover topic metadata. Create DDS subscriber, topic, reader. Implement Take(), Read(), and TryTake() with caller-provided spans. Handle loaning and returning loans. Fill DdsSampleInfo correctly.

**Detailed Task File:** [tasks/FCDC-018.md](../tasks/FCDC-018.md)

---

### FCDC-018A: DDS Integration Validation Suite (CRITICAL)
**Status:** 🔴 Not Started  
**Priority:** **CRITICAL - BLOCKING**  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-016, FCDC-017, FCDC-018, BATCH-13.1  
**Design Reference:** [DDS-INTEGRATION-TEST-DESIGN.md](./DDS-INTEGRATION-TEST-DESIGN.md)

**Description:**  
**PROVE THE INFRASTRUCTURE WORKS before building more features!**

Implement 32 integration tests validating the complete C# → Native DDS pipeline:

**Test Categories:**
- Data Type Coverage (10 tests) - Primitives, arrays, sequences, nested structs, keyed topics
- Marshalling Correctness (5 tests) - Byte-perfect round-trips, UTF-8, deep equality
- Keyed Topics (4 tests) - Multiple instances, dispose, unregister
- QoS Settings (6 tests) - Reliable, best-effort, durability, history, deadline, lifespan
- Partitions (3 tests) - Isolation, multiple partitions, wildcards
- Error Handling (4 tests) - Invalid descriptors, type mismatch, disposal

**What This Validates:**
- ✅ Topic descriptors built correctly (BATCH-13.1)
- ✅ Marshalling accurate (byte-level verification)
- ✅ Native calls succeed (actual DDS pub/sub)
- ✅ Data sent == Data received (end-to-end)
- ✅ QoS, partitions, keys functional

**Critical Success Criteria:**
- 100% pass rate (32/32 tests)
- No data corruption
- No data loss (reliable QoS)
- Proper isolation (partitions/keys)

**Why Critical:**
This is the FIRST TIME actual data flows end-to-end through the entire stack. Must pass before proceeding to advanced features (variable-size types, unions, etc.).

**Test Pattern:** Single-process tests (writer + reader in same test) for determinism.

**Detailed Task File:** [tasks/FCDC-018A.md](../tasks/FCDC-018A.md)  
**Design Document:** [docs/DDS-INTEGRATION-TEST-DESIGN.md](./DDS-INTEGRATION-TEST-DESIGN.md)

---

### FCDC-019: TakeScope Implementation
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-014, FCDC-015, **FCDC-018A**  
**Design Reference:** §6.5 TakeScope, §11.3 Loan Management

**Description:**  
Implement TakeScope<TManaged> ref struct. Wrap dds_take loan with managed views over native samples. Expose ReadOnlySpan<TManaged> and ReadOnlySpan<DdsSampleInfo>. Dispose returns loan and resets arena. Ensure ref struct lifetime safety.

**Note:** Deferred until after integration validation (FCDC-018A) proves basic infrastructure.

**Detailed Task File:** [tasks/FCDC-019.md](../tasks/FCDC-019.md)

---

### FCDC-020: DdsWriter<TManaged> (Variable-Size)
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-017, FCDC-014, **FCDC-018A**  
**Design Reference:** §6.4 DdsWriter<TManaged, TNative, TMarshaller>

**Description:**  
Implement DdsWriter<TManaged, TNative, TMarshaller> for variable-size capable types. Require arena parameter in Write/WriteDispose methods. Use marshaller to convert managed to native with arena-backed allocations. Handle disposal correctly.

**Note:** Build on confidence from FCDC-018A (infrastructure proven working).

**Detailed Task File:** [tasks/FCDC-020.md](../tasks/FCDC-020.md)

---

### FCDC-021: DdsReader<TManaged> (Variable-Size)
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 4-5 days  
**Dependencies:** FCDC-018, FCDC-019  
**Design Reference:** §6.5 DdsReader<TManaged>

**Description:**  
Implement DdsReader<TManaged, TNative, TMarshaller> returning TakeScope. Use marshaller to construct managed views from native loaned samples. Coordinate arena lifetime with TakeScope disposal.

**Detailed Task File:** [tasks/FCDC-021.md](../tasks/FCDC-021.md)

---

### FCDC-022: Comprehensive Runtime Testing Suite
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 4-6 days  
**Dependencies:** FCDC-016 through FCDC-021, **FCDC-018A**  
**Design Reference:** §12.2 Integration Tests

**Description:**  
Expand on FCDC-018A validation with comprehensive tests for advanced features:
- Variable-size types (TManaged) with TakeScope
- Schema evolution (v1 ↔ v2 readers/writers)
- Multiple participants/partitions
- Performance validation (zero allocations in steady state)
- Stress testing (10K+ samples)

**Note:** FCDC-018A already validated basic infrastructure. This extends coverage to advanced features.

**Detailed Task File:** [tasks/FCDC-022.md](../tasks/FCDC-022.md)

---

## Phase 4: Native Shim & Build Integration

### FCDC-023: Native Shim Library
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 4-5 days  
**Dependencies:** None  
**Design Reference:** §7.3 Cyclone Allocator Integration, §3.1 Deliverables #4

**Description:**  
Implement C native shim library that links against Cyclone DDS. Provide fcdc_configure_allocator() for custom allocator integration (ddsrt_set_allocator). Implement any helper functions needed for efficient interop. Set up CMake build.

**Detailed Task File:** [tasks/FCDC-023.md](../tasks/FCDC-023.md)

---

### FCDC-024: IDL Compiler Integration
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-007  
**Design Reference:** §14.1 MSBuild Targets

**Description:**  
Create MSBuild targets to run Cyclone idlc on generated .idl files. Detect IDL changes and invoke idlc. Handle idlc output (type descriptors). Ensure generated C code is available for native shim compilation.

**Detailed Task File:** [tasks/FCDC-024.md](../tasks/FCDC-024.md)

---

### FCDC-025: Native Shim Build Integration
**Status:** 🔴 Not Started  
**Priority:** Critical  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-023, FCDC-024  
**Design Reference:** §14.1 MSBuild Targets

**Description:**  
Integrate native shim compilation into MSBuild pipeline. Invoke CMake to build native shim after IDL compilation. Copy resulting native library to output directory. Support incremental builds.

**Detailed Task File:** [tasks/FCDC-025.md](../tasks/FCDC-025.md)

---

### FCDC-026: NuGet Packaging
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-001 through FCDC-025  
**Design Reference:** §3.1 Deliverables

**Description:**  
Package CycloneDDS.Schema, CycloneDDS.Generator, CycloneDDS.Runtime, and CycloneDDS.NativeShim as NuGet packages. Set up proper dependencies, include build targets and props files. Test package installation and usage in a fresh project.

**Detailed Task File:** [tasks/FCDC-026.md](../tasks/FCDC-026.md)

---

## Phase 5: Advanced Features & Polish

### FCDC-027: Union Support Complete
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** Covered in FCDC-007, FCDC-009, FCDC-010, FCDC-011  
**Dependencies:** Generator tasks  
**Design Reference:** §9 Union Support

**Description:**  
This is a meta-task tracking union support across the generator phases. Verify that unions with variable-size arms, nested unions, and all union edge cases work correctly. Validate with comprehensive tests.

**Detailed Task File:** [tasks/FCDC-027.md](../tasks/FCDC-027.md)

---

### FCDC-028: Optional Members Complete
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** Covered in FCDC-007, FCDC-009, FCDC-010, FCDC-011  
**Dependencies:** Generator tasks  
**Design Reference:** §10 Optional Members

**Description:**  
Meta-task tracking optional member support. Verify presence/absence handling, nullable ref struct views, and correct XTypes @optional emission. Test nested optional types and optional union arms.

**Detailed Task File:** [tasks/FCDC-028.md](../tasks/FCDC-028.md)

---

### FCDC-029: ArenaList<T> Helper
**Status:** 🔴 Not Started  
**Priority:** Medium  
**Estimated Effort:** 2-3 days  
**Dependencies:** FCDC-014  
**Design Reference:** Design Talk §2263-2268

**Description:**  
Implement ArenaList<T> helper type that acts like List<T> but stores data in Arena memory. Enable mutation of sequence data without GC allocations. Provide AsSpan() for direct assignment to native structs. Document usage patterns.

**Detailed Task File:** [tasks/FCDC-029.md](../tasks/FCDC-029.md)

---

### FCDC-030: DebuggerDisplay Attributes
**Status:** 🔴 Not Started  
**Priority:** Low  
**Estimated Effort:** 1-2 days  
**Dependencies:** FCDC-009, FCDC-010  
**Design Reference:** Design Talk §2269-2276

**Description:**  
Add [DebuggerDisplay] attributes to generated TNative and TManaged types. Implement debugger proxies for pointer-based fields (Utf8StringRef, SeqFloat) to show decoded values in debugger. Improve developer experience during debugging.

**Detailed Task File:** [tasks/FCDC-030.md](../tasks/FCDC-030.md)

---

### FCDC-031: Performance Benchmarks
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 3-4 days  
**Dependencies:** FCDC-022  
**Design Reference:** §13 Performance Requirements

**Description:**  
Create performance benchmarks using BenchmarkDotNet. Measure inline-only type throughput/latency, variable-size type throughput, arena allocation overhead, and compare to raw C API. Verify <1μs overhead goal. Document results.

**Detailed Task File:** [tasks/FCDC-031.md](../tasks/FCDC-031.md)

---

### FCDC-032: Fuzz and Stress Testing
**Status:** 🔴 Not Started  
**Priority:** Medium  
**Estimated Effort:** 3-5 days  
**Dependencies:** FCDC-022  
**Design Reference:** §12.3 Fuzz and Stress Tests

**Description:**  
Implement fuzz testing with randomized variable-size payloads, large payloads, and malformed data. Create long-running soak tests (24+ hours) to detect memory leaks. Use memory profilers to validate arena behavior.

**Detailed Task File:** [tasks/FCDC-032.md](../tasks/FCDC-032.md)

---

### FCDC-033: Documentation and Examples
**Status:** 🔴 Not Started  
**Priority:** High  
**Estimated Effort:** 5-7 days  
**Dependencies:** FCDC-026  
**Design Reference:** All sections

**Description:**  
Write user-facing documentation: getting started guide, schema authoring guide, performance best practices, migration guide. Create example projects (simple pub/sub, ECS integration, union usage, optional fields). Generate API reference from XML comments.

**Detailed Task File:** [tasks/FCDC-033.md](../tasks/FCDC-033.md)

---

## Summary Statistics

**Total Tasks:** 33  
**Total Estimated Effort:** 110-150 person-days (~5-7 months with 1 developer)

**Critical Path Tasks (must complete first):**
- FCDC-001, FCDC-003, FCDC-005, FCDC-006, FCDC-007, FCDC-008, FCDC-009, FCDC-011, FCDC-014, FCDC-015, FCDC-016, FCDC-017, FCDC-018, FCDC-023, FCDC-024, FCDC-025

**Phase Completion Gates:**

- **Phase 1 Complete:** Can define schemas with attributes and wrapper types ✅ FCDC-001 through FCDC-004
- **Phase 2 Complete:** Generator produces correct IDL and native/managed code ✅ FCDC-005 through FCDC-013
- **Phase 3 Complete:** Runtime can send/receive inline and variable-size topics ✅ FCDC-014 through FCDC-022
- **Phase 4 Complete:** Fully integrated build pipeline with NuGet packages ✅ FCDC-023 through FCDC-026
- **Phase 5 Complete:** Production-ready with docs, benchmarks, and polish ✅ FCDC-027 through FCDC-033

---

## Next Steps

1. Review and approve this task breakdown
2. Prioritize Phase 1 tasks for immediate implementation
3. Set up project repository with correct structure (docs/, tasks/, src/, tests/)
4. Begin implementation with FCDC-001 (Schema Attribute Definitions)
