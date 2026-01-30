# Native API Analysis for Extended Features

This document analyzes the native Cyclone DDS C API requirements for implementing new features in the C# bindings. It distinguishes between features that are fully supported by the local native library and those that require alternative implementation strategies based on the source code analysis.

## 1. Read vs. Take (Non-Destructive Read)

To support non-destructive reading (peeking at data without removing it from the history cache), we need to expose `dds_readcdr`. This is the counterpart to the already implemented `dds_takecdr`.

### Native API Found
*   **Function**: `dds_readcdr`
*   **File**: `src/core/ddsc/include/dds/dds.h`
*   **Signature**:
    ```c
    DDS_EXPORT dds_return_t
    dds_readcdr(
      dds_entity_t reader_or_condition,
      struct ddsi_serdata **buf,
      uint32_t maxs,
      dds_sample_info_t *si,
      uint32_t mask);
    ```

## 2. Async/Await (Listeners)

To implement `WaitDataAsync`, we need to attach a `dds_listener` to the `DdsReader` that triggers a callback when `DDS_DATA_AVAILABLE_STATUS` is set.

### Native APIs Found

#### Listener Creation & Destruction
*   **File**: `src/core/ddsc/include/dds/ddsc/dds_public_listener.h`
*   **Signatures**:
    ```c
    // Create a listener with an optional argument (passed to callbacks)
    DDS_EXPORT dds_listener_t* dds_create_listener(void* arg);

    // Delete a listener
    DDS_EXPORT void dds_delete_listener (dds_listener_t *listener);
    ```

#### Setting Callbacks
*   **File**: `src/core/ddsc/include/dds/ddsc/dds_public_listener.h`
*   **Signature**:
    ```c
    // Set the DATA_AVAILABLE callback
    DDS_EXPORT void dds_lset_data_available (dds_listener_t *listener, dds_on_data_available_fn callback);
    ```
    *Note: `dds_on_data_available_fn` is defined as:*
    ```c
    typedef void (*dds_on_data_available_fn) (dds_entity_t reader, void* arg);
    ```

#### Attaching Listener to Entity
*   **File**: `src/core/ddsc/include/dds/dds.h`
*   **Signature**:
    ```c
    // Attach the listener to a reader (or other entity)
    DDS_EXPORT dds_return_t
    dds_set_listener(dds_entity_t entity, const dds_listener_t * listener);
    ```

## 3. Content Filtering

The goal is to filter data at the topic level. The standard DDS approach uses `ContentFilteredTopic` with SQL-like expressions.

### Status: Standard SQL API Missing
The standard function `dds_create_contentfilteredtopic` is **NOT present** in the local source code.
*   **Evidence**: `src/core/ddsc/tests/filter.c` (lines 17-19) explicitly states:
    > *"The (not-too-distant) future will bring content filter expressions in the reader QoS that get parsed at run-time and drop these per-topic filter functions."*
*   **Conclusion**: The local version of Cyclone DDS **does not support SQL-based content filtering**.

### Available API: Callback-Based Filtering
The local library supports a programmatic filtering mechanism where a user-defined callback function determines whether a sample is accepted.

#### Native APIs Found
*   **File**: `src/core/ddsc/include/dds/dds.h`
*   **Functions**:
    ```c
    // Simple filter with argument
    DDS_EXPORT dds_return_t
    dds_set_topic_filter_and_arg(
      dds_entity_t topic,
      dds_topic_filter_arg_fn filter,
      void *arg);

    // Extended filter (allows filtering on sample info, etc.)
    DDS_EXPORT dds_return_t
    dds_set_topic_filter_extended(
      dds_entity_t topic,
      const struct dds_topic_filter *filter);
    ```

#### Data Structures & Callbacks
To use this in C#, we must define a delegate matching the C function pointer signature and marshal it.

*   **Filter Function Signature**:
    ```c
    typedef bool (*dds_topic_filter_sample_arg_fn) (const void * sample, void * arg);
    ```
    *   `sample`: Pointer to the deserialized sample (or `ddsi_serdata` depending on implementation, but usually the sample).
    *   `arg`: The user-provided argument.
    *   `return`: `true` to keep the sample, `false` to discard it.

*   **Extended Filter Struct**:
    ```c
    struct dds_topic_filter {
      enum dds_topic_filter_mode mode;         // e.g., DDS_TOPIC_FILTER_SAMPLE_ARG
      union dds_topic_filter_function_union f; // Union containing the function pointer
      void *arg;                               // User argument
    };
    ```

### Implementation Strategy for C#
Since SQL filtering is unavailable, we must implement **Client-Side Filtering** using the callback API:
1.  **Define Delegate**: Create a C# delegate `delegate bool TopicFilterDelegate(IntPtr sample, IntPtr arg);`.
2.  **Interop**: Use `Marshal.GetFunctionPointerForDelegate` (or `UnmanagedCallersOnly` in .NET 5+) to pass this delegate to `dds_set_topic_filter_and_arg`.
3.  **Managed Filtering**: The C# callback will receive the sample pointer.
    *   *Challenge*: The sample pointer is likely a raw C structure. To filter effectively, we might need to partially deserialize it or use offsets to check specific fields (like "ID > 5").
    *   *Performance*: This incurs a managed/unmanaged transition for *every* sample.

