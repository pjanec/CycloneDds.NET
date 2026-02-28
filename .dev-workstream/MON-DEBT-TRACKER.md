# MON-DEBT-TRACKER

**Purpose:** Track P2/P3 deferred issues and technical debt for DDS Monitor workstream.  
**Source:** Review findings and developer reports.  
**Rule:** P1 issues become corrective tasks, not debt entries.

| ID | Date | Source (Batch/Report) | Priority | Area | Description | Target Batch | Status |
|---|---|---|---|---|---|---|---|
| MON-DEBT-001 | 2026-02-28 | N/A | P3 | Init | Debt tracker initialized. | N/A | ? |
| MON-DEBT-002 | 2026-02-28 | MON-BATCH-04 | P2 | Tests/CodeGen | Generated DDS test code emits CS8669 nullable warning (KeyedType.g.cs). Investigate adding explicit '#nullable' or adjusting codegen/test settings to silence. | MON-BATCH-05 | ? |
| MON-DEBT-003 | 2026-02-28 | MON-BATCH-07 | P3 | FilterCompiler | FilterCompiler uses DynamicInvoke for payload field evaluation; consider cached strongly-typed delegates to reduce overhead. | MON-BATCH-08 | ? |
