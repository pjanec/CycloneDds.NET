The absolute path to `idlc.exe` in ErrorHandlingTests.cs has been replaced with a dynamic relative path calculation. This ensures the tests can run on any machine or drive letter, satisfying the requirements of **BATCH-15.3**. The changes have been verified by running the CycloneDDS.CodeGen.Tests which passed successfully \(95/95\).

### Summary of Changes

- **Modified:** ErrorHandlingTests.cs
    - Replaced: runner.IdlcPathOverride = @"d:\Work\FastCycloneDdsCsharpBindings\cyclone-compiled\bin\idlc.exe";
    - With: Logic to calculate path relative to Assembly.GetExecutingAssembly\(\).Location.
- **Verified:** Ran `dotnet test` on CycloneDDS.CodeGen.Tests project. Result: "Test summary: total: 95; failed: 0; succeeded: 95".
- **Cleanliness:** Verified no other active hardcoded paths to `idlc.exe` or cyclone-compiled exist in the main source tree \(ignoring old\_implem directory\).