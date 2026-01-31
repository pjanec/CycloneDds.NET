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

// ============================================================================
// SECTION 13: COMPLEX INTEGRATION SCENARIOS
// ============================================================================

// ----------------------------------------------------------------------------
// SCENARIO 1: OffsetKeyTopic - "Offset Nightmare"
// Tests key extraction when keys are NOT at the start and follow dynamic data
// ----------------------------------------------------------------------------
static void generate_OffsetKeyTopic(void* data, int seed) {
    AtomicTests_OffsetKeyTopic* m = (AtomicTests_OffsetKeyTopic*)data;
    
    // Field 1: Variable length string
    snprintf(m->group_name, sizeof(m->group_name), "Group_%d", seed % 100);
    
    // Key 1: sensor_id (after variable string)
    m->sensor_id = seed;
    
    // Field 2: Dynamic sequence of floats
    uint32_t cal_size = 3 + (seed % 5); // 3-7 elements
    m->calibration_data._maximum = cal_size;
    m->calibration_data._length = cal_size;
    m->calibration_data._buffer = dds_alloc(cal_size * sizeof(float));
    m->calibration_data._release = true;
    for (uint32_t i = 0; i < cal_size; i++) {
        m->calibration_data._buffer[i] = (float)(seed + i) * 0.1f;
    }
    
    // Key 2: instance_sub_id (after sequence)
    m->instance_sub_id = (int16_t)(seed % 1000);
    
    // Payload: Fixed size struct
    m->final_pos.x = (double)seed * 1.1;
    m->final_pos.y = (double)seed * 2.2;
    m->final_pos.z = (double)seed * 3.3;
}

static int validate_OffsetKeyTopic(void* data, int seed) {
    // Basic validation - would need more comprehensive checks
    AtomicTests_OffsetKeyTopic* m = (AtomicTests_OffsetKeyTopic*)data;
    if (m->sensor_id != seed) return -1;
    if (m->instance_sub_id != (int16_t)(seed % 1000)) return -1;
    return 0;
}
DEFINE_HANDLER(OffsetKeyTopic, offset_key_topic);

// ----------------------------------------------------------------------------
// SCENARIO 2: RobotStateTopic - "Kitchen Sink"
// Tests @appendable with arrays, sequences of structs, unions, optional fields
// ----------------------------------------------------------------------------
static void generate_RobotStateTopic(void* data, int seed) {
    AtomicTests_RobotStateTopic* m = (AtomicTests_RobotStateTopic*)data;
    
    // Key: robot_id
    snprintf(m->robot_id, sizeof(m->robot_id), "ROBOT_%04d", seed);
    
    // 1. Primitive timestamp
    m->timestamp_ns = (uint64_t)seed * 1000000ULL;
    
    // 2. Enum
    m->operational_mode = (AtomicTests_SimpleEnum)(seed % 3); // FIRST, SECOND, THIRD
    
    // 3. Fixed 2D array (3x3 matrix)
    for (int i = 0; i < 3; i++) {
        for (int j = 0; j < 3; j++) {
            m->transform_matrix[i][j] = (double)(seed + i * 10 + j);
        }
    }
    
    // 4. Sequence of nested structs (Point2D)
    uint32_t path_size = 2 + (seed % 4); // 2-5 waypoints
    m->current_path._maximum = path_size;
    m->current_path._length = path_size;
    m->current_path._buffer = dds_alloc(path_size * sizeof(AtomicTests_Point2D));
    m->current_path._release = true;
    for (uint32_t i = 0; i < path_size; i++) {
        m->current_path._buffer[i].x = (double)(seed + i) * 10.0;
        m->current_path._buffer[i].y = (double)(seed + i) * 20.0;
    }
    
    // 5. Complex union
    m->current_action._d = (int32_t)(1 + (seed % 3)); // Discriminator 1, 2, or 3
    switch (m->current_action._d) {
        case 1:
            m->current_action._u.int_value = seed * 100;
            break;
        case 2:
            m->current_action._u.double_value = (double)seed * 3.14;
            break;
        case 3:
            m->current_action._u.string_value = dds_alloc(256);
            snprintf(m->current_action._u.string_value, 255, "Action_%d", seed);
            break;
    }
    
    // 6. Optional nested struct (cargo_hold)
    if (seed % 2 == 0) {
        // Present
        m->cargo_hold = dds_alloc(sizeof(AtomicTests_Container));
        m->cargo_hold->count = seed;
        m->cargo_hold->center.x = (double)seed * 10.0;
        m->cargo_hold->center.y = (double)seed * 20.0;
        m->cargo_hold->center.z = (double)seed * 30.0;
        m->cargo_hold->radius = (double)seed * 5.0;
    } else {
        // Absent
        m->cargo_hold = NULL;
    }
    
    // 7. Optional primitive (battery_voltage)
    if (seed % 3 == 0) {
        // Present
        m->battery_voltage = dds_alloc(sizeof(double));
        *m->battery_voltage = 12.5 + (double)(seed % 100) * 0.01;
    } else {
        // Absent
        m->battery_voltage = NULL;
    }
}

