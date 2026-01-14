# FastCycloneDDS C# Bindings - Implementation Plan Summary

**Created:** 2026-01-14  
**Status:** Ready for Implementation

---

## Documentation Overview

This implementation plan consists of three main documents:

### 1. [fcdc-design-talk.md](./fcdc-design-talk.md)
**Purpose:** Cumulative design discussion and rationale  
**Contents:** Complete conversation about design decisions, architectural trade-offs, and technical analysis from the design talk process. Includes detailed discussions of memory management, union encoding, alignment issues, and Cyclone DDS integration.

### 2. [FCDC-DETAILED-DESIGN.md](./FCDC-DETAILED-DESIGN.md)
**Purpose:** Authoritative technical specification  
**Contents:** Comprehensive detailed design document organized into 14 sections covering:
- System Architecture
- Component Design
- Schema DSL Design
- Code Generation Pipeline
- Runtime Components
- Memory Management
- Type System
- Union Support
- Optional Members
- Native Interop Layer
- Testing Strategy
- Performance Requirements
- Build Integration

### 3. [FCDC-TASK-MASTER.md](./FCDC-TASK-MASTER.md)
**Purpose:** Task breakdown and project management  
**Contents:** Master list of 33 implementation tasks organized into 5 phases, each with unique ID, priority, estimated effort, and dependencies.

---

## Quick Start for Implementation

### Phase 1: Foundation (Estimated: 8-12 days)
**Goal:** Create schema package with attributes and wrapper types

**Critical Path Tasks:**
1. **FCDC-001:** Schema Attribute Definitions (2-3 days)
2. **FCDC-002:** Schema Wrapper Types (3-4 days)
3. **FCDC-003:** Global Type Map Registry (2 days)
4. **FCDC-004:** QoS and Error Type Definitions (1-2 days)

**Gate:** Can define schemas with attributes and wrapper types ✅

### Phase 2: Code Generator (Estimated: 25-35 days)
**Goal:** Roslyn source generator produces IDL and C# native/managed code

**Critical Path Tasks:**
5. **FCDC-005:** Generator Infrastructure (3-5 days)
6. **FCDC-006:** Schema Validation Logic (4-5 days)
7. **FCDC-007:** IDL Code Emitter (5-7 days)
8. **FCDC-008:** Alignment and Layout Calculator (3-4 days)
9. **FCDC-009:** Native Type Code Emitter (6-8 days)
10. **FCDC-010:** Managed View Type Code Emitter (5-7 days)
11. **FCDC-011:** Marshaller Code Emitter (7-10 days)
12. **FCDC-012:** Metadata Registry Code Emitter (2-3 days)
13. **FCDC-013:** Generator Testing Suite (5-6 days)

**Gate:** Generator produces correct IDL and native/managed code ✅

### Phase 3: Runtime (Estimated: 20-28 days)
**Goal:** Runtime can send/receive inline and variable-size topics

**Critical Path Tasks:**
14. **FCDC-014:** Arena Memory Manager (4-5 days)
15. **FCDC-015:** P/Invoke Declarations (2-3 days)
16. **FCDC-016:** DdsParticipant Implementation (2-3 days)
17. **FCDC-017:** DdsWriter<TNative> (Inline-Only) (3-4 days)
18. **FCDC-018:** DdsReader<TNative> (Inline-Only) (4-5 days)
19. **FCDC-019:** TakeScope Implementation (3-4 days)
20. **FCDC-020:** DdsWriter<TManaged> (Variable-Size) (3-4 days)
21. **FCDC-021:** DdsReader<TManaged> (Variable-Size) (4-5 days)
22. **FCDC-022:** Runtime Testing Suite (6-8 days)

**Gate:** Runtime can send/receive inline and variable-size topics ✅

### Phase 4: Build Integration (Estimated: 9-12 days)
**Goal:** Fully integrated build pipeline with NuGet packages

**Critical Path Tasks:**
23. **FCDC-023:** Native Shim Library (4-5 days)
24. **FCDC-024:** IDL Compiler Integration (2-3 days)
25. **FCDC-025:** Native Shim Build Integration (3-4 days)
26. **FCDC-026:** NuGet Packaging (2-3 days)

