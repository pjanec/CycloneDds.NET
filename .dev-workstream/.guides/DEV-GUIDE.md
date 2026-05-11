# Developer Workflow Guide - Batch-Based Development

**Purpose:** Guide for developers working on batch-based tasks  
**Scope:** Generic workflow applicable to any project  
**Communication:** All through markdown files in this folder structure

---

## 🎯 Your Role

You are a **developer** implementing features through a structured batch system. Each batch is:
- A focused set of related tasks (typically 4-10 hours of work)
- Self-contained with complete instructions
- Independently testable
- Clearly defined with success criteria

**Important:** Different developers may work on different batches. Each batch includes complete onboarding instructions - always read them carefully.

---

## 📁 Folder Structure

```
.dev/topic/
├── templates/                     # Templates for your submissions
│   ├── BATCH-REPORT-TEMPLATE.md
│
├── batches/                       # Your task instructions
│   ├── BATCH-01-INSTRUCTIONS.md
│   ├── BATCH-02-INSTRUCTIONS.md
│   └── ...
│
├── reports/                       # Your completed reports
│   ├── BATCH-01-REPORT.md
│   └── ...
│
├── questions/                     # Your questions (if needed)
│   ├── BATCH-01-QUESTIONS.md
│   └── ...
│
└── reviews/                       # Development Lead feedback
    ├── BATCH-01-REVIEW.md
    └── ...
```

---

## 🔄 The Workflow

### Step 1: Receive Assignment

You'll be directed to a batch instruction file:
```
.dev/topic/batches/BATCH-XX-INSTRUCTIONS.md
```

**What to do:**
1. **Read the entire instruction file** - Every section matters
2. **Read referenced documents** - Design docs, previous reviews, architecture
3. **Review existing code** - Understand current implementation
4. **Check previous batch review** - Learn from feedback (if referenced)

**Time investment:** 30-60 minutes before writing any code

**Why this matters:** Understanding the context prevents mistakes and saves time later.

### Step 2: Plan Your Work

**Before coding:**
- [ ] Understand all tasks and their dependencies
- [ ] Identify what tests are required
- [ ] Note any ambiguities (ask questions if needed)
- [ ] Estimate your time per task
- [ ] Set up test infrastructure if needed

**Ask yourself:**
- Do I understand WHY we're doing this, not just WHAT?
- Do I know what "done" looks like?
- Are there any unclear requirements?

### Step 3: Implement

**Development approach:**

1. **Work incrementally**
   - One task at a time
   - Test as you go
   - Commit frequently (local commits are fine)

2. **Follow TDD when practical**
   - Write failing test first
   - Implement to make it pass
   - Refactor if needed

3. **Follow existing patterns**
   - Study similar code in the codebase
   - Match existing style and architecture
   - Don't reinvent solved problems

4. **Document as you code**
   - XML comments on public APIs
   - Inline comments for complex logic
   - Update design docs if making architectural decisions

**Run tests frequently:**
```bash
# Project-specific test commands will be in batch instructions
# Example patterns:
dotnet test                    # .NET projects
npm test                       # Node.js projects
pytest                         # Python projects
cargo test                     # Rust projects
```

### Step 4: Handle Questions/Blockers

**When to ask questions:**
- ✅ Specification is ambiguous or contradictory
- ✅ Integration point with existing code is unclear
- ✅ Performance target seems impossible to meet
- ✅ Architectural decision required
- ✅ You discover a fundamental design issue

**When NOT to ask questions:**
- ❌ Minor implementation details (use your judgment)
- ❌ Code style preferences (follow existing patterns)
- ❌ Language/framework basics (research first)
- ❌ Something clearly explained in the instructions
- ❌ Some files changed from outside - this is normal and you should adapt automatocally.


**How to ask:**


3. **Notify the Development Lead**

4. **While waiting:** Work on other tasks in the batch if possible

### Step 5: Self-Review

**Before submitting, review your own work:**

#### Code Review
- [ ] Follows existing patterns and architecture
- [ ] No compiler warnings
- [ ] Public APIs documented
- [ ] Error handling present
- [ ] Edge cases handled
- [ ] No obvious performance issues
- [ ] Code is readable

