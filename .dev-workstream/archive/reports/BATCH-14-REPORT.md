Ihave completed the tasks in **BATCH-14**.

### Summary of Changes

1. **Native Extension \(Phase 1\):**

    - Modified dds\_write.c to export dds\_dispose\_serdata and dds\_unregister\_serdata functions. These functions wrap `dds_writecdr_impl` but set the correct `statusinfo` for disposal/unregistration.
    - Rebuilt `ddsc.dll` using `cmake` and updated the binary in ddsc.dll.
2. **P/Invoke Layer \(Phase 2\):**

    - Updated DdsApi.cs to add P/Invoke declarations for:
        - dds\_dispose\_serdata\(DdsEntity writer, IntPtr serdata\)
        - dds\_unregister\_serdata\(DdsEntity writer, IntPtr serdata\)
3. **DdsWriter Implementation \(Phase 3\):**

    - Refactored DdsWriter&lt;T&gt;.Write\(\) to use a new helper method PerformOperation.
    - PerformOperation handles the zero-allocation serialization, CDR header prepending, serdata creation, and invokes the delegated native operation.
    - Implemented DisposeInstance\(in T sample\) invoking dds\_dispose\_serdata.
    - Implemented UnregisterInstance\(in T sample\) invoking dds\_unregister\_serdata.
    - Added comprehensive XML documentation comments.
4. **Testing \(Phase 4\):**

    - Added integration tests DisposeInstance\_RemovesInstance and UnregisterInstance\_RemovesWriterOwnership to IntegrationTests.cs.
    - **Note:** Tests are currently marked \[Fact\(Skip = "Requires Keyed Topic"\)\]. The existing TestMessage type is Keyless, which prevents verification of instance-specific disposal \(DDS treats it as a singleton instance\). To fully verify, a Keyed Topic descriptor is required in the test environment.