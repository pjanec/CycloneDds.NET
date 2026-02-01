#include <stdio.h>
#include <stddef.h>
#include "UnionProbe.h"

int main() {
    printf("Probe_MyUnion size: %zu\n", sizeof(Probe_MyUnion));
    printf("Probe_MyUnion _d offset: %zu\n", offsetof(Probe_MyUnion, _d));
    printf("Probe_MyUnion _u offset: %zu\n", offsetof(Probe_MyUnion, _u));
    
    printf("Probe_UnionProbeStruct size: %zu\n", sizeof(Probe_UnionProbeStruct));
    printf("Probe_UnionProbeStruct P1 offset: %zu\n", offsetof(Probe_UnionProbeStruct, P1));
    printf("Probe_UnionProbeStruct U offset: %zu\n", offsetof(Probe_UnionProbeStruct, U));
    printf("Probe_UnionProbeStruct P2 offset: %zu\n", offsetof(Probe_UnionProbeStruct, P2));

    return 0;
}
