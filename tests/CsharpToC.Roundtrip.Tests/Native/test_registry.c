#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "test_registry.h"
#include "dds/cdr/dds_cdrstream.h"

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
extern const topic_handler_t bounded_sequence_int32_topic_handler;
extern const topic_handler_t sequence_int64_topic_handler;
extern const topic_handler_t sequence_float32_topic_handler;
extern const topic_handler_t sequence_float64_topic_handler;
extern const topic_handler_t sequence_boolean_topic_handler;
extern const topic_handler_t sequence_octet_topic_handler;
extern const topic_handler_t sequence_string_topic_handler;
extern const topic_handler_t sequence_enum_topic_handler;
extern const topic_handler_t sequence_struct_topic_handler;
extern const topic_handler_t sequence_union_topic_handler;
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

extern const topic_handler_t array_2d_int32_topic_handler;
extern const topic_handler_t array_3d_int32_topic_handler;
extern const topic_handler_t array_struct_topic_handler;

extern const topic_handler_t nested_struct_topic_handler;
extern const topic_handler_t nested_3d_topic_handler;
extern const topic_handler_t doubly_nested_topic_handler;
extern const topic_handler_t complex_nested_topic_handler;

extern const topic_handler_t two_key_int32_topic_handler;
extern const topic_handler_t two_key_string_topic_handler;
extern const topic_handler_t three_key_topic_handler;
extern const topic_handler_t four_key_topic_handler;
extern const topic_handler_t nested_key_topic_handler;
extern const topic_handler_t nested_key_geo_topic_handler;
extern const topic_handler_t nested_triple_key_topic_handler;

extern const topic_handler_t union_bool_disc_topic_handler;
extern const topic_handler_t union_enum_disc_topic_handler;
extern const topic_handler_t union_short_disc_topic_handler;
extern const topic_handler_t sequence_union_appendable_topic_handler;
extern const topic_handler_t sequence_enum_appendable_topic_handler;

// --- Part 2 New Handlers ---
extern const topic_handler_t bounded_sequence_int32_topic_appendable_handler;
extern const topic_handler_t sequence_int64_topic_appendable_handler;
extern const topic_handler_t sequence_float32_topic_appendable_handler;
extern const topic_handler_t sequence_float64_topic_appendable_handler;
extern const topic_handler_t sequence_boolean_topic_appendable_handler;
extern const topic_handler_t sequence_octet_topic_appendable_handler;
extern const topic_handler_t sequence_string_topic_appendable_handler;
extern const topic_handler_t sequence_struct_topic_appendable_handler;
extern const topic_handler_t nested_struct_topic_appendable_handler;
extern const topic_handler_t nested_3d_topic_appendable_handler;
extern const topic_handler_t doubly_nested_topic_appendable_handler;
extern const topic_handler_t complex_nested_topic_appendable_handler;
extern const topic_handler_t union_bool_disc_topic_appendable_handler;
extern const topic_handler_t union_enum_disc_topic_appendable_handler;
extern const topic_handler_t union_short_disc_topic_appendable_handler;
extern const topic_handler_t optional_int32_topic_appendable_handler;
extern const topic_handler_t optional_float64_topic_appendable_handler;
extern const topic_handler_t optional_string_topic_appendable_handler;
extern const topic_handler_t optional_struct_topic_appendable_handler;
extern const topic_handler_t optional_enum_topic_appendable_handler;
extern const topic_handler_t multi_optional_topic_appendable_handler;
extern const topic_handler_t two_key_int32_topic_appendable_handler;
extern const topic_handler_t two_key_string_topic_appendable_handler;
extern const topic_handler_t three_key_topic_appendable_handler;
extern const topic_handler_t four_key_topic_appendable_handler;
extern const topic_handler_t nested_key_topic_appendable_handler;
extern const topic_handler_t nested_key_geo_topic_appendable_handler;
extern const topic_handler_t nested_triple_key_topic_appendable_handler;
extern const topic_handler_t empty_sequence_topic_appendable_handler;
extern const topic_handler_t unbounded_string_topic_appendable_handler;
extern const topic_handler_t all_primitives_atomic_topic_appendable_handler;
extern const topic_handler_t max_size_string_topic_handler;
extern const topic_handler_t max_size_string_topic_appendable_handler;
extern const topic_handler_t max_length_sequence_topic_handler;
extern const topic_handler_t max_length_sequence_topic_appendable_handler;
extern const topic_handler_t deep_nested_struct_topic_handler;
extern const topic_handler_t deep_nested_struct_topic_appendable_handler;
extern const topic_handler_t union_with_optional_topic_handler;
extern const topic_handler_t union_with_optional_topic_handler;
extern const topic_handler_t union_with_optional_topic_appendable_handler;

