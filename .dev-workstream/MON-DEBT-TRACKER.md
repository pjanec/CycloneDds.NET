# MON-DEBT-TRACKER

**Purpose:** Track P2/P3 deferred issues and technical debt for DDS Monitor workstream.  
**Source:** Review findings and developer reports.  
**Rule:** P1 issues become corrective tasks, not debt entries.

| ID | Date | Source (Batch/Report) | Priority | Area | Description | Target Batch | Status |
|---|---|---|---|---|---|---|---|
| MON-DEBT-001 | 2026-02-28 | N/A | P3 | Init | Debt tracker initialized. | N/A | ? |
| MON-DEBT-002 | 2026-02-28 | MON-BATCH-04 | P2 | Tests/CodeGen | Generated DDS test code emits CS8669 nullable warning (KeyedType.g.cs). Investigate adding explicit '#nullable' or adjusting codegen/test settings to silence. | MON-BATCH-16 | ? |
| MON-DEBT-003 | 2026-02-28 | MON-BATCH-07 | P3 | FilterCompiler | FilterCompiler uses DynamicInvoke for payload field evaluation; consider cached strongly-typed delegates to reduce overhead. | MON-BATCH-16 | ? |
| MON-DEBT-004 | 2026-03-01 | MON-BATCH-14 | P2 | SamplesPanel | Keyboard navigation loses scroll sync after PageUp/PageDown; selection moves offscreen. | MON-BATCH-16 | ? |
| MON-DEBT-005 | 2026-03-01 | MON-BATCH-14 | P2 | DetailPanel | Detail panel open latency excessive (debounce too long). | MON-BATCH-16 | ? |
| MON-DEBT-006 | 2026-03-01 | MON-BATCH-14 | P2 | Desktop | Topics panel cannot be reopened after close (missing launcher/menu). | MON-BATCH-16 | ? |
| MON-DEBT-007 | 2026-03-01 | MON-BATCH-14 | P2 | ContextMenu | Right-click shows browser menu; app context menu not rendered reliably. | MON-BATCH-16 | ? |
| MON-DEBT-008 | 2026-03-01 | MON-BATCH-15 | P3 | Tests/Automation | Playwright automation unreliable for right-click and panel button interactions; consider bUnit or UI test harness improvements. | MON-BATCH-16 | ? |