static int validate_RobotStateTopic(void* data, int seed) {
    AtomicTests_RobotStateTopic* m = (AtomicTests_RobotStateTopic*)data;
    char expected_id[65];
    snprintf(expected_id, sizeof(expected_id), "ROBOT_%04d", seed);
    if (strcmp(m->robot_id, expected_id) != 0) return -1;
    if (m->timestamp_ns != (uint64_t)seed * 1000000ULL) return -1;
    return 0;
}
DEFINE_HANDLER(RobotStateTopic, robot_state_topic);

// ----------------------------------------------------------------------------
// SCENARIO 3: IoTDeviceMutableTopic - "Sparse Mutable"
// Tests @mutable with sparse IDs and non-sequential keys
// ----------------------------------------------------------------------------
static void generate_IoTDeviceMutableTopic(void* data, int seed) {
    AtomicTests_IoTDeviceMutableTopic* m = (AtomicTests_IoTDeviceMutableTopic*)data;
    
    // @id(10) @key device_serial
    m->device_serial = seed;
    
    // @id(50) temperature
    m->temperature = 20.0f + (float)(seed % 50);
    
    // @id(60) @optional location_label
    if (seed % 2 == 0) {
        // Present
        m->location_label = dds_alloc(sizeof(*m->location_label));
        snprintf(*m->location_label, 128, "Location_%d", seed);
        (*m->location_label)[128] = '\0';
    } else {
        // Absent
        m->location_label = NULL;
    }
    
    // @id(70) sequence<ColorEnum> status_leds
    uint32_t led_count = 1 + (seed % 4); // 1-4 LEDs
    m->status_leds._maximum = led_count;
    m->status_leds._length = led_count;
    m->status_leds._buffer = dds_alloc(led_count * sizeof(AtomicTests_ColorEnum));
    m->status_leds._release = true;
    for (uint32_t i = 0; i < led_count; i++) {
        m->status_leds._buffer[i] = (AtomicTests_ColorEnum)((seed + i) % 4); // RED, GREEN, BLUE, YELLOW
    }
    
    // @id(80) last_ping_geo
    m->last_ping_geo.x = (double)seed * 0.1;
    m->last_ping_geo.y = (double)seed * 0.2;
    m->last_ping_geo.z = (double)seed * 0.3;
}

static int validate_IoTDeviceMutableTopic(void* data, int seed) {
    AtomicTests_IoTDeviceMutableTopic* m = (AtomicTests_IoTDeviceMutableTopic*)data;
    if (m->device_serial != seed) return -1;
    if (fabs(m->temperature - (20.0f + (float)(seed % 50))) > 0.01f) return -1;
    return 0;
}
DEFINE_HANDLER(IoTDeviceMutableTopic, iot_device_mutable_topic);

// ----------------------------------------------------------------------------
// SCENARIO 4: AlignmentCheckTopic - "Alignment Torture Test"
// Tests mixing 1-byte, 2-byte, 4-byte, and 8-byte types
// ----------------------------------------------------------------------------
static void generate_AlignmentCheckTopic(void* data, int seed) {
    AtomicTests_AlignmentCheckTopic* m = (AtomicTests_AlignmentCheckTopic*)data;
    
    // @key id
    m->id = seed;
    
    // 1 byte
    m->b1 = (uint8_t)(seed % 256);
    
    // 8 bytes (forces padding)
    m->d1 = (double)seed * 1.23456789;
    
    // 2 bytes
    m->s1 = (int16_t)(seed % 30000);
    
    // 1 byte
    m->c1 = (char)('A' + (seed % 26));
    
    // 4 bytes
    m->l1 = seed * 1000;
    
    // Sequence of octets (variable length)
    uint32_t blob_size = 5 + (seed % 10); // 5-14 bytes
    m->blob._maximum = blob_size;
    m->blob._length = blob_size;
    m->blob._buffer = dds_alloc(blob_size * sizeof(uint8_t));
    m->blob._release = true;
    for (uint32_t i = 0; i < blob_size; i++) {
        m->blob._buffer[i] = (uint8_t)((seed + i) % 256);
    }
    
    // 8 byte field (after variable sequence)
    m->check_value = (uint64_t)seed * 123456789ULL;
}

static int validate_AlignmentCheckTopic(void* data, int seed) {
    AtomicTests_AlignmentCheckTopic* m = (AtomicTests_AlignmentCheckTopic*)data;
    if (m->id != seed) return -1;
    if (m->b1 != (uint8_t)(seed % 256)) return -1;
    if (m->s1 != (int16_t)(seed % 30000)) return -1;
    if (m->c1 != (char)('A' + (seed % 26))) return -1;
    if (m->l1 != seed * 1000) return -1;
    if (m->check_value != (uint64_t)seed * 123456789ULL) return -1;
    return 0;
}
DEFINE_HANDLER(AlignmentCheckTopic, alignment_check_topic);