### Writer-Side Filtering
**Not Supported via SQL**. Since the SQL parser is missing, the writer cannot automatically filter based on a reader's subscription string. The filtering happens strictly at the Reader's side (specifically, at the Topic level within the Reader's participant) via the callback.

## 4. Status & Discovery (Events)

To implement typed status events (e.g., `PublicationMatched`, `LivelinessChanged`), we need to access the status structures and getter functions.

### Native APIs Found
*   **File**: `src/core/ddsc/include/dds/ddsc/dds_public_status.h`

#### Status Structures (Verified)
The following structures have been verified against the native source. `dds_instance_handle_t` is `uint64_t`.

*   **Publication Matched**:
    ```c
    typedef struct dds_publication_matched_status {
      uint32_t total_count;
      int32_t total_count_change;
      uint32_t current_count;
      int32_t current_count_change;
      dds_instance_handle_t last_subscription_handle; // uint64_t
    } dds_publication_matched_status_t;
    ```

*   **Subscription Matched**:
    ```c
    typedef struct dds_subscription_matched_status {
      uint32_t total_count;
      int32_t total_count_change;
      uint32_t current_count;
      int32_t current_count_change;
      dds_instance_handle_t last_publication_handle; // uint64_t
    } dds_subscription_matched_status_t;
    ```

*   **Liveliness Changed**:
    ```c
    typedef struct dds_liveliness_changed_status {
      uint32_t alive_count;
      uint32_t not_alive_count;
      int32_t alive_count_change;
      int32_t not_alive_count_change;
      dds_instance_handle_t last_publication_handle; // uint64_t
    } dds_liveliness_changed_status_t;
    ```

#### Status Getters
*   `dds_get_publication_matched_status`
*   `dds_get_subscription_matched_status`
*   `dds_get_liveliness_changed_status`
*   `dds_get_offered_deadline_missed_status`
*   `dds_get_requested_deadline_missed_status`
*   `dds_get_offered_incompatible_qos_status`
*   `dds_get_requested_incompatible_qos_status`
*   `dds_get_sample_lost_status`
*   `dds_get_sample_rejected_status`
*   `dds_get_inconsistent_topic_status`

**Example Signature**:
```c
DDS_EXPORT dds_return_t
dds_get_publication_matched_status (
  dds_entity_t writer,
  dds_publication_matched_status_t * status);
```

## 5. Instance Management (Keyed Lookups)

To implement `LookupInstance` and `TakeInstance`, we need APIs to map keys to instance handles and to read/take specific instances.

### Native APIs Found

#### Instance Handle Type
*   **Native**: `typedef uint64_t dds_instance_handle_t;`
*   **Managed Recommendation**:
    ```csharp
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct DdsInstanceHandle : IEquatable<DdsInstanceHandle>
    {
        public readonly long Value; // Matches 64-bit size. Signedness mismatch is acceptable for opaque handles.
        public static readonly DdsInstanceHandle Nil = new DdsInstanceHandle(0);
        public bool IsNil => Value == 0;
        public DdsInstanceHandle(long value) => Value = value;
        public bool Equals(DdsInstanceHandle other) => Value == other.Value;
        public override string ToString() => $"Handle(0x{Value:X})";
    }
    ```

#### GUID Type
*   **Native**: `typedef struct dds_builtintopic_guid { uint8_t v[16]; } dds_guid_t;`
*   **Managed Recommendation**:
    ```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct DdsGuid : IEquatable<DdsGuid>
    {
        public long High;
        public long Low;
        public bool Equals(DdsGuid other) => High == other.High && Low == other.Low;
        public override int GetHashCode() => HashCode.Combine(High, Low);
        public override string ToString() => $"{High:X16}{Low:X16}";
    }
    ```
    *Note: This maps the 16-byte byte array to two 64-bit integers for efficient copying and comparison, which is binary-compatible.*

#### Lookup Instance
*   **File**: `src/core/ddsc/include/dds/dds.h`
*   **Signature**:
    ```c
    // Get instance handle from a sample with key fields set
    DDS_EXPORT dds_instance_handle_t
    dds_lookup_instance(dds_entity_t entity, const void *data);
    ```

#### Read/Take Specific Instance
*   **File**: `src/core/ddsc/include/dds/dds.h`
*   **Signatures**:
    ```c
    // Read specific instance
    DDS_EXPORT dds_return_t
    dds_readcdr_instance (
        dds_entity_t reader_or_condition,
        struct ddsi_serdata **buf,
        uint32_t maxs,
        dds_sample_info_t *si,
        dds_instance_handle_t handle,
        uint32_t mask);

    // Take specific instance
    DDS_EXPORT dds_return_t
    dds_takecdr_instance (
        dds_entity_t reader_or_condition,
        struct ddsi_serdata **buf,
        uint32_t maxs,
        dds_sample_info_t *si,
        dds_instance_handle_t handle,
        uint32_t mask);
    ```
