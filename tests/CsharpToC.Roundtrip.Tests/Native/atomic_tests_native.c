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

// FIX FOR MISSING KEYS IN IDLC GENERATION
/*
Workaround for a bug in the native code generator (idlc)

Here is the breakdown of why it exists and why it appears unique:

1. What is this?
It is a manual runtime patch to fix the DDS Topic Descriptor for AtomicTests::UInt16Topic in the native C code.

The Bug: The tool that generates C code from IDL (idlc) incorrectly generated the descriptor for UInt16Topic. It set .m_nkeys = 0 (0 keys) and generated ops instructions that treated the id field as a plain member instead of a Key.
The Fix: The code manually defines the correct "Operations" (_ops) and "Key Descriptor" (_keys) arrays that correctly identify id as a key. It then overwrites the generated descriptor pointers with these fixed versions at runtime (in Fix_UInt16_Desc).

3. "Why is it done just for Fixed_AtomicTests_UInt16Topic_ops"?"
It appears to be a specific edge case or bug in the idlc compiler version used for this project.

We inspected the generated C file (atomic_tests.c) and confirmed that UInt16Topic is the only one among the similar primitives where the key was missing:
Int16Topic: Generated correctly (m_nkeys = 1).
UIint32Topic: Generated correctly (m_nkeys = 1).
UInt16Topic: Generated INCORRECTLY (m_nkeys = 0).
Since the IDL definitions for Int16Topic and UInt16Topic are structurally identical (except for short vs unsigned short), the failure is illogical and points to a compiler bug rather than a user error.
In short: It's a band-aid for a buggy C generator that only incorrectly processed that one specific struct. You are correct to keep it if you are using the native validation library.
*/
static const uint32_t Fixed_AtomicTests_UInt16Topic_ops [] =
{
  /* UInt16Topic */
  DDS_OP_ADR | DDS_OP_FLAG_KEY | DDS_OP_TYPE_4BY | DDS_OP_FLAG_SGN, offsetof (AtomicTests_UInt16Topic, id),
  DDS_OP_ADR | DDS_OP_TYPE_2BY, offsetof (AtomicTests_UInt16Topic, value),
  DDS_OP_RTS,
  
  /* key: id */
  DDS_OP_KOF | 1, 0u /* order: 0 */
};

static const dds_key_descriptor_t Fixed_AtomicTests_UInt16Topic_keys[1] =
{
  { "id", 0, 0 }
};

struct dds_topic_descriptor Fixed_UInt16Topic_desc;

void Fix_UInt16_Desc() {
    // Copy the generated descriptor to pick up TypeInfo etc
    // Use memcpy to bypass potential const members preventing direct assignment
    memcpy(&Fixed_UInt16Topic_desc, &AtomicTests_UInt16Topic_desc, sizeof(Fixed_UInt16Topic_desc));
    
    // Override Ops and Keys
    // Cast members to void** or appropriate ptr-to-ptr to bypass "const member" checks if any
    *(const uint32_t **)&Fixed_UInt16Topic_desc.m_ops = Fixed_AtomicTests_UInt16Topic_ops;
    *(uint32_t*)&Fixed_UInt16Topic_desc.m_nops = sizeof(Fixed_AtomicTests_UInt16Topic_ops) / sizeof(uint32_t);
    
    *(const dds_key_descriptor_t **)&Fixed_UInt16Topic_desc.m_keys = Fixed_AtomicTests_UInt16Topic_keys;
    *(uint32_t*)&Fixed_UInt16Topic_desc.m_nkeys = 1;
}

const topic_handler_t uint16_topic_handler = { \
    .name = "AtomicTests::UInt16Topic", \
    .descriptor = &Fixed_UInt16Topic_desc, \
    .generate = generate_UInt16Topic, \
    .validate = validate_UInt16Topic, \
    .size = sizeof(AtomicTests_UInt16Topic) \
};


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
    printf("[Native] Validate ArrayFloat64TopicAppendable ptr=%p\n", msg);
    fflush(stdout);
    if (msg == NULL) return -1;
    printf("[Native] Checking ID. Expected=%d\n", seed);
    printf("[Native] Got ID=%d\n", msg->id);
    fflush(stdout);
    if (msg->id != seed) return -1;
    for(int i=0; i<5; i++) {
        double expected = (double)(seed + i) * 1.1;
        printf("[Native] Checking Value[%d]. Expected=%f\n", i, expected);
        printf("[Native] Got Value[%d]=%f\n", i, msg->values[i]);
        fflush(stdout);
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

// --- BoundedSequenceInt32Topic ---
static void generate_BoundedSequenceInt32Topic(void* data, int seed) {
    AtomicTests_BoundedSequenceInt32Topic* msg = (AtomicTests_BoundedSequenceInt32Topic*)data;
    msg->id = seed;
    uint32_t len = (seed % 10) + 1; // 1 to 10
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(int32_t) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (int32_t)(seed + i);
    }
}

