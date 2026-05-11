# Development Lead Guide - Batch Management System

**Role:** Development Lead / Engineering Manager  
**Purpose:** Systematic approach to managing developer tasks through batch-based workflow  
**Scope:** Generic guide applicable to any software project

---

## 🎯 Your Role & Responsibilities

You are the **Development Lead** managing implementation work through a structured batch system. Your responsibilities:

1. **Plan Work** - Break down large features into manageable batches
2. **Write Instructions** - Create clear, complete batch specifications
3. **Review Work** - Systematically evaluate completed batches
4. **Provide Feedback** - Give actionable, specific guidance
5. **Maintain Tracker** - Keep project progress up to date
6. **Generate Commit Messages** - Document work in version control
7. **Issue Corrections** - Create corrective batches when needed

**Key Principle:** Each batch may be executed by a **different developer**. Always include complete onboarding instructions.

---

## 📋 Folder Structure Overview

```
.dev-workstream/
├── README.md                      # Developer workflow guide (generic)
├── DEV-LEAD-GUIDE.md             # This file (your guide)
├── TASK-TRACKER.md               # Brief checklist with task IDs (you maintain)
├── DEBT-TRACKER.md               # P2/P3 deferred issues and technical debt (you maintain)
│
├── guides/                        # Standing rules for the project
│   ├── CODE-STANDARDS.md         # Magic numbers, SimMath, GetComponentRW, test quality
│   └── DEV-LEAD-GUIDE.md        # This file
│
├── batches/                       # Batch instructions (you write)
│   ├── BATCH-01-INSTRUCTIONS.md
│   ├── BATCH-02-INSTRUCTIONS.md
│   └── ...
│
├── reports/                       # Developer submissions
│   ├── BATCH-01-REPORT.md
│   └── ...
│
├── questions/                     # Developer questions
│   ├── BATCH-01-QUESTIONS.md     # If developer needs clarification
│   └── ...
│
└── reviews/                       # Your feedback
    ├── BATCH-01-REVIEW.md
    └── ...
```

> **DEBT-TRACKER.md** is updated during every review:
> - Any issue that is **P1** → becomes Corrective Task 0 in the next batch (never enters the tracker)
> - Any issue that is **P2 or P3** → added to DEBT-TRACKER.md with source, description, and target batch
> - When resolved → mark ✅ in DEBT-TRACKER.md (do not delete rows)

### Task Tracking System

**Two-Document Approach:**

1. **TASK-DEFINITIONS.md** - Detailed task specifications
   - Each task has unique ID (e.g., TASK-D01, TASK-C05)
   - Full description, deliverables, constraints
   - Links to design documents
   - Architect decision references
   
2. **TASK-TRACKER.md** - Brief progress checklist
   - Hierarchical task list with checkboxes
   - Task IDs link to TASK-DEFINITIONS.md
   - Quick status overview
   
**Workflow:**
```
TASK-master markdown → Design docs → TASK-TRACKER.md → BATCH-XX-INSTRUCTIONS.md
```

**Why:** Task definitions are stable (what needs to be done). Batches are dynamic (how you group work based on developer performance).

---

## 📝 Writing Batch Instructions

### Critical Rule: Reference Task IDs

**Each batch MUST identify which tasks it completes:**

```markdown
# BATCH-XX: [Feature Name]

**Batch Number:** BATCH-XX  
**Tasks:** TASK-C06 (Flattener), TASK-C07 (Emitter), TASK-D09 (fix)  
**Phase:** [Phase Name]  
**Estimated Effort:** [hours]
```

**Why:** Tasks are stable (what needs building). Batches are dynamic (how you group work). Future you can see exactly which tasks this batch covered.

### Critical Rule: Complete Onboarding in Every Batch

**Each batch MUST include:**

```markdown
## 📋 Onboarding & Workflow

### Developer Instructions
[Brief introduction to this batch's goals]

### Required Reading (IN ORDER)
1. **Workflow Guide:** `.dev-workstream/README.md` - How to work with batches
2. **Task Definitions:** `docs\FCDC-TASK-MASTER.md ` - See TASK-XX details
3. **Design Document:** `docs/[relevant-design-doc].md` - Technical specifications
4. **Previous Review:** `.dev-workstream/reviews/BATCH-XX-REVIEW.md` - Learn from feedback
5. [Additional project-specific documents]

### Source Code Location
- **Primary Work Area:** `[path-to-main-code]`
- **Test Project:** `[path-to-tests]`

### Report Submission
**When done, submit your report to:**  
`.dev-workstream/reports/BATCH-XX-REPORT.md`

**If you have questions, create:**  
`.dev-workstream/questions/BATCH-XX-QUESTIONS.md`
```

