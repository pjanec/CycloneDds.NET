#include <stdio.h>
#include <string.h>
#include "dds/dds.h"
#include "dds/cdr/dds_cdrstream.h"
#include "UnionTest.h"

void print_hex(const char* label, const unsigned char* data, size_t len) {
    printf("%s (%zu bytes):\n", label, len);
    for (size_t i = 0; i < len; i++) {
        printf("%02X", data[i]); 
        if (i < len - 1) printf(" ");
    }
    printf("\n");
}

int main() {
    // 1. Initialize Union
    TestUnion u;
    u._d = 1; // Selector for valueA
    u._u.valueA = 0xDEADBEEF; 

    // 2. Prepare Output Buffer and Stream
    unsigned char buffer[1024];
    memset(buffer, 0, sizeof(buffer));

    dds_ostream_t os;
    os.m_buffer = buffer;
    os.m_size = sizeof(buffer);
    os.m_index = 0;
    os.m_xcdr_version = DDSI_RTPS_CDR_ENC_VERSION_2;

    // 3. Prepare Descriptor
    struct dds_cdrstream_desc desc;
    dds_cdrstream_desc_from_topic_desc(&desc, &TestUnion_desc);

    // 4. Serialize
    printf("Serializing TestUnion {_d=1, valueA=0xDEADBEEF} with XCDR2...\n");
    bool result = dds_stream_write_sample(&os, &u, &desc);
    
    if (result) {
        print_hex("HEX DUMP", buffer, os.m_index);
        
        if(os.m_index >= 4) {
             uint32_t dheader = *(uint32_t*)buffer;
             printf("DHEADER (Raw): 0x%08X\n", dheader);
        }
    } else {
        printf("Serialization failed!\n");
    }

    dds_cdrstream_desc_fini(&desc);

    return 0;
}