// --- Part 3 Missing Handlers ---
extern const topic_handler_t unbounded_string_topic_handler;
extern const topic_handler_t long_string_topic_handler;
extern const topic_handler_t large_sequence_topic_handler;
extern const topic_handler_t empty_sequence_topic_handler;
extern const topic_handler_t all_primitives_atomic_topic_handler;
extern const topic_handler_t final_int32_topic_handler;
extern const topic_handler_t final_struct_topic_handler;
extern const topic_handler_t mutable_int32_topic_handler;
extern const topic_handler_t mutable_struct_topic_handler;
extern const topic_handler_t optional_int32_topic_handler;
extern const topic_handler_t optional_float64_topic_handler;
extern const topic_handler_t optional_string_topic_handler;
extern const topic_handler_t optional_struct_topic_handler;
extern const topic_handler_t optional_enum_topic_handler;
extern const topic_handler_t multi_optional_topic_handler;



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
    &bounded_sequence_int32_topic_handler,
    &sequence_int64_topic_handler,
    &sequence_float32_topic_handler,
    &sequence_float64_topic_handler,
    &sequence_boolean_topic_handler,
    &sequence_octet_topic_handler,
    &sequence_string_topic_handler,
    &sequence_enum_topic_handler,
    &sequence_struct_topic_handler,
    &sequence_union_topic_handler,
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
    &array_2d_int32_topic_handler,
    &array_3d_int32_topic_handler,
    &array_struct_topic_handler,
    &nested_struct_topic_handler,
    &nested_3d_topic_handler,
    &doubly_nested_topic_handler,
    &complex_nested_topic_handler,
    &two_key_int32_topic_handler,
    &two_key_string_topic_handler,
    &three_key_topic_handler,
    &four_key_topic_handler,
    &nested_key_topic_handler,
    &nested_key_geo_topic_handler,
    &nested_triple_key_topic_handler,
    &union_bool_disc_topic_handler,
    &union_enum_disc_topic_handler,
    &union_short_disc_topic_handler,
    &sequence_union_appendable_topic_handler,
    &sequence_enum_appendable_topic_handler,
    // Part 2
    &bounded_sequence_int32_topic_appendable_handler,
    &sequence_int64_topic_appendable_handler,
    &sequence_float32_topic_appendable_handler,
    &sequence_float64_topic_appendable_handler,
    &sequence_boolean_topic_appendable_handler,
    &sequence_octet_topic_appendable_handler,
    &sequence_string_topic_appendable_handler,
    &sequence_struct_topic_appendable_handler,
    &nested_struct_topic_appendable_handler,
    &nested_3d_topic_appendable_handler,
    &doubly_nested_topic_appendable_handler,
    &complex_nested_topic_appendable_handler,
    &union_bool_disc_topic_appendable_handler,
    &union_enum_disc_topic_appendable_handler,
    &union_short_disc_topic_appendable_handler,
    &optional_int32_topic_appendable_handler,
    &optional_float64_topic_appendable_handler,
    &optional_string_topic_appendable_handler,
    &optional_struct_topic_appendable_handler,
    &optional_enum_topic_appendable_handler,
    &multi_optional_topic_appendable_handler,
    &two_key_int32_topic_appendable_handler,
    &two_key_string_topic_appendable_handler,
    &three_key_topic_appendable_handler,
    &four_key_topic_appendable_handler,
    &nested_key_topic_appendable_handler,
    &nested_key_geo_topic_appendable_handler,
    &nested_triple_key_topic_appendable_handler,
    &empty_sequence_topic_appendable_handler,
    &unbounded_string_topic_appendable_handler,
    &all_primitives_atomic_topic_appendable_handler,
    &max_size_string_topic_handler,
    &max_size_string_topic_appendable_handler,
    &max_length_sequence_topic_handler,
    &max_length_sequence_topic_appendable_handler,
    &deep_nested_struct_topic_handler,
    &deep_nested_struct_topic_appendable_handler,
    &union_with_optional_topic_handler,
    &union_with_optional_topic_handler,
    &union_with_optional_topic_appendable_handler,
    // Part 3 Added
    &unbounded_string_topic_handler,
    &long_string_topic_handler,
    &large_sequence_topic_handler,
    &empty_sequence_topic_handler,
    &all_primitives_atomic_topic_handler,
    &final_int32_topic_handler,
    &final_struct_topic_handler,
    &mutable_int32_topic_handler,
    &mutable_struct_topic_handler,
    &optional_int32_topic_handler,
    &optional_float64_topic_handler,
    &optional_string_topic_handler,
    &optional_struct_topic_handler,
    &optional_enum_topic_handler,
    &multi_optional_topic_handler,
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
    
    // Create participant
    participant = dds_create_participant(domain_id, NULL, NULL);
    if (participant < 0) {
        snprintf(last_error, sizeof(last_error), "dds_create_participant failed: %d", participant);
        return;
    }
    
    // Create Publisher
    // In Cyclone, publisher/subscriber are often implicit or created from participant
    // But we can create explicit ones if needed. 
    // dds_create_publisher(participant, qos, listener)
    publisher = dds_create_publisher(participant, NULL, NULL);
    if (publisher < 0) {
        set_error("dds_create_publisher failed");
        return;
    }
    
    // Create Subscriber
    subscriber = dds_create_subscriber(participant, NULL, NULL);
    if (subscriber < 0) {
        set_error("dds_create_subscriber failed");
        return;
    }

    // Fix for UInt16Topic missing key bug
    extern void Fix_UInt16_Desc();
    Fix_UInt16_Desc();
    
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
    void* data = malloc(handler->size);
    memset(data, 0, handler->size);
    handler->generate(data, seed);
    
    // Write
    int rc = dds_write(writer, data);
    if (rc < 0) {
        snprintf(last_error, sizeof(last_error), "dds_write failed: %d", rc);
    }
    
    // Cleanup generated data (simple free of container, leaking pointers inside if any? 
    // Yes, leaking for now unless we implement free handler or use dds_alloc'd pointers and manage them)
    // In a test environment, small leaks per test might be acceptable for now given no generic free.
    
    // Give time to flush?
    dds_sleepfor(DDS_MSECS(50)); 
    
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
            }
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