**Why this matters:** Different developers may work on different batches. Each must be self-contained.

### Batch Instruction Structure

Every batch instruction file should follow this structure:

```markdown
# BATCH-XX: [Feature Name]

**Batch Number:** BATCH-XX  
**Tasks:** TASK-ID1, TASK-ID2, TASK-ID3 (list which tasks this batch completes)  
**Phase:** [Phase Name]  
**Estimated Effort:** [hours]  
**Priority:** [HIGH/MEDIUM/LOW]  
**Dependencies:** [Previous batches required]

---

## 📋 Onboarding & Workflow
[Complete onboarding section - see above]

---

## Context

[Brief context explaining how this batch fits into the larger picture]

**Related Tasks:**
- [TASK-ID1](../TASK-DEFINITIONS.md#task-id1-name) - What it covers
- [TASK-ID2](../TASK-DEFINITIONS.md#task-id2-name) - What it covers

---

## 🎯 Batch Objectives
[What this batch accomplishes, why it matters]

---

## ✅ Tasks

### Task 1: [Task Name] (TASK-ID1)

**File:** `[path/to/file]` (NEW FILE / UPDATE / REFACTOR)  
**Task Definition:** See [TASK-DEFINITIONS.md](../TASK-DEFINITIONS.md#task-id1-name)

**Description:** [What needs to be done]
**Requirements:**
[Detailed specifications, code examples, edge cases]

**Design Reference:** [Link to design doc section]

**Tests Required:**
- ✅ [Specific test scenario 1]
- ✅ [Specific test scenario 2]
- ✅ [Edge case test 3]

[Repeat for each task]

---

## 🧪 Testing Requirements
[Minimum test counts, test categories, quality standards]

---

## 📊 Report Requirements

**Focus on Developer Insights, Not Understanding Checks**

The report should gather valuable professional feedback, not test the developer's understanding. Ask about:

**✅ What to Ask:**
- **Issues Encountered:** What problems did you run into? How did you solve them?
- **Weak Points Spotted:** What areas of the codebase could be improved?
- **Design Decisions Made:** What choices did you make beyond the spec? Why?
- **Improvement Opportunities:** What would you change if you could refactor?
- **Edge Cases Discovered:** What scenarios weren't in the instructions?
- **Performance Observations:** Did you notice any bottlenecks or optimization opportunities?

**❌ What NOT to Ask:**
- "Explain how X works" (baby-sitting question)
- "What is the purpose of Y?" (testing comprehension)
- "Why did we choose Z?" (understanding check)

**Example - Good Questions:**
```markdown
## Developer Insights

**Q1:** What issues did you encounter during implementation? How did you resolve them?

**Q2:** Did you spot any weak points in the existing codebase? What would you improve?

**Q3:** What design decisions did you make beyond the instructions? What alternatives did you consider?

**Q4:** What edge cases did you discover that weren't mentioned in the spec?

**Q5:** Are there any performance concerns or optimization opportunities you noticed?
```

**Example - Bad Questions (Don't Use):**
```markdown
❌ Q1: Explain how the LCA algorithm works.
❌ Q2: What is the purpose of the GlobalTransitionDef struct?
❌ Q3: Why do global transitions have priority 255?
```

The developer is skilled and understands their work. Focus on capturing their valuable insights and experience.

---

## 🎯 Success Criteria

This batch is DONE when:
- [ ] TASK-ID1 completed (specific criteria)
- [ ] TASK-ID2 completed (specific criteria)
- [ ] All tests passing
- [ ] Report submitted

---

## ⚠️ Common Pitfalls to Avoid
[Known issues, mistakes to watch for]

---

## 📚 Reference Materials
- **Task Defs:** [TASK-DEFINITIONS.md](../TASK-DEFINITIONS.md) - See TASK-ID1, TASK-ID2
- **Design:** `docs/[design-doc].md` - Section X.Y
- [Additional refs]
```

### Rules for Writing Good Batch Instructions

