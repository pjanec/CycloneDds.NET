I have completed the tasks outlined for BATCH-15 including the Performance Foundation updates.

**Summary of Changes:**

1. **Standard .NET Types Support \(Task 1\):**

    - **Mapped Types:** Added support for Guid, DateTime, DateTimeOffset, and TimeSpan.
    - **TypeMapper.cs:** Updated to register writer methods \(WriteGuid, WriteDateTime, etc.\) and IsBlittable/GetSize logic.
    - **CdrWriter.cs / CdrReader.cs:** Implemented serialization and deserialization methods for these types. DateTime and TimeSpan are serialized as `int64` \(Ticks\). Guid as 16 bytes. DateTimeOffset as 16 bytes \(Ticks + Offset\).
    - **IdlEmitter.cs:** Added IDL mappings \(e.g., Guid -&gt; `octet[16]`, DateTime -&gt; `int64`\).
2. **Arrays & Block Copy Optimization \(Task 2 & 3\):**

    - **TypeMapper.cs:** Added IsBlittable\(Type\) helper to identify types safe for block copying \(Primitives + Guid + Vector types\).
    - **SerializerEmitter.cs:** Implemented EmitArrayWriter which uses AsSpan\(\) and MemoryMarshal.AsBytes\(\) to write blittable arrays as a block, bypassing element-wise loop. Implemented EmitArraySizer for fast sizing.
    - **DeserializerEmitter.cs:** Implemented EmitArrayReader which uses ReadFixedBytes and MemoryMarshal.Cast\(\) to read blittable arrays directly into memory.
    - **IdlEmitter.cs:** Logic added to map T\[\] arrays to IDL sequence&lt;T&gt;.
3. **System.Numerics Support \(Task 4\):**

    - **New Types:** Added support for Vector2, Vector3, Vector4, Quaternion, and Matrix4x4.
    - **Components Updated:** TypeMapper, CdrWriter, CdrReader, CdrSizer, IdlEmitter \(mapped to `float` arrays\), SerializerEmitter, and DeserializerEmitter.
    - **Optimization:** these types are marked IsBlittable so generic lists/arrays of Vectors are automatically block-copied.
4. **Alignment Fixes:**

    - Updated GetAlignment logic in both Emitters to correctly handle standard DDS alignments \(`double`/`int64` aligned to 8, Vectors aligned to 4\).