**Gate:** Fully integrated build pipeline with NuGet packages ✅

### Phase 5: Polish (Estimated: 15-22 days)
**Goal:** Production-ready with docs, benchmarks, and polish

**Tasks:**
27. **FCDC-027:** Union Support Complete (verification)
28. **FCDC-028:** Optional Members Complete (verification)
29. **FCDC-029:** ArenaList<T> Helper (2-3 days)
30. **FCDC-030:** DebuggerDisplay Attributes (1-2 days)
31. **FCDC-031:** Performance Benchmarks (3-4 days)
32. **FCDC-032:** Fuzz and Stress Testing (3-5 days)
33. **FCDC-033:** Documentation and Examples (5-7 days)

**Gate:** Production-ready with docs, benchmarks, and polish ✅

---

## Total Project Estimate

**Duration:** 110-150 person-days (~5-7 months with 1 developer)

**Critical Path:** Phases 1-4 required for MVP (62-87 days ~3-4 months)

---

## Key Design Decisions (from Design Talk)

### 1. C#-First Schema DSL ✅
- Users define schemas in C# with attributes
- Generator emits IDL → Cyclone idlc processes it
- Avoids hand-writing IDL while leveraging Cyclone's tooling

### 2. @appendable Everywhere (Mandatory) ✅
- All types implicitly @appendable (backward compatible)
- Append-only evolution enforced by generator
- Schema fingerprinting detects breaking changes

### 3. Three-Type Model ✅
- **Schema Type:** User-authored C# with attributes
- **Native Type (TNative):** Blittable, matches Cyclone C layout
- **Managed Type (TManaged):** ref struct views, allocation-free

### 4. Arena-Based Memory Management ✅
- Variable-size data backed by reusable Arena
- Zero GC allocations in steady state
- Explicit lifetimes via TakeScope

### 5. Zero-Copy Reads via Loaning ✅
- Use Cyclone's dds_take loaning
- TakeScope wraps loan, returns on dispose
- No copying for inline-only types

### 6. Union Layout with C-Compatible Alignment ✅
- **NOT hardcoded to offset 4**
- Calculate padding based on max arm alignment
- Explicit [FieldOffset] generation

### 7. Global Type Map for Custom .NET Types ✅
- Guid → octet[16]
- DateTime → int64 ticks
- Quaternion → struct { float x,y,z,w }
- FixedString32 → octet[32] (UTF-8 NUL-padded)

---

## Critical Implementation Notes

### From Design Talk §2279-2301: Union Alignment Trap 🚨

**DO NOT hardcode union payload offset to 4 bytes!**

Correct calculation:
```csharp
int payloadOffset = (discriminatorSize + (maxArmAlignment - 1)) & ~(maxArmAlignment - 1);
```

Example: If discriminator is `int` (4 bytes) but an arm contains `double` (8-byte alignment), payload offset must be 8, not 4.

### From Design Talk §2203-2210: Arena Trim Policy 🚨

**Prevent memory bloat in long-running processes:**

On Arena.Reset(), check if capacity > MaxRetainedCapacity and shrink if needed.

Example: Arena grows to 100MB during level load, but normal operation only needs 10MB. Trim excess on reset.

### From Design Talk §2193-2201: UTF-8 Validation 🚨

**Validate UTF-8 correctness for FixedStringN:**

Reject at marshal time if:
- String exceeds max bytes (after UTF-8 encoding)
- Truncation would split a multi-byte character

Debug builds should validate; release builds can optionally skip for performance.

---

## Detailed Task Files

Individual task files provide comprehensive implementation guidance for each task:

- **tasks/FCDC-001.md:** Schema Attribute Definitions
- **tasks/FCDC-005.md:** Generator Infrastructure
- **tasks/FCDC-009.md:** Native Type Code Emitter
- **tasks/FCDC-014.md:** Arena Memory Manager *(to be created)*
- **tasks/FCDC-017.md:** DdsWriter<TNative> *(to be created)*
- *(Additional task files to be created as needed)*

