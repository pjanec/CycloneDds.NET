# CsharpToC.Symmetry Onboarding Guide

## 🚀 Welcome to the Project

You are joining the **CsharpToC.Symmetry** project, a specialized high-velocity testing framework for the C# bindings of FastCycloneDDS.

**Your Goal:** Maintain and expand the test suite that verifies byte-level compatibility with the native CycloneDDS implementation.

---

## 📚 Essential Reading

Before writing a single line of code, you **MUST** read these documents in order:

1. **[README.md](README.md)**  
   *Start here.* Understand the "Golden Data" concept and why we built this.
   
2. **[FAST-ITERATION-GUIDE.md](FAST-ITERATION-GUIDE.md)**  
   *Your daily bible.* Learn the "Hot-Patch" workflow. If you aren't using this workflow, you are working 10x slower than expected.

3. **[DESIGN.md](DESIGN.md)**  
   *The architecture.* Understand how `Infrastructure/`, `Native/`, and `GoldenData/` interact.

---

## ⚠️ The Golden Rules

### 1. Zero Tolerance for Failure
*   **Requirement:** Your task is NOT done until **ALL tests are passing**. 
*   **Definition of Done:** `rebuild_and_test.ps1` returns green for every single test case.
*   **No "Known Failures":** If a test fails, code is broken. Fix it.

### 2. Copy from Roundtrip
*   **Don't Reinvent:** This suite mirrors `tests/CsharpToC.Roundtrip.Tests`.
*   **Source of Truth:** If you need a test case (Topic Name, Seed), find it in the Roundtrip project.
*   **Maintain Parity:** We want coverage symmetry between Roundtrip (Integration) and Symmetry (Unit) tests.

### 3. Golden Data is Sactrosanct
*   **Do Not Edit Manually:** Never edit `.txt` files in `GoldenData/` manually unless you really know what you are doing.
*   **Regenerate:** Use `generate_golden_data.ps1` if seeds or native logic changes.

---

## 🛠️ Your Workflow

1.  **Setup:** Ensure `atomic_tests.idl` and `ddsc_test_lib.dll` are present (copied from Roundtrip).
2.  **Pick a Test:** Look at `Roundtrip.Tests` -> `BasicPrimitivesTests.cs`.
3.  **Implement:** Create the corresponding test in `Symmetry/Part1_PrimitiveTests.cs`.
4.  **Run:** `.\run_tests_only.ps1 -Filter "MyTestName"`
5.  **Fail?** Use the hot-patch workflow to fix the serializer.
6.  **Pass?** Move to the next test.

---

## 🔑 Key Resources

*   **Roundtrip Tests:** `../CsharpToC.Roundtrip.Tests/` - Copy topic names and seeds from here.
*   **Native DLL:** `Native/ddsc_test_lib.dll` - Must match the one used by Roundtrip.
*   **IDL:** `atomic_tests.idl` - Must match the one used by Roundtrip.

Good luck. We expect high velocity and high quality.
