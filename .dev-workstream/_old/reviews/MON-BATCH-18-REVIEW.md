# MON-BATCH-18 Review

**Batch:** MON-BATCH-18  
**Reviewer:** Development Lead  
**Date:** 2026-03-04  
**Status:** ❌ FAILED (Needs Rework)

---

## Summary

The developer failed to correctly implement multiple critical corrective tasks and introduced duplicated code instead of adhering to the DRY principle. The overall quality of the UI implementation and bug fixing was entirely unsatisfactory, largely ignoring the manual verification instructions provided in the batch.

---

## Issues Found

1. **DRY Violation (AllSamplesPanel):** The developer created an entirely new `AllSamplesPanel.razor` instead of adapting the existing `SamplesPanel.razor` to handle an "All Topics" mode. This resulted in the new panel missing crucial features like the "action" column (the "D" icon for hover details) and the standard toolbar buttons. This must be refactored into a single versatile `SamplesPanel` component.
2. **Context Menu Bug:** The context menu fix completely failed. It still closes immediately on the first mouse release if the cursor is not inside the menu rectangle.
3. **Window State Memory (Samples and Filter Builders):** 
   - Samples windows opened via double-click on a topic still do not recall their last position/size (they ignore the requested indexed-memory system).
   - The generic "All samples" window was not assigned index `0` to persist its layout.
   - Filter windows do not remember their location and open at default sizes. They must remember their bounds based on their parent samples window name/index.
4. **Toolbar Aesthetics:** The toolbars for panels still use cheap text buttons. They were strictly requested to use nicely colored graphical icons with tooltips.
5. **Topic Filter Bar UI:** The topics panel filter bar is too bulky, taking up too much space. It needs a smaller font and a visual styling distinct from normal buttons.
6. **Filter Builder Input Bug:** When adding a new condition in the Filter Builder, the field name is hardcoded to pre-fill with the string `"id"`, which actively prevents the user from selecting from the dropdown list of fields.

---

## Verdict

**Status:** FAILED

**Batch 18 requirements were not met. A heavy rework batch (MON-BATCH-19) has been issued prioritizing the resolution of all missed items.**
