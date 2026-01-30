#ifndef TEST_REGISTRY_H
#define TEST_REGISTRY_H

#include "atomic_tests.h"
#include "dds/dds.h"

#ifdef WIN32
    #ifdef NATIVE_EXPORT
        #define EXPORT __declspec(dllexport)
    #else
        #define EXPORT __declspec(dllimport)
    #endif
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

typedef struct {
    const char* name;
    const dds_topic_descriptor_t* descriptor;
    void (*generate)(void* data, int seed);
    int (*validate)(void* data, int seed);
    size_t size;
} topic_handler_t;

// Registry lookup
const topic_handler_t* find_handler(const char* topic_name);

// Exported API
EXPORT void Native_Init(uint32_t domain_id);
EXPORT void Native_Cleanup();
EXPORT int Native_SendWithSeed(const char* topic_name, int seed);
EXPORT int Native_ExpectWithSeed(const char* topic_name, int seed, int timeout_ms);
EXPORT const char* Native_GetLastError();

// Symmetry API
EXPORT int Native_GeneratePayload(const char* topic_name, int seed, void** buffer);
EXPORT void Native_FreeBuffer(void* buffer);

#endif // TEST_REGISTRY_H
