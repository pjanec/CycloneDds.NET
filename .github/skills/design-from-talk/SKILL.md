---
name: design-from-talk
description: Convert a design talk document (conversation transcript, raw notes, or brainstorming doc) into formal project planning artifacts. Use when given a design talk and asked to create structured planning/design files, produce task breakdowns, or bootstrap a development workstream. Produces DESIGN.md (phased design with tasks), TASK-DETAIL.md (per-task specs with unique IDs and success conditions), TASK-TRACKER.md (brief progress checklist), and ONBOARDING.md (newcomer guide).
---

# Design From Talk

Transform a design talk document into four actionable project planning artifacts.

## Critical: Read the Entire Design Talk First

**Read the design talk document completely — to the last line — before producing any output.**

Design talks are cumulative: early ideas get refined, rejected, or superseded. The final concept only
emerges at the end. Stopping early produces incorrect artifacts based on discarded ideas.

Also inspect the current implementation in areas the design talk refers to (refactoring targets,
components being replaced, interfaces being extended) before writing any design docs.

---

## Step 1: DESIGN.md

Create `DESIGN.md` capturing all final ideas from the design talk.

- Break the design into numbered implementation phases (e.g., Phase 1: Foundation, Phase 2: Core)
- Each phase contains named tasks
- Cover WHAT and WHY; leave HOW to TASK-DETAIL.md
- Record architectural decisions and constraints
- Do NOT duplicate content that will be in TASK-DETAIL.md — reference sections by name

---

## Step 2: TASK-DETAIL.md

Create `TASK-DETAIL.md` with a complete description of every task.

**Required per task:**

| Field | Requirement |
|---|---|
| Unique ID | e.g. `TASK-S001`, `TASK-C03` — prefix reflects phase/area |
| Design Reference | Link to the relevant DESIGN.md chapter (avoid duplicating it) |
| Scope | What IS and is NOT included in this task |
| Constraints | Critical rules, dependencies, prohibited patterns |
| Success Conditions | Concrete, specific — expressed as unit test specifications |

**Success Conditions format:**
- Describe the test scenario (setup → action → assertion)
- Name specific fields, values, or behaviors to assert
- Include edge cases and negative cases

Together with DESIGN.md, the task detail must give a developer complete understanding with no
additional context needed.

---

## Step 3: TASK-TRACKER.md

Create `TASK-TRACKER.md` as a brief hierarchical checklist.

```markdown
# Task Tracker

**Reference:** See [TASK-DETAIL.md](./TASK-DETAIL.md) for detailed task descriptions

## Phase 1: [Phase Name]

**Goal:** [One sentence]

- [ ] **TASK-X001** [Task Name] [details](./TASK-DETAIL.md#task-x001-task-name)
- [ ] **TASK-X002** [Task Name] [details](./TASK-DETAIL.md#task-x002-task-name)

## Phase 2: [Phase Name]

**Goal:** [One sentence]

- [ ] **TASK-X003** [Task Name] [details](./TASK-DETAIL.md#task-x003-task-name)
```

Rules:
- One line per task — no detail here, just name and link
- Binary status: `[ ]` not done, `[x]` done
- Anchor links must match TASK-DETAIL.md headers exactly

---

## Step 4: ONBOARDING.md

Create `./ONBOARDING.md` for developers new to this project.

Include:

1. **Project overview** — what is being built or refactored (concise summary from DESIGN.md)
2. **Planning artifacts** — locations of DESIGN.md, TASK-DETAIL.md, TASK-TRACKER.md
3. **Folder layout** — where are the components being built/refactored; which existing code they
   draw from or replace
4. **Build & run** — commands to build and run tests
5. **Workflow** — instruct the developer to read `.dev-workstream/guides/DEV-GUIDE.md` to
   understand the batch-based development workflow
