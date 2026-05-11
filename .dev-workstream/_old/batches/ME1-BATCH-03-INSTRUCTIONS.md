# ME1-BATCH-03: UI Enhancements & Headless Lifecycle

**Batch Number:** ME1-BATCH-03
**Tasks:** ME1-T08, ME1-T09, ME1-T10, ME1-T11
**Phase:** Phase 4 & 5 — DDS Monitor UI and Lifecycle / Headless Mode
**Estimated Effort:** 10-12 hours
**Priority:** HIGH
**Dependencies:** ME1-BATCH-02 (Completed)

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to ME1-BATCH-03! You are stepping into the final leg of the Monitoring Extensions 1 workstream. Your focus shifts heavily to the presentation layer (Blazor UI) and application execution environments (Headless mode, Auto-browser launch). You will bring the underlying multi-participant and telemetry infrastructure built in the last batch directly to the user's fingertips, and then provide a robust mechanism for pipeline automation without a UI at all.

**CRITICAL:** Be completely autonomous. If you encounter missing configuration or errors, do **not** stop and ask for permission. Fix the root cause, write necessary tests, ensure tests pass, and proceed. Please provide a full account of your actions in the final report.

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/guides/DEV-GUIDE.md` - How to work with batches
2. **Onboarding:** `docs/mon-ext-1/ME1-ONBOARDING.md`
3. **Design Document:** `docs/mon-ext-1/ME1-DESIGN.md` - Read Phases 4 & 5
4. **Task Definitions:** `docs/mon-ext-1/ME1-TASK-DETAILS.md` - Review ME1-T08 to ME1-T11
5. **Previous Review:** `.dev-workstream/reviews/ME1-BATCH-02-REVIEW.md`

### Source Code Location
- **Main Client:** `tools/DdsMonitor/DdsMonitor.Blazor/`
- **Application Host:** `tools/DdsMonitor/DdsMonitor.App/`
- **Engine Layer:** `tools/DdsMonitor/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/ME1-BATCH-03-REPORT.md`

---

## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1 (ME1-T08):** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2 (ME1-T09):** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3 (ME1-T10):** Implement → Write tests → **ALL tests pass** ✅  
4. **Task 4 (ME1-T11):** Implement → Write tests → **ALL tests pass** ✅  

**DO NOT** move to the next task until the current one is entirely complete and tests pass cleanly locally.

---

## ✅ Tasks

### Task 1: Union Arm Visibility (ME1-T08)
**Files:**
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/DetailPanel.razor`
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/DataInspectorNode.razor`

**Description:**
Unions currently render all generated properties (e.g., `ValueAsFloat`, `ValueAsInt`, `ValueAsBool`), cluttering the UI significantly. Evaluate the structure properties and use reflection or generated markers to only render the active union arm property based on the active `Discriminator`.

### Task 2: Start/Pause/Reset Toolbar + Participant Editor (ME1-T09)
**Files:**
- `tools/DdsMonitor/DdsMonitor.Blazor/Layout/MainLayout.razor` (or a dedicated Toolbar component)
- `tools/DdsMonitor/DdsMonitor.Blazor/Components/ParticipantEditor.razor` (New)
- `tools/DdsMonitor/DdsMonitor.Engine/IDdsBridge.cs`

**Description:**
Implement a visual playback toolbar mapping directly to `DdsBridge.IsPaused` and `DdsBridge.ResetAll()`. Additionally, build a dropdown/modal editor utilizing the newly exposed participants list to intuitively add/remove concurrent networks on the fly (restarting ingestion if changed). 

### Task 3: Auto-Browser Open + HTTP-Only Lifecycle (ME1-T10)
**Files:**
- `tools/DdsMonitor/DdsMonitor.App/Program.cs`
- `tools/DdsMonitor/DdsMonitor.App/BrowserOpener.cs` (New)

**Description:**
Since the app hosts Blazor Server natively, we want it to launch the default system browser upon successful boot. Additionally, rip out any ASP.NET generic HTTPS redirection and rely entirely on `http://localhost:<random-port>` explicitly. This prevents edge-case environment certificate errors on local debug machines.

### Task 4: Headless Recorder / Replay Mode (ME1-T11)
**Files:**
- `tools/DdsMonitor/DdsMonitor.App/Program.cs`
- `tools/DdsMonitor/DdsMonitor.Engine/HeadlessRunner.cs` (New)

**Description:**
Wire in CLI arguments `--headless`, `--record-to <file>`, and `--replay-from <file>`. When supplied, bypass the ASP.NET Kestrel web-host startup entirely, and instead loop through the `ExportService` or `ImportService` on the main thread, tracking ordinals to standard out.

---

## 📊 Report Requirements

Create `.dev-workstream/reports/ME1-BATCH-03-REPORT.md` and explicitly include:

## Developer Insights

**Q1:** For Union Arm Visibility, did you rely heavily on Reflection at runtime in the Blazor view, or did you augment `SchemaDiscovery` to provide cleaner UI metadata?
**Q2:** When modifying the ASP.NET pipeline for the Auto-Browser open, where did you hook the launch command to ensure Kestrel was genuinely listening before the browser attempted a GET request?
**Q3:** During headless mode development, did you discover any dependencies in the `Engine` tier that accidentally relied on Blazor/Web components (IJSRuntime, HttpContext)?

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Task ME1-T08 completed.
- [ ] Task ME1-T09 completed.
- [ ] Task ME1-T10 completed.
- [ ] Task ME1-T11 completed.
- [ ] NO test regressions or warnings.
- [ ] Build compiles via CLI and launches effectively.
- [ ] Developer Report submitted including responses to Developer Insights.
