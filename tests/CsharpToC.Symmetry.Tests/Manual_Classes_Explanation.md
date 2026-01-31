# Manual Test Infrastructure Explanation

## 1. What are the "Manual" classes?

The `*_Manual.cs` classes (e.g., `IoTDeviceMutableTopic_Manual.cs`, `RobotStateTopic_Manual.cs`) are **manually implemented references** of the serialization logic. 

**Purpose:**
- They serve as a **Sandbox** to write the correct logic (XCDR2, Parameter Lists, etc.) without wrestling with the Code Generator's templating engine immediately.
- They allow us to verify the "Golden" byte stream behavior against a working C# implementation.
- Once the "Manual" class passes the tests, its logic is "Backported" to the `SerializerEmitter.cs` to fix the Code Generator for everyone.

## 2. Process check against `FAST-ITERATION-GUIDE.md`

**Does it follow the guide?** 
**No**, technically it deviates from the "Hot-Patch Workflow".

| Feature | FAST-ITERATION-GUIDE.md | "Manual Class" Approach |
| :--- | :--- | :--- |
| **File Edited** | `obj/Generated/AtomicTests/*.Serializer.cs` | `Infrastructure/*_Manual.cs` |
| **Persistence** | **Temporary**. Lost on full rebuild/clean. | **Permanent**. Persists across rebuilds. |
| **Integration** | Automatic (Class is partial, used by tests naturally). | Manual. Requires editing `Part5_ComplexIntegrationTests.cs` to point to the Manual class. |
| **Iteration Speed** | Very Fast (Edit -> Run). | Fast (Edit -> Run), but requires setup. |

**Why this deviation?**
For complex failures like **XTypes Mutable/Appendable**, the changes required are structural (e.g., adding `switch` statements for IDs, handling `DHEADER`). 
- Editing the generated file directly is risky if we need to rewrite the entire method body (it's easy to make a syntax error or lose work on a stray rebuild).
- The "Manual Class" acts as a stable reference implementation that we can debug until it matches the `failing-tests-cdr-analysis.md` spec.

## 3. Recommended Alignment
Once we fix a topic using the Manual Class:
1. Copy the working logic.
2. Update the `SerializerEmitter` or template properties.
3. Delete the Manual class and revert the Test file to use the generated code.

This effectively merges the "Stability" of the Manual approach with the "Finalization" step of the Guide.
