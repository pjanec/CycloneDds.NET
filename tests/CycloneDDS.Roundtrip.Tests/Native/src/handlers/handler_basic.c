#include "type_registry.h"
#include "roundtrip_test.h"
#include <stdio.h>
#include <string.h>
#include <math.h>

// Helper macro for sequence allocation
// Assumes standard CycloneDDS sequence layout
#define ALLOC_SEQ(seq, count, type) do { \
    (seq)._maximum = (count); \
    (seq)._length = (count); \
    (seq)._buffer = dds_alloc((count) * sizeof(type)); \
    (seq)._release = true; \
} while(0)

// ============================================================================
// AllPrimitives Handler
// ============================================================================

void* alloc_AllPrimitives() {
    return dds_alloc(sizeof(RoundtripTests_AllPrimitives));
}

void free_AllPrimitives(void* sample) {
    dds_sample_free(sample, &RoundtripTests_AllPrimitives_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_AllPrimitives() {
    return &RoundtripTests_AllPrimitives_desc;
}

void fill_AllPrimitives(void* sample, int seed) {
    RoundtripTests_AllPrimitives* data = (RoundtripTests_AllPrimitives*)sample;
    
    // Logic matches C# DataGenerator.cs
    
    // Key (int) -> seed
    data->id = seed;
    
    // Boolean -> seed % 2 == 0
    // C# 'true' if even.
    data->bool_field = (seed % 2) == 0;
    
    // Character -> 'A' + (seed % 26) -- Note: C# uses byte for char if not specified? 
    // DataGenerator says: if (type == typeof(char)) return (char)('A' + (seed % 26));
    // AllPrimitives.Char_field is 'byte' in C#? 
    // TestMessageTypes.cs: public byte Char_field { get; set; }
    // Wait. In IDL it is `char`. In C# IDL mapping `char` maps to `byte`? or `char`?
    // In Standard C# mapping: char (8-bit) maps to byte.
    // DataGenerator handles 'byte' as seed % 256.
    // If Char_field is byte in C#, DataGenerator uses 'byte' logic!
    data->char_field = (char)(seed % 256);
    
    // Octet -> byte -> seed % 256
    data->octet_field = (uint8_t)(seed % 256);
    
    // Short -> seed % 10000
    data->short_field = (int16_t)(seed % 10000);
    
    // Ushort -> seed % 10000
    data->ushort_field = (uint16_t)(seed % 10000);
    
    // Long (int) -> seed
    data->long_field = (int32_t)seed;
    
    // Ulong (uint) -> seed
    data->ulong_field = (uint32_t)seed;
    
    // Llong (long) -> seed * 1000
    data->llong_field = (int64_t)seed * 1000L;
    
    // Ullong (ulong) -> seed * 1000
    data->ullong_field = (uint64_t)seed * 1000ULL;
    
    // Floating-point
    // C#: seed + 0.5f
    data->float_field = (float)seed + 0.5f;
    
    // Double
    // C#: seed + 0.25
    data->double_field = (double)seed + 0.25;
}

bool compare_AllPrimitives(const void* a, const void* b) {
    const RoundtripTests_AllPrimitives* x = (const RoundtripTests_AllPrimitives*)a;
    const RoundtripTests_AllPrimitives* y = (const RoundtripTests_AllPrimitives*)b;
    
    #define CHECK_FIELD(field) \
        if (x->field != y->field) { \
            printf("[MISMATCH] AllPrimitives." #field ": "); \
            return false; \
        }
    
    #define CHECK_FLOAT(field, epsilon) \
        if (fabs((double)(x->field) - (double)(y->field)) > epsilon) { \
            printf("[MISMATCH] AllPrimitives." #field ": %.6f != %.6f\n", \
                   (double)(x->field), (double)(y->field)); \
            return false; \
        }
    
    CHECK_FIELD(id)
    CHECK_FIELD(bool_field)
    CHECK_FIELD(char_field)
    CHECK_FIELD(octet_field)
    CHECK_FIELD(short_field)
    CHECK_FIELD(ushort_field)
    CHECK_FIELD(long_field)
    CHECK_FIELD(ulong_field)
    CHECK_FIELD(llong_field)
    CHECK_FIELD(ullong_field)
    CHECK_FLOAT(float_field, 0.0001)
    CHECK_FLOAT(double_field, 0.0000001)
    
    #undef CHECK_FIELD
    #undef CHECK_FLOAT
    
    return true;
}

// ============================================================================
// CompositeKey Handler
// ============================================================================

void* alloc_CompositeKey() {
    return dds_alloc(sizeof(RoundtripTests_CompositeKey));
}

void free_CompositeKey(void* sample) {
    dds_sample_free(sample, &RoundtripTests_CompositeKey_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_CompositeKey() {
    return &RoundtripTests_CompositeKey_desc;
}

void fill_CompositeKey(void* sample, int seed) {
    RoundtripTests_CompositeKey* data = (RoundtripTests_CompositeKey*)sample;
    
    // Keys
    snprintf(data->region, sizeof(data->region), "Region_%d", seed);
    data->zone = (int32_t)((seed + 1) * 31);
    data->sector = (int16_t)((seed + 2) * 7);
    
    // Other fields
    snprintf(data->name, sizeof(data->name), "Name_%d", seed + 10);
    data->value = (double)((seed + 20) * 3.14159);
    data->priority = (RoundtripTests_Priority)((seed + 3) % 4); // 0-3
}

bool compare_CompositeKey(const void* a, const void* b) {
    const RoundtripTests_CompositeKey* x = (const RoundtripTests_CompositeKey*)a;
    const RoundtripTests_CompositeKey* y = (const RoundtripTests_CompositeKey*)b;
    
    if (strcmp(x->region, y->region) != 0) {
        printf("[MISMATCH] CompositeKey.region: '%s' != '%s'\n", x->region, y->region);
        return false;
    }
    
    if (x->zone != y->zone) {
        printf("[MISMATCH] CompositeKey.zone: %d != %d\n", x->zone, y->zone);
        return false;
    }
    
    if (x->sector != y->sector) {
        printf("[MISMATCH] CompositeKey.sector: %d != %d\n", x->sector, y->sector);
        return false;
    }
    
    if (strcmp(x->name, y->name) != 0) {
        printf("[MISMATCH] CompositeKey.name: '%s' != '%s'\n", x->name, y->name);
        return false;
    }
    
    if (fabs(x->value - y->value) > 0.0000001) {
        printf("[MISMATCH] CompositeKey.value: %.6f != %.6f\n", x->value, y->value);
        return false;
    }
    
    if (x->priority != y->priority) {
        printf("[MISMATCH] CompositeKey.priority: %d != %d\n", x->priority, y->priority);
        return false;
    }
    
    return true;
}

// ============================================================================
// NestedKeyTopic Handler
// ============================================================================

void* alloc_NestedKeyTopic() {
    return dds_alloc(sizeof(RoundtripTests_NestedKeyTopic));
}

void free_NestedKeyTopic(void* sample) {
    dds_sample_free(sample, &RoundtripTests_NestedKeyTopic_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_NestedKeyTopic() {
    return &RoundtripTests_NestedKeyTopic_desc;
}

void fill_NestedKeyTopic(void* sample, int seed) {
    RoundtripTests_NestedKeyTopic* data = (RoundtripTests_NestedKeyTopic*)sample;
    
    // Nested key
    data->location.building = (int32_t)seed;
    data->location.floor = (int16_t)((seed % 10) + 1);
    data->location.room = (int32_t)((seed + 100) * 31);
    
    // Other fields
    snprintf(data->description, sizeof(data->description), "Room_Desc_%d", seed);
    data->temperature = (double)((seed + 50) * 0.5);
    
    data->last_updated.seconds = (int64_t)(seed + 1000000);
    data->last_updated.nanoseconds = (uint32_t)((seed * 1000) % 1000000000);
}

bool compare_NestedKeyTopic(const void* a, const void* b) {
    const RoundtripTests_NestedKeyTopic* x = (const RoundtripTests_NestedKeyTopic*)a;
    const RoundtripTests_NestedKeyTopic* y = (const RoundtripTests_NestedKeyTopic*)b;
    
    if (x->location.building != y->location.building) {
        printf("[MISMATCH] NestedKeyTopic.location.building\n");
        return false;
    }
    
    if (x->location.floor != y->location.floor) {
        printf("[MISMATCH] NestedKeyTopic.location.floor\n");
        return false;
    }
    
    if (x->location.room != y->location.room) {
        printf("[MISMATCH] NestedKeyTopic.location.room\n");
        return false;
    }
    
    if (strcmp(x->description, y->description) != 0) {
        printf("[MISMATCH] NestedKeyTopic.description\n");
        return false;
    }
    
    if (fabs(x->temperature - y->temperature) > 0.0001) {
        printf("[MISMATCH] NestedKeyTopic.temperature\n");
        return false;
    }
    
    if (x->last_updated.seconds != y->last_updated.seconds) {
        printf("[MISMATCH] NestedKeyTopic.last_updated.seconds\n");
        return false;
    }
    
    if (x->last_updated.nanoseconds != y->last_updated.nanoseconds) {
        printf("[MISMATCH] NestedKeyTopic.last_updated.nanoseconds\n");
        return false;
    }
    
    return true;
}

// ============================================================================
// SequenceTopic Handler
// ============================================================================

void* alloc_SequenceTopic() {
    return dds_alloc(sizeof(RoundtripTests_SequenceTopic));
}

void free_SequenceTopic(void* sample) {
    dds_sample_free(sample, &RoundtripTests_SequenceTopic_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_SequenceTopic() {
    return &RoundtripTests_SequenceTopic_desc;
}

void fill_SequenceTopic(void* sample, int seed) {
    RoundtripTests_SequenceTopic* data = (RoundtripTests_SequenceTopic*)sample;
    
    data->id = seed;
    
    // Determine sequence lengths deterministically
    uint32_t base_len = ((seed % 5) + 1); // 1-5 elements
    
    // unbounded_long_seq
    ALLOC_SEQ(data->unbounded_long_seq, base_len, int32_t);
    for (uint32_t i = 0; i < base_len; i++) {
        data->unbounded_long_seq._buffer[i] = (int32_t)((seed + i + 10) * 31);
    }
    
    // bounded_long_seq (max 10)
    uint32_t bounded_len = (base_len > 10) ? 10 : base_len;
    ALLOC_SEQ(data->bounded_long_seq, bounded_len, int32_t);
    for (uint32_t i = 0; i < bounded_len; i++) {
        data->bounded_long_seq._buffer[i] = (int32_t)((seed + i + 20) * 31);
    }
    
    // unbounded_double_seq
    ALLOC_SEQ(data->unbounded_double_seq, base_len, double);
    for (uint32_t i = 0; i < base_len; i++) {
        data->unbounded_double_seq._buffer[i] = (double)((seed + i + 30) * 3.14);
    }
    
    // string_seq
    ALLOC_SEQ(data->string_seq, base_len, RoundtripTests_BoundedString16);
    for (uint32_t i = 0; i < base_len; i++) {
        // _buffer is array of char[17], so memory is already contiguous
        snprintf(data->string_seq._buffer[i], 17, "Str_%d_%u", seed, i);
    }
    
    // color_seq
    ALLOC_SEQ(data->color_seq, base_len, RoundtripTests_Color);
    for (uint32_t i = 0; i < base_len; i++) {
        data->color_seq._buffer[i] = (RoundtripTests_Color)((seed + i) % 4);
    }
}

bool compare_SequenceTopic(const void* a, const void* b) {
    const RoundtripTests_SequenceTopic* x = (const RoundtripTests_SequenceTopic*)a;
    const RoundtripTests_SequenceTopic* y = (const RoundtripTests_SequenceTopic*)b;
    
    if (x->id != y->id) {
        printf("[MISMATCH] SequenceTopic.id\n");
        return false;
    }
    
    // Compare sequences
    #define COMPARE_SEQ(seq, type_fmt) \
        if (x->seq._length != y->seq._length) { \
            printf("[MISMATCH] SequenceTopic." #seq "._length: %u != %u\n", \
                   x->seq._length, y->seq._length); \
            return false; \
        } \
        for (uint32_t i = 0; i < x->seq._length; i++) { \
            if (x->seq._buffer[i] != y->seq._buffer[i]) { \
                printf("[MISMATCH] SequenceTopic." #seq "[%u]\n", i); \
                return false; \
            } \
        }
    
    COMPARE_SEQ(unbounded_long_seq, "%d")
    COMPARE_SEQ(bounded_long_seq, "%d")
    
    // Double sequence (with tolerance)
    if (x->unbounded_double_seq._length != y->unbounded_double_seq._length) {
        printf("[MISMATCH] SequenceTopic.unbounded_double_seq._length\n");
        return false;
    }
    for (uint32_t i = 0; i < x->unbounded_double_seq._length; i++) {
        if (fabs(x->unbounded_double_seq._buffer[i] - y->unbounded_double_seq._buffer[i]) > 0.0001) {
            printf("[MISMATCH] SequenceTopic.unbounded_double_seq[%u]\n", i);
            return false;
        }
    }
    
    // String sequence
    if (x->string_seq._length != y->string_seq._length) {
        printf("[MISMATCH] SequenceTopic.string_seq._length\n");
        return false;
    }
    for (uint32_t i = 0; i < x->string_seq._length; i++) {
        if (strcmp(x->string_seq._buffer[i], y->string_seq._buffer[i]) != 0) {
            printf("[MISMATCH] SequenceTopic.string_seq[%u]: '%s' != '%s'\n", 
                   i, x->string_seq._buffer[i], y->string_seq._buffer[i]);
            return false;
        }
    }
    
    COMPARE_SEQ(color_seq, "%d")
    
    #undef COMPARE_SEQ
    
    return true;
}