#### Test Review
- [ ] All tests passing
- [ ] Tests verify behavior, not implementation
- [ ] Edge cases covered
- [ ] Error conditions tested
- [ ] Integration tests present (if specified)
- [ ] Tests are maintainable

#### Completeness Check
- [ ] All tasks from instructions completed
- [ ] All required tests written
- [ ] Performance benchmarks run (if applicable)
- [ ] Documentation updated (if needed)
- [ ] No TODOs or FIXMEs left in code

### Step 6: Submit Report

**Critical:** Your report is how the Development Lead understands your work. Take it seriously.

1. **Copy the template:**
   ```bash
   cp .dev/topic/.guides/BATCH-REPORT-TEMPLATE.md \
      .dev/topic/reports/BATCH-XX-REPORT.md
   ```

2. **Fill out EVERY section:**

   **✅ DO:**
   - Answer all specific questions thoroughly
   - Explain design decisions YOU made
   - Document all deviations with rationale
   - Include full test output
   - List known issues honestly
   - Explain challenges and how you solved them

   **❌ DON'T:**
   - Skip sections ("N/A" is not acceptable unless explicitly allowed)
   - Give one-word answers to complex questions
   - Say "all tests pass" without showing output
   - Hide issues or limitations
   - Write a minimal report

3. **Include these key sections:**
   - **Implementation Summary:** What you built
   - **Design Decisions:** Choices YOU made beyond the spec
   - **Deviations:** Any changes from instructions (with WHY)
   - **Test Results:** Full output, not just "passing"
   - **Challenges:** What was difficult and how you solved it
   - **Integration Notes:** How this fits with the rest of the system
   - **Known Issues:** Limitations or concerns

4. **Notify the Development Lead**

---

## ✅ Definition of Done

A batch is **DONE** when:

### Code Quality
- ✅ All tasks implemented per specifications
- ✅ Code follows existing architecture and patterns
- ✅ No compiler warnings or errors
- ✅ Public APIs have XML documentation
- ✅ Complex logic has inline comments
- ✅ Error handling appropriate for context

### Test Quality
- ✅ All required tests written
- ✅ All tests passing
- ✅ Tests verify actual behavior (not just compilation)
- ✅ Edge cases covered
- ✅ Integration tests present (if specified)
- ✅ Performance benchmarks run (if applicable)

### Documentation Quality
- ✅ Report completed using full template structure
- ✅ All specific questions answered thoroughly
- ✅ Deviations documented with rationale
- ✅ Design decisions explained
- ✅ Known issues listed

### Process Quality
- ✅ Code committed to version control
- ✅ No work-in-progress or commented-out code
- ✅ No debugging statements left in
- ✅ Pre-submission checklist completed

---

## 🎯 What Actually Matters

### Quality Over Speed

**We value:**
- ✅ Correct implementation (works as designed)
- ✅ Maintainable code (others can understand and modify)
- ✅ Thoughtful testing (catches real bugs)
- ✅ Clear communication (thorough reports)

**We don't value:**
- ❌ Fast but wrong
- ❌ High test count with no meaningful validation
- ❌ Clever code that's hard to understand
- ❌ Minimal reports with hidden issues

### Tests That Matter

**Good test example:**
```
Test: EntityStateTranslator creates Ghost entity with correct position

Setup: EntityState descriptor with position (10, 20, 30)
Action: Call PollIngress()
Verify: 
  - Entity exists
  - Lifecycle state is Ghost
  - Position component has values (10, 20, 30)
  - Entity excluded from standard queries
  
✅ Tests actual behavior that matters
```

**Bad test example:**
```
Test: NetworkSpawnRequest can be created

Action: new NetworkSpawnRequest()
Verify: Result is not null

❌ Tests nothing meaningful
```

### Deviations Are OK (When Documented)

**It's acceptable to:**
- Improve on the original design (with rationale)
- Take a different approach (if you explain why)
- Add enhancements beyond requirements (with documentation)
- Fix issues you discover (and document them)

**You must:**
- Document WHAT you changed
- Explain WHY you changed it
- Describe the BENEFIT
- Note any RISKS or trade-offs
- Flag it clearly in your report

