# Documentation Standards

## Required Document Structure

Every per-project documentation file must include these sections in order:

### 1. Header
```markdown
# [Project Name]

**Path:** `relative/path/to/Project.csproj`
**Layer:** Core / Infrastructure / Domain / Presentation
**Date:** YYYY-MM-DD
**README Status:** Up-to-date / Diverged / Missing
```

### 2. Overview
- **Purpose:** What problem this project solves
- **Key Features:** Bullet list of primary capabilities
- **Architectural Layer:** Where it sits in the solution hierarchy

### 3. Architecture

- High-level design description
- Main components and their responsibilities
- Key constraints (e.g., "zero-allocation", "main-thread only", "read-only snapshot")
- Design decisions and rationale

**ASCII Block Diagram (mandatory — see guidelines below)**

### 4. Source Analysis

Key files with their purpose:
```markdown
| File | Purpose |
|---|---|
| `Foo.cs` | Entry point for X |
| `Bar.cs` | Handles Y processing |
```

Namespaces and their roles.

Core classes/structs/interfaces — with brief descriptions.

### 5. Dependencies

**Internal (project references):**
```markdown
- `ProjectA` — used for X
- `ProjectB` — provides Y interface
```

**External (NuGet):**
```markdown
- `SomePackage v1.2.3` — used for Z
```

### 6. Usage Examples

Minimum **3 code blocks** showing:
1. Initialization / construction
2. Primary usage / most common operation
3. Advanced or edge-case usage

```csharp
// Example 1: Initialization
var service = new FooService(config);
service.Initialize();

// Example 2: Primary usage
var result = service.Process(input);
Assert.Equal(expected, result);
```

### 7. Best Practices

- Thread safety: is it thread-safe? How to use correctly across threads?
- Performance: zero-alloc constraints, pooling requirements, hot-path rules
- Common mistakes to avoid

### 8. Relationships

Links to projects that depend on this one and projects it depends on:
```markdown
- Depends on: [CoreProject](../Core/CoreProject.md), [DataProject](../Data/DataProject.md)
- Used by: [AppHost](../Presentation/AppHost.md)
```

---

## ASCII Diagram Guidelines

Use box-drawing characters for all diagrams. Minimum 2–3 diagrams per document.

**Box and flow:**
```
┌───────────────┐       ┌───────────────┐
│  ComponentA   │──────▶│  ComponentB   │
└───────────────┘       └───────────────┘
         │
         ▼
┌───────────────┐
│  ComponentC   │
└───────────────┘
```

**Layered architecture:**
```
┌─────────────────────────────────────┐
│           Presentation              │
├─────────────────────────────────────┤
│           Domain Logic              │
├─────────────────────────────────────┤
│           Infrastructure            │
├─────────────────────────────────────┤
│              Core                   │
└─────────────────────────────────────┘
```

**State / sequence:**
```
[State A] ──event──▶ [State B] ──event──▶ [State C]
                          │
                          └──error──▶ [Error State]
```

Aim for diagrams that show:
1. **Block diagram** — component structure and ownership
2. **Flow diagram** — data or control flow through the system
3. **State or sequence diagram** — for stateful components or multi-step interactions

---

## Quality Criteria

| Criterion | Requirement |
|---|---|
| Minimum length | ≥ 500 lines |
| Diagrams | 2–3 ASCII diagrams |
| Code examples | ≥ 3 working code blocks |
| README validation | Explicitly stated (Up-to-date / Diverged / Missing) |
| Relationships | All inbound and outbound dependencies listed |

A document that merely lists class names without explaining behaviour, constraints, or usage does
not meet the standard. Favour depth over breadth within each document.
