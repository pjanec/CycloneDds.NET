#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stddef.h>
#include <inttypes.h>

// Include the IDLC generated header (C Backend)
#include "verification.h"

// Include cJSON
#include "cJSON/cJSON.h"

#define ASSERT_EQ(name, actual, expected) \
    do { \
        if ((long long)(actual) != (long long)(expected)) { \
            fprintf(stderr, "[FAIL] %s: C-Compiler %lld != JSON %lld\n", \
                    name, (long long)(actual), (long long)(expected)); \
            errors++; \
        } else { \
            printf("[PASS] %s: %lld\n", name, (long long)(actual)); \
        } \
    } while(0)

// Helper to find a type definition in the JSON array
cJSON* find_type(cJSON* root, const char* name) {
    cJSON* types = cJSON_GetObjectItem(root, "Types");
    cJSON* item = NULL;
    cJSON_ArrayForEach(item, types) {
        cJSON* n = cJSON_GetObjectItem(item, "Name");
        if (n && strcmp(n->valuestring, name) == 0) return item;
    }
    return NULL;
}

// Helper to find a member in a Type object
cJSON* find_member(cJSON* typeNode, const char* memberName) {
    cJSON* members = cJSON_GetObjectItem(typeNode, "Members");
    cJSON* item = NULL;
    cJSON_ArrayForEach(item, members) {
        cJSON* n = cJSON_GetObjectItem(item, "Name");
        if (n && strcmp(n->valuestring, memberName) == 0) return item;
    }
    return NULL;
}

// Generic descriptor verification
int verify_descriptor(const char* type_name, const dds_topic_descriptor_t* desc, cJSON* jNode, int* errors) {
    printf("\n--- Checking Topic Descriptor: %s ---\n", type_name);
    
    cJSON* jDesc = cJSON_GetObjectItem(jNode, "TopicDescriptor");
    if (!jDesc) {
        fprintf(stderr, "[SKIP] TopicDescriptor missing for %s\n", type_name);
        return 0;
    }
    
    cJSON* jOps = cJSON_GetObjectItem(jDesc, "Ops");
    if (!jOps) {
        fprintf(stderr, "[FAIL] Ops missing for %s\n", type_name);
        (*errors)++;
        return 0;
    }
    
    const uint32_t* cOps = desc->m_ops;
    uint32_t cNops = desc->m_nops;
    int jNops = cJSON_GetArraySize(jOps);
    
    ASSERT_EQ("Ops Count", cNops, jNops);
    
    int match_count = 0;
    for (int i = 0; i < jNops && i < (int)cNops; i++) {
        uint32_t jsonOp = (uint32_t)cJSON_GetArrayItem(jOps, i)->valueint;
        uint32_t cOp = cOps[i];
        
        if (jsonOp != cOp) {
            fprintf(stderr, "[FAIL] Opcode[%d]: C 0x%08X != JSON 0x%08X\n", i, cOp, jsonOp);
            (*errors)++;
        } else {
            match_count++;
        }
    }
    
    if (match_count == jNops && jNops == (int)cNops) {
        printf("[PASS] All %d Opcodes match.\n", match_count);
    }
    
    return 1;
}