**Example:**
```markdown
### Deviation 1: Used Strategy Pattern Instead of Direct Implementation

**What:** Implemented IOwnershipStrategy interface instead of hardcoded logic
**Why:** Original design had ownership rules in NetworkSpawner, making them hard to test and change
**Benefit:** Testable (can mock strategy), flexible (can swap at runtime), follows existing patterns in codebase
**Risk:** Adds one level of indirection
**Recommendation:** Keep this approach - fits architecture better
```

---

## 🚨 Common Pitfalls to Avoid

### 1. Starting Without Understanding
❌ **Pitfall:** Jump into coding without reading all instructions  
✅ **Solution:** Read everything first - instructions, design docs, existing code

### 2. Not Asking When Unclear
❌ **Pitfall:** Guess at ambiguous requirements, implement wrong thing  
✅ **Solution:** Ask questions early. Better to clarify than rebuild.

### 3. Shallow Testing
❌ **Pitfall:** Write tests that just verify compilation or property setting  
✅ **Solution:** Test actual behavior and edge cases that matter

### 4. Minimal Reports
❌ **Pitfall:** Brief, incomplete reports that skip sections  
✅ **Solution:** Use full template, answer all questions thoroughly

### 5. Hidden Deviations
❌ **Pitfall:** Change approach without documenting why  
✅ **Solution:** Document all deviations clearly in report

### 6. Ignoring Existing Patterns
❌ **Pitfall:** Invent new patterns when existing ones exist  
✅ **Solution:** Study codebase, follow established conventions

### 7. Performance Afterthoughts
❌ **Pitfall:** "It works" without checking performance  
✅ **Solution:** Run benchmarks if specified, watch for obvious issues

### 8. Last-Minute Testing
❌ **Pitfall:** Write all tests at the end, discover design issues  
✅ **Solution:** Test as you go (TDD when practical)

---

## 📊 Report Quality Standards

### What Makes a Good Report

**Complete:**
- Every section filled out
- All specific questions answered
- Full test output included
- Known issues listed

**Detailed:**
- Design decisions explained
- Challenges and solutions described
- Integration notes thorough
- Performance observations noted

**Honest:**
- Limitations acknowledged
- Known issues documented
- Deviations explained
- Uncertainty flagged

**Professional:**
- Clear writing
- Proper formatting
- Evidence provided (test output, benchmarks)
- Organized structure

### Specific Questions - Answer Thoroughly

When batch instructions include specific questions like:

> **Question 1:** How did you handle the Ghost to Constructing transition?

**❌ Bad answer:**
> "I changed the lifecycle state."

**✅ Good answer:**
> "The transition happens in NetworkSpawnerSystem.ProcessSpawnRequest():
> 1. Check current state (could be Ghost or new entity)
> 2. If Ghost, set preserveExisting=true when applying TKB template (preserves Position from EntityState)
> 3. Call repo.SetLifecycleState(entity, EntityLifecycle.Constructing)
> 4. Call elm.BeginConstruction() to start ELM coordination
> 
> Key consideration: Must preserve Position component from Ghost. If we apply template without preserveExisting flag, it would overwrite the position from the network with the template default (0,0,0).
> 
> Test coverage: `Test_GhostPromotion_PreservesPositionFromNetworkState` verifies Position retained after promotion."

---

## 🔧 Working with Reviews

### When You Receive Feedback

**Review file location:** `.dev/topic/reviews/BATCH-XX-REVIEW.md`

**Read it carefully:**
- Understand what's approved
- Note what needs changes
- Clarify anything unclear

**Categories of feedback:**

#### 1. APPROVED
**Meaning:** Your work is good, ready to integrate  
**Your action:** Celebrate! Prepare for next batch.

#### 2. APPROVED WITH NOTES
**Meaning:** Work is good, minor suggestions for future  
**Your action:** Read notes, apply learnings to next batch

#### 3. CHANGES REQUIRED
**Meaning:** Specific issues need fixing  
**Your action:** 
- Address each issue listed
- Update code
- Re-run all tests
- Update report with changes
- Resubmit for review


### How to Handle Criticism