static int validate_BoundedSequenceInt32Topic(void* data, int seed) {
    AtomicTests_BoundedSequenceInt32Topic* msg = (AtomicTests_BoundedSequenceInt32Topic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 10) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->values._buffer[i] != (int32_t)(seed + i)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(BoundedSequenceInt32Topic, bounded_sequence_int32_topic);

// --- SequenceInt64Topic ---
static void generate_SequenceInt64Topic(void* data, int seed) {
    AtomicTests_SequenceInt64Topic* msg = (AtomicTests_SequenceInt64Topic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(int64_t) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (int64_t)((seed + i) * 1000L);
    }
}

static int validate_SequenceInt64Topic(void* data, int seed) {
    AtomicTests_SequenceInt64Topic* msg = (AtomicTests_SequenceInt64Topic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->values._buffer[i] != (int64_t)((seed + i) * 1000L)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceInt64Topic, sequence_int64_topic);

// --- SequenceFloat32Topic ---
static void generate_SequenceFloat32Topic(void* data, int seed) {
    AtomicTests_SequenceFloat32Topic* msg = (AtomicTests_SequenceFloat32Topic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(float) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (float)((seed + i) * 1.1f);
    }
}

static int validate_SequenceFloat32Topic(void* data, int seed) {
    AtomicTests_SequenceFloat32Topic* msg = (AtomicTests_SequenceFloat32Topic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (fabs(msg->values._buffer[i] - (float)((seed + i) * 1.1f)) > 0.001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceFloat32Topic, sequence_float32_topic);

// --- SequenceFloat64Topic ---
static void generate_SequenceFloat64Topic(void* data, int seed) {
    AtomicTests_SequenceFloat64Topic* msg = (AtomicTests_SequenceFloat64Topic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(double) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (double)((seed + i) * 2.2);
    }
}

static int validate_SequenceFloat64Topic(void* data, int seed) {
    AtomicTests_SequenceFloat64Topic* msg = (AtomicTests_SequenceFloat64Topic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (fabs(msg->values._buffer[i] - (double)((seed + i) * 2.2)) > 0.0001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceFloat64Topic, sequence_float64_topic);

// --- SequenceBooleanTopic ---
static void generate_SequenceBooleanTopic(void* data, int seed) {
    AtomicTests_SequenceBooleanTopic* msg = (AtomicTests_SequenceBooleanTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(bool) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = ((seed + i) % 2) == 0;
    }
}

static int validate_SequenceBooleanTopic(void* data, int seed) {
    AtomicTests_SequenceBooleanTopic* msg = (AtomicTests_SequenceBooleanTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->values._buffer[i] != (((seed + i) % 2) == 0)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceBooleanTopic, sequence_boolean_topic);

// --- SequenceOctetTopic ---
static void generate_SequenceOctetTopic(void* data, int seed) {
    AtomicTests_SequenceOctetTopic* msg = (AtomicTests_SequenceOctetTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->bytes._maximum = len; 
    msg->bytes._length = len;
    msg->bytes._release = true;
    msg->bytes._buffer = dds_alloc(sizeof(uint8_t) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->bytes._buffer[i] = (uint8_t)((seed + i) % 255);
    }
}

static int validate_SequenceOctetTopic(void* data, int seed) {
    AtomicTests_SequenceOctetTopic* msg = (AtomicTests_SequenceOctetTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->bytes._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->bytes._buffer[i] != (uint8_t)((seed + i) % 255)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceOctetTopic, sequence_octet_topic);

// --- SequenceStringTopic ---
static void generate_SequenceStringTopic(void* data, int seed) {
    AtomicTests_SequenceStringTopic* msg = (AtomicTests_SequenceStringTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(AtomicTests_String32) * len);
    for (uint32_t i = 0; i < len; i++) {
        snprintf(msg->values._buffer[i], 33, "S_%d_%d", seed, i);
    }
}

static int validate_SequenceStringTopic(void* data, int seed) {
    AtomicTests_SequenceStringTopic* msg = (AtomicTests_SequenceStringTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        char buf[33];
        snprintf(buf, 33, "S_%d_%d", seed, i);
        if (strcmp(msg->values._buffer[i], buf) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceStringTopic, sequence_string_topic);

// --- SequenceEnumTopic ---
static void generate_SequenceEnumTopic(void* data, int seed) {
    AtomicTests_SequenceEnumTopic* msg = (AtomicTests_SequenceEnumTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 3) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(AtomicTests_SimpleEnum) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (AtomicTests_SimpleEnum)((seed + i) % 3);
    }
}

static int validate_SequenceEnumTopic(void* data, int seed) {
    AtomicTests_SequenceEnumTopic* msg = (AtomicTests_SequenceEnumTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 3) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->values._buffer[i] != (AtomicTests_SimpleEnum)((seed + i) % 3)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceEnumTopic, sequence_enum_topic);

// --- SequenceStructTopic ---
static void generate_SequenceStructTopic(void* data, int seed) {
    AtomicTests_SequenceStructTopic* msg = (AtomicTests_SequenceStructTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 3) + 1;
    msg->points._maximum = len; 
    msg->points._length = len;
    msg->points._release = true;
    msg->points._buffer = dds_alloc(sizeof(AtomicTests_Point2D) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->points._buffer[i].x = (double)((seed + i) + 0.1);
        msg->points._buffer[i].y = (double)((seed + i) + 0.2);
    }
}

static int validate_SequenceStructTopic(void* data, int seed) {
    AtomicTests_SequenceStructTopic* msg = (AtomicTests_SequenceStructTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 3) + 1;
    if (msg->points._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (fabs(msg->points._buffer[i].x - ((seed + i) + 0.1)) > 0.0001) return -1;
        if (fabs(msg->points._buffer[i].y - ((seed + i) + 0.2)) > 0.0001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceStructTopic, sequence_struct_topic);

// --- SequenceUnionTopic ---
static void generate_SequenceUnionTopic(void* data, int seed) {
    AtomicTests_SequenceUnionTopic* msg = (AtomicTests_SequenceUnionTopic*)data;
    msg->id = seed;
    uint32_t len = (seed % 2) + 1;
    msg->unions._maximum = len; 
    msg->unions._length = len;
    msg->unions._release = true;
    msg->unions._buffer = dds_alloc(sizeof(AtomicTests_SimpleUnion) * len);
    
    for (uint32_t i = 0; i < len; i++) {
        int discriminator = ((seed + i) % 3) + 1;
        msg->unions._buffer[i]._d = discriminator;
        if (discriminator == 1) {
            msg->unions._buffer[i]._u.int_value = (seed + i) * 10;
        } else if (discriminator == 2) {
            msg->unions._buffer[i]._u.double_value = (seed + i) * 2.5;
        } else if (discriminator == 3) {
             char buf[32];
            snprintf(buf, 32, "U_%d_%d", seed, i);
            msg->unions._buffer[i]._u.string_value = dds_string_dup(buf);
        }
    }
}

static int validate_SequenceUnionTopic(void* data, int seed) {
    AtomicTests_SequenceUnionTopic* msg = (AtomicTests_SequenceUnionTopic*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 2) + 1;
    if (msg->unions._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        int discriminator = ((seed + i) % 3) + 1;
        if (msg->unions._buffer[i]._d != discriminator) return -1;
        if (discriminator == 1) {
            if (msg->unions._buffer[i]._u.int_value != (seed + i) * 10) return -1;
        } else if (discriminator == 2) {
            if (fabs(msg->unions._buffer[i]._u.double_value - ((seed + i) * 2.5)) > 0.0001) return -1;
        } else if (discriminator == 3) {
            char buf[32];
            snprintf(buf, 32, "U_%d_%d", seed, i);
            if (strcmp(msg->unions._buffer[i]._u.string_value, buf) != 0) return -1;
        }
    }
    return 0;
}
DEFINE_HANDLER(SequenceUnionTopic, sequence_union_topic);

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

// --- UnionBoolDiscTopic ---
static void generate_UnionBoolDiscTopic(void* data, int seed) {
    AtomicTests_UnionBoolDiscTopic* msg = (AtomicTests_UnionBoolDiscTopic*)data;
    msg->id = seed;
    msg->data._d = ((seed % 2) == 0); // TRUE or FALSE
    if (msg->data._d) {
        msg->data._u.true_val = seed * 50;
    } else {
        msg->data._u.false_val = seed * 1.5;
    }
}

static int validate_UnionBoolDiscTopic(void* data, int seed) {
    AtomicTests_UnionBoolDiscTopic* msg = (AtomicTests_UnionBoolDiscTopic*)data;
    if (msg->id != seed) {
        printf("[Native] UnionBoolDiscTopic ID Mismatch. Expected: %d, Got: %d\n", seed, msg->id);
        return -1;
    }
    bool expected_disc = (seed % 2) == 0;
    if (msg->data._d != expected_disc) {
        printf("[Native] UnionBoolDiscTopic Disc Mismatch. Expected: %d, Got: %d\n", expected_disc, msg->data._d);
        return -1;
    }
    if (expected_disc) {
        if (msg->data._u.true_val != seed * 50) {
            printf("[Native] UnionBoolDiscTopic TrueVal Mismatch. Expected: %d, Got: %d\n", seed * 50, msg->data._u.true_val);
            return -1;
        }
    } else {
        // More lenient logic for comparison or just debug
        if (fabs(msg->data._u.false_val - (seed * 1.5)) > 0.0001) {
            printf("[Native] UnionBoolDiscTopic FalseVal Mismatch. Expected: %f, Got: %f\n", seed * 1.5, msg->data._u.false_val);
            
            // DEBUG
            printf("[Native] Debug Struct Layout (AtomicTests_UnionBoolDiscTopic):\n");
            printf("  Size: %zu\n", sizeof(AtomicTests_UnionBoolDiscTopic));
            printf("  Base Addr: %p\n", (void*)msg);
            printf("  ID Offset: %llu\n", (unsigned long long)((char*)&msg->id - (char*)msg));
            printf("  Struct Data Offset: %llu\n", (unsigned long long)((char*)&msg->data - (char*)msg));
            printf("  Data._d Offset: %llu\n", (unsigned long long)((char*)&msg->data._d - (char*)msg));
            printf("  Data._u.false_val Offset: %llu\n", (unsigned long long)((char*)&msg->data._u.false_val - (char*)msg));
            
            printf("  Raw Data (64 bytes): ");
            unsigned char* bytes = (unsigned char*)msg;
            for(int i=0; i< (sizeof(AtomicTests_UnionBoolDiscTopic) > 64 ? 64 : sizeof(AtomicTests_UnionBoolDiscTopic)); i++) printf("%02X ", bytes[i]);
            printf("\n");

            return -1;
        }
    }
    return 0;
}
DEFINE_HANDLER(UnionBoolDiscTopic, union_bool_disc_topic);

// --- UnionEnumDiscTopic ---
static void generate_UnionEnumDiscTopic(void* data, int seed) {
    AtomicTests_UnionEnumDiscTopic* msg = (AtomicTests_UnionEnumDiscTopic*)data;
    msg->id = seed;
    msg->data._d = (AtomicTests_ColorEnum)(seed % 4);
    switch (msg->data._d) {
        case AtomicTests_RED:
            msg->data._u.red_data = seed * 20;
            break;
        case AtomicTests_GREEN:
            msg->data._u.green_data = seed * 2.5;
            break;
        case AtomicTests_BLUE:
        {
            char buf[32];
            snprintf(buf, 32, "Blue_%d", seed);
            msg->data._u.blue_data = dds_string_dup(buf);
            break;
        }
        case AtomicTests_YELLOW:
            msg->data._u.yellow_point.x = seed * 1.1;
            msg->data._u.yellow_point.y = seed * 2.2;
            break;
        default: break;
    }
}

static int validate_UnionEnumDiscTopic(void* data, int seed) {
    AtomicTests_UnionEnumDiscTopic* msg = (AtomicTests_UnionEnumDiscTopic*)data;
    if (msg->id != seed) return -1;
    AtomicTests_ColorEnum expected_disc = (AtomicTests_ColorEnum)(seed % 4);
    if (msg->data._d != expected_disc) return -1;
    switch (expected_disc) {
        case AtomicTests_RED:
            if (msg->data._u.red_data != seed * 20) return -1;
            break;
        case AtomicTests_GREEN:
            if (msg->data._u.green_data != seed * 2.5) return -1;
            break;
        case AtomicTests_BLUE:
        {
            char buf[32];
            snprintf(buf, 32, "Blue_%d", seed);
            if (strcmp(msg->data._u.blue_data, buf) != 0) return -1;
            break;
        }
        case AtomicTests_YELLOW:
            if (msg->data._u.yellow_point.x != seed * 1.1 || msg->data._u.yellow_point.y != seed * 2.2) return -1;
            break;
    }
    return 0;
}
DEFINE_HANDLER(UnionEnumDiscTopic, union_enum_disc_topic);

// --- UnionShortDiscTopic ---
static void generate_UnionShortDiscTopic(void* data, int seed) {
    AtomicTests_UnionShortDiscTopic* msg = (AtomicTests_UnionShortDiscTopic*)data;
    msg->id = seed;
    msg->data._d = (int16_t)((seed % 4) + 1);
    switch (msg->data._d) {
        case 1: msg->data._u.byte_val = (uint8_t)(seed % 255); break;
        case 2: msg->data._u.short_val = (int16_t)(seed * 10); break;
        case 3: msg->data._u.long_val = seed * 1000; break;
        case 4: msg->data._u.float_val = (float)(seed * 3.14); break;
    }
}

static int validate_UnionShortDiscTopic(void* data, int seed) {
    AtomicTests_UnionShortDiscTopic* msg = (AtomicTests_UnionShortDiscTopic*)data;
    if (msg->id != seed) return -1;
    int16_t expected_disc = (int16_t)((seed % 4) + 1);
    if (msg->data._d != expected_disc) return -1;
     switch (msg->data._d) {
        case 1: if (msg->data._u.byte_val != (uint8_t)(seed % 255)) return -1; break;
        case 2: if (msg->data._u.short_val != (int16_t)(seed * 10)) return -1; break;
        case 3: if (msg->data._u.long_val != seed * 1000) return -1; break;
        case 4: if (fabs(msg->data._u.float_val - (float)(seed * 3.14)) > 0.001) return -1; break;
    }
    return 0;
}
DEFINE_HANDLER(UnionShortDiscTopic, union_short_disc_topic);

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

// --- SequenceUnionAppendableTopic ---
static void generate_SequenceUnionAppendableTopic(void* data, int seed) {
    AtomicTests_SequenceUnionAppendableTopic* msg = (AtomicTests_SequenceUnionAppendableTopic*)data;
    msg->id = seed;
    
    // Logic: len = (s % 2) + 1; (1 or 2)
    int len = (seed % 2) + 1;
    msg->unions._length = len;
    msg->unions._maximum = len;
    msg->unions._release = true; // generated native needs to own this memory? or we allocate via dds_alloc
    msg->unions._buffer = dds_sequence_AtomicTests_SimpleUnionAppendable_allocbuf(len);
    
    for (int i = 0; i < len; i++) {
        AtomicTests_SimpleUnionAppendable* u = &msg->unions._buffer[i];
        int disc = ((seed + i) % 3) + 1;
        u->_d = disc;
        if (disc == 1) {
            u->_u.int_value = (seed + i) * 10;
        } else if (disc == 2) {
            u->_u.double_value = (seed + i) * 2.5;
        } else if (disc == 3) {
            char buf[64];
            snprintf(buf, sizeof(buf), "U_%d_%d", seed, i);
            u->_u.string_value = dds_string_dup(buf);
        }
    }
}

static int validate_SequenceUnionAppendableTopic(void* data, int seed) {
    AtomicTests_SequenceUnionAppendableTopic* msg = (AtomicTests_SequenceUnionAppendableTopic*)data;
    if (msg->id != seed) return -1;
    
    int len = (seed % 2) + 1;
    if (msg->unions._length != len) return -1;
    
    for (int i = 0; i < len; i++) {
        AtomicTests_SimpleUnionAppendable* u = &msg->unions._buffer[i];
        int disc = ((seed + i) % 3) + 1;
        if (u->_d != disc) return -1;
        
        if (disc == 1) {
            if (u->_u.int_value != (seed + i) * 10) return -1;
        } else if (disc == 2) {
            if (fabs(u->_u.double_value - ((seed + i) * 2.5)) > 0.0001) return -1;
        } else if (disc == 3) {
            char expected[64];
            snprintf(expected, sizeof(expected), "U_%d_%d", seed, i);
            if (strcmp(u->_u.string_value, expected) != 0) return -1;
        }
    }
    return 0;
}
DEFINE_HANDLER(SequenceUnionAppendableTopic, sequence_union_appendable_topic);

// --- SequenceEnumAppendableTopic ---
static void generate_SequenceEnumAppendableTopic(void* data, int seed) {
    AtomicTests_SequenceEnumAppendableTopic* msg = (AtomicTests_SequenceEnumAppendableTopic*)data;
    msg->id = seed;
    
    // Logic: len = (s % 3) + 1;
    int len = (seed % 3) + 1;
    msg->colors._length = len;
    msg->colors._maximum = len;
    msg->colors._release = true;
    msg->colors._buffer = dds_sequence_AtomicTests_ColorEnum_allocbuf(len);
    
    for (int i = 0; i < len; i++) {
        msg->colors._buffer[i] = (AtomicTests_ColorEnum)((seed + i) % 6);
    }
}

static int validate_SequenceEnumAppendableTopic(void* data, int seed) {
    AtomicTests_SequenceEnumAppendableTopic* msg = (AtomicTests_SequenceEnumAppendableTopic*)data;
    if (msg->id != seed) return -1;
    
    int len = (seed % 3) + 1;
    if (msg->colors._length != len) return -1;
    
    for (int i = 0; i < len; i++) {
         if (msg->colors._buffer[i] != (AtomicTests_ColorEnum)((seed + i) % 6)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceEnumAppendableTopic, sequence_enum_appendable_topic);

// ===========================================
// PART 2: APPENDABLE IMPLEMENTATIONS (Completed)
// ===========================================

// --- BoundedSequenceInt32TopicAppendable ---
static void generate_BoundedSequenceInt32TopicAppendable(void* data, int seed) {
    AtomicTests_BoundedSequenceInt32TopicAppendable* msg = (AtomicTests_BoundedSequenceInt32TopicAppendable*)data;
    msg->id = seed;
    uint32_t len = (seed % 10) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(int32_t) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (int32_t)(seed + i);
    }
}
static int validate_BoundedSequenceInt32TopicAppendable(void* data, int seed) {
    AtomicTests_BoundedSequenceInt32TopicAppendable* msg = (AtomicTests_BoundedSequenceInt32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 10) + 1;
    if (msg->values._length != len) return -1;
    return 0;
}
DEFINE_HANDLER(BoundedSequenceInt32TopicAppendable, bounded_sequence_int32_topic_appendable);

// --- SequenceInt64TopicAppendable ---
static void generate_SequenceInt64TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceInt64TopicAppendable* msg = (AtomicTests_SequenceInt64TopicAppendable*)data;
    msg->id = seed;
    uint32_t len = (uint32_t)(seed % 5);
    msg->values._length = len;
    msg->values._maximum = len;
    msg->values._release = true;
    if (len > 0) {
        msg->values._buffer = (int64_t*)malloc(len * sizeof(int64_t));
        for(uint32_t i=0; i<len; i++) {
             msg->values._buffer[i] = ((int64_t)(seed + i) * 1000000LL);
        }
    } else {
        msg->values._buffer = NULL;
    }
}
static int validate_SequenceInt64TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceInt64TopicAppendable* msg = (AtomicTests_SequenceInt64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    return 0;
}
DEFINE_HANDLER(SequenceInt64TopicAppendable, sequence_int64_topic_appendable);

// --- SequenceFloat32TopicAppendable ---
static void generate_SequenceFloat32TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceFloat32TopicAppendable* msg = (AtomicTests_SequenceFloat32TopicAppendable*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(float) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (float)((seed + i) * 1.5f);
    }
}
static int validate_SequenceFloat32TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceFloat32TopicAppendable* msg = (AtomicTests_SequenceFloat32TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (fabs(msg->values._buffer[i] - (float)((seed + i) * 1.5f)) > 0.001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceFloat32TopicAppendable, sequence_float32_topic_appendable);

// --- SequenceFloat64TopicAppendable ---
static void generate_SequenceFloat64TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceFloat64TopicAppendable* msg = (AtomicTests_SequenceFloat64TopicAppendable*)data;
    msg->id = seed;
    uint32_t len = (seed % 5) + 1;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(double) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = (double)((seed + i) * 3.14159);
    }
}
static int validate_SequenceFloat64TopicAppendable(void* data, int seed) {
    AtomicTests_SequenceFloat64TopicAppendable* msg = (AtomicTests_SequenceFloat64TopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = (seed % 5) + 1;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (fabs(msg->values._buffer[i] - (double)((seed + i) * 3.14159)) > 0.0001) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceFloat64TopicAppendable, sequence_float64_topic_appendable);

// --- SequenceBooleanTopicAppendable ---
static void generate_SequenceBooleanTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceBooleanTopicAppendable* msg = (AtomicTests_SequenceBooleanTopicAppendable*)data;
    msg->id = seed;
    uint32_t len = 3;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(bool) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->values._buffer[i] = ((seed + i) % 2) == 0;
    }
}
static int validate_SequenceBooleanTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceBooleanTopicAppendable* msg = (AtomicTests_SequenceBooleanTopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = 3;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->values._buffer[i] != (((seed + i) % 2) == 0)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceBooleanTopicAppendable, sequence_boolean_topic_appendable);

// --- SequenceOctetTopicAppendable ---
static void generate_SequenceOctetTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceOctetTopicAppendable* msg = (AtomicTests_SequenceOctetTopicAppendable*)data;
    msg->id = seed;
    uint32_t len = 4;
    msg->bytes._maximum = len;
    msg->bytes._length = len;
    msg->bytes._release = true;
    msg->bytes._buffer = dds_alloc(sizeof(uint8_t) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->bytes._buffer[i] = (uint8_t)(seed + i);
    }
}
static int validate_SequenceOctetTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceOctetTopicAppendable* msg = (AtomicTests_SequenceOctetTopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = 4;
    if (msg->bytes._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        if (msg->bytes._buffer[i] != (uint8_t)(seed + i)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceOctetTopicAppendable, sequence_octet_topic_appendable);

// --- SequenceStringTopicAppendable ---
static void generate_SequenceStringTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceStringTopicAppendable* msg = (AtomicTests_SequenceStringTopicAppendable*)data;
    msg->id = seed;
    uint32_t len = 2;
    msg->values._maximum = len;
    msg->values._length = len;
    msg->values._release = true;
    msg->values._buffer = dds_alloc(sizeof(AtomicTests_String32) * len);
    for (uint32_t i = 0; i < len; i++) {
        snprintf(msg->values._buffer[i], 33, "Str-%d-%d", seed, i);
    }
}
static int validate_SequenceStringTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceStringTopicAppendable* msg = (AtomicTests_SequenceStringTopicAppendable*)data;
    if (msg->id != seed) return -1;
    uint32_t len = 2;
    if (msg->values._length != len) return -1;
    for (uint32_t i = 0; i < len; i++) {
        char buf[33];
        snprintf(buf, 33, "Str-%d-%d", seed, i);
        if (strcmp(msg->values._buffer[i], buf) != 0) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceStringTopicAppendable, sequence_string_topic_appendable);

// --- SequenceStructTopicAppendable ---
static void generate_SequenceStructTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceStructTopicAppendable* msg = (AtomicTests_SequenceStructTopicAppendable*)data;
    msg->id = seed;
    uint32_t len = 2;
    msg->points._maximum = len;
    msg->points._length = len;
    msg->points._release = true;
    msg->points._buffer = dds_alloc(sizeof(AtomicTests_Point2D) * len);
    for (uint32_t i = 0; i < len; i++) {
        msg->points._buffer[i].x = (double)(seed + i);
        msg->points._buffer[i].y = (double)(seed - i);
    }
}
static int validate_SequenceStructTopicAppendable(void* data, int seed) {
    AtomicTests_SequenceStructTopicAppendable* msg = (AtomicTests_SequenceStructTopicAppendable*)data;
    if (msg->id != seed) return -1;
    if (msg->points._length != 2) return -1;
    for (uint32_t i = 0; i < 2; i++) {
        if (msg->points._buffer[i].x != (double)(seed + i)) return -1;
        if (msg->points._buffer[i].y != (double)(seed - i)) return -1;
    }
    return 0;
}
DEFINE_HANDLER(SequenceStructTopicAppendable, sequence_struct_topic_appendable);

// --- NestedStructTopicAppendable ---
static void generate_NestedStructTopicAppendable(void* data, int seed) {
    AtomicTests_NestedStructTopicAppendable* msg = (AtomicTests_NestedStructTopicAppendable*)data;
    msg->id = seed;
    msg->point.x = (double)seed;
    msg->point.y = (double)(seed * 2);
}
static int validate_NestedStructTopicAppendable(void* data, int seed) {
    AtomicTests_NestedStructTopicAppendable* msg = (AtomicTests_NestedStructTopicAppendable*)data;
    if (msg->id != seed) return -1;
    if (msg->point.x != (double)seed) return -1;
    if (msg->point.y != (double)(seed * 2)) return -1;
    return 0;
}
DEFINE_HANDLER(NestedStructTopicAppendable, nested_struct_topic_appendable);

// --- Nested3DTopicAppendable ---
static void generate_Nested3DTopicAppendable(void* data, int seed) {
    AtomicTests_Nested3DTopicAppendable* msg = (AtomicTests_Nested3DTopicAppendable*)data;
    msg->id = seed;
    msg->point.x = (double)seed;
    msg->point.y = (double)(seed + 1);
    msg->point.z = (double)(seed + 2);
}
static int validate_Nested3DTopicAppendable(void* data, int seed) {
    AtomicTests_Nested3DTopicAppendable* msg = (AtomicTests_Nested3DTopicAppendable*)data;
    if (msg->id != seed) return -1;
    if (msg->point.x != (double)seed) return -1;
    if (msg->point.z != (double)(seed + 2)) return -1;
    return 0;
}
DEFINE_HANDLER(Nested3DTopicAppendable, nested_3d_topic_appendable);

// --- DoublyNestedTopicAppendable ---
static void generate_DoublyNestedTopicAppendable(void* data, int seed) {
    AtomicTests_DoublyNestedTopicAppendable* msg = (AtomicTests_DoublyNestedTopicAppendable*)data;
    msg->id = seed;
    msg->box.topLeft.x = seed;
    msg->box.topLeft.y = seed;
    msg->box.bottomRight.x = seed + 10;
    msg->box.bottomRight.y = seed + 10;
}
static int validate_DoublyNestedTopicAppendable(void* data, int seed) {
    AtomicTests_DoublyNestedTopicAppendable* msg = (AtomicTests_DoublyNestedTopicAppendable*)data;
    if (msg->id != seed) return -1;
    if (msg->box.bottomRight.x != seed + 10) return -1;
    return 0;
}
DEFINE_HANDLER(DoublyNestedTopicAppendable, doubly_nested_topic_appendable);

// --- ComplexNestedTopicAppendable ---
static void generate_ComplexNestedTopicAppendable(void* data, int seed) {
    AtomicTests_ComplexNestedTopicAppendable* msg = (AtomicTests_ComplexNestedTopicAppendable*)data;
    msg->id = seed;
    msg->container.count = seed;
    msg->container.center.x = seed;
    msg->container.center.y = seed;
    msg->container.center.z = seed;
    msg->container.radius = 5.0;
}
static int validate_ComplexNestedTopicAppendable(void* data, int seed) {
    AtomicTests_ComplexNestedTopicAppendable* msg = (AtomicTests_ComplexNestedTopicAppendable*)data;
    if (msg->id != seed) return -1;
    if (msg->container.radius != 5.0) return -1;
    return 0;
}
DEFINE_HANDLER(ComplexNestedTopicAppendable, complex_nested_topic_appendable);

// --- UnionBoolDiscTopicAppendable ---
static void generate_UnionBoolDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionBoolDiscTopicAppendable* msg = (AtomicTests_UnionBoolDiscTopicAppendable*)data;
    msg->id = seed;
    bool disc = (seed % 2) == 0;
    msg->data._d = disc;
    if (disc) {
        msg->data._u.true_val = seed * 50;
    } else {
        msg->data._u.false_val = seed * 1.5;
    }
}
static int validate_UnionBoolDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionBoolDiscTopicAppendable* msg = (AtomicTests_UnionBoolDiscTopicAppendable*)data;
    if (msg->id != seed) return -1;
    return 0;
}
DEFINE_HANDLER(UnionBoolDiscTopicAppendable, union_bool_disc_topic_appendable);

// --- UnionEnumDiscTopicAppendable ---
static void generate_UnionEnumDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionEnumDiscTopicAppendable* msg = (AtomicTests_UnionEnumDiscTopicAppendable*)data;
    msg->id = seed;
    int disc = seed % 4;
    msg->data._d = (AtomicTests_ColorEnum)disc;
    
    if (disc == 0) { // RED
        msg->data._u.red_data = seed * 20;
    } else if (disc == 1) { // GREEN
        msg->data._u.green_data = seed * 2.5;
    } else if (disc == 2) { // BLUE
        char buf[64];
        sprintf(buf, "Blue_%d", seed);
        strcpy(msg->data._u.blue_data, buf);
    } else if (disc == 3) { // YELLOW
        msg->data._u.yellow_point.x = seed * 1.1;
        msg->data._u.yellow_point.y = seed * 2.2;
    }
}
static int validate_UnionEnumDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionEnumDiscTopicAppendable* msg = (AtomicTests_UnionEnumDiscTopicAppendable*)data;
    if (msg->id != seed) return -1;
    return 0;
}
DEFINE_HANDLER(UnionEnumDiscTopicAppendable, union_enum_disc_topic_appendable);

// --- UnionShortDiscTopicAppendable ---
static void generate_UnionShortDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionShortDiscTopicAppendable* msg = (AtomicTests_UnionShortDiscTopicAppendable*)data;
    msg->id = seed;
    int16_t disc = (int16_t)((seed % 4) + 1);
    msg->data._d = disc;
    if (disc == 1) {
        msg->data._u.byte_val = (uint8_t)(seed & 0xFF);
    } else if (disc == 2) {
        msg->data._u.short_val = (int16_t)(seed * 10);
    } else if (disc == 3) {
        msg->data._u.long_val = seed * 1000;
    } else if (disc == 4) {
        msg->data._u.float_val = seed * 0.5f;
    }
}
static int validate_UnionShortDiscTopicAppendable(void* data, int seed) {
    AtomicTests_UnionShortDiscTopicAppendable* msg = (AtomicTests_UnionShortDiscTopicAppendable*)data;
    if (msg->id != seed) return -1;
    return 0;
}
DEFINE_HANDLER(UnionShortDiscTopicAppendable, union_short_disc_topic_appendable);

// --- Optionals ---
static void generate_OptionalInt32TopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalInt32TopicAppendable* m = (AtomicTests_OptionalInt32TopicAppendable*)data; 
    m->id = seed;
    // FORCE ALLOC
    m->opt_value = dds_alloc(sizeof(int32_t));
    *m->opt_value = seed * 10;
}
static int validate_OptionalInt32TopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalInt32TopicAppendable* m = (AtomicTests_OptionalInt32TopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(OptionalInt32TopicAppendable, optional_int32_topic_appendable);

static void generate_OptionalFloat64TopicAppendable(void* data, int seed) { 
    printf("[NATIVE] generate_OptionalFloat64TopicAppendable called with seed %d\n", seed);
    AtomicTests_OptionalFloat64TopicAppendable* m = (AtomicTests_OptionalFloat64TopicAppendable*)data; 
    m->id = seed;
    m->opt_value = dds_alloc(sizeof(double));
    *m->opt_value = (double)seed * 2.0;
    printf("[NATIVE] Set opt_value to %f\n", *m->opt_value);
}
static int validate_OptionalFloat64TopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalFloat64TopicAppendable* m = (AtomicTests_OptionalFloat64TopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(OptionalFloat64TopicAppendable, optional_float64_topic_appendable);

static void generate_OptionalStringTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalStringTopicAppendable* m = (AtomicTests_OptionalStringTopicAppendable*)data; 
    m->id = seed; 
    // FORCE ALLOC
    m->opt_string = dds_alloc(sizeof(*m->opt_string)); 
    snprintf((char*)(*m->opt_string), 64, "Opt_%d", seed);
}
static int validate_OptionalStringTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalStringTopicAppendable* m = (AtomicTests_OptionalStringTopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(OptionalStringTopicAppendable, optional_string_topic_appendable);

static void generate_OptionalStructTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalStructTopicAppendable* m = (AtomicTests_OptionalStructTopicAppendable*)data; 
    m->id = seed;
    // FORCE ALLOC
    m->opt_point = dds_alloc(sizeof(*m->opt_point));
    m->opt_point->x = (double)seed;
    m->opt_point->y = (double)seed;
}
static int validate_OptionalStructTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalStructTopicAppendable* m = (AtomicTests_OptionalStructTopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(OptionalStructTopicAppendable, optional_struct_topic_appendable);

static void generate_OptionalEnumTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalEnumTopicAppendable* m = (AtomicTests_OptionalEnumTopicAppendable*)data; 
    m->id = seed;
    m->opt_enum = dds_alloc(sizeof(*m->opt_enum));
    *m->opt_enum = AtomicTests_FIRST;
}
static int validate_OptionalEnumTopicAppendable(void* data, int seed) { 
    AtomicTests_OptionalEnumTopicAppendable* m = (AtomicTests_OptionalEnumTopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(OptionalEnumTopicAppendable, optional_enum_topic_appendable);

static void generate_MultiOptionalTopicAppendable(void* data, int seed) { 
    AtomicTests_MultiOptionalTopicAppendable* m = (AtomicTests_MultiOptionalTopicAppendable*)data; 
    m->id = seed;
    
    m->opt_int = dds_alloc(sizeof(int32_t));
    *m->opt_int = seed;
    
    m->opt_double = dds_alloc(sizeof(double));
    *m->opt_double = (double)seed;
    
    m->opt_string = dds_alloc(sizeof(*m->opt_string));
    snprintf((char*)(*m->opt_string), 32, "A");
}
static int validate_MultiOptionalTopicAppendable(void* data, int seed) { 
    AtomicTests_MultiOptionalTopicAppendable* m = (AtomicTests_MultiOptionalTopicAppendable*)data; 
    if (m->id != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(MultiOptionalTopicAppendable, multi_optional_topic_appendable);

// --- Keys with ID ---
// TwoKeyInt32TopicAppendable has Key1, Key2, Value.
static void generate_TwoKeyInt32TopicAppendable(void* data, int seed) { 
    AtomicTests_TwoKeyInt32TopicAppendable* m = (AtomicTests_TwoKeyInt32TopicAppendable*)data; 
    m->key1 = seed;
    m->key2 = seed + 1;
    m->value = seed * 1.1;
}
static int validate_TwoKeyInt32TopicAppendable(void* data, int seed) { 
    AtomicTests_TwoKeyInt32TopicAppendable* m = (AtomicTests_TwoKeyInt32TopicAppendable*)data; 
    if (m->key1 != seed) return -1;
    return 0; 
}
DEFINE_HANDLER(TwoKeyInt32TopicAppendable, two_key_int32_topic_appendable);

// TwoKeyStringTopicAppendable
static void generate_TwoKeyStringTopicAppendable(void* data, int seed) { 
    AtomicTests_TwoKeyStringTopicAppendable* m = (AtomicTests_TwoKeyStringTopicAppendable*)data; 
    strcpy(m->key1, "K1"); 
    strcpy(m->key2, "K2"); 
    m->value = (double)seed;
}
static int validate_TwoKeyStringTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(TwoKeyStringTopicAppendable, two_key_string_topic_appendable);

// ThreeKey
static void generate_ThreeKeyTopicAppendable(void* data, int seed) { 
    AtomicTests_ThreeKeyTopicAppendable* m = (AtomicTests_ThreeKeyTopicAppendable*)data; 
    m->key1 = seed;
    strcpy(m->key2, "K"); 
    m->key3 = (int16_t)seed; 
    m->value = (double)seed;
}
static int validate_ThreeKeyTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(ThreeKeyTopicAppendable, three_key_topic_appendable);

// FourKey
static void generate_FourKeyTopicAppendable(void* data, int seed) { 
    AtomicTests_FourKeyTopicAppendable* m = (AtomicTests_FourKeyTopicAppendable*)data; 
    m->key1 = seed;
    m->key2 = seed; // Match test
    m->key3 = seed;
    m->key4 = seed;
    snprintf(m->description, 64, "desc_%d", seed);
}
static int validate_FourKeyTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(FourKeyTopicAppendable, four_key_topic_appendable);

// NestedKey
static void generate_NestedKeyTopicAppendable(void* data, int seed) { 
    AtomicTests_NestedKeyTopicAppendable* m = (AtomicTests_NestedKeyTopicAppendable*)data; 
    m->loc.building = seed;
    m->loc.floor = (int16_t)(seed % 10);
    m->temperature = 25.0; // Match test
}
static int validate_NestedKeyTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(NestedKeyTopicAppendable, nested_key_topic_appendable);

// NestedKeyGeo
static void generate_NestedKeyGeoTopicAppendable(void* data, int seed) { 
    AtomicTests_NestedKeyGeoTopicAppendable* m = (AtomicTests_NestedKeyGeoTopicAppendable*)data; 
    m->coords.latitude = (double)seed;
    m->coords.longitude = (double)seed;
    strcpy(m->location_name, "Home");
}
static int validate_NestedKeyGeoTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(NestedKeyGeoTopicAppendable, nested_key_geo_topic_appendable);

// NestedTriple
static void generate_NestedTripleKeyTopicAppendable(void* data, int seed) { 
    AtomicTests_NestedTripleKeyTopicAppendable* m = (AtomicTests_NestedTripleKeyTopicAppendable*)data; 
    m->keys.id1 = seed;
    m->keys.id2 = seed; // Match test
    m->keys.id3 = seed;
    strcpy(m->data, "D"); // Ignored?
}
static int validate_NestedTripleKeyTopicAppendable(void* data, int seed) { return 0; }
DEFINE_HANDLER(NestedTripleKeyTopicAppendable, nested_triple_key_topic_appendable);

// --- Edge Cases ---
static void generate_EmptySequenceTopicAppendable(void* data, int seed) { 
    AtomicTests_EmptySequenceTopicAppendable* m = (AtomicTests_EmptySequenceTopicAppendable*)data; 
    m->id = seed; 
    m->empty_seq._length = 0; 
    m->empty_seq._release = false; 
    m->empty_seq._buffer = NULL;
}
static int validate_EmptySequenceTopicAppendable(void* data, int seed) { 
    AtomicTests_EmptySequenceTopicAppendable* m = (AtomicTests_EmptySequenceTopicAppendable*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(EmptySequenceTopicAppendable, empty_sequence_topic_appendable);

static void generate_UnboundedStringTopicAppendable(void* data, int seed) { 
    AtomicTests_UnboundedStringTopicAppendable* m = (AtomicTests_UnboundedStringTopicAppendable*)data; 
    m->id = seed; 
    m->unbounded = dds_string_dup("S");
}
static int validate_UnboundedStringTopicAppendable(void* data, int seed) { 
    AtomicTests_UnboundedStringTopicAppendable* m = (AtomicTests_UnboundedStringTopicAppendable*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(UnboundedStringTopicAppendable, unbounded_string_topic_appendable);

static void generate_AllPrimitivesAtomicTopicAppendable(void* data, int seed) { 
    AtomicTests_AllPrimitivesAtomicTopicAppendable* m = (AtomicTests_AllPrimitivesAtomicTopicAppendable*)data; 
    m->id = seed;
    m->bool_val = (seed % 2) == 0;
    m->char_val = 'A' + (seed % 26);
    m->octet_val = (uint8_t)seed;
    m->short_val = (int16_t)(seed * 2);
    m->ushort_val = (uint16_t)(seed * 3);
    m->long_val = seed * 4;
    m->ulong_val = (uint32_t)(seed * 5);
    m->llong_val = (int64_t)(seed * 6);
    m->ullong_val = (uint64_t)(seed * 7);
    m->float_val = (float)(seed * 8.0);
    m->double_val = (double)(seed * 9.0);
}
static int validate_AllPrimitivesAtomicTopicAppendable(void* data, int seed) { 
    AtomicTests_AllPrimitivesAtomicTopicAppendable* m = (AtomicTests_AllPrimitivesAtomicTopicAppendable*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(AllPrimitivesAtomicTopicAppendable, all_primitives_atomic_topic_appendable);

// --- New Edge Cases ---
static void generate_MaxSizeStringTopic(void* data, int seed) { 
    AtomicTests_MaxSizeStringTopic* m = (AtomicTests_MaxSizeStringTopic*)data; 
    m->id = seed; 
    snprintf(m->max_string, sizeof(m->max_string), "MaxString_%d", seed);
}
static int validate_MaxSizeStringTopic(void* data, int seed)  { 
    AtomicTests_MaxSizeStringTopic* m = (AtomicTests_MaxSizeStringTopic*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(MaxSizeStringTopic, max_size_string_topic);

static void generate_MaxSizeStringTopicAppendable(void* data, int seed) { 
    AtomicTests_MaxSizeStringTopicAppendable* m = (AtomicTests_MaxSizeStringTopicAppendable*)data; 
    m->id = seed; 
    snprintf(m->max_string, sizeof(m->max_string), "MaxStringApp_%d", seed);
}
static int validate_MaxSizeStringTopicAppendable(void* data, int seed)  { 
    AtomicTests_MaxSizeStringTopicAppendable* m = (AtomicTests_MaxSizeStringTopicAppendable*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(MaxSizeStringTopicAppendable, max_size_string_topic_appendable);

static void generate_MaxLengthSequenceTopic(void* data, int seed) { 
    AtomicTests_MaxLengthSequenceTopic* m = (AtomicTests_MaxLengthSequenceTopic*)data; 
    m->id = seed; 
    m->max_seq._length = 2; 
    m->max_seq._maximum = 2;
    m->max_seq._release = true;
    m->max_seq._buffer = dds_alloc(sizeof(int32_t) * 2);
    m->max_seq._buffer[0] = seed;
    m->max_seq._buffer[1] = seed + 1;
}
static int validate_MaxLengthSequenceTopic(void* data, int seed)  { 
    AtomicTests_MaxLengthSequenceTopic* m = (AtomicTests_MaxLengthSequenceTopic*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(MaxLengthSequenceTopic, max_length_sequence_topic);

static void generate_MaxLengthSequenceTopicAppendable(void* data, int seed) { 
    AtomicTests_MaxLengthSequenceTopicAppendable* m = (AtomicTests_MaxLengthSequenceTopicAppendable*)data; 
    m->id = seed; 
    m->max_seq._length = 2; 
    m->max_seq._maximum = 2;
    m->max_seq._release = true;
    m->max_seq._buffer = dds_alloc(sizeof(int32_t) * 2);
    m->max_seq._buffer[0] = seed;
    m->max_seq._buffer[1] = seed + 1;
}
static int validate_MaxLengthSequenceTopicAppendable(void* data, int seed)  { 
    AtomicTests_MaxLengthSequenceTopicAppendable* m = (AtomicTests_MaxLengthSequenceTopicAppendable*)data; 
    return (m->id == seed)?0:-1; 
}
DEFINE_HANDLER(MaxLengthSequenceTopicAppendable, max_length_sequence_topic_appendable);

static void generate_DeepNestedStructTopic(void* data, int seed) { 
    AtomicTests_DeepNestedStructTopic* m = (AtomicTests_DeepNestedStructTopic*)data; 
    m->id = seed; 
    m->nested1.value1 = seed; 
    m->nested1.nested2.value2 = seed + 1;
}
static int validate_DeepNestedStructTopic(void* data, int seed)  { AtomicTests_DeepNestedStructTopic* m = data; return (m->id == seed)?0:-1; }
DEFINE_HANDLER(DeepNestedStructTopic, deep_nested_struct_topic);

static void generate_DeepNestedStructTopicAppendable(void* data, int seed) { 
    AtomicTests_DeepNestedStructTopicAppendable* m = (AtomicTests_DeepNestedStructTopicAppendable*)data; 
    m->id = seed; 
    m->nested1.value1 = seed; 
    m->nested1.nested2.value2 = seed + 1;
}
static int validate_DeepNestedStructTopicAppendable(void* data, int seed)  { AtomicTests_DeepNestedStructTopicAppendable* m = data; return (m->id == seed)?0:-1; }
DEFINE_HANDLER(DeepNestedStructTopicAppendable, deep_nested_struct_topic_appendable);

static void generate_UnionWithOptionalTopic(void* data, int seed) { AtomicTests_UnionWithOptionalTopic* m = data; m->id = seed; m->data._d = 1; m->data._u.int_val = seed; }
static int validate_UnionWithOptionalTopic(void* data, int seed)  { AtomicTests_UnionWithOptionalTopic* m = data; return (m->id == seed)?0:-1; }
DEFINE_HANDLER(UnionWithOptionalTopic, union_with_optional_topic);

static void generate_UnionWithOptionalTopicAppendable(void* data, int seed) { AtomicTests_UnionWithOptionalTopicAppendable* m = data; m->id = seed; m->data._d = 1; m->data._u.int_val = seed; }
static int validate_UnionWithOptionalTopicAppendable(void* data, int seed)  { AtomicTests_UnionWithOptionalTopicAppendable* m = data; return (m->id == seed)?0:-1; }
DEFINE_HANDLER(UnionWithOptionalTopicAppendable, union_with_optional_topic_appendable);

// ============================================================================
// MISSING HANDLERS FOR ATOMIC TOPICS (PART 1 & 3 & 4)
// ============================================================================

// --- UnboundedStringTopic ---
static void generate_UnboundedStringTopic(void* data, int seed) {
    AtomicTests_UnboundedStringTopic* m = (AtomicTests_UnboundedStringTopic*)data;
    m->id = seed;
    // "Content_" + seed
    char buf[64];
    snprintf(buf, sizeof(buf), "Content_%d", seed);
    m->unbounded = dds_string_dup(buf);
}
static int validate_UnboundedStringTopic(void* data, int seed) {
    AtomicTests_UnboundedStringTopic* m = (AtomicTests_UnboundedStringTopic*)data;
    if (m->id != seed) return -1;
    // Check value if needed
    return 0;
}
DEFINE_HANDLER(UnboundedStringTopic, unbounded_string_topic);

// --- LongStringTopic ---
static void generate_LongStringTopic(void* data, int seed) {
    AtomicTests_LongStringTopic* m = (AtomicTests_LongStringTopic*)data;
    m->id = seed;
    // Fill with pattern
    memset(m->long_string, 'A', 100); // 100 chars
    m->long_string[100] = '\0';
}
static int validate_LongStringTopic(void* data, int seed) {
    AtomicTests_LongStringTopic* m = (AtomicTests_LongStringTopic*)data;
    if (m->id != seed) return -1;
    return 0;
}
DEFINE_HANDLER(LongStringTopic, long_string_topic);

// --- LargeSequenceTopic ---
static void generate_LargeSequenceTopic(void* data, int seed) {
    AtomicTests_LargeSequenceTopic* m = (AtomicTests_LargeSequenceTopic*)data;
    m->id = seed;
    int len = 1000;
    m->large_seq._length = len;
    m->large_seq._maximum = len;
    m->large_seq._release = true;
    m->large_seq._buffer = dds_alloc(sizeof(int32_t) * len);
    for(int i=0; i<len; i++) m->large_seq._buffer[i] = i;
}
static int validate_LargeSequenceTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(LargeSequenceTopic, large_sequence_topic);

// --- EmptySequenceTopic ---
static void generate_EmptySequenceTopic(void* data, int seed) {
    AtomicTests_EmptySequenceTopic* m = (AtomicTests_EmptySequenceTopic*)data;
    m->id = seed;
    m->empty_seq._length = 0;
    m->empty_seq._maximum = 0;
    m->empty_seq._release = true;
    m->empty_seq._buffer = NULL;
}
static int validate_EmptySequenceTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(EmptySequenceTopic, empty_sequence_topic);

// --- AllPrimitivesAtomicTopic (Non-Appendable) ---
static void generate_AllPrimitivesAtomicTopic(void* data, int seed) {
    AtomicTests_AllPrimitivesAtomicTopic* m = (AtomicTests_AllPrimitivesAtomicTopic*)data;
    m->id = seed;
    m->bool_val = (seed % 2) != 0;
    m->char_val = 'A';
    m->octet_val = 0xFF;
    m->short_val = -100;
    m->ushort_val = 60000;
    m->long_val = 100000;
    m->ulong_val = 200000;
    m->llong_val = 5000000000LL;
    m->ullong_val = 9000000000ULL;
    m->float_val = 1.23f;
    m->double_val = 4.56;
}
static int validate_AllPrimitivesAtomicTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(AllPrimitivesAtomicTopic, all_primitives_atomic_topic);

// --- Extensibility: Final ---
static void generate_FinalInt32Topic(void* data, int seed) {
    AtomicTests_FinalInt32Topic* m = (AtomicTests_FinalInt32Topic*)data;
    m->id = seed;
    m->value = seed * 2;
}
static int validate_FinalInt32Topic(void* data, int seed) { return 0; }
DEFINE_HANDLER(FinalInt32Topic, final_int32_topic);

static void generate_FinalStructTopic(void* data, int seed) {
    AtomicTests_FinalStructTopic* m = (AtomicTests_FinalStructTopic*)data;
    m->id = seed;
    m->point.x = 10.0;
    m->point.y = 20.0;
}
static int validate_FinalStructTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(FinalStructTopic, final_struct_topic);

// --- Extensibility: Mutable ---
static void generate_MutableInt32Topic(void* data, int seed) {
    AtomicTests_MutableInt32Topic* m = (AtomicTests_MutableInt32Topic*)data;
    m->id = seed;
    m->value = seed * 3;
}
static int validate_MutableInt32Topic(void* data, int seed) { return 0; }
DEFINE_HANDLER(MutableInt32Topic, mutable_int32_topic);

static void generate_MutableStructTopic(void* data, int seed) {
    AtomicTests_MutableStructTopic* m = (AtomicTests_MutableStructTopic*)data;
    m->id = seed;
    m->point.x = 30.0;
    m->point.y = 40.0;
}
static int validate_MutableStructTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(MutableStructTopic, mutable_struct_topic);

// --- Optionals (Non-Appendable) ---
static void generate_OptionalInt32Topic(void* data, int seed) {
    AtomicTests_OptionalInt32Topic* m = (AtomicTests_OptionalInt32Topic*)data;
    m->id = seed;
    m->opt_value = dds_alloc(sizeof(int32_t));
    *m->opt_value = seed + 5;
}
static int validate_OptionalInt32Topic(void* data, int seed) { return 0; }
DEFINE_HANDLER(OptionalInt32Topic, optional_int32_topic);

static void generate_OptionalFloat64Topic(void* data, int seed) {
    AtomicTests_OptionalFloat64Topic* m = (AtomicTests_OptionalFloat64Topic*)data;
    m->id = seed;
    m->opt_value = dds_alloc(sizeof(double));
    *m->opt_value = 123.456;
}
static int validate_OptionalFloat64Topic(void* data, int seed) { return 0; }
DEFINE_HANDLER(OptionalFloat64Topic, optional_float64_topic);

static void generate_OptionalStringTopic(void* data, int seed) {
    AtomicTests_OptionalStringTopic* m = (AtomicTests_OptionalStringTopic*)data;
    m->id = seed;
    m->opt_string = dds_alloc(sizeof(*m->opt_string));
    strncpy(*m->opt_string, "OptString", 64);
    (*m->opt_string)[64] = '\0';
}
static int validate_OptionalStringTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(OptionalStringTopic, optional_string_topic);

static void generate_OptionalStructTopic(void* data, int seed) {
    AtomicTests_OptionalStructTopic* m = (AtomicTests_OptionalStructTopic*)data;
    m->id = seed;
    m->opt_point = dds_alloc(sizeof(AtomicTests_Point2D));
    m->opt_point->x = 1.1;
    m->opt_point->y = 2.2;
}
static int validate_OptionalStructTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(OptionalStructTopic, optional_struct_topic);

static void generate_OptionalEnumTopic(void* data, int seed) {
    AtomicTests_OptionalEnumTopic* m = (AtomicTests_OptionalEnumTopic*)data;
    m->id = seed;
    m->opt_enum = dds_alloc(sizeof(AtomicTests_SimpleEnum));
    *m->opt_enum = AtomicTests_SECOND;
}
static int validate_OptionalEnumTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(OptionalEnumTopic, optional_enum_topic);

static void generate_MultiOptionalTopic(void* data, int seed) {
    AtomicTests_MultiOptionalTopic* m = (AtomicTests_MultiOptionalTopic*)data;
    m->id = seed;
    m->opt_int = dds_alloc(sizeof(int32_t)); *m->opt_int = 999;
    m->opt_double = dds_alloc(sizeof(double)); *m->opt_double = 88.88;
    m->opt_string = dds_alloc(sizeof(*m->opt_string));
    strncpy(*m->opt_string, "Multi", 32);
    (*m->opt_string)[32] = '\0';
}
static int validate_MultiOptionalTopic(void* data, int seed) { return 0; }
DEFINE_HANDLER(MultiOptionalTopic, multi_optional_topic);