// Macro to verify a topic
#define VERIFY_TOPIC(TYPE_NAME, C_TYPE) \
    do { \
        cJSON* jNode = find_type(json, "Verification::" TYPE_NAME); \
        if (jNode) { \
            ASSERT_EQ("sizeof(" TYPE_NAME ")", sizeof(Verification_##C_TYPE), \
                      cJSON_GetObjectItem(jNode, "Size")->valueint); \
            verify_descriptor(TYPE_NAME, &Verification_##C_TYPE##_desc, jNode, &errors); \
        } else { \
            printf("[SKIP] Type %s not found in JSON\n", TYPE_NAME); \
        } \
    } while(0)

// Macro to verify RoundtripTests topics
#define VERIFY_ROUNDTRIP_TOPIC(TYPE_NAME, C_TYPE) \
    do { \
        cJSON* jNode = find_type(json, "RoundtripTests::" TYPE_NAME); \
        if (jNode) { \
            ASSERT_EQ("sizeof(RoundtripTests::" TYPE_NAME ")", sizeof(RoundtripTests_##C_TYPE), \
                      cJSON_GetObjectItem(jNode, "Size")->valueint); \
            verify_descriptor("RoundtripTests::" TYPE_NAME, &RoundtripTests_##C_TYPE##_desc, jNode, &errors); \
        } else { \
            printf("[SKIP] Type RoundtripTests::%s not found in JSON\n", TYPE_NAME); \
        } \
    } while(0)

// Macro to verify struct/union size only
#define VERIFY_SIZE(TYPE_NAME, C_TYPE) \
    do { \
        cJSON* jNode = find_type(json, "Verification::" TYPE_NAME); \
        if (jNode) { \
            ASSERT_EQ("sizeof(" TYPE_NAME ")", sizeof(Verification_##C_TYPE), \
                      cJSON_GetObjectItem(jNode, "Size")->valueint); \
        } \
    } while(0)

int main(int argc, char** argv) {
    if (argc < 2) {
        fprintf(stderr, "Usage: %s <path_to_json_file>\n", argv[0]);
        return 1;
    }

    // --- Load JSON ---
    FILE* f = fopen(argv[1], "rb");
    if (!f) { perror("File open"); return 1; }
    fseek(f, 0, SEEK_END);
    long len = ftell(f);
    fseek(f, 0, SEEK_SET);
    char* data = (char*)malloc(len + 1);
    fread(data, 1, len, f);
    data[len] = '\0';
    fclose(f);

    cJSON* json = cJSON_Parse(data);
    if (!json) { fprintf(stderr, "JSON Parse Error\n"); free(data); return 1; }

    int errors = 0;

    printf("==================================================\n");
    printf("VERIFYING LAYOUT AGAINST C COMPILER ABI\n");
    printf("==================================================\n");

    // Verify basic structs
    VERIFY_SIZE("Point2D", Point2D);
    VERIFY_SIZE("Point3D", Point3D);
    VERIFY_SIZE("NestedStruct", NestedStruct);
    VERIFY_SIZE("Shape", Shape);
    
    // Verify all topics
    VERIFY_TOPIC("AllPrimitives", AllPrimitives);
    VERIFY_TOPIC("CompositeKey", CompositeKey);
    VERIFY_TOPIC("NestedKeyTopic", NestedKeyTopic);
    VERIFY_TOPIC("SequenceTopic", SequenceTopic);
    VERIFY_TOPIC("NestedSequences", NestedSequences);
    VERIFY_TOPIC("ArrayTopic", ArrayTopic);
    VERIFY_TOPIC("StringTopic", StringTopic);
    VERIFY_TOPIC("OptionalFields", OptionalFields);
    VERIFY_TOPIC("MixedContent", MixedContent);
    VERIFY_TOPIC("UnionTopic", UnionTopic);
    VERIFY_TOPIC("TypedefStruct", TypedefStruct);

    // Verify RoundtripTests topics
    VERIFY_ROUNDTRIP_TOPIC("AllPrimitives", AllPrimitives);
    VERIFY_ROUNDTRIP_TOPIC("CompositeKey", CompositeKey);
    VERIFY_ROUNDTRIP_TOPIC("NestedKeyTopic", NestedKeyTopic);

    // Macro for AtomicTests
    #define VERIFY_ATOMIC_TOPIC(TYPE_NAME, C_TYPE) \
    do { \
        cJSON* jNode = find_type(json, "AtomicTests::" TYPE_NAME); \
        if (jNode) { \
            ASSERT_EQ("sizeof(AtomicTests::" TYPE_NAME ")", sizeof(AtomicTests_##C_TYPE), \
                      cJSON_GetObjectItem(jNode, "Size")->valueint); \
            verify_descriptor("AtomicTests::" TYPE_NAME, &AtomicTests_##C_TYPE##_desc, jNode, &errors); \
        } else { \
            printf("[SKIP] Type AtomicTests::%s not found in JSON\n", TYPE_NAME); \
        } \
    } while(0)

    // Verify AtomicTests (Batch 1: Basic Primitives)
    VERIFY_ATOMIC_TOPIC("BooleanTopic", BooleanTopic);
    VERIFY_ATOMIC_TOPIC("CharTopic", CharTopic);
    VERIFY_ATOMIC_TOPIC("OctetTopic", OctetTopic);
    VERIFY_ATOMIC_TOPIC("Int16Topic", Int16Topic);
    VERIFY_ATOMIC_TOPIC("UInt16Topic", UInt16Topic);
    VERIFY_ATOMIC_TOPIC("Int32Topic", Int32Topic);
    VERIFY_ATOMIC_TOPIC("UInt32Topic", UInt32Topic);
    VERIFY_ATOMIC_TOPIC("Int64Topic", Int64Topic);
    VERIFY_ATOMIC_TOPIC("UInt64Topic", UInt64Topic);
    VERIFY_ATOMIC_TOPIC("Float32Topic", Float32Topic);
    VERIFY_ATOMIC_TOPIC("Float64Topic", Float64Topic);
    VERIFY_ATOMIC_TOPIC("StringUnboundedTopic", StringUnboundedTopic);
    VERIFY_ATOMIC_TOPIC("StringBounded32Topic", StringBounded32Topic);
    VERIFY_ATOMIC_TOPIC("StringBounded256Topic", StringBounded256Topic);

    // Verify AtomicTests (Batch 2: Enums)
    VERIFY_ATOMIC_TOPIC("EnumTopic", EnumTopic);
    VERIFY_ATOMIC_TOPIC("ColorEnumTopic", ColorEnumTopic);

    // Verify AtomicTests (Batch 3: Nested Structs)
    VERIFY_ATOMIC_TOPIC("NestedStructTopic", NestedStructTopic);
    VERIFY_ATOMIC_TOPIC("Nested3DTopic", Nested3DTopic);
    VERIFY_ATOMIC_TOPIC("DoublyNestedTopic", DoublyNestedTopic);
    VERIFY_ATOMIC_TOPIC("ComplexNestedTopic", ComplexNestedTopic);

    // Verify AtomicTests (Batch 4: Unions)
    VERIFY_ATOMIC_TOPIC("UnionLongDiscTopic", UnionLongDiscTopic);
    VERIFY_ATOMIC_TOPIC("UnionBoolDiscTopic", UnionBoolDiscTopic);
    VERIFY_ATOMIC_TOPIC("UnionEnumDiscTopic", UnionEnumDiscTopic);
    VERIFY_ATOMIC_TOPIC("UnionShortDiscTopic", UnionShortDiscTopic);

    // Verify AtomicTests (Batch 5: Optional Fields)
    VERIFY_ATOMIC_TOPIC("OptionalInt32Topic", OptionalInt32Topic);
    VERIFY_ATOMIC_TOPIC("OptionalFloat64Topic", OptionalFloat64Topic);
    VERIFY_ATOMIC_TOPIC("OptionalStringTopic", OptionalStringTopic);
    VERIFY_ATOMIC_TOPIC("OptionalStructTopic", OptionalStructTopic);
    VERIFY_ATOMIC_TOPIC("OptionalEnumTopic", OptionalEnumTopic);
    VERIFY_ATOMIC_TOPIC("MultiOptionalTopic", MultiOptionalTopic);

    // Verify AtomicTests (Batch 6: Sequences)
    VERIFY_ATOMIC_TOPIC("SequenceInt32Topic", SequenceInt32Topic);
    VERIFY_ATOMIC_TOPIC("BoundedSequenceInt32Topic", BoundedSequenceInt32Topic);
    VERIFY_ATOMIC_TOPIC("SequenceInt64Topic", SequenceInt64Topic);
    VERIFY_ATOMIC_TOPIC("SequenceFloat32Topic", SequenceFloat32Topic);
    VERIFY_ATOMIC_TOPIC("SequenceFloat64Topic", SequenceFloat64Topic);
    VERIFY_ATOMIC_TOPIC("SequenceBooleanTopic", SequenceBooleanTopic);
    VERIFY_ATOMIC_TOPIC("SequenceOctetTopic", SequenceOctetTopic);
    VERIFY_ATOMIC_TOPIC("SequenceStringTopic", SequenceStringTopic);
    VERIFY_ATOMIC_TOPIC("SequenceEnumTopic", SequenceEnumTopic);
    VERIFY_ATOMIC_TOPIC("SequenceStructTopic", SequenceStructTopic);
    VERIFY_ATOMIC_TOPIC("SequenceUnionTopic", SequenceUnionTopic);

    // Verify AtomicTests (Batch 7: Arrays)
    VERIFY_ATOMIC_TOPIC("ArrayInt32Topic", ArrayInt32Topic);
    VERIFY_ATOMIC_TOPIC("ArrayFloat64Topic", ArrayFloat64Topic);
    VERIFY_ATOMIC_TOPIC("ArrayStringTopic", ArrayStringTopic);
    VERIFY_ATOMIC_TOPIC("Array2DInt32Topic", Array2DInt32Topic);
    VERIFY_ATOMIC_TOPIC("Array3DInt32Topic", Array3DInt32Topic);
    VERIFY_ATOMIC_TOPIC("ArrayStructTopic", ArrayStructTopic);

    // Verify AtomicTests (Batch 8: Extensibility)
    VERIFY_ATOMIC_TOPIC("AppendableInt32Topic", AppendableInt32Topic);
    VERIFY_ATOMIC_TOPIC("AppendableStructTopic", AppendableStructTopic);
    VERIFY_ATOMIC_TOPIC("FinalInt32Topic", FinalInt32Topic);
    VERIFY_ATOMIC_TOPIC("FinalStructTopic", FinalStructTopic);
    VERIFY_ATOMIC_TOPIC("MutableInt32Topic", MutableInt32Topic);
    VERIFY_ATOMIC_TOPIC("MutableStructTopic", MutableStructTopic);

    // Verify AtomicTests (Batch 9: Composite Keys)
    VERIFY_ATOMIC_TOPIC("TwoKeyInt32Topic", TwoKeyInt32Topic);
    VERIFY_ATOMIC_TOPIC("TwoKeyStringTopic", TwoKeyStringTopic);
    VERIFY_ATOMIC_TOPIC("ThreeKeyTopic", ThreeKeyTopic);
    VERIFY_ATOMIC_TOPIC("FourKeyTopic", FourKeyTopic);

    // Verify AtomicTests (Batch 10: Nested Keys)
    VERIFY_ATOMIC_TOPIC("NestedKeyTopic", NestedKeyTopic);
    VERIFY_ATOMIC_TOPIC("NestedKeyGeoTopic", NestedKeyGeoTopic);
    VERIFY_ATOMIC_TOPIC("NestedTripleKeyTopic", NestedTripleKeyTopic);

    // Verify AtomicTests (Batch 11: Edge Cases)
    VERIFY_ATOMIC_TOPIC("EmptySequenceTopic", EmptySequenceTopic);
    VERIFY_ATOMIC_TOPIC("LargeSequenceTopic", LargeSequenceTopic);
    VERIFY_ATOMIC_TOPIC("LongStringTopic", LongStringTopic);
    VERIFY_ATOMIC_TOPIC("UnboundedStringTopic", UnboundedStringTopic);
    VERIFY_ATOMIC_TOPIC("AllPrimitivesAtomicTopic", AllPrimitivesAtomicTopic);

    // Verify AtomicTests (Batch 12: Appendable Duplicates)
    VERIFY_ATOMIC_TOPIC("BooleanTopicAppendable", BooleanTopicAppendable);
    VERIFY_ATOMIC_TOPIC("Int32TopicAppendable", Int32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("StringBounded32TopicAppendable", StringBounded32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("SequenceInt32TopicAppendable", SequenceInt32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("UnionLongDiscTopicAppendable", UnionLongDiscTopicAppendable);
    VERIFY_ATOMIC_TOPIC("CharTopicAppendable", CharTopicAppendable);
    VERIFY_ATOMIC_TOPIC("OctetTopicAppendable", OctetTopicAppendable);
    VERIFY_ATOMIC_TOPIC("Int16TopicAppendable", Int16TopicAppendable);
    VERIFY_ATOMIC_TOPIC("UInt16TopicAppendable", UInt16TopicAppendable);
    VERIFY_ATOMIC_TOPIC("UInt32TopicAppendable", UInt32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("Int64TopicAppendable", Int64TopicAppendable);
    VERIFY_ATOMIC_TOPIC("UInt64TopicAppendable", UInt64TopicAppendable);
    VERIFY_ATOMIC_TOPIC("Float32TopicAppendable", Float32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("Float64TopicAppendable", Float64TopicAppendable);
    VERIFY_ATOMIC_TOPIC("StringUnboundedTopicAppendable", StringUnboundedTopicAppendable);
    VERIFY_ATOMIC_TOPIC("StringBounded256TopicAppendable", StringBounded256TopicAppendable);
    VERIFY_ATOMIC_TOPIC("EnumTopicAppendable", EnumTopicAppendable);
    VERIFY_ATOMIC_TOPIC("ColorEnumTopicAppendable", ColorEnumTopicAppendable);
    VERIFY_ATOMIC_TOPIC("ArrayInt32TopicAppendable", ArrayInt32TopicAppendable);
    VERIFY_ATOMIC_TOPIC("ArrayFloat64TopicAppendable", ArrayFloat64TopicAppendable);
    VERIFY_ATOMIC_TOPIC("ArrayStringTopicAppendable", ArrayStringTopicAppendable);


    printf("\n==================================================\n");
    if (errors == 0) printf("RESULT: PASSED (All topics verified)\n");
    else printf("RESULT: FAILED (%d errors)\n", errors);
    printf("==================================================\n");

    free(data);
    cJSON_Delete(json);
    return errors;
}
