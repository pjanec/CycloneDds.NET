# ME1 — Task Tracker

**Project:** DDS Monitor Feature Extensions (Monitoring Extensions 1)  
**Status:** Not Started  
**Last Updated:** 2026-03-14

**Reference:** See [ME1-TASK-DETAILS.md](./ME1-TASK-DETAILS.md) for detailed task descriptions.  
**Design:** See [ME1-DESIGN.md](./ME1-DESIGN.md) for architecture and rationale.

---

## Phase 1 — CodeGen & Schema Core

**Goal:** Extend the code generator and schema layer with typed enum bit-bounds, `[InlineArray]` support, and optional topic names.

- [ ] **ME1-T01** Typed Enum `@bit_bound` → [details](./ME1-TASK-DETAILS.md#me1-t01--typed-enum-bit_bound-support)
- [ ] **ME1-T02** `[InlineArray]` Support → [details](./ME1-TASK-DETAILS.md#me1-t02--inlinearray-support)
- [ ] **ME1-T03** Default Topic Name from Namespace → [details](./ME1-TASK-DETAILS.md#me1-t03--default-topic-name-from-namespace)

---

## Phase 2 — Filter Engine Enhancements

**Goal:** Expose string-method filter operators in the visual builder; enable CLI-safe alphabetical comparison operators.

- [ ] **ME1-T04** StartsWith / EndsWith in Filter Builder UI → [details](./ME1-TASK-DETAILS.md#me1-t04--startswith--endswith-in-filter-builder-ui)
- [ ] **ME1-T05** CLI-Safe Filter Operators (`gt`, `lt`, `ge`, `le`, `eq`, `ne`) → [details](./ME1-TASK-DETAILS.md#me1-t05--cli-safe-filter-operators)

---

## Phase 3 — Engine Architecture Extensions

**Goal:** Support multi-participant DDS ingestion with global sample ordinals, participant metadata, and filter-first ordinal allocation.

- [ ] **ME1-T06** Multi-Participant Reception → [details](./ME1-TASK-DETAILS.md#me1-t06--multi-participant-reception)
- [ ] **ME1-T07** Global Sample Ordinal + Participant Stamping → [details](./ME1-TASK-DETAILS.md#me1-t07--global-sample-ordinal--participant-stamping)

---

## Phase 4 — DDS Monitor UI

**Goal:** Hide inactive union arms in editor and inspector panels; add transport controls and a participant configuration dialog to the main toolbar.

- [ ] **ME1-T08** Union Arm Visibility → [details](./ME1-TASK-DETAILS.md#me1-t08--union-arm-visibility)
- [ ] **ME1-T09** Start/Pause/Reset Toolbar + Participant Editor → [details](./ME1-TASK-DETAILS.md#me1-t09--startpausereset-toolbar--participant-editor)

---

## Phase 5 — Lifecycle & Headless Mode

**Goal:** Auto-open browser and auto-shutdown; headless CLI record and replay.

- [ ] **ME1-T10** Auto-Browser Open + HTTP-Only Lifecycle → [details](./ME1-TASK-DETAILS.md#me1-t10--auto-browser-open--http-only-lifecycle)
- [ ] **ME1-T11** Headless Recorder / Replay Mode → [details](./ME1-TASK-DETAILS.md#me1-t11--headless-recorder--replay-mode)
