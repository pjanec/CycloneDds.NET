# IDL Importer - Task Tracker

**Project:** FastCycloneDDS C# Bindings  
**Tool:** CycloneDDS.IdlImporter  
**Status:** 🔵 NOT STARTED  
**Last Updated:** 2026-01-28

**Reference:** See [IDLImport-TASK-DETAILS.md](./IDLImport-TASK-DETAILS.md) for detailed task descriptions

---

## Overview

The IDL Importer Tool enables importing existing IDL files into the C# DSL ecosystem, creating a bidirectional bridge between legacy IDL-based DDS systems and the modern FastCycloneDDS C# Bindings.

**Key Features:**
- Automated IDL → C# DSL translation via `idlc -l json`
- Multi-assembly support with folder structure preservation
- Functional wire compatibility with original IDL
- Comprehensive test coverage with roundtrip validation

**Total Tasks:** 15  
**Estimated Effort:** 30-45 development days

---

## Phase 1: Foundation 🔵

**Goal:** Establish project structure and shared infrastructure  
**Status:** 🔵 NOT STARTED

- [x] **IDLIMP-001** Project Setup and Shared Infrastructure → [details](./IDLImport-TASK-DETAILS.md#idlimp-001-project-setup-and-shared-infrastructure) ✅
- [x] **IDLIMP-002** IdlcRunner Enhancement for Include Paths → [details](./IDLImport-TASK-DETAILS.md#idlimp-002-idlcrunner-enhancement-for-include-paths) ✅
- [x] **IDLIMP-003** Type Mapper Implementation → [details](./IDLImport-TASK-DETAILS.md#idlimp-003-type-mapper-implementation) ✅

**Success Criteria:**
- ✅ .NET 8 console project builds successfully
- ✅ Can execute `idlc -l json` with include paths
- ✅ All primitive and collection type mappings implemented and tested

---

## Phase 2: Core Importer Logic 🔵

**Goal:** Implement recursive IDL processing and file structure mirroring  
**Status:** 🔵 NOT STARTED

- [ ] **IDLIMP-004** Importer Core - File Queue and Recursion → [details](./IDLImport-TASK-DETAILS.md#idlimp-004-importer-core---file-queue-and-recursion) 🔵
- [ ] **IDLIMP-005** JSON Parsing and File Metadata Extraction → [details](./IDLImport-TASK-DETAILS.md#idlimp-005-json-parsing-and-file-metadata-extraction) 🔵

**Success Criteria:**
- ✅ Processes master file and all recursive includes exactly once
- ✅ Mirrors folder structure from source to output
- ✅ Extracts type-to-file mappings from JSON
- ✅ Handles circular includes gracefully

---

## Phase 3: C# Code Generation 🔵

**Goal:** Implement C# DSL code emission for all IDL type constructs  
**Status:** 🔵 NOT STARTED

- [ ] **IDLIMP-006** CSharpEmitter - Struct and Enum Generation → [details](./IDLImport-TASK-DETAILS.md#idlimp-006-csharpemitter---struct-and-enum-generation) 🔵
- [ ] **IDLIMP-007** CSharpEmitter - Collection Type Support → [details](./IDLImport-TASK-DETAILS.md#idlimp-007-csharpemitter---collection-type-support) 🔵
- [ ] **IDLIMP-008** CSharpEmitter - Union Type Support → [details](./IDLImport-TASK-DETAILS.md#idlimp-008-csharpemitter---union-type-support) 🔵

**Success Criteria:**
- ✅ Generates valid C# syntax with proper attributes
- ✅ All collection types (sequences, arrays, bounded strings) supported
- ✅ Union types with discriminator and case labels working
- ✅ Generated code compiles without errors

---

## Phase 4: CLI and Integration 🔵

**Goal:** Complete tool with user-friendly CLI and end-to-end validation  
**Status:** 🔵 NOT STARTED

- [ ] **IDLIMP-009** Command-Line Interface Implementation → [details](./IDLImport-TASK-DETAILS.md#idlimp-009-command-line-interface-implementation) 🔵
- [ ] **IDLIMP-010** End-to-End Integration with Existing Test IDL → [details](./IDLImport-TASK-DETAILS.md#idlimp-010-end-to-end-integration-with-existing-test-idl) 🔵 **🚨 GATE**

**Success Criteria:**
- ✅ CLI with argument validation and help system
- ✅ Successfully imports `atomic_tests.idl`
- ✅ Generated C# compiles and CodeGen produces equivalent IDL
- ✅ End-to-end workflow validated

**Note:** IDLIMP-010 is a validation gate - must pass before advancing to Phase 5

---

## Phase 5: Advanced Features 🔵

**Goal:** Support advanced IDL features (nested types, optional, member IDs)  
**Status:** 🔵 NOT STARTED

- [ ] **IDLIMP-011** Nested Struct Support → [details](./IDLImport-TASK-DETAILS.md#idlimp-011-nested-struct-support) 🔵
- [ ] **IDLIMP-012** Optional Member Support → [details](./IDLImport-TASK-DETAILS.md#idlimp-012-optional-member-support) 🔵
- [ ] **IDLIMP-013** Member ID (@id) Support → [details](./IDLImport-TASK-DETAILS.md#idlimp-013-member-id-id-support) 🔵

**Success Criteria:**
- ✅ Nested struct types with proper dependencies
- ✅ Optional members in Appendable/Mutable types
- ✅ Member IDs for Mutable type evolution
- ✅ All features work with existing CodeGen pipeline

---

## Phase 6: Testing Infrastructure 🔵

**Goal:** Comprehensive test coverage and roundtrip validation  
**Status:** 🔵 NOT STARTED

- [ ] **IDLIMP-014** Comprehensive Unit Test Suite → [details](./IDLImport-TASK-DETAILS.md#idlimp-014-comprehensive-unit-test-suite) 🔵
- [ ] **IDLIMP-015** Roundtrip Validation Test Suite → [details](./IDLImport-TASK-DETAILS.md#idlimp-015-roundtrip-validation-test-suite) 🔵 **🚨 GATE**

**Success Criteria:**
- ✅ 90%+ code coverage across all components
- ✅ Roundtrip tests validate wire compatibility
- ✅ All atomic test types pass roundtrip validation
- ✅ Tests integrated into CI/CD pipeline

**Note:** IDLIMP-015 is the final validation gate - tool is production-ready when this passes

---

## Development Phases Summary

| Phase | Tasks | Status | Estimated Effort |
|-------|-------|--------|------------------|
| Phase 1: Foundation | 3 | ✅ COMPLETE | 5-8 days |
| Phase 2: Core Logic | 2 | 🔵 NOT STARTED | 4-6 days |
| Phase 3: Generation | 4 | 🔵 NOT STARTED | 8-12 days |
| Phase 4: CLI & Integration | 2 | 🔵 NOT STARTED | 3-5 days |
| Phase 5: Advanced | 3 | 🔵 NOT STARTED | 5-8 days |
| Phase 6: Testing | 2 | 🔵 NOT STARTED | 5-8 days |
| **Total** | **15** | **3/15** | **30-45 days** |

---

## Legend

**Status Icons:**
- 🔵 **NOT STARTED**: Task not yet begun
- 🟡 **IN PROGRESS**: Currently being worked on
- ✅ **COMPLETE**: Task finished and validated
- 🚨 **GATE**: Validation gate - must pass before proceeding

**Phase Status:**
- All tasks complete → ✅ COMPLETE
- Any task in progress → 🟡 IN PROGRESS
- No tasks started → 🔵 NOT STARTED

---

## Quick Start

To begin development:

1. **Start with Phase 1, Task IDLIMP-001**: Create project structure
2. **Work sequentially**: Complete dependencies before dependent tasks
3. **Run tests frequently**: Each task has unit test requirements
4. **Validate at gates**: IDLIMP-010 and IDLIMP-015 are critical validation points

---

## Notes

### Critical Dependencies

- **IDLIMP-002** must complete before **IDLIMP-004** (need include path support)
- **IDLIMP-003** must complete before any emitter tasks (need type mapping)
- **IDLIMP-006, 007, 008** must complete before **IDLIMP-009** (need full code generation)
- **IDLIMP-010** validates all previous work (end-to-end gate)

### Shared Code with CodeGen

Several components are shared with existing `CycloneDDS.CodeGen`:
- `IdlcRunner`: Enhanced to support include paths
- `JsonModels`: Reused for JSON deserialization
- `IdlJsonParser`: Reused for parsing logic

Recommendation: Extract shared code to `CycloneDDS.Compiler.Common` library for better maintainability.

### Testing Strategy

- **Unit tests**: Written alongside implementation (TDD recommended)
- **Integration tests**: Created during Phase 4
- **Roundtrip tests**: Created during Phase 6
- **Minimum coverage**: 90% for production readiness

---

## Related Documents

- [Design Document](../../docs/IdlImport-design.md) - Architecture and design details
- [Task Details](./IDLImport-TASK-DETAILS.md) - Detailed task specifications
- [IDLJSON README](../../cyclonedds/src/tools/idljson/IDLJSON-README.md) - Input format documentation
- [IDL Generation Guide](../../IDL-GENERATION.md) - Output format expectations

---

## Changelog

- **2026-01-28**: Initial task tracker created with 15 tasks across 6 phases
