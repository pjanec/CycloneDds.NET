# MON-BATCH-06 Review

**Batch:** MON-BATCH-06  
**Reviewer:** Development Lead  
**Date:** 2026-02-28  
**Status:** ?? NEEDS FIXES

---

## Summary

`SampleStore` and tests were added, but the sort worker does not implement the required merge-sort behavior from the design.

---

## Issues Found

### Issue 1: Missing merge-sort behavior in background worker

**File:** `tools/DdsMonitor/DdsMonitor.Engine/SampleStore.cs`  
**Problem:** The design and task definition call for a background sort/merge worker that incrementally merges new arrivals. The current implementation rebuilds a full snapshot and sorts it each time, which does not meet the specified behavior.  
**Fix:** Implement the incremental merge-sort worker described in `docs/ddsmon/DESIGN.md` §6.2 and the DMON-010 task detail. Preserve the existing public surface and tests, but update the worker to sort new arrivals and merge into the existing sorted view.

---

## Test Quality Assessment

Tests validate ordering and filtering, but do not cover incremental merge behavior. Once the merge worker is implemented, add at least one test that appends additional samples after an initial sort and verifies the merged view preserves ordering without a full rebuild.

---

## Verdict

**Status:** NEEDS FIXES

**Required Actions:**
1. Replace the full snapshot sort with the incremental merge-sort worker per design.
2. Add a test that verifies merge behavior when new samples arrive after an initial sorted view.

---

**Next Batch:** MON-BATCH-07 (with corrective task 0)
