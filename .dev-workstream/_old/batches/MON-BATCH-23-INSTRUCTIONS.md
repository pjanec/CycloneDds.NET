# BATCH-23: Message Emulator & Form Generation

**Batch Number:** MON-BATCH-23  
**Tasks:** Send Sample panel, Dynamic form generation, Type Drawer registry  
**Phase:** Phase 4 (Operational Tools)  
**Estimated Effort:** 4-6 hours  
**Priority:** NORMAL  
**Dependencies:** MON-BATCH-22

---

## 📋 Onboarding & Workflow

### Developer Instructions
Welcome to Phase 4! The core monitoring UI and stability passes are complete. This phase introduces **Operational Tools**, starting with the ability for users to construct and inject custom simulated payload samples back into the `DdsBridge` to test their monitored systems.

Your job in this batch is to implement the **Send Sample Panel** alongside a dynamic, recursive property editor component capable of reading our `TopicMetadata` schema and generating user-fillable fields.

### Source Code Location
- **Primary Work Area:** `tools/DdsMonitor/` and `tools/DdsMonitor.Engine/`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/MON-BATCH-23-REPORT.md`

---

## 🎯 Batch Objectives

### Task 1: Custom Type Drawer Registry (`DMON-036`)
- **Implement:** A service (`ITypeDrawerRegistry`) that registers Blazor `RenderFragment` builders mapping common .NET types (`int`, `string`, `bool`, `enum`, etc.) to specific UI input elements.
- **Requirement:** Allow for recursive traversal, meaning complex structs should be able to call back into the registry to render their child fields within an indented panel/group.

### Task 2: Dynamic Form Components (`DMON-035`)
- **Implement:** A `<DynamicForm Model="Context" />` Blazor component that iterates the `TopicMetadata` fields and utilizes the `TypeDrawerRegistry` to display an inline editor grid for a target object instance.
- **Requirement:** It must support editing nested structs, properly applying two-way data binding so the underlying object instance updates when the user types in the input fields.

### Task 3: Send Sample Panel (`DMON-034`)
- **Implement:** A new standalone desktop panel component (`SendSamplePanel.razor`).
- **Feature:** Must contain a Topic dropdown/picker allowing the user to select *which* topic they want to simulate.
- **Feature:** Based on the selected topic, instantiate an empty payload object (using `Activator.CreateInstance`) and feed it to the `<DynamicForm>`.
- **Feature:** A `[Send]` button at the bottom of the form that takes the populated payload and publishes it directly to the CycloneDDS network using `DdsBridge.GetWriter()` and `.Write()`.
- **Requirement:** Add a way to open this panel from the main `Windows` menu, and also add an action button in the `Topic Explorer` table to instantly open the Send Sample relative to that specific topic.

---

## 📊 Report Requirements

**✅ What to Answer in Your Report (`MON-BATCH-23-REPORT.md`):**

1. Detail how the dynamic two-way data binding was implemented (e.g., how the typed input components mutate the underlying reflected properties).
2. Explain the handling for deeply nested structs or sequences (arrays) in the visual layout.
3. Show the code path used by the `Send` button to obtain a writer and broadcast.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] Implement robust `ITypeDrawerRegistry` and standard primitive drawer components.
- [ ] Implement recursive `<DynamicForm>` capable of mutating instantiated reflection objects.
- [ ] Provide `SendSamplePanel` with a topic picker and a dynamic payload entry screen.
- [ ] Users can successfully author and inject samples directly into the DDS network.
- [ ] Add thorough unit test coverage mapping complex UI form generation cases.
- [ ] All 116+ existing tests continue to pass.
