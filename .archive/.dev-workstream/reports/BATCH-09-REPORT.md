# BATCH-09: Union Support

**Batch Number:** BATCH-09  
**Status:** Completed  
**Tasks:** FCDC-S013 (Union Support)  
**Date:** 2026-01-16

---

## 🏆 Summary

Implemented code generation for discriminated unions, including `[DdsUnion]`, `[DdsDiscriminator]`, and `[DdsCase]` attributes. Verified full compliance with XCDR2 spec for `@appendable` unions (DHEADER present).

## 🛠 Tasks Completed

### 1. Golden Rig Verification (Task 0)
- **Investigation:** Executed `idlc` on `GoldenUnion.idl`. Analyzed generated `GoldenUnion.c`.
- **Finding:** Generated opcodes start with `DDS_OP_DLC` ("Delimited CDR, inserts DHEADER").
- **Confirmation:** Cyclone DDS **DOES** emit DHEADER for unions.
- **Decision:** Implemented **DHEADER** support for all unions. Unions are treated as delimited types (`@appendable`).
- **Wire Format:** `[DHEADER: 4 bytes] [Discriminator: 4 bytes] [Active Payload]`

### 2. Implementation (Task 1)
- **SerializerEmitter:** Added `EmitUnionSerialize` logic. 
  - Emits DHEADER placeholder.
  - Emits Discriminator.
  - Uses `switch` statement to serialize active case.
  - Patches DHEADER.
- **DeserializerEmitter:** Added `EmitUnionDeserialize` logic.
  - Reads DHEADER to determine `endPos`.
  - Reads Discriminator.
  - Uses `switch` statement to read active case.
  - **Safety:** Used `if (reader.Position < endPos)` checks to prevent buffer overruns.
  - **Unknown Cases:** Implemented skipping of unknown union arms (discriminator mismatch) by seeking to `endPos`.
- **View Support:** `ToOwned()` now correctly reconstructs the active union case from the View.

### 3. Verification & Testing (Task 2)
- Created `tests/CycloneDDS.CodeGen.Tests/UnionTests.cs`.
- **Tests Covered:**
  - Serialization of Case 1 (Radius).
  - Serialization of Case 2 (Side).
  - Round-trip Deserialization.
  - Handling of Unknown Discriminator (Skipping logic).
- **Results:** ALL Tests Passed.

## 📊 Test Results

`dotnet test FastCycloneDdsCsharpBindings.sln`

```text
Test summary: total: 111; failed: 0; succeeded: 111; skipped: 0; duration: 4.7s
Build succeeded with 9 warning(s) in 6.8s
```

## 📝 Notes

- **ToOwned() Logic:** Logic copies the discriminator first, then switches on it to copy only the relevant field.
- **DHEADER Compliance:** Implementation ensures forward/backward compatibility by treating unions as appendable.

---
**Signed off by:** GitHub Copilot
