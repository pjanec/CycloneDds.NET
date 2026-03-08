# BATCH-24: Export/Import Tooling & Streaming Extensibility

**Batch Number:** MON-BATCH-24  
**Tasks:** JSON Export Service, JSON Import Service, Replay Engine wiring  
**Phase:** Phase 4 (Operational Tools)  
**Estimated Effort:** 5-7 hours  
**Priority:** NORMAL  
**Dependencies:** MON-BATCH-23

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome back! With the Send Sample panel merged and Operational Tools now taking shape, we are moving onto long-term telemetry features. Users must be able to export their active monitoring sessions out of the `SampleStore` and dump them to disk as a massive JSON stream. Conversely, they must be able to import these streams and "replay" them back into the memory buffer or push them up to the real DDS network.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-24-REPORT.md`

---

## 🎯 Batch Objectives

### Task 1: Export Service (DMON-037)
- **Implement:** `IExportService` with streaming JSON write logic. Since the Sample Store can comfortably house 1,000,000+ entries, do NOT buffer the entire serialized block in memory before disk write. Use iterating serializers (like `System.Text.Json` UTF8 JSON writers) running asynchronously to write out `SampleData` records efficiently.
- **Requirement:** Allow the user to export *all* topics or merely the samples of a *single* specified topic.

### Task 2: Import Service (DMON-038)
- **Implement:** `IImportService` that efficiently parses large JSON dumps file-stream-by-file-stream without loading the entire structure into VRAM. 
- **Requirement:** Reconstruct the generic `SampleData` records by reading their encoded Topic Type strings, then dynamically instantiating the typed payload and using custom JSON readers to map property states.

### Task 3: Replay Engine Skeleton (DMON-039)
- **Implement:** `IReplayEngine` logic that allows users to ingest an import loop and either route those samples locally (feeding them into the `SampleStore` for localized GUI investigation) or globally (pushing them up via `DdsBridge.GetWriter()` out to the distributed WAN).
- **Requirement:** Do NOT build the UI (`ReplayPanel.razor`) for it yet; only structure the dependency injection layout, the service interface, and the functional testing suite.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-24-REPORT.md`):**

1. Prove mathematically or contextually how your Export logic sidesteps Out Of Memory `OutOfMemoryException` spikes when tracking million-node arrays.
2. Outline the JSON token parsing approach used for reconstructing polymorphic generic structs.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Implement `IExportService` streaming infrastructure without memory spikes.
- [ ] Implement `IImportService` asynchronous token parsing for rebuilding runtime samples.
- [ ] Implement foundational `IReplayEngine` routing capabilities (GUI sink vs DDS push target).
- [ ] Maintain 100% test coverage bridging serialize/deserialize accuracy paths.
- [ ] Ensure all code adheres to previous structural layout rules!
