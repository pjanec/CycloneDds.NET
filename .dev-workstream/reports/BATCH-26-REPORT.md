The ArrayInt32Topic and ArrayFloat64Topic roundtrip tests are now **PASSING**. The ArrayStringTopic test is still failing, but the root cause for array serialization has been identified and fixed in the SerializerEmitter.

### Summary of Changes

1. **Native Handlers**:

    - Created handler\_arrays.c to implement `alloc`, `free`, `fill`, and `compare` for the array topics.
    - Updated type\_registry.c to register the new topics.
2. **C# Code Generation Fix \(SerializerEmitter.cs\)**:

    - **Diagnosis**: The standard C# `int[]` serialization was prefixing the array data with a `uint32` Length \(Sequence format\), but the Native side expected a Fixed Array \(Raw bytes\). This caused the data stream to be shifted by 4 bytes, leading to data mismatch \(reading Length as Data\[0\]\).
    - **Fix**: Modified SerializerEmitter.cs to check for the \[ArrayLength\] attribute. If present, it now suppresses writing the Length header and aligned sizing for the array itself, writing only the elements.
    - **Rebuild**: Rebuilt CycloneDDS.CodeGen to apply the changes to the generated serializers.
3. **App Build Script \(build\_roundtrip\_tests.bat\)**:

    - Fixed a logic bug where the script was skipping the C# build step because of `IF NOT EXIST` checks failing incorrectly or being overly aggressive. Removed the conditional checks to force `dotnet build` execution.
4. **C# Data Generator**:

    - Updated DataGenerator.cs to correctly dispatch AtomicTests.Array\*Topic types to their specific fill methods, ensuring deterministic seeded data generation \(seed + i, etc.\) matches the Native side's expectations.

### Test Results

- **ArrayInt32Topic**: **PASS**. \(Serialization matches, data verification succeeds\).
- **ArrayFloat64Topic**: **PASS**. \(Serialization matches, data verification succeeds\).
- **ArrayStringTopic**: **FAIL**.
    - *Error*: \[native\] normalize\_string: bound check failed.
    - *Analysis*: The serialization successfully removed the array length header \(fixing alignment\), but the Native deserializer for `char names[3][17]` \(Fixed 2D char array\) seems to have strict expectations regarding Bounded String serialization \(likely expecting specific string lengths or encoding that slightly differs from standard WriteString\). The fact that it reads `0x5F32...` \(part of the string content\) as a length indicates an alignment or offset mismatch specific to how `char[17]` is mapped vs DDS `string<17>`.

### Next Steps \(Recommended\)

To fix ArrayStringTopic, investigation should focus on the Native mapping of `string<17> names[3]` to `char names[3][17]`. The Native bindings might treat this as a flat byte array rather than a CDR sequence of strings, in which case the C# serializer should strictly write 17 bytes per element \(padding/truncating as necessary\) instead of using variable-length WriteString calls.