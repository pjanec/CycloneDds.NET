# MON-DEBT-TRACKER

**Purpose:** Track P2/P3 deferred issues and technical debt for DDS Monitor workstream.  
**Source:** Review findings and developer reports.  
**Rule:** P1 issues become corrective tasks, not debt entries.

| ID | Date | Source (Batch/Report) | Priority | Area | Description | Target Batch | Status |
|---|---|---|---|---|---|---|---|
| MON-DEBT-002 | 2026-02-28 | MON-BATCH-04 | P2 | Tests/CodeGen | Generated DDS test code emits CS8669 nullable warning (KeyedType.g.cs). Investigate adding explicit '#nullable' or adjusting codegen/test settings to silence. | MON-BATCH-16 | ? |
| MON-DEBT-003 | 2026-02-28 | MON-BATCH-07 | P3 | FilterCompiler | FilterCompiler uses DynamicInvoke for payload field evaluation; consider cached strongly-typed delegates to reduce overhead. | MON-BATCH-16 | ? |
| MON-DEBT-012 | 2026-03-04 | User report | P2 | Desktop / Menu | Main menu should look like a normal pull-down menu with items, not a set of buttons in a single line. | MON-BATCH-20 | Resolved |
| MON-DEBT-013 | 2026-03-04 | User report | P3 | Panel Toolbars | The "Toolbar" of each panel consists of buttons with text, which looks cheap. Replace with colored icons showing tooltips. | N/A | Pending |
| MON-DEBT-014 | 2026-03-04 | User report | P3 | Topics Panel | The filter bar in the topics window is too bulky and takes too much space. Needs smaller font and a graphical look distinct from normal buttons. | N/A | Pending |
| MON-DEBT-015 | 2026-03-19 | ME2-BATCH-01 | P3 | Workspace | `GetPanelBaseName` doesn't fully sanitize long AQNs, making generated IDs verbose and fragile. | ME2-BATCH-06 | Pending |
| MON-DEBT-016 | 2026-03-19 | ME2-BATCH-01 | P2 | Extensibility | `Type.GetType` in Desktop.razor relies strictly on executing assembly resolution; blockers for future plugin extensions. | ME2-BATCH-06 | Pending |
| MON-DEBT-017 | 2026-03-19 | ME2-BATCH-01 | P2 | Detail Panel Perf | `IsUnionArmVisible` traverses fields using O(N^2) LINQ every render-run; extract to a dictionary cache keyed by Discriminator. | ME2-BATCH-04 | Resolved |
| MON-DEBT-018 | 2026-03-19 | ME2-BATCH-01 | P2 | Detail Panel Perf | `GetUnionInfo` triggers expensive MemberInfo reflection continuously on union rows; needs ConcurrentDictionary caching. | ME2-BATCH-04 | Resolved |
| MON-DEBT-019 | 2026-03-19 | ME2-BATCH-02 | P2 | Samples Sorting | `ApplySortToViewCache` relies on monotonic ordinal insertion; this sorting technique faces determinism correctness hazards during out-of-order Replay mode data. | ME2-BATCH-03 | Resolved |
