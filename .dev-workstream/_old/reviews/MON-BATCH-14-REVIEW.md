# MON-BATCH-14 Review

**Batch:** MON-BATCH-14  
**Reviewer:** Development Lead  
**Date:** 2026-03-01  
**Status:** ?? NEEDS FIXES

---

## Summary

Core fixes and tests were delivered, but required manual verification for DMON-017/024/025/026 remains incomplete, and several UX issues were reported during manual runs.

---

## Issues Found

### Issue 1: Manual verification incomplete (DMON-017/024/025/026)

**File:** `.dev-workstream/reports/MON-BATCH-14-REPORT.md`  
**Problem:** Manual checks for desktop, text view panel, keyboard navigation, and context menu are still pending.  
**Fix:** Run the manual checklist and document pass/fail results in the report.

### Issue 2: Keyboard navigation loses scroll sync after Page Up/Down

**File:** `tools/DdsMonitor/Components/SamplesPanel.razor`  
**Problem:** After Page Up/Down, arrow navigation moves selection out of the visible viewport while the scroll position remains unchanged.  
**Fix:** Ensure selection changes after Page Up/Down keep the selected row within the virtualized viewport (scroll-to-index on selection change).

### Issue 3: Detail panel open latency is excessive

**File:** `tools/DdsMonitor/Components/DetailPanel.razor`  
**Problem:** Opening detail via double-click/Enter shows high latency; debounce appears overly aggressive for interactive use.  
**Fix:** Reduce debounce delay for opening detail or separate selection debounce from panel open command.

### Issue 4: Topics panel cannot be reopened after close

**File:** `tools/DdsMonitor/Components/Desktop.razor`, `tools/DdsMonitor/Components/Layout/MainLayout.razor`  
**Problem:** After closing Topics panel, there is no UI control to reopen it.  
**Fix:** Add a menu/toolbar/launcher to spawn Topics panel (uses `IWindowManager.SpawnPanel`).

### Issue 5: Context menu does not appear on right click

**File:** `tools/DdsMonitor/Components/ContextMenu.razor`, `tools/DdsMonitor/Components/SamplesPanel.razor`, `tools/DdsMonitor/Components/DetailPanel.razor`  
**Problem:** Browser default context menu appears; app context menu not shown.  
**Fix:** Ensure `oncontextmenu:preventDefault` is used and that the context menu service renders a portal reliably.

---

## Verdict

**Status:** NEEDS FIXES

**Required Actions:**
1. Complete and document DMON-017/024/025/026 manual checks.
2. Fix keyboard navigation scroll sync after Page Up/Down.
3. Reduce/adjust detail panel open latency.
4. Add a Topics panel launcher to reopen after close.
5. Fix context menu rendering on right-click.

---

**Next Batch:** MON-BATCH-15 (corrective UX + manual verification)