**Remember:**
- Feedback is about code, not about you
- Reviews help you improve
- Development Lead wants you to succeed
- Learning from mistakes makes you better

**When you disagree:**
- Ask for clarification
- Explain your reasoning
- Be open to alternative approaches
- Focus on finding the best solution

---

## 💡 Tips for Success

### 1. Read Everything First
Batch instructions, design docs, previous reviews - read it all before coding.

### 2. Study Existing Code
Best patterns are already in the codebase. Find them and follow them.

### 3. Test Early and Often
Don't wait until the end. Test as you implement each piece.

### 4. Document Your Thinking
Write down why you made decisions. You'll need it for the report.

### 5. Take Breaks
If stuck, step away. Fresh perspective helps.

### 6. Ask Questions Early
Don't waste hours guessing. Clarify ambiguity immediately.

### 7. Write Thorough Reports
Invest time in your report. It's how your work is evaluated.

### 8. Learn from Reviews
Each review is a learning opportunity. Apply feedback to future batches.

### 9. Commit Frequently
Small, focused commits make it easier to track progress and revert if needed.

### 10. Celebrate Progress
Completing a batch is an achievement. Recognize your progress.

---

## ⚡ Quick Reference

### Starting a Batch
```
1. Read .dev/topic/batches/BATCH-XX-INSTRUCTIONS.md (fully)
2. Read referenced design docs
3. Review existing code
4. Plan your approach
5. Start implementing (test as you go)
```

### Asking Questions
```
1. Try to find answer first (docs, existing code)
2. If still unclear, use template:
   cp .dev/topic/templates/QUESTIONS-TEMPLATE.md \
      .dev/topic/questions/BATCH-XX-QUESTIONS.md
3. Fill out thoroughly
4. Notify Development Lead
5. Work on other tasks while waiting (if possible)
```

### Submitting Report
```
1. Self-review your work
2. Run all tests one final time
3. Copy template:
   cp .dev/topic/.guides/BATCH-REPORT-TEMPLATE.md \
      .dev/topic/reports/BATCH-XX-REPORT.md
4. Fill out EVERY section thoroughly
5. Include full test output
6. Answer all specific questions
7. Document all deviations
8. Notify Development Lead
```

### Pre-Submission Checklist
```
- [ ] All tasks completed
- [ ] All tests passing
- [ ] No compiler warnings
- [ ] Code follows existing patterns
- [ ] Public APIs documented
- [ ] Performance benchmarks run (if applicable)
- [ ] Report filled out completely (every section)
- [ ] All specific questions answered thoroughly
- [ ] Deviations documented with rationale
- [ ] Known issues listed
- [ ] Code committed
```

---

## 🎯 Success Criteria by Role

### As a Developer, You're Successful When:

- ✅ **First-time approval rate > 80%** - Getting it right the first time
- ✅ **Test quality improving** - Better tests with each batch
- ✅ **Fewer questions over time** - Understanding the system better
- ✅ **Thorough reports** - Complete documentation of your work
- ✅ **Learning from reviews** - Not repeating same issues

### What the Development Lead Looks For:

1. **Correct implementation** - Works as designed
2. **Architectural fit** - Follows patterns and principles
3. **Quality tests** - Validates what matters
4. **Clear communication** - Thorough, honest reports
5. **Thoughtful decisions** - Rationale for choices
6. **Problem-solving** - How you handled challenges

---

## 📚 Additional Resources

### Templates Location
- Report Template: `.dev/.guides/BATCH-REPORT-TEMPLATE.md`

### Your Work Goes Here
- Instructions: `.dev/topic/batches/BATCH-XX-INSTRUCTIONS.md`
- Your Report: `.dev/topic/reports/BATCH-XX-REPORT.md`
- Feedback: `.dev/topic/reviews/BATCH-XX-REVIEW.md`

---

**Remember:** You're a capable developer. The batch system is here to provide structure and clarity, not to restrict you. Use your judgment, document your decisions, and communicate clearly.

**Communication Model:** All communication happens through markdown files in this folder structure. This ensures clarity, documentation, and works across different time zones.

**Most Important:** Take pride in your work. Do it right, not just fast. Your thoroughness and quality will be recognized and appreciated.

Good luck! 🚀
