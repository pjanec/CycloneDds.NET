#include "type_registry.h"
#include "atomic_tests.h"
#include "dds/dds.h"
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <stdlib.h>

// ============================================================================
// ArrayInt32Topic Handler
// ============================================================================

void* alloc_ArrayInt32Topic() {
    return dds_alloc(sizeof(AtomicTests_ArrayInt32Topic));
}

void free_ArrayInt32Topic(void* sample) {
    dds_sample_free(sample, &AtomicTests_ArrayInt32Topic_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_ArrayInt32Topic() {
    return &AtomicTests_ArrayInt32Topic_desc;
}

void fill_ArrayInt32Topic(void* sample, int seed) {
    AtomicTests_ArrayInt32Topic* msg = (AtomicTests_ArrayInt32Topic*)sample;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = seed + i;
    }
}

bool compare_ArrayInt32Topic(const void* a, const void* b) {
    const AtomicTests_ArrayInt32Topic* x = (const AtomicTests_ArrayInt32Topic*)a;
    const AtomicTests_ArrayInt32Topic* y = (const AtomicTests_ArrayInt32Topic*)b;
    
    if (x->id != y->id) {
        printf("ArrayInt32Topic.id mismatch: %d != %d\n", x->id, y->id);
        return false;
    }
    for(int i=0; i<5; i++) {
        if (x->values[i] != y->values[i]) {
            printf("ArrayInt32Topic.values[%d] mismatch: %d != %d\n", i, x->values[i], y->values[i]);
            return false;
        }
    }
    return true;
}

// ============================================================================
// ArrayFloat64Topic Handler
// ============================================================================

void* alloc_ArrayFloat64Topic() {
    return dds_alloc(sizeof(AtomicTests_ArrayFloat64Topic));
}

void free_ArrayFloat64Topic(void* sample) {
    dds_sample_free(sample, &AtomicTests_ArrayFloat64Topic_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_ArrayFloat64Topic() {
    return &AtomicTests_ArrayFloat64Topic_desc;
}

void fill_ArrayFloat64Topic(void* sample, int seed) {
    AtomicTests_ArrayFloat64Topic* msg = (AtomicTests_ArrayFloat64Topic*)sample;
    msg->id = seed;
    for(int i=0; i<5; i++) {
        msg->values[i] = (double)(seed + i) * 1.1;
    }
}

bool compare_ArrayFloat64Topic(const void* a, const void* b) {
    const AtomicTests_ArrayFloat64Topic* x = (const AtomicTests_ArrayFloat64Topic*)a;
    const AtomicTests_ArrayFloat64Topic* y = (const AtomicTests_ArrayFloat64Topic*)b;

    if (x->id != y->id) {
         printf("ArrayFloat64Topic.id mismatch\n");
         return false;
    }
    for(int i=0; i<5; i++) {
        if (fabs(x->values[i] - y->values[i]) > 0.0001) {
            printf("ArrayFloat64Topic.values[%d] mismatch: %f != %f\n", i, x->values[i], y->values[i]);
            return false;
        }
    }
    return true;
}

// ============================================================================
// ArrayStringTopic Handler
// ============================================================================

void* alloc_ArrayStringTopic() {
    return dds_alloc(sizeof(AtomicTests_ArrayStringTopic));
}

void free_ArrayStringTopic(void* sample) {
    dds_sample_free(sample, &AtomicTests_ArrayStringTopic_desc, DDS_FREE_ALL);
}

const dds_topic_descriptor_t* descriptor_ArrayStringTopic() {
    return &AtomicTests_ArrayStringTopic_desc;
}

void fill_ArrayStringTopic(void* sample, int seed) {
    AtomicTests_ArrayStringTopic* msg = (AtomicTests_ArrayStringTopic*)sample;
    msg->id = seed;
    for(int i=0; i<3; i++) {
        char buffer[16];
        snprintf(buffer, 16, "S_%d_%d", seed, i);
        // Assuming the generated struct for string<16> array is char[3][17] or similar
        // if IDL is: string<16> names[3];
        // C mapping: char names[3][17]; (including null)
        strncpy(msg->names[i], buffer, 16);
        msg->names[i][16] = '\0'; 
    }
}

bool compare_ArrayStringTopic(const void* a, const void* b) {
    const AtomicTests_ArrayStringTopic* x = (const AtomicTests_ArrayStringTopic*)a;
    const AtomicTests_ArrayStringTopic* y = (const AtomicTests_ArrayStringTopic*)b;

    if (x->id != y->id) {
        printf("ArrayStringTopic.id mismatch\n");
        return false;
    }
    for(int i=0; i<3; i++) {
        if (strncmp(x->names[i], y->names[i], 16) != 0) {
            printf("ArrayStringTopic.names[%d] mismatch: '%s' != '%s'\n", i, x->names[i], y->names[i]);
            return false;
        }
    }
    return true;
}
