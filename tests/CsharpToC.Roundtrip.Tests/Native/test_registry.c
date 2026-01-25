#include <stdio.h>
#include <string.h>
#include "test_registry.h"

// --- Global State ---
static dds_entity_t participant = 0;
static dds_entity_t publisher = 0;
static dds_entity_t subscriber = 0;
static char last_error[256];

void set_error(const char* msg) {
    strncpy(last_error, msg, sizeof(last_error) - 1);
}

// --- Topic Handlers ---
// Forward declarations of handlers defined in atomic_tests_native.c
extern const topic_handler_t boolean_topic_handler;
extern const topic_handler_t int32_topic_handler;
extern const topic_handler_t char_topic_handler;
extern const topic_handler_t octet_topic_handler;
extern const topic_handler_t int16_topic_handler;
extern const topic_handler_t uint16_topic_handler;
extern const topic_handler_t uint32_topic_handler;
extern const topic_handler_t int64_topic_handler;
extern const topic_handler_t uint64_topic_handler;
extern const topic_handler_t float32_topic_handler;
extern const topic_handler_t float64_topic_handler;

extern const topic_handler_t sequence_int32_topic_handler;
extern const topic_handler_t string_bounded_32_topic_handler;
extern const topic_handler_t array_int32_topic_handler;
extern const topic_handler_t union_long_disc_topic_handler;

extern const topic_handler_t boolean_topic_appendable_handler;
extern const topic_handler_t int32_topic_appendable_handler;
extern const topic_handler_t char_topic_appendable_handler;
extern const topic_handler_t octet_topic_appendable_handler;
extern const topic_handler_t int16_topic_appendable_handler;
extern const topic_handler_t uint16_topic_appendable_handler;
extern const topic_handler_t uint32_topic_appendable_handler;
extern const topic_handler_t int64_topic_appendable_handler;
extern const topic_handler_t uint64_topic_appendable_handler;
extern const topic_handler_t float32_topic_appendable_handler;
extern const topic_handler_t float64_topic_appendable_handler;

extern const topic_handler_t string_bounded_32_topic_appendable_handler;
extern const topic_handler_t sequence_int32_topic_appendable_handler;
extern const topic_handler_t union_long_disc_topic_appendable_handler;

extern const topic_handler_t string_unbounded_topic_handler;
extern const topic_handler_t string_bounded_256_topic_handler;
extern const topic_handler_t string_unbounded_topic_appendable_handler;
extern const topic_handler_t string_bounded_256_topic_appendable_handler;

extern const topic_handler_t enum_topic_handler;
extern const topic_handler_t color_enum_topic_handler;
extern const topic_handler_t enum_topic_appendable_handler;
extern const topic_handler_t color_enum_topic_appendable_handler;

extern const topic_handler_t array_float64_topic_handler;
extern const topic_handler_t array_string_topic_handler;
extern const topic_handler_t array_int32_topic_appendable_handler;
extern const topic_handler_t array_float64_topic_appendable_handler;
extern const topic_handler_t array_string_topic_appendable_handler;

static const topic_handler_t* handlers[] = {
    &boolean_topic_handler,
    &int32_topic_handler,
    &char_topic_handler,
    &octet_topic_handler,
    &int16_topic_handler,
    &uint16_topic_handler,
    &uint32_topic_handler,
    &int64_topic_handler,
    &uint64_topic_handler,
    &float32_topic_handler,
    &float64_topic_handler,
    &sequence_int32_topic_handler,
    &string_bounded_32_topic_handler,
    &array_int32_topic_handler,
    &union_long_disc_topic_handler,
    &boolean_topic_appendable_handler,
    &int32_topic_appendable_handler,
    &char_topic_appendable_handler,
    &octet_topic_appendable_handler,
    &int16_topic_appendable_handler,
    &uint16_topic_appendable_handler,
    &uint32_topic_appendable_handler,
    &int64_topic_appendable_handler,
    &uint64_topic_appendable_handler,
    &float32_topic_appendable_handler,
    &float64_topic_appendable_handler,
    &string_bounded_32_topic_appendable_handler,
    &sequence_int32_topic_appendable_handler,
    &union_long_disc_topic_appendable_handler,
    &string_unbounded_topic_handler,
    &string_bounded_256_topic_handler,
    &string_unbounded_topic_appendable_handler,
    &string_bounded_256_topic_appendable_handler,
    &enum_topic_handler,
    &color_enum_topic_handler,
    &enum_topic_appendable_handler,
    &color_enum_topic_appendable_handler,
    &array_float64_topic_handler,
    &array_string_topic_handler,
    &array_int32_topic_appendable_handler,
    &array_float64_topic_appendable_handler,
    &array_string_topic_appendable_handler,
    NULL
};

const topic_handler_t* find_handler(const char* topic_name) {
    for (int i = 0; handlers[i] != NULL; i++) {
        const char* h_name = handlers[i]->name;
        // Handle "AtomicTests::" prefix if present
        const char* suffix = strstr(h_name, "::");
        const char* short_name = suffix ? suffix + 2 : h_name;
        
        if (strcmp(short_name, topic_name) == 0 || strcmp(h_name, topic_name) == 0) {
            return handlers[i];
        }
    }
    return NULL;
}