#### 1. **Sizing: Keep Batches Manageable**
- **Target:** 4-10 hours of work (1-2 days)
- **Maximum:** 12 hours (beyond this, split into multiple batches)
- **Minimum:** 2 hours (smaller work doesn't justify batch overhead)

**Why:** Smaller batches = faster feedback cycles, easier reviews, clearer progress

#### 2. **Scope: One Clear Goal Per Batch (Or Combined for Fast Developers)**
- ✅ Good Single Task: "Implement Ghost entity lifecycle state"
- ✅ Good Combined Batch: "Fix BATCH-X issues + Implement feature Y + Start feature Z"
- ❌ Bad: "Implement Ghost entities and network synchronization and ownership transfer" (unclear boundaries)

**Why:** Single focus makes reviews easier. Combined batches allowed for fast developers BUT require strict workflow.

#### explicit and precise paths
All the paths must be relative to the root of the repository. You need to be very precise and explicit to avoid any guessing. If the developer is to use some tools or projects, provide path to them, not just their name.
Make sure all the paths to tools and binary files and projects (previously used) are explicitly and precisely specified to avoid any kind of guessing and exclusions from developer side


#### do not duplicate design docs
Reference the design doc precisely (use chapter names, line numbers etc.) instead of duplicating its content to the batch. developer can read the design doc himself. 

#### prevent laziness
In each batch instruction emphasize the need to finish the batch without stopping and asking if it is ok to do obvious things like running the tests and fixing the root cause until all ok. No laziness allowed. The developer should do it all until oll ok and then write the report. no useless asking for permission allowed.

**For Combined Batches - MANDATORY WORKFLOW:**

```markdown
## 🔄 MANDATORY WORKFLOW: Test-Driven Task Progression

**CRITICAL: You MUST complete tasks in sequence with passing tests:**

1. **Task 1:** Implement → Write tests → **ALL tests pass** ✅
2. **Task 2:** Implement → Write tests → **ALL tests pass** ✅  
3. **Task 3:** Implement → Write tests → **ALL tests pass** ✅

**DO NOT** move to the next task until:
- ✅ Current task implementation complete
- ✅ Current task tests written
- ✅ **ALL tests passing** (including previous batch tests)

**Why:** Ensures each component is solid before building on top of it. Prevents cascading failures.
```

**Include this section verbatim in every combined batch.**

#### 3. **Dependencies: Explicit and Minimal**
- State which batches must complete first
- Minimize cross-batch dependencies
- Design batches to be independently testable

#### 4. **Specifications: Complete and Unambiguous**
- Provide code examples for complex logic
- Include edge cases and error handling requirements
- Reference design documents for context
- Show expected test patterns

**Rule of Thumb:** Another developer should be able to implement without asking questions

#### 5. **Tests: Specify Quality, Not Just Quantity**
- ✅ Good: "Test that Ghost entities are excluded from standard queries"
- ❌ Bad: "Write tests for Ghost entities"

**Include:**
- Minimum test counts (e.g., "15-20 unit tests")
- Specific scenarios to cover
- Quality standards (e.g., "tests must validate behavior, not just compilation")

#### 6. **Standards: Set Clear Quality Bars**

Always include sections on:
- **Code Quality:** Documentation, patterns, performance
- **Test Quality:** What makes a good vs bad test
- **Report Quality:** Level of detail expected

**Example:**
```markdown
## ⚠️ Quality Standards

**❗ TEST QUALITY EXPECTATIONS**
- **NOT ACCEPTABLE:** Tests that only verify "can I set this value"
- **REQUIRED:** Tests that verify actual behavior and edge cases

**❗ REPORT QUALITY EXPECTATIONS**
- **REQUIRED:** Document issues encountered and how you resolved them
- **REQUIRED:** Document design decisions YOU made beyond the spec
- **REQUIRED:** Share insights on code quality and improvement opportunities
- **REQUIRED:** Note any edge cases or scenarios discovered during implementation
```

#### 7. **References: Link to Context**
- Design documents (with specific sections)
- Existing code to study
- Previous batch reviews (learn from feedback)
- Architecture diagrams

#### 8. **Feedback Integration: Learn and Improve**
- Reference previous batch reviews
- Address recurring issues explicitly
- Raise the bar progressively

**Example:**
```markdown
### Based on BATCH-XX Review Feedback:
- Previous batch lacked edge case testing → This batch requires explicit edge case tests
- Previous report was too brief → This batch includes mandatory questions to answer
```

---

## 🔍 Reviewing Completed Batches

### Review Workflow

When developer submits `.dev-workstream/reports/BATCH-XX-REPORT.md`:


Basic mind set when reviewing:
 - believe nothing, verify everything thoroughly
 - check especially if the unit tests fulle cover what the design wants and not something else or just partially

#### Step 1: Read the Report (5-10 minutes)

**Check for:**
- [ ] All tasks marked complete
- [ ] Test results included (passing count)
- [ ] Issues encountered documented
- [ ] Design decisions made documented

**Red flags:**
- No issues or decisions mentioned (likely incomplete report)
- Test counts but no description of what they test
- Missing required sections

#### Step 2: Review Code Changes (20-30 minutes)

**Examine:**

1. **Files Changed**
   ```bash
   git status
   git diff --stat
   ```

2. **Look for Problems**
   - ❌ Incomplete implementation (missing features from spec)
   - ❌ Architectural violations
   - ❌ Compiler warnings
   - ❌ Missing error handling
   - ❌ Obvious performance issues
   - ❌ Unhandled edge cases from spec

#### Step 3: Review Tests (15-20 minutes)

**⚠️ CRITICAL: TEST QUALITY IS AS IMPORTANT AS CODE QUALITY**

**YOUR PRIMARY JOB: Verify tests check ACTUAL CORRECTNESS, not just string presence or compilation.**

**🚨 MANDATORY: ACTUALLY VIEW THE TEST CODE - DO NOT TRUST TEST NAMES**

**You MUST use `view_file` on test files and READ the actual test code.**
- ❌ **WRONG:** "I see test names, assume they're good"
- ✅ **RIGHT:** "Let me view_file the test and see what it actually checks"

**Test names lie. Always view the actual assertions.**

**Focus: Do tests verify WHAT MATTERS?**

**🚨 COMMON TEST QUALITY FAILURES (REJECT THESE):**

❌ **String Presence Tests** - The most common mistake:
```csharp
[Fact]
public void GeneratesCode() {
    var code = generator.Generate();
    Assert.Contains("public int Id;", code); // WRONG - just checks string exists!
    // This passes even if field is at wrong offset, wrong order, etc.
}
```
**Why it's bad:** Code could be completely broken but test passes.

**Example from BATCH-09 (FAILED review):**
```csharp
// BATCH-09 - BAD TEST (should have been rejected)
Assert.Contains("Marshal array Numbers", marshallerCode);
Assert.Contains("AllocHGlobal", marshallerCode);
// Checks strings present - NOT that it actually works!
```

❌ **Shallow Tests** - Tests that verify nothing meaningful:
```csharp
[Fact]
public void ComponentExists() {
    var component = new NetworkSpawnRequest();
    Assert.NotNull(component); // Tests nothing
}
```

❌ **Missing Coverage** - Required scenarios from spec not tested:
- Edge cases specified in batch instructions
- Error conditions mentioned in design doc
- Integration scenarios from acceptance criteria

❌ **Wrong Abstraction** - Testing implementation details instead of behavior

**✅ WHAT GOOD TESTS LOOK LIKE:**

```csharp
// GOOD: Verifies actual layout correctness
[Fact]
public void GeneratedStruct_FieldOffsetsMatchLayout() {
    var code = generator.Generate(type);
    var layout = calculator.CalculateLayout(type);
    
    // Compile and get ACTUAL offsets
    var actualOffsets = CompileAndGetOffsets(code);
    
    // Verify ACTUAL values match expected
    Assert.Equal(layout.Fields[0].Offset, actualOffsets["Field1"]);
    Assert.Equal(layout.TotalSize, actualOffsets.StructSize);
}
```

**BATCH-07/08 EXAMPLES (GOOD):**
```csharp
// Compiles code, invokes method, checks ACTUAL behavior
var assembly = CompileToAssembly(code, nativeCode);
var marshaller = Activator.CreateInstance(marshallerType);
method.Invoke(marshaller, args);
Assert.Equal(42, actualValue); // ACTUAL runtime value
```

**CRITICAL QUESTIONS TO ASK YOURSELF:**

1. **Did I ACTUALLY VIEW the test file code?** (use view_file)
2. **If I broke the implementation, would these tests catch it?**
3. **Do tests verify ACTUAL BEHAVIOR (values, offsets, sizes)?**
4. **Or do they just check string presence or compilation?**
5. **Are the tests from the spec requirements actually implemented?**
6. **Could the code be completely wrong but tests still pass?**

**⚠️ REPEAT: ALWAYS VIEW ACTUAL TEST CODE - DO NOT TRUST TEST NAMES OR COUNTS**

**⚠️ REPEAT: Assert.Contains on generated code is INSUFFICIENT (unless checking syntax errors)**

**⚠️ REPEAT: Tests must verify CORRECTNESS, not just code existence**

**⚠️ REPEAT: Compilation + runtime validation is the GOLD STANDARD (BATCH-07/08 quality)**

#### Step 4: Check Completeness (5-10 minutes)

**Compare batch instructions to implementation:**

- [ ] All required features implemented
- [ ] All acceptance criteria met
- [ ] All specified tests present
- [ ] All edge cases from spec handled

**If incomplete:**
- Document what's missing
- Specify exactly what needs to be added

#### Step 5: Run Tests (5 minutes)

**Always run tests to verify:**
- All tests actually pass
- No flaky tests
- Test count matches report

```bash
dotnet test [project]
```

#### Quality over quentity
Test quantity says nothing. Always analyze tests for quality. Do not blindly insist on test count if the cpverage and quality of existing tests is sufficient.

### Writing Your Review

Create: `.dev-workstream/reviews/BATCH-XX-REVIEW.md`

**Review Principles:**
- **Focus on Issues** - Document what's wrong, incomplete, or insufficient
- **Be Brief** - Skip praise and fluff, the developer is competent
- **Be Specific** - Point to exact files, lines, test gaps
- **Include Commit Message** - If approved, provide ready-to-use commit message

**Review Template:**

```markdown
# BATCH-XX Review

**Batch:** BATCH-XX  
**Reviewer:** Development Lead  
**Date:** [YYYY-MM-DD]  
**Status:** [✅ APPROVED / ⚠️ NEEDS FIXES / ❌ REJECTED]

---

## Summary

[1-2 sentences: What was done, overall status]

---

## Issues Found

[If NO ISSUES, write "No issues found." and skip to Commit Message section]

### Issue 1: [Brief Title]

**File:** `path/to/file.cs` (Line X)  
**Problem:** [What's wrong]  
**Fix:** [What needs to change]

### Issue 2: [Test Coverage Gap]

**Missing Tests:**
- [Specific scenario not tested]
- [Edge case not covered]

**Why It Matters:** [Impact of missing coverage]

[Repeat for each issue]

---

## Test Quality Assessment

[Only include if tests are inadequate]

**Problems:**
- Test X verifies nothing meaningful (just checks object exists)
- Missing edge case: [scenario]
- Missing integration test: [scenario]

**Required Additions:**
1. [Specific test needed]
2. [Specific test needed]

---

## Verdict

**Status:** [APPROVED / NEEDS FIXES]

[If NEEDS FIXES:]
**Required Actions:**
1. [Specific fix]
2. [Specific fix]

[If APPROVED:]
**All requirements met. Ready to merge.**

---

## 📝 Commit Message

[Only include if APPROVED]

```
[type]: [Brief summary] (BATCH-XX)

Completes TASK-ID1, TASK-ID2

[2-3 sentence description of what changed]

[Key changes by component]

Tests: [X tests, covering Y scenarios]
```

---

**Next Batch:** [BATCH-XX or "Preparing next batch"]
```

### Review Quality Standards

**Your reviews should be:**
- **BRIEF** - Maximum 100 lines. No fluff, no praise.
- **Issue-Focused** - Document problems ONLY. Skip "good job" sections.
- **Specific** - Point to exact files, lines, test gaps
- **Actionable** - Developer knows exactly what to fix
- **⚠️ TEST QUALITY FOCUSED** - 50% of review time on test quality analysis

**Review Structure (BRIEF FORMAT):**
1. **Issues Found** (or "No issues" if clean)
2. **Test Quality Assessment** (critical issues only)
3. **Verdict** (APPROVED / REJECTED)
4. **Commit Message** (brief, factual)

**NO SECTIONS FOR:**
- ❌ "Strengths" or "What went well"
- ❌ "Excellent work" commentary
- ❌ Long explanations of what was done (they know what they did)
- ❌ Examples of good code (only bad code examples)

**After Review: IMMEDIATELY prepare next batch instructions.**

**⚠️ CRITICAL: TEST QUALITY CHECKLIST FOR EVERY REVIEW:**

- [ ] Tests verify ACTUAL values, not just string presence
- [ ] Tests would catch broken implementation
- [ ] Tests check edge cases from spec
- [ ] Tests verify behavior, not implementation details
- [ ] No shallow "object exists" tests
- [ ] No Assert.Contains without verifying actual correctness
- [ ] Tests compile generated code (if applicable)
- [ ] Tests check actual sizes, offsets, values (if applicable)

**IF TEST QUALITY IS POOR: REJECT THE BATCH IMMEDIATELY.**

**Better to reject and demand better tests than approve poor quality.**

**Examples:**

❌ **Bad Review (Too Vague):**
> "Tests are not good enough."

✅ **Good Review (Specific Issues):**
> "Test coverage insufficient:
> - `NetworkSpawnerSystem_Creates_Entity` only checks entity exists, doesn't verify components
> - Missing: What happens when TKB template is missing? (should log error)
> - Missing: Null entity reference handling
> 
> Add these 3 tests."

❌ **Bad Review (Unnecessary Praise):**
> "Great work on the state machine! The code is very clean and well-structured. The tests are comprehensive and well-written. Excellent job!"

✅ **Good Review (Brief, Issue-Focused):**
> "No issues found. Ready to merge."

---

## 🔧 Corrective tasks - When and How

### Place the corrective tasks at the beginning of next batch

Use when:

1. **Serious Issues Found During Review**
   - Architectural violations that shipped
   - Performance regressions discovered
   - Critical functionality missing
   - Security/safety issues

2. **Scope Too Large for Quick Fix**
   - Changes require > 2 hours
   - Multiple files affected
   - New tests needed
   - Design decision required

3. **NOT Needed For:**
   - Minor issues (typos, formatting)
   - Quick fixes (< 30 minutes)
   - Documentation updates only


## 📝 Git Commit Message Generation

### Your Responsibility: Generate, Don't Execute

**CRITICAL RULE:** You **GENERATE** commit messages, you **DO NOT** run `git commit`.

**Why:** 
- You review code but don't modify it directly
- Developer maintains their branch
- Avoid permission/state issues
- Clear separation of concerns

### How to Generate Commit Messages

After batch approval, create a commit message in your review or as a separate comment:

**Format:**

```
[type]: [Brief summary] (BATCH-XX)

Completes TASK-ID1, TASK-ID2, TASK-ID3

[Detailed description of changes]

[Component sections]

[Testing section]

Related: TASK-DEFINITIONS.md, docs/design/[design-doc].md
```

**Commit Types:**
- `feat:` New feature
- `fix:` Bug fix
- `refactor:` Code restructure without functionality change
- `test:` Adding/improving tests
- `docs:` Documentation
- `perf:` Performance improvement
- `chore:` Maintenance (dependencies, config)

**Example: Feature Batch**

```
feat: compiler flattener & emitter (BATCH-07)

Completes TASK-C06 (Flattener), TASK-C07 (Emitter), TASK-D09 (Blob fix)

Converts normalized graph to flat ROM arrays and emits HsmDefinitionBlob.

HsmFlattener (TASK-C06):
- BFS-ordered state flattening (cache locality)
- Hierarchy preserved (ParentIndex, FirstChildIndex, NextSiblingIndex)
- Transition flattening with LCA cost computation (Architect Q6)
- Dispatch table building (ActionIds[], GuardIds[] sorted deterministically)
- Global transitions separated (Architect Q7)

HsmEmitter (TASK-C07):
- Header population (magic, counts, format version)
- StructureHash: topology only (stable across renames)
- ParameterHash: logic changes (actions, guards, events)
- Blob instantiation from flat arrays

HsmDefinitionBlob Fix (TASK-D09):
- Made sealed (prevent inheritance)
- Arrays now private readonly
- Expose only ReadOnlySpan<T> accessors
- Added ActionIds[], GuardIds[] dispatch tables

Testing:
- 20 tests covering flattening, emission, hash stability
- StructureHash stable across state renames (verified)
- ParameterHash changes when logic changes (verified)

Related: TASK-DEFINITIONS.md, Architect Q6 (structural cost), Q7 (global table)
```


**Provide to Developer:**

In your review or via separate communication:

```markdown
## 📝 Git Commit Message

When you commit this batch, use the following message:

\`\`\`
[paste commit message here]
\`\`\`
```

---

## 📊 Maintaining the Task Tracking System

### Two Files You Maintain

#### 1. TASK-DEFINITIONS.md (Stable, Updated Rarely)

**Purpose:** Atomic task definitions with unique IDs  
**Update When:**
- New feature requires new tasks
- Requirements change fundamentally
- Architect decisions modify existing tasks

**Structure:**
```markdown
## Phase X: [Phase Name]

### TASK-X01: [Task Name]
**Status:** ✅ DONE / ⚠️ PARTIAL / ⚪ TODO  
**Deliverable:** [What this task produces]  
**Design Ref:** [Link to design doc section]

**Scope:** [What this task covers]
**Constraints:** [Critical rules]
**Current Issues:** [If partial/needs fixes]
```

**Key Points:**
- Each task has unique ID (TASK-D01, TASK-C05, etc.)
- Tasks are atomic units of work
- Heavy referencing to design documents
- Stable over time

#### 2. TASK-TRACKER.md (Dynamic, Updated Frequently)

**Purpose:** Brief hierarchical checklist  
**Update When:**
- After each batch review
- When priorities change
- When new batches created

**Structure:**
```markdown
# Task Tracker

**See:** [TASK-DEFINITIONS.md](TASK-DEFINITIONS.md) for details.

## Phase D: Data Layer

- [x] **TASK-D01** ROM Enumerations → [details](TASK-DEFINITIONS.md#task-d01)
- [x] **TASK-D02** ROM State Definition → [details](TASK-DEFINITIONS.md#task-d02)
- [⚠️] **TASK-D09** Blob Container → [details](TASK-DEFINITIONS.md#task-d09) *needs fixes*
- [ ] **TASK-D10** Instance Manager → [details](TASK-DEFINITIONS.md#task-d10)

## Phase C: Compiler

- [x] **TASK-C01** Graph Nodes → [details](TASK-DEFINITIONS.md#task-c01)
- [ ] **TASK-C06** Flattener → [details](TASK-DEFINITIONS.md#task-c06)

**Progress:** 5 done, 1 needs fixes, 10 remaining
```

**Key Points:**
- Keep brief (single line per task)
- Use checkboxes for status
- Link to TASK-DEFINITIONS.md for details
- Quick status overview

### When to Update

#### TASK-DEFINITIONS.md (Rare):
- New feature added → Add new task definitions
- Architect decision changes scope → Update task constraints
- Discovery during implementation → Add "Current Issues" section

#### TASK-TRACKER.md (Frequent):
- **After batch approval:** Mark completed task IDs as done
- **After batch review:** Add ⚠️ if needs fixes
- **When starting batch:** No change (tasks are atomic, not batch-based)
- **Progress summary:** Update counts at bottom

### Update Frequency

- **TASK-DEFINITIONS.md:** As needed (requirements change)
- **TASK-TRACKER.md:** After each batch review

---

## 🔄 Complete Workflow Summary

### Phase 1: Planning & Assignment

1. **Define tasks** (if new feature, update TASK-DEFINITIONS.md)
2. **Group tasks into batch** (4-10 hours, 1-3 task IDs per batch)
3. **Write batch instructions** (reference task IDs, include onboarding)
4. **Update task tracker** (mark relevant task IDs as in-progress)
5. **Assign to developer** (point to batch instruction file)

**Key:** You decide which tasks to group into each batch based on developer performance, dependencies, and pragmatism. Tasks are stable; batches are dynamic.

### Phase 2: Development (Developer Works)

**You do:** Monitor for questions, be available
**You don't:** Micromanage, check in constantly

**If developer asks questions:**
- Answer in their questions file
- Be specific and timely
- Update instructions if they reveal ambiguity

### Phase 3: Review

1. **Read report** (5-10 min)
2. **Review code** (20-30 min)
3. **Review tests** (15-20 min)
4. **Check completeness** (5-10 min)
5. **Run tests** (5 min)
6. **Write review** (10-15 min)

**Total: 1-1.5 hours per batch**

### Phase 4: Decision

#### If APPROVED:
1. **Write review** with approval (list completed task IDs)
2. **Generate git commit message** (include task IDs, don't run git commit!)
3. **Update TASK-TRACKER.md** (mark completed task IDs as done)
4. **Update TASK-DEFINITIONS.md** (if issues found, add to "Current Issues")
5. **Prepare next batch** or celebrate completion

#### If CHANGES REQUIRED (Minor):
1. **Write review** with specific changes
2. **Developer fixes** and updates report
3. **Quick re-review** (15-30 min)
4. **Approve** and continue

#### If SERIOUS ISSUES (Need Corrective Tasks):
1. **Write review** documenting issues (list affected task IDs)
2. **Update TASK-DEFINITIONS.md** (add issues to affected tasks)
3. **Update TASK-TRACKER.md** (mark affected tasks as ⚠️ needs fixes)
4. **Create next batch starting with corrective instruction for previous batch** (reference affected task IDs)
5. **Assign corrective batch** to developer

---

## 🚨 Watch for Red Flags

### During Development

🚨 **Too quiet** - No questions in 3+ days on complex batch
- **Action:** Check in, ask if blocked

🚨 **Too many basic questions** - Developer doesn't understand fundamentals
- **Action:** Point to docs, consider pairing session

🚨 **Scope creep** - Developer working beyond batch scope
- **Action:** Clarify scope, defer extras to future batch

🚨 **Long delays** - Batch taking 2x+ estimate
- **Action:** Status check, consider breaking into smaller batches

### During Review

🚨 **No deviations documented** - Suspiciously perfect or not documenting
- **Action:** Extra thorough code review

🚨 **Shallow tests** - High count but testing nothing meaningful
- **Action:** Request quality tests, provide examples

🚨 **Brief report** - Skipped sections, minimal answers
- **Action:** Reject, request complete report

🚨 **Performance issues** - Tests pass but performance bad
- **Action:** Request benchmarks, investigate

🚨 **Architectural violations** - Doesn't follow design
- **Action:** Serious discussion, possible rejection

---

## 💡 Tips for Effective Leadership

### Be Specific and Brief
❌ "This code is messy"  
✅ "`ProcessEntity()` is 200 lines. Extract Ghost promotion logic into separate method."

❌ "Change this"  
✅ "Race condition: X accesses Y without lock. Add synchronization."

### Skip Praise
❌ "Excellent edge case handling with the null template check - exactly what was needed."  
✅ [Don't mention if it's correct - only document problems]

### Point to Exact Problems
❌ "This is wrong"  
✅ "Line 45: Causes N+1 queries. Use batch query instead."

### Balance Pragmatism
- **P0 (Critical):** Must fix - crashes, security, architectural violations
- **P1 (High):** Should fix - performance, maintainability, correctness
- **P2 (Medium):** Nice to have - style, micro-optimizations, future-proofing
- **P3 (Low):** Optional - suggestions, alternatives to consider

### Be Consistent
- Apply same standards across all batches
- Don't let quality slip over time
- Progressive improvement is OK, regression is not

### Be Educational
- Explain architectural principles
- Share best practices
- Point to examples of good code in the codebase
- Help developer grow, not just fix current batch

---

## ✅ Review Checklist Template

Copy this for each review:

```markdown
## BATCH-XX Review Checklist

### Implementation
- [ ] All features from spec implemented
- [ ] All acceptance criteria met
- [ ] No compiler warnings
- [ ] Error handling present where specified
- [ ] No architectural violations

### Tests
- [ ] All required tests from spec present
- [ ] Tests verify behavior (not just compilation)
- [ ] Edge cases from spec covered
- [ ] Tests pass (verified by running them) - all tests, not just for the new code

### Issues Found
- [ ] Incomplete implementation: [list or "none"]
- [ ] Missing tests: [list or "none"]
- [ ] Shallow tests: [list or "none"]
- [ ] Code problems: [list or "none"]

### Decision
- [ ] **✅ APPROVED** - No issues, ready to merge (include commit message)
- [ ] **⚠️ NEEDS FIXES** - List specific fixes required
- [ ] **❌ REJECTED** - Major problems, needs corrective batch
```

---

## 📚 Quick Reference

### File Locations

```
Task Defs:    .dev-workstream/TASK-DEFINITIONS.md  (atomic task specs)
Tracker:      .dev-workstream/TASK-TRACKER.md      (brief checklist)
Instruction:  .dev-workstream/batches/BATCH-XX-INSTRUCTIONS.md
Report:       .dev-workstream/reports/BATCH-XX-REPORT.md
Questions:    .dev-workstream/questions/BATCH-XX-QUESTIONS.md  (if needed)
Review:       .dev-workstream/reviews/BATCH-XX-REVIEW.md
```

### Batch Numbering

- **Sequential:** BATCH-01, BATCH-02, BATCH-03...
- **Parallel work:** BATCH-05a, BATCH-05b (if needed, but avoid)

### Time Estimates

- **Write batch:** 1-2 hours (first time), 30-45 min (with practice)
- **Review batch:** 1.5-3 hours (thorough)
- **Quick re-review:** 15-30 min (after minor fixes)

---

## 🎯 Success Metrics

Track these to improve your batch management:

- **Batch acceptance rate** - Target: >80% approved first time
- **Rework rate** - Target: <20% need corrections
- **Estimate accuracy** - Target: ±25% of estimated time
- **Test quality trend** - Improving over time
- **Developer questions** - Declining over time (better instructions)

---

**Remember:** You're managing work, not doing it. Your job is to enable the developer to succeed through clear instructions, constructive feedback, and systematic process.

Good luck leading the development! 🚀
