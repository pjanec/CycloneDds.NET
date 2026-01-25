#include "atomic_tests.h"
#include "test_registry.h"
#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include "dds/dds.h" 

// --- Helper Macros ---
#define DEFINE_HANDLER(TYPE, NAME) \
    const topic_handler_t NAME##_handler = { \
        .name = "AtomicTests::" #TYPE, \
        .descriptor = &AtomicTests_##TYPE##_desc, \
        .generate = generate_##TYPE, \
        .validate = validate_##TYPE, \
        .size = sizeof(AtomicTests_##TYPE) \
    }

// ----------------------------------------------------------------------------
// Primitives
// ----------------------------------------------------------------------------

// --- BooleanTopic ---
static void generate_BooleanTopic(void* data, int seed) {
    AtomicTests_BooleanTopic* msg = (AtomicTests_BooleanTopic*)data;
    msg->id = seed;
    msg->value = (seed % 2) != 0;
}

static int validate_BooleanTopic(void* data, int seed) {
    AtomicTests_BooleanTopic* msg = (AtomicTests_BooleanTopic*)data;
    if (msg->id != seed) return -1;
    bool expected = (seed % 2) != 0;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(BooleanTopic, boolean_topic);

// --- Int32Topic ---
static void generate_Int32Topic(void* data, int seed) {
    AtomicTests_Int32Topic* msg = (AtomicTests_Int32Topic*)data;
    msg->id = seed;
    msg->value = (int32_t)((seed * 1664525L) + 1013904223L);
}

static int validate_Int32Topic(void* data, int seed) {
    AtomicTests_Int32Topic* msg = (AtomicTests_Int32Topic*)data;
    if (msg->id != seed) return -1;
    int32_t expected = (int32_t)((seed * 1664525L) + 1013904223L);
    if (msg->value != expected) {
        fprintf(stderr, "Int32Topic mismatch: expected %d, got %d\n", expected, msg->value);
        return -1;
    }
    return 0;
}
DEFINE_HANDLER(Int32Topic, int32_topic);

// --- CharTopic ---
static void generate_CharTopic(void* data, int seed) {
    AtomicTests_CharTopic* msg = (AtomicTests_CharTopic*)data;
    msg->id = seed;
    msg->value = (char)('A' + (seed % 26));
}
static int validate_CharTopic(void* data, int seed) {
    AtomicTests_CharTopic* msg = (AtomicTests_CharTopic*)data;
    if (msg->id != seed) return -1;
    char expected = (char)('A' + (seed % 26));
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(CharTopic, char_topic);

// --- OctetTopic ---
static void generate_OctetTopic(void* data, int seed) {
    AtomicTests_OctetTopic* msg = (AtomicTests_OctetTopic*)data;
    msg->id = seed;
    msg->value = (uint8_t)(seed & 0xFF);
}
static int validate_OctetTopic(void* data, int seed) {
    AtomicTests_OctetTopic* msg = (AtomicTests_OctetTopic*)data;
    if (msg->id != seed) return -1;
    uint8_t expected = (uint8_t)(seed & 0xFF);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(OctetTopic, octet_topic);

// --- Int16Topic ---
static void generate_Int16Topic(void* data, int seed) {
    AtomicTests_Int16Topic* msg = (AtomicTests_Int16Topic*)data;
    msg->id = seed;
    msg->value = (int16_t)(seed * 31);
}
static int validate_Int16Topic(void* data, int seed) {
    AtomicTests_Int16Topic* msg = (AtomicTests_Int16Topic*)data;
    if (msg->id != seed) return -1;
    int16_t expected = (int16_t)(seed * 31);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(Int16Topic, int16_topic);

// --- UInt16Topic ---
static void generate_UInt16Topic(void* data, int seed) {
    AtomicTests_UInt16Topic* msg = (AtomicTests_UInt16Topic*)data;
    msg->id = seed;
    msg->value = (uint16_t)(seed * 31);
}
static int validate_UInt16Topic(void* data, int seed) {
    AtomicTests_UInt16Topic* msg = (AtomicTests_UInt16Topic*)data;
    if (msg->id != seed) return -1;
    uint16_t expected = (uint16_t)(seed * 31);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt16Topic, uint16_topic);

// --- UInt32Topic ---
static void generate_UInt32Topic(void* data, int seed) {
    AtomicTests_UInt32Topic* msg = (AtomicTests_UInt32Topic*)data;
    msg->id = seed;
    msg->value = (uint32_t)((seed * 1664525L) + 1013904223L);
}
static int validate_UInt32Topic(void* data, int seed) {
    AtomicTests_UInt32Topic* msg = (AtomicTests_UInt32Topic*)data;
    if (msg->id != seed) return -1;
    uint32_t expected = (uint32_t)((seed * 1664525L) + 1013904223L);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt32Topic, uint32_topic);

// --- Int64Topic ---
static void generate_Int64Topic(void* data, int seed) {
    AtomicTests_Int64Topic* msg = (AtomicTests_Int64Topic*)data;
    msg->id = seed;
    msg->value = (int64_t)seed * 1000000LL;
}
static int validate_Int64Topic(void* data, int seed) {
    AtomicTests_Int64Topic* msg = (AtomicTests_Int64Topic*)data;
    if (msg->id != seed) return -1;
    int64_t expected = (int64_t)seed * 1000000LL;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(Int64Topic, int64_topic);

// --- UInt64Topic ---
static void generate_UInt64Topic(void* data, int seed) {
    AtomicTests_UInt64Topic* msg = (AtomicTests_UInt64Topic*)data;
    msg->id = seed;
    msg->value = (uint64_t)seed * 1000000ULL;
}
static int validate_UInt64Topic(void* data, int seed) {
    AtomicTests_UInt64Topic* msg = (AtomicTests_UInt64Topic*)data;
    if (msg->id != seed) return -1;
    uint64_t expected = (uint64_t)seed * 1000000ULL;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt64Topic, uint64_topic);

// --- Float32Topic ---
static void generate_Float32Topic(void* data, int seed) {
    AtomicTests_Float32Topic* msg = (AtomicTests_Float32Topic*)data;
    msg->id = seed;
    msg->value = (float)(seed * 3.14159f);
}
static int validate_Float32Topic(void* data, int seed) {
    AtomicTests_Float32Topic* msg = (AtomicTests_Float32Topic*)data;
    if (msg->id != seed) return -1;
    float expected = (float)(seed * 3.14159f);
    if (fabsf(msg->value - expected) > 0.0001f) return -1;
    return 0;
}
DEFINE_HANDLER(Float32Topic, float32_topic);

// --- Float64Topic ---
static void generate_Float64Topic(void* data, int seed) {
    AtomicTests_Float64Topic* msg = (AtomicTests_Float64Topic*)data;
    msg->id = seed;
    msg->value = (double)(seed * 3.14159265359);
}
static int validate_Float64Topic(void* data, int seed) {
    AtomicTests_Float64Topic* msg = (AtomicTests_Float64Topic*)data;
    if (msg->id != seed) return -1;
    double expected = (double)(seed * 3.14159265359);
    if (fabs(msg->value - expected) > 0.000001) return -1;
    return 0;
}
DEFINE_HANDLER(Float64Topic, float64_topic);

// ----------------------------------------------------------------------------
// Strings
// ----------------------------------------------------------------------------

// --- StringBounded32Topic ---
static void generate_StringBounded32Topic(void* data, int seed) {
    AtomicTests_StringBounded32Topic* msg = (AtomicTests_StringBounded32Topic*)data;
    msg->id = seed;
    char buffer[32];
    snprintf(buffer, 32, "Str_%d", seed);
    // Fixed size array in struct
    strncpy(msg->value, buffer, 32);
    msg->value[32] = '\0'; // Ensure null termination (although array is 33)
}

static int validate_StringBounded32Topic(void* data, int seed) {
    AtomicTests_StringBounded32Topic* msg = (AtomicTests_StringBounded32Topic*)data;
    if (msg->id != seed) return -1;
    char buffer[32];
    snprintf(buffer, 32, "Str_%d", seed);
    
    // Fixed size array comparison
    if (strncmp(msg->value, buffer, 32) != 0) {
        fprintf(stderr, "StringBounded32Topic mismatch: expected '%s', got '%s'\n", buffer, msg->value);
        return -1;
    }
    return 0;
}
DEFINE_HANDLER(StringBounded32Topic, string_bounded_32_topic);

// --- StringUnboundedTopic ---
static void generate_StringUnboundedTopic(void* data, int seed) {
    AtomicTests_StringUnboundedTopic* msg = (AtomicTests_StringUnboundedTopic*)data;
    msg->id = seed;
    char buffer[64];
    snprintf(buffer, 64, "StrUnbound_%d", seed);
    msg->value = dds_string_dup(buffer);
}

static int validate_StringUnboundedTopic(void* data, int seed) {
    AtomicTests_StringUnboundedTopic* msg = (AtomicTests_StringUnboundedTopic*)data;
    if (msg->id != seed) return -1;
    char buffer[64];
    snprintf(buffer, 64, "StrUnbound_%d", seed);
    if (strcmp(msg->value, buffer) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(StringUnboundedTopic, string_unbounded_topic);

// --- StringBounded256Topic ---
static void generate_StringBounded256Topic(void* data, int seed) {
    AtomicTests_StringBounded256Topic* msg = (AtomicTests_StringBounded256Topic*)data;
    msg->id = seed;
    char buffer[256];
    snprintf(buffer, 256, "StrBound256_%d", seed);
    strncpy(msg->value, buffer, 256);
    msg->value[256] = '\0';
}

static int validate_StringBounded256Topic(void* data, int seed) {
    AtomicTests_StringBounded256Topic* msg = (AtomicTests_StringBounded256Topic*)data;
    if (msg->id != seed) return -1;
    char buffer[256];
    snprintf(buffer, 256, "StrBound256_%d", seed);
    if (strncmp(msg->value, buffer, 256) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(StringBounded256Topic, string_bounded_256_topic);

// ----------------------------------------------------------------------------
// Enums
// ----------------------------------------------------------------------------

// --- EnumTopic ---
static void generate_EnumTopic(void* data, int seed) {
    AtomicTests_EnumTopic* msg = (AtomicTests_EnumTopic*)data;
    msg->id = seed;
    msg->value = (AtomicTests_SimpleEnum)(seed % 3);
}

static int validate_EnumTopic(void* data, int seed) {
    AtomicTests_EnumTopic* msg = (AtomicTests_EnumTopic*)data;
    if (msg->id != seed) return -1;
    AtomicTests_SimpleEnum expected = (AtomicTests_SimpleEnum)(seed % 3);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(EnumTopic, enum_topic);

// --- ColorEnumTopic ---
static void generate_ColorEnumTopic(void* data, int seed) {
    AtomicTests_ColorEnumTopic* msg = (AtomicTests_ColorEnumTopic*)data;
    msg->id = seed;
    msg->color = (AtomicTests_ColorEnum)(seed % 6);
}

static int validate_ColorEnumTopic(void* data, int seed) {
    AtomicTests_ColorEnumTopic* msg = (AtomicTests_ColorEnumTopic*)data;
    if (msg->id != seed) return -1;
    AtomicTests_ColorEnum expected = (AtomicTests_ColorEnum)(seed % 6);
    if (msg->color != expected) return -1;
    return 0;
}
DEFINE_HANDLER(ColorEnumTopic, color_enum_topic);

// ----------------------------------------------------------------------------
// Arrays
// ----------------------------------------------------------------------------

// --- ArrayInt32Topic ---
static void generate_ArrayInt32Topic(void* data, int seed) {
    AtomicTests_ArrayInt32Topic* msg = (AtomicTests_ArrayInt32Topic*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = seed + i;
    }
}

static int validate_ArrayInt32Topic(void* data, int seed) {
    AtomicTests_ArrayInt32Topic* msg = (AtomicTests_ArrayInt32Topic*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        if (msg->values[i] != (seed + i)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayInt32Topic, array_int32_topic);

// --- ArrayFloat64Topic ---
static void generate_ArrayFloat64Topic(void* data, int seed) {
    AtomicTests_ArrayFloat64Topic* msg = (AtomicTests_ArrayFloat64Topic*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = (double)(seed + i) * 1.1;
    }
}

static int validate_ArrayFloat64Topic(void* data, int seed) {
    AtomicTests_ArrayFloat64Topic* msg = (AtomicTests_ArrayFloat64Topic*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        double expected = (double)(seed + i) * 1.1;
        if (fabs(msg->values[i] - expected) > 0.0001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayFloat64Topic, array_float64_topic);

// --- ArrayStringTopic ---
static void generate_ArrayStringTopic(void* data, int seed) {
    AtomicTests_ArrayStringTopic* msg = (AtomicTests_ArrayStringTopic*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        char buffer[16];
        snprintf(buffer, 16, "S_%d_%d", seed, i);
        strncpy(msg->names[i], buffer, 16);
        // msg->names[i][16] = '\0'; // Implicit if declared size 17
    }
}

static int validate_ArrayStringTopic(void* data, int seed) {
    AtomicTests_ArrayStringTopic* msg = (AtomicTests_ArrayStringTopic*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        char buffer[16];
        snprintf(buffer, 16, "S_%d_%d", seed, i);
        if (strncmp(msg->names[i], buffer, 16) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayStringTopic, array_string_topic);

// --- ArrayInt32TopicAppendable ---
static void generate_ArrayInt32TopicAppendable(void* data, int seed) {
    AtomicTests_ArrayInt32TopicAppendable* msg = (AtomicTests_ArrayInt32TopicAppendable*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = seed + i;
    }
}
static int validate_ArrayInt32TopicAppendable(void* data, int seed) {
    AtomicTests_ArrayInt32TopicAppendable* msg = (AtomicTests_ArrayInt32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        if (msg->values[i] != seed + i) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayInt32TopicAppendable, array_int32_topic_appendable);

// --- ArrayFloat64TopicAppendable ---
static void generate_ArrayFloat64TopicAppendable(void* data, int seed) {
    AtomicTests_ArrayFloat64TopicAppendable* msg = (AtomicTests_ArrayFloat64TopicAppendable*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = (double)(seed + i) * 1.1;
    }
}
static int validate_ArrayFloat64TopicAppendable(void* data, int seed) {
    AtomicTests_ArrayFloat64TopicAppendable* msg = (AtomicTests_ArrayFloat64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        double expected = (double)(seed + i) * 1.1;
        if (fabs(msg->values[i] - expected) > 0.0001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayFloat64TopicAppendable, array_float64_topic_appendable);

// --- ArrayStringTopicAppendable ---
static void generate_ArrayStringTopicAppendable(void* data, int seed) {
    AtomicTests_ArrayStringTopicAppendable* msg = (AtomicTests_ArrayStringTopicAppendable*)data;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        char buffer[16];
        snprintf(buffer, 16, "S_%d_%d", seed, i);
        strncpy(msg->names[i], buffer, 16);
    }
}
static int validate_ArrayStringTopicAppendable(void* data, int seed) {
    AtomicTests_ArrayStringTopicAppendable* msg = (AtomicTests_ArrayStringTopicAppendable*)data;
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        char buffer[16];
        snprintf(buffer, 16, "S_%d_%d", seed, i);
        if (strncmp(msg->names[i], buffer, 16) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayStringTopicAppendable, array_string_topic_appendable);

// ----------------------------------------------------------------------------
// Sequences
// ----------------------------------------------------------------------------

// --- SequenceInt32Topic ---
static void generate_SequenceInt32Topic(void* data, int seed) {
    AtomicTests_SequenceInt32Topic* msg = (AtomicTests_SequenceInt32Topic*)data;
    msg->id = seed;
    
    uint32_t len = (seed % 6);
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true; 
    
    if (len > 0) {
        msg->values._buffer = dds_alloc(sizeof(int32_t) * len);
        for (uint32_t i = 0; i < len; i++) {
            msg->values._buffer[i] = (int32_t)((seed + i) * 31);
        }
    } else {
        msg->values._buffer = NULL;
    }
}

static int validate_SequenceInt32Topic(void* data, int seed) {
    AtomicTests_SequenceInt32Topic* msg = (AtomicTests_SequenceInt32Topic*)data;
    if (msg->id != seed) return -1;
    
    uint32_t expected_len = (seed % 6);
    if (msg->values._length != expected_len) {
        fprintf(stderr, "Seq len mismatch: expected %d, got %d\n", expected_len, msg->values._length);
        return -1;
    }
    
    for (uint32_t i = 0; i < expected_len; i++) {
        int32_t expected = (int32_t)((seed + i) * 31);
        if (msg->values._buffer[i] != expected) {
            fprintf(stderr, "Seq elem[%d] mismatch: expected %d, got %d\n", i, expected, msg->values._buffer[i]);
            return -1;
        }
    }
    return 0;
}
DEFINE_HANDLER(SequenceInt32Topic, sequence_int32_topic);

// ----------------------------------------------------------------------------
// Unions
// ----------------------------------------------------------------------------

// --- UnionLongDiscTopic ---
// switch(long) { case 1: long; case 2: double; case 3: string; }
static void generate_UnionLongDiscTopic(void* data, int seed) {
    AtomicTests_UnionLongDiscTopic* msg = (AtomicTests_UnionLongDiscTopic*)data;
    msg->id = seed;
    
    int discriminator = (seed % 3) + 1; // 1, 2, 3
    msg->data._d = discriminator;
    
    if (discriminator == 1) {
        msg->data._u.int_value = seed * 100;
    } else if (discriminator == 2) {
        msg->data._u.double_value = seed * 1.5;
    } else if (discriminator == 3) {
        char buffer[64];
        snprintf(buffer, 64, "Union_%d", seed);
        msg->data._u.string_value = dds_string_dup(buffer);
    }
}

static int validate_UnionLongDiscTopic(void* data, int seed) {
    AtomicTests_UnionLongDiscTopic* msg = (AtomicTests_UnionLongDiscTopic*)data;
    if (msg->id != seed) return -1;
    
    int expected_disc = (seed % 3) + 1;
    if (msg->data._d != expected_disc) {
        fprintf(stderr, "Union disc mismatch: expected %d, got %d\n", expected_disc, msg->data._d);
        return -1;
    }
    
    if (expected_disc == 1) {
        if (msg->data._u.int_value != seed * 100) return -1;
    } else if (expected_disc == 2) {
        if (msg->data._u.double_value != (seed * 1.5)) return -1;
    } else if (expected_disc == 3) {
        char buffer[64];
        snprintf(buffer, 64, "Union_%d", seed);
        if (strcmp(msg->data._u.string_value, buffer) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(UnionLongDiscTopic, union_long_disc_topic);

// ============================================================================
// APPENDABLE DUPLICATES HANDLERS
// ============================================================================

// --- BooleanTopicAppendable ---
static void generate_BooleanTopicAppendable(void* data, int seed) {
    AtomicTests_BooleanTopicAppendable* msg = (AtomicTests_BooleanTopicAppendable*)data;
    msg->id = seed;
    msg->value = (seed % 2) != 0;
}

static int validate_BooleanTopicAppendable(void* data, int seed) {
    AtomicTests_BooleanTopicAppendable* msg = (AtomicTests_BooleanTopicAppendable*)data;
    if (msg->id != seed) return -1;
    bool expected = (seed % 2) != 0;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(BooleanTopicAppendable, boolean_topic_appendable);

// --- Int32TopicAppendable ---
static void generate_Int32TopicAppendable(void* data, int seed) {
    AtomicTests_Int32TopicAppendable* msg = (AtomicTests_Int32TopicAppendable*)data;
    msg->id = seed;
    msg->value = (int32_t)((seed * 1664525L) + 1013904223L);
}

static int validate_Int32TopicAppendable(void* data, int seed) {
    AtomicTests_Int32TopicAppendable* msg = (AtomicTests_Int32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    int32_t expected = (int32_t)((seed * 1664525L) + 1013904223L);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(Int32TopicAppendable, int32_topic_appendable);

// --- CharTopicAppendable ---
static void generate_CharTopicAppendable(void* data, int seed) {
    AtomicTests_CharTopicAppendable* msg = (AtomicTests_CharTopicAppendable*)data;
    msg->id = seed;
    msg->value = (char)('A' + (seed % 26));
}
static int validate_CharTopicAppendable(void* data, int seed) {
    AtomicTests_CharTopicAppendable* msg = (AtomicTests_CharTopicAppendable*)data;
    if (msg->id != seed) return -1;
    char expected = (char)('A' + (seed % 26));
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(CharTopicAppendable, char_topic_appendable);

// --- OctetTopicAppendable ---
static void generate_OctetTopicAppendable(void* data, int seed) {
    AtomicTests_OctetTopicAppendable* msg = (AtomicTests_OctetTopicAppendable*)data;
    msg->id = seed;
    msg->value = (uint8_t)(seed & 0xFF);
}
static int validate_OctetTopicAppendable(void* data, int seed) {
    AtomicTests_OctetTopicAppendable* msg = (AtomicTests_OctetTopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint8_t expected = (uint8_t)(seed & 0xFF);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(OctetTopicAppendable, octet_topic_appendable);

// --- Int16TopicAppendable ---
static void generate_Int16TopicAppendable(void* data, int seed) {
    AtomicTests_Int16TopicAppendable* msg = (AtomicTests_Int16TopicAppendable*)data;
    msg->id = seed;
    msg->value = (int16_t)(seed * 31);
}
static int validate_Int16TopicAppendable(void* data, int seed) {
    AtomicTests_Int16TopicAppendable* msg = (AtomicTests_Int16TopicAppendable*)data;
    if (msg->id != seed) return -1;
    int16_t expected = (int16_t)(seed * 31);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(Int16TopicAppendable, int16_topic_appendable);

// --- UInt16TopicAppendable ---
static void generate_UInt16TopicAppendable(void* data, int seed) {
    AtomicTests_UInt16TopicAppendable* msg = (AtomicTests_UInt16TopicAppendable*)data;
    msg->id = seed;
    msg->value = (uint16_t)(seed * 31);
}
static int validate_UInt16TopicAppendable(void* data, int seed) {
    AtomicTests_UInt16TopicAppendable* msg = (AtomicTests_UInt16TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint16_t expected = (uint16_t)(seed * 31);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt16TopicAppendable, uint16_topic_appendable);

// --- UInt32TopicAppendable ---
static void generate_UInt32TopicAppendable(void* data, int seed) {
    AtomicTests_UInt32TopicAppendable* msg = (AtomicTests_UInt32TopicAppendable*)data;
    msg->id = seed;
    msg->value = (uint32_t)((seed * 1664525L) + 1013904223L);
}
static int validate_UInt32TopicAppendable(void* data, int seed) {
    AtomicTests_UInt32TopicAppendable* msg = (AtomicTests_UInt32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t expected = (uint32_t)((seed * 1664525L) + 1013904223L);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt32TopicAppendable, uint32_topic_appendable);

// --- Int64TopicAppendable ---
static void generate_Int64TopicAppendable(void* data, int seed) {
    AtomicTests_Int64TopicAppendable* msg = (AtomicTests_Int64TopicAppendable*)data;
    msg->id = seed;
    msg->value = (int64_t)seed * 1000000LL;
}
static int validate_Int64TopicAppendable(void* data, int seed) {
    AtomicTests_Int64TopicAppendable* msg = (AtomicTests_Int64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    int64_t expected = (int64_t)seed * 1000000LL;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(Int64TopicAppendable, int64_topic_appendable);

// --- UInt64TopicAppendable ---
static void generate_UInt64TopicAppendable(void* data, int seed) {
    AtomicTests_UInt64TopicAppendable* msg = (AtomicTests_UInt64TopicAppendable*)data;
    msg->id = seed;
    msg->value = (uint64_t)seed * 1000000ULL;
}
static int validate_UInt64TopicAppendable(void* data, int seed) {
    AtomicTests_UInt64TopicAppendable* msg = (AtomicTests_UInt64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint64_t expected = (uint64_t)seed * 1000000ULL;
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(UInt64TopicAppendable, uint64_topic_appendable);

// --- Float32TopicAppendable ---
static void generate_Float32TopicAppendable(void* data, int seed) {
    AtomicTests_Float32TopicAppendable* msg = (AtomicTests_Float32TopicAppendable*)data;
    msg->id = seed;
    msg->value = (float)(seed * 3.14159f);
}
static int validate_Float32TopicAppendable(void* data, int seed) {
    AtomicTests_Float32TopicAppendable* msg = (AtomicTests_Float32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    float expected = (float)(seed * 3.14159f);
    if (fabsf(msg->value - expected) > 0.0001f) return -1;
    return 0;
}
DEFINE_HANDLER(Float32TopicAppendable, float32_topic_appendable);

// --- Float64TopicAppendable ---
static void generate_Float64TopicAppendable(void* data, int seed) {
    AtomicTests_Float64TopicAppendable* msg = (AtomicTests_Float64TopicAppendable*)data;
    msg->id = seed;
    msg->value = (double)(seed * 3.14159265359);
}
static int validate_Float64TopicAppendable(void* data, int seed) {
    AtomicTests_Float64TopicAppendable* msg = (AtomicTests_Float64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    double expected = (double)(seed * 3.14159265359);
    if (fabs(msg->value - expected) > 0.000001) return -1;
    return 0;
}
DEFINE_HANDLER(Float64TopicAppendable, float64_topic_appendable);

// --- StringBounded32TopicAppendable ---
static void generate_StringBounded32TopicAppendable(void* data, int seed) {
    AtomicTests_StringBounded32TopicAppendable* msg = (AtomicTests_StringBounded32TopicAppendable*)data;
    msg->id = seed;
    char buffer[32];
    snprintf(buffer, 32, "Str_%d", seed);
    strncpy(msg->value, buffer, 32);
    msg->value[32] = '\0';
}

static int validate_StringBounded32TopicAppendable(void* data, int seed) {
    AtomicTests_StringBounded32TopicAppendable* msg = (AtomicTests_StringBounded32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    char buffer[32];
    snprintf(buffer, 32, "Str_%d", seed);
    if (strncmp(msg->value, buffer, 32) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(StringBounded32TopicAppendable, string_bounded_32_topic_appendable);

// --- SequenceInt32TopicAppendable ---
static void generate_SequenceInt32TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceInt32TopicAppendable* msg = (AtomicTests_SequenceInt32TopicAppendable*)data;
    msg->id = seed;
    
    uint32_t len = (seed % 6);
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true; 
    
    if (len > 0) {
        msg->values._buffer = dds_alloc(sizeof(int32_t) * len);
        for (uint32_t i = 0; i < len; i++) {
            msg->values._buffer[i] = (int32_t)((seed + i) * 31);
        }
    } else {
        msg->values._buffer = NULL;
    }
}

static int validate_SequenceInt32TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceInt32TopicAppendable* msg = (AtomicTests_SequenceInt32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    
    uint32_t expected_len = (seed % 6);
    if (msg->values._length != expected_len) return -1;
    
    for (uint32_t i = 0; i < expected_len; i++) {
        int32_t expected = (int32_t)((seed + i) * 31);
        if (msg->values._buffer[i] != expected) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceInt32TopicAppendable, sequence_int32_topic_appendable);

// --- UnionLongDiscTopicAppendable ---
static void generate_UnionLongDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionLongDiscTopicAppendable* msg = (AtomicTests_UnionLongDiscTopicAppendable*)data;
    msg->id = seed;
    
    int discriminator = (seed % 3) + 1;
    msg->data._d = discriminator;
    
    if (discriminator == 1) {
        msg->data._u.int_value = seed * 100;
    } else if (discriminator == 2) {
        msg->data._u.double_value = seed * 1.5;
    } else if (discriminator == 3) {
        char buffer[64];
        snprintf(buffer, 64, "Union_%d", seed);
        msg->data._u.string_value = dds_string_dup(buffer);
    }
}

static int validate_UnionLongDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionLongDiscTopicAppendable* msg = (AtomicTests_UnionLongDiscTopicAppendable*)data;
    if (msg->id != seed) return -1;
    
    int expected_disc = (seed % 3) + 1;
    if (msg->data._d != expected_disc) return -1;
    
    if (expected_disc == 1) {
        if (msg->data._u.int_value != seed * 100) return -1;
    } else if (expected_disc == 2) {
        if (msg->data._u.double_value != (seed * 1.5)) return -1;
    } else if (expected_disc == 3) {
        char buffer[64];
        snprintf(buffer, 64, "Union_%d", seed);
        if (strcmp(msg->data._u.string_value, buffer) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(UnionLongDiscTopicAppendable, union_long_disc_topic_appendable);

// --- StringUnboundedTopicAppendable ---
static void generate_StringUnboundedTopicAppendable(void* data, int seed) {
    AtomicTests_StringUnboundedTopicAppendable* msg = (AtomicTests_StringUnboundedTopicAppendable*)data;
    msg->id = seed;
    char buffer[64];
    snprintf(buffer, 64, "StrUnbound_%d", seed);
    msg->value = dds_string_dup(buffer);
}

static int validate_StringUnboundedTopicAppendable(void* data, int seed) {
    AtomicTests_StringUnboundedTopicAppendable* msg = (AtomicTests_StringUnboundedTopicAppendable*)data;
    if (msg->id != seed) return -1;
    char buffer[64];
    snprintf(buffer, 64, "StrUnbound_%d", seed);
    if (strcmp(msg->value, buffer) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(StringUnboundedTopicAppendable, string_unbounded_topic_appendable);

// --- StringBounded256TopicAppendable ---
static void generate_StringBounded256TopicAppendable(void* data, int seed) {
    AtomicTests_StringBounded256TopicAppendable* msg = (AtomicTests_StringBounded256TopicAppendable*)data;
    msg->id = seed;
    char buffer[256];
    snprintf(buffer, 256, "StrBound256_%d", seed);
    strncpy(msg->value, buffer, 256);
    msg->value[256] = '\0';
}

static int validate_StringBounded256TopicAppendable(void* data, int seed) {
    AtomicTests_StringBounded256TopicAppendable* msg = (AtomicTests_StringBounded256TopicAppendable*)data;
    if (msg->id != seed) return -1;
    char buffer[256];
    snprintf(buffer, 256, "StrBound256_%d", seed);
    if (strncmp(msg->value, buffer, 256) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(StringBounded256TopicAppendable, string_bounded_256_topic_appendable);

// --- EnumTopicAppendable ---
static void generate_EnumTopicAppendable(void* data, int seed) {
    AtomicTests_EnumTopicAppendable* msg = (AtomicTests_EnumTopicAppendable*)data;
    msg->id = seed;
    msg->value = (AtomicTests_SimpleEnum)(seed % 3);
}

static int validate_EnumTopicAppendable(void* data, int seed) {
    AtomicTests_EnumTopicAppendable* msg = (AtomicTests_EnumTopicAppendable*)data;
    if (msg->id != seed) return -1;
    AtomicTests_SimpleEnum expected = (AtomicTests_SimpleEnum)(seed % 3);
    if (msg->value != expected) return -1;
    return 0;
}
DEFINE_HANDLER(EnumTopicAppendable, enum_topic_appendable);

// --- ColorEnumTopicAppendable ---
static void generate_ColorEnumTopicAppendable(void* data, int seed) {
    AtomicTests_ColorEnumTopicAppendable* msg = (AtomicTests_ColorEnumTopicAppendable*)data;
    msg->id = seed;
    msg->color = (AtomicTests_ColorEnum)(seed % 6);
}

static int validate_ColorEnumTopicAppendable(void* data, int seed) {
    AtomicTests_ColorEnumTopicAppendable* msg = (AtomicTests_ColorEnumTopicAppendable*)data;
    if (msg->id != seed) return -1;
    AtomicTests_ColorEnum expected = (AtomicTests_ColorEnum)(seed % 6);
    if (msg->color != expected) return -1;
    return 0;
}
DEFINE_HANDLER(ColorEnumTopicAppendable, color_enum_topic_appendable);
// --- Array2DInt32Topic ---
static void generate_Array2DInt32Topic(void* data, int seed) {
    AtomicTests_Array2DInt32Topic* msg = (AtomicTests_Array2DInt32Topic*)data;
    msg->id = seed;
    for (int i = 0; i < 3; i++) {
        for (int j = 0; j < 4; j++) {
            msg->matrix[i][j] = seed + (i * 4) + j;
        }
    }
}
static int validate_Array2DInt32Topic(void* data, int seed) {
    AtomicTests_Array2DInt32Topic* msg = (AtomicTests_Array2DInt32Topic*)data;
    if (msg->id != seed) return -1;
    for (int i = 0; i < 3; i++) {
        for (int j = 0; j < 4; j++) {
            if (msg->matrix[i][j] != (seed + (i * 4) + j)) return -1;
        }
    }
    return 0;
}
DEFINE_HANDLER(Array2DInt32Topic, array_2d_int32_topic);

// --- Array3DInt32Topic ---
static void generate_Array3DInt32Topic(void* data, int seed) {
    AtomicTests_Array3DInt32Topic* msg = (AtomicTests_Array3DInt32Topic*)data;
    msg->id = seed;
    for (int i = 0; i < 2; i++) {
        for (int j = 0; j < 3; j++) {
            for (int k = 0; k < 4; k++) {
                msg->cube[i][j][k] = seed + (i * 12) + (j * 4) + k;
            }
        }
    }
}
static int validate_Array3DInt32Topic(void* data, int seed) {
    AtomicTests_Array3DInt32Topic* msg = (AtomicTests_Array3DInt32Topic*)data;
    if (msg->id != seed) return -1;
    for (int i = 0; i < 2; i++) {
        for (int j = 0; j < 3; j++) {
            for (int k = 0; k < 4; k++) {
                if (msg->cube[i][j][k] != (seed + (i * 12) + (j * 4) + k)) return -1;
            }
        }
    }
    return 0;
}
DEFINE_HANDLER(Array3DInt32Topic, array_3d_int32_topic);

// --- ArrayStructTopic ---
static void generate_ArrayStructTopic(void* data, int seed) {
    AtomicTests_ArrayStructTopic* msg = (AtomicTests_ArrayStructTopic*)data;
    msg->id = seed;
    for(int i = 0; i < 3; i++) {
         msg->points[i].x = (double)(seed + i);
         msg->points[i].y = (double)(seed + i) + 0.5;
    }
}
static int validate_ArrayStructTopic(void* data, int seed) {
    AtomicTests_ArrayStructTopic* msg = (AtomicTests_ArrayStructTopic*)data;
    if (msg->id != seed) return -1;
    for(int i = 0; i < 3; i++) {
        if(fabs(msg->points[i].x - (double)(seed + i)) > 0.000001) return -1;
        if(fabs(msg->points[i].y - ((double)(seed + i) + 0.5)) > 0.000001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(ArrayStructTopic, array_struct_topic);

// ----------------------------------------------------------------------------
// Nested Structures (Phase 4)
// ----------------------------------------------------------------------------

// --- NestedStructTopic ---
static void generate_NestedStructTopic(void* data, int seed) {
    AtomicTests_NestedStructTopic* msg = (AtomicTests_NestedStructTopic*)data;
    msg->id = seed;
    msg->point.x = (double)seed * 1.1;
    msg->point.y = (double)seed * 2.2;
}

static int validate_NestedStructTopic(void* data, int seed) {
    AtomicTests_NestedStructTopic* msg = (AtomicTests_NestedStructTopic*)data;
    if (msg->id != seed) return -1;
    if (fabs(msg->point.x - ((double)seed * 1.1)) > 0.0001) return -1;
    if (fabs(msg->point.y - ((double)seed * 2.2)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(NestedStructTopic, nested_struct_topic);

// --- Nested3DTopic ---
static void generate_Nested3DTopic(void* data, int seed) {
    AtomicTests_Nested3DTopic* msg = (AtomicTests_Nested3DTopic*)data;
    msg->id = seed;
    msg->point.x = (double)seed + 1.0;
    msg->point.y = (double)seed + 2.0;
    msg->point.z = (double)seed + 3.0;
}

static int validate_Nested3DTopic(void* data, int seed) {
    AtomicTests_Nested3DTopic* msg = (AtomicTests_Nested3DTopic*)data;
    if (msg->id != seed) return -1;
    if (fabs(msg->point.x - ((double)seed + 1.0)) > 0.0001) return -1;
    if (fabs(msg->point.y - ((double)seed + 2.0)) > 0.0001) return -1;
    if (fabs(msg->point.z - ((double)seed + 3.0)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(Nested3DTopic, nested_3d_topic);

// --- DoublyNestedTopic ---
static void generate_DoublyNestedTopic(void* data, int seed) {
    AtomicTests_DoublyNestedTopic* msg = (AtomicTests_DoublyNestedTopic*)data;
    msg->id = seed;
    // TopLeft
    msg->box.topLeft.x = (double)seed;
    msg->box.topLeft.y = (double)seed + 1.0;
    // BottomRight
    msg->box.bottomRight.x = (double)seed + 10.0;
    msg->box.bottomRight.y = (double)seed + 11.0;
}

static int validate_DoublyNestedTopic(void* data, int seed) {
    AtomicTests_DoublyNestedTopic* msg = (AtomicTests_DoublyNestedTopic*)data;
    if (msg->id != seed) return -1;
    if (fabs(msg->box.topLeft.x - ((double)seed)) > 0.0001) return -1;
    if (fabs(msg->box.topLeft.y - ((double)seed + 1.0)) > 0.0001) return -1;
    if (fabs(msg->box.bottomRight.x - ((double)seed + 10.0)) > 0.0001) return -1;
    if (fabs(msg->box.bottomRight.y - ((double)seed + 11.0)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(DoublyNestedTopic, doubly_nested_topic);

// --- ComplexNestedTopic ---
static void generate_ComplexNestedTopic(void* data, int seed) {
    AtomicTests_ComplexNestedTopic* msg = (AtomicTests_ComplexNestedTopic*)data;
    msg->id = seed;
    msg->container.count = seed;
    msg->container.radius = (double)seed * 0.5;
    msg->container.center.x = (double)seed + 0.1;
    msg->container.center.y = (double)seed + 0.2;
    msg->container.center.z = (double)seed + 0.3;
}

static int validate_ComplexNestedTopic(void* data, int seed) {
    AtomicTests_ComplexNestedTopic* msg = (AtomicTests_ComplexNestedTopic*)data;
    if (msg->id != seed) return -1;
    if (msg->container.count != seed) return -1;
    if (fabs(msg->container.radius - ((double)seed * 0.5)) > 0.0001) return -1;
    if (fabs(msg->container.center.x - ((double)seed + 0.1)) > 0.0001) return -1;
    if (fabs(msg->container.center.y - ((double)seed + 0.2)) > 0.0001) return -1;
    if (fabs(msg->container.center.z - ((double)seed + 0.3)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(ComplexNestedTopic, complex_nested_topic);

// ============================================================================
// SECTION 9: COMPOSITE KEYS
// ============================================================================

// --- TwoKeyInt32Topic ---
static void generate_TwoKeyInt32Topic(void* data, int seed) {
    AtomicTests_TwoKeyInt32Topic* msg = (AtomicTests_TwoKeyInt32Topic*)data;
    msg->key1 = seed;
    msg->key2 = seed + 1;
    msg->value = (double)seed * 1.5;
}

static int validate_TwoKeyInt32Topic(void* data, int seed) {
    AtomicTests_TwoKeyInt32Topic* msg = (AtomicTests_TwoKeyInt32Topic*)data;
    if (msg->key1 != seed) return -1;
    if (msg->key2 != seed + 1) return -1;
    if (fabs(msg->value - ((double)seed * 1.5)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(TwoKeyInt32Topic, two_key_int32_topic);

// --- TwoKeyStringTopic ---
static void generate_TwoKeyStringTopic(void* data, int seed) {
    AtomicTests_TwoKeyStringTopic* msg = (AtomicTests_TwoKeyStringTopic*)data;
    snprintf(msg->key1, sizeof(msg->key1), "k1_%d", seed);
    snprintf(msg->key2, sizeof(msg->key2), "k2_%d", seed);
    msg->value = (double)seed * 2.5;
}

static int validate_TwoKeyStringTopic(void* data, int seed) {
    AtomicTests_TwoKeyStringTopic* msg = (AtomicTests_TwoKeyStringTopic*)data;
    char expected1[32];
    char expected2[32];
    snprintf(expected1, sizeof(expected1), "k1_%d", seed);
    snprintf(expected2, sizeof(expected2), "k2_%d", seed);
    
    if (strcmp(msg->key1, expected1) != 0) return -1;
    if (strcmp(msg->key2, expected2) != 0) return -1;
    if (fabs(msg->value - ((double)seed * 2.5)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(TwoKeyStringTopic, two_key_string_topic);

// --- ThreeKeyTopic ---
static void generate_ThreeKeyTopic(void* data, int seed) {
    AtomicTests_ThreeKeyTopic* msg = (AtomicTests_ThreeKeyTopic*)data;
    msg->key1 = seed;
    snprintf(msg->key2, sizeof(msg->key2), "k2_%d", seed);
    msg->key3 = (int16_t)(seed % 100);
    msg->value = (double)seed * 3.5;
}

static int validate_ThreeKeyTopic(void* data, int seed) {
    AtomicTests_ThreeKeyTopic* msg = (AtomicTests_ThreeKeyTopic*)data;
    char expected2[32];
    snprintf(expected2, sizeof(expected2), "k2_%d", seed);
    
    if (msg->key1 != seed) return -1;
    if (strcmp(msg->key2, expected2) != 0) return -1;
    if (msg->key3 != (int16_t)(seed % 100)) return -1;
    if (fabs(msg->value - ((double)seed * 3.5)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(ThreeKeyTopic, three_key_topic);

// --- FourKeyTopic ---
static void generate_FourKeyTopic(void* data, int seed) {
    AtomicTests_FourKeyTopic* msg = (AtomicTests_FourKeyTopic*)data;
    msg->key1 = seed;
    msg->key2 = seed + 1;
    msg->key3 = seed + 2;
    msg->key4 = seed + 3;
    snprintf(msg->description, sizeof(msg->description), "Desc_%d", seed);
}

static int validate_FourKeyTopic(void* data, int seed) {
    AtomicTests_FourKeyTopic* msg = (AtomicTests_FourKeyTopic*)data;
    char expectedDesc[64];
    snprintf(expectedDesc, sizeof(expectedDesc), "Desc_%d", seed);
    
    if (msg->key1 != seed) return -1;
    if (msg->key2 != seed + 1) return -1;
    if (msg->key3 != seed + 2) return -1;
    if (msg->key4 != seed + 3) return -1;
    if (strcmp(msg->description, expectedDesc) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(FourKeyTopic, four_key_topic);

// ============================================================================
// SECTION 10: NESTED KEYS
// ============================================================================

// --- NestedKeyTopic ---
static void generate_NestedKeyTopic(void* data, int seed) {
    AtomicTests_NestedKeyTopic* msg = (AtomicTests_NestedKeyTopic*)data;
    msg->loc.building = seed;
    msg->loc.floor = (int16_t)(seed % 10);
    msg->temperature = 20.0 + (double)seed;
}

static int validate_NestedKeyTopic(void* data, int seed) {
    AtomicTests_NestedKeyTopic* msg = (AtomicTests_NestedKeyTopic*)data;
    if (msg->loc.building != seed) return -1;
    if (msg->loc.floor != (int16_t)(seed % 10)) return -1;
    if (fabs(msg->temperature - (20.0 + (double)seed)) > 0.0001) return -1;
    return 0;
}
DEFINE_HANDLER(NestedKeyTopic, nested_key_topic);

// --- NestedKeyGeoTopic ---
static void generate_NestedKeyGeoTopic(void* data, int seed) {
    AtomicTests_NestedKeyGeoTopic* msg = (AtomicTests_NestedKeyGeoTopic*)data;
    msg->coords.latitude = (double)seed * 0.1;
    msg->coords.longitude = (double)seed * 0.2;
    snprintf(msg->location_name, sizeof(msg->location_name), "Loc_%d", seed);
}

static int validate_NestedKeyGeoTopic(void* data, int seed) {
    AtomicTests_NestedKeyGeoTopic* msg = (AtomicTests_NestedKeyGeoTopic*)data;
    char expected[128];
    snprintf(expected, sizeof(expected), "Loc_%d", seed);
    
    if (fabs(msg->coords.latitude - ((double)seed * 0.1)) > 0.0001) return -1;
    if (fabs(msg->coords.longitude - ((double)seed * 0.2)) > 0.0001) return -1;
    if (strcmp(msg->location_name, expected) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(NestedKeyGeoTopic, nested_key_geo_topic);

// --- NestedTripleKeyTopic ---
static void generate_NestedTripleKeyTopic(void* data, int seed) {
    AtomicTests_NestedTripleKeyTopic* msg = (AtomicTests_NestedTripleKeyTopic*)data;
    msg->keys.id1 = seed;
    msg->keys.id2 = seed + 1;
    msg->keys.id3 = seed + 2;
    snprintf(msg->data, sizeof(msg->data), "Data_%d", seed);
}

static int validate_NestedTripleKeyTopic(void* data, int seed) {
    AtomicTests_NestedTripleKeyTopic* msg = (AtomicTests_NestedTripleKeyTopic*)data;
    char expected[64];
    snprintf(expected, sizeof(expected), "Data_%d", seed);
    
    if (msg->keys.id1 != seed) return -1;
    if (msg->keys.id2 != seed + 1) return -1;
    if (msg->keys.id3 != seed + 2) return -1;
    if (strcmp(msg->data, expected) != 0) return -1;
    return 0;
}
DEFINE_HANDLER(NestedTripleKeyTopic, nested_triple_key_topic);
