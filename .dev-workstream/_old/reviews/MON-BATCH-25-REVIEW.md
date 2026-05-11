# Batch 25 Review (Phase 4 UX Polish)

**Status:** APPROVED WITH UX FEEDBACK
**Phase:** Phase 4 Finish / Pre-Phase 7

## Code Review Observations
The structural improvements to Phase 4 were excellent. Exporting using `FullName` safely decoupled us from Assembly name volatility, and removing the allocation tracking (`SizeBytes`) generated cleaner data sets.

The `FileDialog.razor` integration represents a massive leap forward in usability, bridging the server-side host capability straight into the browser elegantly. It completely eradicates the ugly JavaScript prompts. Additionally, the Replay Panel redesign correctly implemented all scrubbing, time-modes, filtering links, and metrics.

Tests are all green (194/194), preserving core stability.

**User Feedback & Adjustments Required Post-Run:**
1. **Replay Local Store Target is Confusing**: The option to replay into the "Local Store (GUI)" is unnecessary since the Replay Panel itself acts as a playback buffer. The primary objective of the Replay tool is to broadcast recorded packets back into the true DDS Network (to simulate a recorded drone/system).
2. **Scrubber Live Updating**: When dragging the track-bar, the numeric readout needs to update instantly, not statically wait for `onmouseup` to compute the drop coordinates.
3. **Next Sample Preview Integration**: When stepping, users need to precisely verify the payload of the sample about to be played. Needs the 'D' json action-preview icon and the detail panel button.
4. **Auto-advance Scrubber during Playback**: The range slider should probably be locked or animate smoothly when playing.
5. **SampleState Tracking**: `SampleInfo.InstanceState` (Alive/Disposed) is silently getting dropped from `ExportService` serialization. This leads to imported files showing everything as Disposed. This needs to be captured.
6. **Alive/Disposed UI Display**: `SamplesPanel` and `DetailPanel` need dedicated textual hovering or explicit color-coded visual indicators for `SampleState`.
7. **Toolbar Uniformity**: The filter string input should live in the top icon toolbar, rather than the bottom. All toolbars need harmonious muted core colors for their action buttons to create a clean, identifiable UX.

In response, I have drafted **BATCH-26**. Phase 5 (Plugins) is delayed slightly while we route these explicit UX fixes through Phase 7 (Polish).