Each task file includes:
- **Objectives:** Clear goals
- **Acceptance Criteria:** Testable requirements
- **Implementation Details:** Code samples, file structure, key algorithms
- **Testing Requirements:** Unit and integration tests
- **Definition of Done:** Checklist for task completion

---

## Next Actions

### Immediate (Week 1)
1. Review and approve this implementation plan
2. Set up repository structure:
   ```
   src/
   ├── CycloneDDS.Schema/
   ├── CycloneDDS.Generator/
   ├── CycloneDDS.Runtime/
   └── CycloneDDS.NativeShim/
   tests/
   ├── CycloneDDS.Schema.Tests/
   ├── CycloneDDS.Generator.Tests/
   └── CycloneDDS.Runtime.Tests/
   docs/
   ├── fcdc-design-talk.md ✅
   ├── FCDC-DETAILED-DESIGN.md ✅
   ├── FCDC-TASK-MASTER.md ✅
   └── FCDC-IMPLEMENTATION-PLAN-SUMMARY.md ✅
   tasks/
   ├── FCDC-001.md ✅
   ├── FCDC-005.md ✅
   ├── FCDC-009.md ✅
   └── ... (30 more to create)
   ```
3. Begin with **FCDC-001: Schema Attribute Definitions**

### Short-term (Weeks 2-4)
- Complete Phase 1 (Foundation)
- Begin Phase 2 (Generator Infrastructure and Discovery)

### Medium-term (Months 2-3)
- Complete Phase 2 (Code Generation Pipeline)
- Begin Phase 3 (Runtime Components)

### Long-term (Months 4-5)
- Complete Phase 3 and 4 (Runtime + Build Integration)
- MVP Release

### Polish (Months 6-7)
- Complete Phase 5 (Polish, Docs, Benchmarks)
- Production Release

---

## Success Metrics

### MVP (End of Phase 4)
- [ ] Can define schemas in C# with attributes
- [ ] Generator produces correct IDL
- [ ] Cyclone idlc integrates into build
- [ ] Can send/receive inline-only topics (zero-copy)
- [ ] Can send/receive variable-size topics (arena-backed)
- [ ] Unions work correctly with proper alignment
- [ ] Optional members work correctly
- [ ] Build pipeline fully integrated
- [ ] NuGet packages published
- [ ] All critical path tests pass

### Production (End of Phase 5)
- [ ] Performance benchmarks validate <1μs overhead
- [ ] Zero allocations in steady state (verified)
- [ ] Comprehensive documentation and examples
- [ ] Fuzz and stress tests pass (24+ hour soak)
- [ ] Production users successfully integrate

---

## Risk Mitigation

### Technical Risks

**Risk 1: Alignment calculation bugs**
- *Mitigation:* Comprehensive tests with unions containing different alignment requirements (double, long, nested structs)
- *Detection:* Debug asserts on sizeof/offsetof in generated code

**Risk 2: Arena memory leaks**
- *Mitigation:* Long-running soak tests, memory profilers
- *Detection:* Track arena high watermark, verify reset behavior

**Risk 3: Cyclone idlc integration complexity**
- *Mitigation:* Start simple (inline-only types first), incrementally add complexity
- *Detection:* Integration tests comparing generated C types with expectations

**Risk 4: Generator performance on large schemas**
- *Mitigation:* Proper incremental generation caching
- *Detection:* Benchmark generation time for 100+ topic types

### Schedule Risks

**Risk 1: Underestimated complexity**
- *Mitigation:* Phases are independently useful, can slip Phase 5 if needed
- *Response:* Re-prioritize tasks, defer ArenaList/DebuggerDisplay to later

**Risk 2: Cyclone DDS API changes**
- *Mitigation:* Pin Cyclone version, abstract P/Invoke layer
- *Response:* Update P/Invoke declarations, re-test

---

## Conclusion

This implementation plan provides a clear path from design to production-ready FastCycloneDDS C# Bindings. The phased approach allows for incremental validation and course correction while maintaining focus on the critical path to MVP.

**The project is ready to begin implementation with FCDC-001.**
