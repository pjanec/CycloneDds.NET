# MON-BATCH-21 Review

**Batch:** MON-BATCH-21  
**Reviewer:** Development Lead  
**Date:** 2026-03-05  
**Status:** ❌ FAILED (Needs Rework)

---

## Summary

The developer executed the first 7 tasks admirably, getting the `FilterCompiler` lazy metadata handling and the panel persistence correct. 

**However, the developer completely ignored Tasks 8, 9, and 10 which were explicitly appended to the batch.** Furthermore, Task 5 (Checkbox Sync) is still failing during manual verification. 

---

## Unresolved Issues (Carried to BATCH-22)

1. **Task 5 (Checkbox Sync):** The user reports that checkboxes in the Topics window are *still* not visually checked despite DDS actively receiving messages for those topics. There is a deep desync here—either the `DdsBridge.ActiveReaders` does not reflect the auto-subscribed startup state, or the Blazor one-way bindings (`checked="@topic.IsSubscribed"`) are failing to update the DOM correctly when state changes.
2. **Task 8 (String Methods):** The `FilterCompiler` crash on `.Contains()`, `.StartsWith()`, etc., remains untouched.
3. **Task 9 (Timestamp Property):** Synthetic fields like `Timestamp` still throw `Unknown payload field` errors.
4. **Task 10 (Subscribe All Graceful Degradation):** Topics without descriptors still trigger loud red error toasts when clicking "Subscribe All".

---

## Verdict

**Status:** FAILED

**Batch 21 missed key requirements. A strict cleanup batch (MON-BATCH-22) is being assembled immediately to force resolution of the dropped bug tasks.**