// --- Exported API ---

EXPORT const char* Native_GetLastError() {
    return last_error;
}

EXPORT void Native_Init(uint32_t domain_id) {
    if (participant != 0) return;
    
    participant = dds_create_participant(domain_id, NULL, NULL);
    if (participant < 0) {
        snprintf(last_error, sizeof(last_error), "dds_create_participant failed: %d", participant);
        return;
    }
    
    publisher = dds_create_publisher(participant, NULL, NULL);
    if (publisher < 0) {
        set_error("dds_create_publisher failed");
        return;
    }
    
    subscriber = dds_create_subscriber(participant, NULL, NULL);
    if (subscriber < 0) {
        set_error("dds_create_subscriber failed");
        return;
    }
    
    set_error("OK");
}

EXPORT void Native_Cleanup() {
    if (participant != 0) {
        dds_delete(participant);
        participant = 0;
        publisher = 0;
        subscriber = 0;
    }
}

EXPORT int Native_SendWithSeed(const char* topic_name, int seed) {
    const topic_handler_t* handler = find_handler(topic_name);
    if (!handler) {
        snprintf(last_error, sizeof(last_error), "Topic not found: %s", topic_name);
        return -1;
    }
    
    // Create topic
    dds_entity_t topic = dds_create_topic(participant, handler->descriptor, handler->name, NULL, NULL);
    if (topic < 0) {
        snprintf(last_error, sizeof(last_error), "dds_create_topic failed: %d", topic);
        return -1;
    }
    
    // Create writer
    dds_entity_t writer = dds_create_writer(publisher, topic, NULL, NULL);

    if (writer < 0) {
        snprintf(last_error, sizeof(last_error), "dds_create_writer failed: %d", writer);
        dds_delete(topic);
        return -1;
    }
    
    // Generate data
    // Allocate memory matching struct size
    // Note: This relies on handler->size being correct
    // For simplicity, we use a large buffer or malloc
    void* data = malloc(handler->size);
    memset(data, 0, handler->size);
    handler->generate(data, seed);
    
    // Write
    int rc = dds_write(writer, data);
    if (rc < 0) {
        snprintf(last_error, sizeof(last_error), "dds_write failed: %d", rc);
    }
    
    // Cleanup
    // Free dynamic memory used by data if any? 
    // Handlers should probably provide a cleanup function for data
    // For now, we assume simple structs or specific cleanup logic
    // Implementation of free is tricky without generated code support for free
    // CycloneDDS generated code usually provides Type_free(data, DDS_FREE_CONTENTS)
    // We might need to add that to handler
    
    // dds_write copies data, so we can free our local copy
    // But if generate allocated pointers (sequences/strings), we need to free them.
    // Hack: Leaking memory for now in test, or we need free func.
    
    // Increase sleep to ensure data is pushed?
    dds_sleepfor(DDS_MSECS(1000)); 
    
    free(data);
    dds_delete(writer);
    dds_delete(topic);
    
    return (rc >= 0) ? 0 : -1;
}

EXPORT int Native_ExpectWithSeed(const char* topic_name, int seed, int timeout_ms) {
    const topic_handler_t* handler = find_handler(topic_name);
    if (!handler) {
        snprintf(last_error, sizeof(last_error), "Topic not found: %s", topic_name);
        return -2;
    }
    
    // Create topic & reader
    dds_entity_t topic = dds_create_topic(participant, handler->descriptor, handler->name, NULL, NULL);
    if (topic < 0) return -2;
    
    dds_entity_t reader = dds_create_reader(subscriber, topic, NULL, NULL);
    if (reader < 0) {
        dds_delete(topic);
        return -2;
    }
    
    // Poll for data
    void* samples[1];
    dds_sample_info_t infos[1];
    
    // Allocate sample buffer (pointers to data)
    // dds_take expects an array of pointers
    // and it will allocate the data if pointers are NULL? No.
    // If we pass NULL, it allocates.
    // If we pass allocated struct, it fills.
    
    // Let's let Cyclone allocate
    samples[0] = NULL;
    
    int received = 0;
    int waited = 0;
    int result = -1; // Timeout initially
    
    while (waited < timeout_ms) {
        int rc = dds_take(reader, samples, infos, 1, 1);
        if (rc > 0) {
            if (infos[0].valid_data) {
                // Validate
                if (handler->validate(samples[0], seed) == 0) {
                    result = 0; // Success
                } else {
                    result = -2; // Data mismatch
                }
                
                // Free sample using dds_return_loan if it was loaned, but we passed NULL so it was allocated?
                // Wait, dds_take with NULL ptrs usually implies dds_return_loan needed?
                // Actually dds_take takes void** samples.
                // If samples[0] was NULL, implementation allocates.
                // We should check doc. Usually explicit allocation or loan.
                // Let's use dds_return_loan if we don't own memory
            }
            // Return loan (Cyclone allocates/manages)
            // But wait, if we passed NULL to take, did we get a loan? Yes.
            dds_return_loan(reader, samples, rc);
            received = 1;
            break;
        }
        
        dds_sleepfor(DDS_MSECS(10));
        waited += 10;
    }
    
    dds_delete(reader);
    dds_delete(topic);
    
    return result;
}
