#include <stdio.h>
#include <string.h>
#include "dds/dds.h"
#include "dds/cdr/dds_cdrstream.h"
#include "UnionNew.h"

void print_hex(const unsigned char* data, size_t len) {
    for (size_t i = 0; i < len; i++) {
        printf("%02X", data[i]);
        if (i < len - 1) printf(" ");
    }
    printf("\n");
}

int main() {
    Container c;
    
    // Set discriminator to case 3 (UNKNOWN to old readers)
    c.u._d = 3;
    c.u._u.valueC = "Hello";
    
    unsigned char buffer[1024];
    memset(buffer, 0, sizeof(buffer));
    
    dds_ostream_t os;
    os.m_buffer = buffer;
    os.m_size = sizeof(buffer);
    os.m_index = 0;
    os.m_xcdr_version = DDSI_RTPS_CDR_ENC_VERSION_2;
    
    struct dds_cdrstream_desc desc;
    dds_cdrstream_desc_from_topic_desc(&desc, &Container_desc);
    
    printf("=== NEW Publisher Sending Case 3 (Unknown to OLD Readers) ===\n");
    bool result = dds_stream_write_sample(&os, &c, &desc);
    
    if (result) {
        printf("Size: %zu bytes\n", os.m_index);
        printf("HEX: ");
        print_hex(buffer, os.m_index);
    } else {
        printf("ERROR: Serialization failed!\n");
    }
    
    dds_cdrstream_desc_fini(&desc);
    
    return 0;
}
