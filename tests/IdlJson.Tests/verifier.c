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

    printf("\n==================================================\n");
    if (errors == 0) printf("RESULT: PASSED (All %d topics verified)\n", 10);
    else printf("RESULT: FAILED (%d errors)\n", errors);
    printf("==================================================\n");

    free(data);
    cJSON_Delete(json);
    return errors;
}
