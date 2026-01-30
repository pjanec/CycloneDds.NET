The `idlc.exe` path configuration issue has been resolved, and all CycloneDDS.CodeGen.Tests are now passing.

### Summary of Fixes

1. **Resolved `idlc.exe` Path Issue**:

    - The test runner expected `idlc.exe` in idlc.exe.
    - The file was located in bin.
    - **Fix**: Created the expected directory structure and copied `idlc.exe` to the target location.
2. **Updated Test Assertions for BATCH-15 Alignment**:

    - The recent BATCH-15 update correctly enforced 8-byte alignment for `double` values \(standard XCDR behavior\), which introduced 4 bytes of padding when following a 4-byte `int`.
    - The existing tests \(GoldenRigTests, UnionTests, SerializerEmitterTests\) expected "packed" data \(12 bytes\) instead of aligned data \(16 bytes\).
    - **Fix**: Updated the test assertions to match the correct, aligned behavior:
        - **SerializerEmitterTests.cs**: Updated expected size from 12 to **16** and updated the expected hex string to include the 4 zero-padding bytes.
        - **GoldenRigTests.cs**: Updated the expected hex string to include padding \(`...15CD5B07`**`00000000`**`77BE...`\).
        - **UnionTests.cs**: Updated the expected hex string to match the aligned layout \(`...00000000` padding before the double value\).

### Verification

All 95 tests in CycloneDDS.CodeGen.Tests passed successfully.