// --- Symmetry API ---
#include "dds/ddsi/ddsi_serdata.h"

EXPORT int Native_GeneratePayload(const char* topic_name, int seed, void** out_buffer) {
    const topic_handler_t* handler = find_handler(topic_name);
    if (!handler) {
        set_error("Topic handler not found");
        return -1;
    }

    // Ensure participant exists (lazy init)
    if (participant == 0) {
        Native_Init(0);
        if (participant == 0) return -1;
    }

    // 1. Setup Entities
    // We create topic/writer/reader in temporary scope.
    // Assuming domain is same as Native_Init.
    
    dds_entity_t topic = dds_create_topic(participant, handler->descriptor, handler->name, NULL, NULL);
    if (topic < 0) { 
        snprintf(last_error, sizeof(last_error), "create_topic failed: %d", topic); 
        return -1; 
    }
    
    dds_qos_t *qos = dds_create_qos();
    dds_qset_reliability(qos, DDS_RELIABILITY_RELIABLE, DDS_SECS(1));
    dds_qset_durability(qos, DDS_DURABILITY_TRANSIENT_LOCAL);
    dds_qset_history(qos, DDS_HISTORY_KEEP_ALL, 0);

    // Force XCDR2
    // dds_data_representation_id_t reps[] = { DDS_DATA_REPRESENTATION_XCDR2 };
    // dds_qset_data_representation(qos, reps, 1);
    // printf("DEBUG: dds_qset_data_representation call done\n");

    dds_entity_t writer = dds_create_writer(publisher, topic, qos, NULL);
    if (writer < 0) { 
        dds_delete(topic); dds_delete_qos(qos); 
        set_error("create_writer failed"); 
        return -1; 
    }

    dds_entity_t reader = dds_create_reader(subscriber, topic, qos, NULL);
    if (reader < 0) { 
        dds_delete(writer); dds_delete(topic); dds_delete_qos(qos); 
        set_error("create_reader failed"); 
        return -1; 
    }
    
    dds_delete_qos(qos);

    // 2. Wait for Match (In-proc should be nearly instant)
    dds_sleepfor(DDS_MSECS(50));

    // 3. Write Sample
    void* sample = malloc(handler->size);
    memset(sample, 0, handler->size);
    handler->generate(sample, seed);
    
    int rc = dds_write(writer, sample);
    free(sample);
    
    if (rc < 0) {
        snprintf(last_error, sizeof(last_error), "dds_write failed: %d", rc);
        dds_delete(reader); dds_delete(writer); dds_delete(topic);
        return -1;
    }

    // 4. Read Loopback (Wait for data)
    dds_attach_t wsresults[1];
    dds_entity_t ws = dds_create_waitset(participant);
    dds_entity_t read_cond = dds_create_readcondition(reader, DDS_ANY_STATE);
    dds_waitset_attach(ws, read_cond, 1);
    
    rc = dds_waitset_wait(ws, wsresults, 1, DDS_SECS(2)); // 2 sec timeout
    
    void* result_buf = NULL;
    int result_len = -1;

    if (rc > 0) {
        struct ddsi_serdata *sd[1];
        dds_sample_info_t info[1];
        
        // Take CDR (Get serialized form directly from middleware)
        rc = dds_takecdr(reader, sd, 1, info, DDS_ANY_STATE);
        if (rc > 0 && info[0].valid_data) {
             struct ddsi_serdata *d = sd[0];
             uint32_t sz = ddsi_serdata_size(d);
             
             // DEBUG PRINT
             fprintf(stderr, "DEBUG: Topic %s size %u\n", handler->descriptor->m_typename, sz);
             fflush(stderr);

             result_buf = malloc(sz);
             if (result_buf) {
                 ddsi_serdata_to_ser(d, 0, sz, result_buf);
                 result_len = (int)sz;
                 
                 // DEBUG HEX DUMP
                 unsigned char* p = (unsigned char*)result_buf;
                 fprintf(stderr, "DEBUG: DATA: ");
                 for(unsigned int i=0; i<sz; i++) fprintf(stderr, "%02X ", p[i]);
                 fprintf(stderr, "\n");
                 fflush(stderr);
                 
             } else {
                 set_error("Out of memory for result buffer");
             }
             
             ddsi_serdata_unref(d);
        } else {
             set_error("No valid data in takecdr");
        }
    } else {
        set_error("Timeout waiting for loopback data");
    }

    // Cleanup
    dds_waitset_detach(ws, read_cond);
    dds_delete(ws);
    dds_delete(read_cond);
    dds_delete(reader);
    dds_delete(writer);
    dds_delete(topic);
    
    if (result_len >= 0) {
        *out_buffer = result_buf;
        return result_len;
    } else {
        if (result_buf) free(result_buf);
        return -1;
    }
}

EXPORT void Native_FreeBuffer(void* buffer) {
    if (buffer) free(buffer);
}
