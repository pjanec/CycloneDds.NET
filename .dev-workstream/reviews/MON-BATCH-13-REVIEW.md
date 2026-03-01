# MON-BATCH-13 Review

**Batch:** MON-BATCH-13  
**Reviewer:** Development Lead  
**Date:** 2026-03-01  
**Status:** ?? NEEDS FIXES

---

## Summary

Missing manual verification for DMON-017/024/025/026 and two critical runtime issues were observed (Detail panel stack overflow and Subscribe All failure).

---

## Issues Found

### Issue 1: Manual verification incomplete (DMON-017/024/025/026)

**File:** `.dev-workstream/reports/MON-BATCH-13-REPORT.md`  
**Problem:** Required manual checks for desktop shell, text view panel, keyboard navigation, and context menu are still pending.  
**Fix:** Run the manual checks and update the report with explicit results.

### Issue 2: Detail panel rendering stack overflow

**File:** `tools/DdsMonitor/Components/DetailPanel.razor`  
**Problem:** Double-clicking a sample row triggers a StackOverflow in `DetailPanel.RenderNode`, indicating unbounded recursion.  
**Fix:** Add cycle/recursion guards or depth limits in the tree renderer and validate the double-click path with a regression test.

### Issue 3: Subscribe All fails on types without descriptors

**File:** `tools/DdsMonitor/Components/TopicExplorerPanel.razor`, `tools/DdsMonitor/DdsMonitor.Engine/DdsBridge.cs`  
**Problem:** Clicking “Subscribe All” throws when encountering topic types missing `GetDescriptorOps()`, aborting the operation.  
**Fix:** Guard bulk subscribe to skip invalid topics and surface a user-facing message; ensure `DdsBridge.Subscribe` handles this case without killing the circuit.

---

## Verdict

**Status:** NEEDS FIXES

**Required Actions:**
1. Complete and document manual DMON-017/024/025/026 checks.
2. Fix Detail panel recursive rendering stack overflow and add a regression test.
3. Harden Subscribe All against invalid topic metadata and add a test.

---

**Next Batch:** MON-BATCH-14 (corrective + UI fixes)
