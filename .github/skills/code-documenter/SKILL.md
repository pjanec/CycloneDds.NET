---
name: code-documenter
description: Systematic guide for generating comprehensive technical documentation for a .NET/C# software solution. Use when asked to document a codebase, create per-project architectural documentation, run a documentation initiative, or produce a solution overview. Produces per-project Markdown files (≥500 lines each) with architecture diagrams, usage examples, and dependency maps, plus a master solution overview document.
---

# Code Documenter

Document every project in a solution through a systematic checklist-driven loop.

## Core Workflow Loop

Execute this loop until the checklist is fully marked `[X]`:

1. **LOAD** — Open `00-PROJECT-CHECKLIST.md`
2. **SELECT** — Find the first item marked `[ ]` (Pending)
3. **ANALYZE** — Read source code, directory structure, and existing READMEs for that project
4. **WRITE** — Create the documentation file — see [references/documentation-standards.md](references/documentation-standards.md) for required structure
5. **DISCOVER** — Note cross-project architectural patterns; add them as new items in the "Emerging Relationships" section of the checklist
6. **VERIFY** — File exists · ≥500 lines · 2–3 ASCII diagrams present
7. **UPDATE** — Mark item `[X]` in `00-PROJECT-CHECKLIST.md`
8. **REPEAT** — Go to step 1

## Bootstrap (First Session Only)

Before the first documentation session, create the checklist:

1. Scan all subfolders recursively for `.csproj` files
2. **Exclude** test/benchmark projects (names containing `Test`, `UnitTests`, `IntegrationTests`, `Specs`, `Benchmark`)
3. Create `00-PROJECT-CHECKLIST.md` — copy from [assets/checklist-template.md](assets/checklist-template.md)
4. Group projects by architectural layer (Core/Kernel, Infrastructure/Data, Domain/Business Logic, Presentation/Examples)
5. Fill each section; format: `- [ ] [ProjectName] (Path: relative/path/to.csproj)`
6. Save checklist, then switch to the Core Workflow Loop above

## Output Location

```
Docs/projects/[Category]/[ProjectName].md
```

## README Validation

For each project, explicitly check whether the existing README is:
- **Up-to-date** — source code matches what README describes
- **Diverged** — implementation has moved on; note specific discrepancies
- **Missing** — no README exists

State this status at the top of the documentation file.

## Completion — Solution Overview

When all per-project and relationship items are `[X]`:

1. Write `Docs/00-SOLUTION-OVERVIEW.md` linking all individual documents
2. Include solution-level block diagrams showing component layers and dependencies
3. Add a "Key Architectural Patterns" section for cross-cutting concerns

## Reference Files

- **[references/documentation-standards.md](references/documentation-standards.md)** — Required document structure, ASCII diagram guidelines, code example requirements
- **[assets/checklist-template.md](assets/checklist-template.md)** — Copy into project root as `00-PROJECT-CHECKLIST.md` during bootstrap
