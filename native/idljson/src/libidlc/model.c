#include "model.h"
#include "idl/heap.h"
#include "idl/string.h"
#include <stdlib.h>
#include <string.h>
#include <stdio.h>
#include <inttypes.h>

#ifndef _WIN32
#define strtok_s strtok_r
#endif

dm_rec_t* dm_sources = NULL;
dm_rec_t* dm_types = NULL;
dm_rec_t* dm_last_struct = NULL;
dm_rec_t* dm_last_enum = NULL;

dm_rec_t* dm_new(void) {
    return (dm_rec_t*)calloc(1, sizeof(dm_rec_t));
}

dm_rec_t* dm_add(dm_rec_t** list, dm_rec_t* item) {
    if (!list || !item) return NULL;
    
    if (*list == NULL) {
        *list = item;
        return item;
    }
    
    dm_rec_t* p = *list;
    while (p->next) {
        p = p->next;
    }
    p->next = item;
    return item;
}

dm_rec_t* dm_find_by_name(dm_rec_t* list, const char* name) {
    if (!name) return NULL;
    
    for (dm_rec_t* p = list; p != NULL; p = p->next) {
        if (p->name && strcmp(p->name, name) == 0) {
            return p;
        }
    }
    return NULL;
}

dm_rec_t* dm_find_by_c_name(dm_rec_t* list, const char* c_name) {
    if (!c_name) return NULL;
    
    for (dm_rec_t* p = list; p != NULL; p = p->next) {
        if (p->c_name && strcmp(p->c_name, c_name) == 0) {
            return p;
        }
    }
    return NULL;
}

static dm_rec_t* find_member_by_name(dm_rec_t* type_rec, const char* name) {
    if (!type_rec || !name) return NULL;
    
    for (dm_rec_t* m = type_rec->members; m != NULL; m = m->next) {
        if (m->name && strcmp(m->name, name) == 0) {
            return m;
        }
    }
    return NULL;
}

size_t get_primitive_size_align(const char* type_name) {
    if (!type_name) return 0;
    
    // IDL Types
    if (strcmp(type_name, "boolean") == 0) return 1;
    if (strcmp(type_name, "char") == 0) return 1;
    if (strcmp(type_name, "octet") == 0) return 1;
    if (strcmp(type_name, "short") == 0) return 2;
    if (strcmp(type_name, "unsigned short") == 0) return 2;
    if (strcmp(type_name, "long") == 0) return 4;
    if (strcmp(type_name, "unsigned long") == 0) return 4;
    if (strcmp(type_name, "long long") == 0) return 8;
    if (strcmp(type_name, "unsigned long long") == 0) return 8;
    if (strcmp(type_name, "float") == 0) return 4;
    if (strcmp(type_name, "double") == 0) return 8;
    
    // C Mapped Types
    if (strcmp(type_name, "bool") == 0) return 1;
    if (strcmp(type_name, "uint8_t") == 0) return 1;
    if (strcmp(type_name, "int16_t") == 0) return 2;
    if (strcmp(type_name, "uint16_t") == 0) return 2;
    if (strcmp(type_name, "int32_t") == 0) return 4;
    if (strcmp(type_name, "uint32_t") == 0) return 4;
    if (strcmp(type_name, "int64_t") == 0) return 8;
    if (strcmp(type_name, "uint64_t") == 0) return 8;
    
    if (strcmp(type_name, "long double") == 0) return sizeof(long double);
    if (strcmp(type_name, "string") == 0) return 8; // pointer
    if (strcmp(type_name, "wstring") == 0) return 8; // pointer
    
    return 0;
}

void resolve_type_size(const char* type_name, uint32_t* size, uint32_t* align) {
    *size = 0; *align = 1;
    if (!type_name) return;

    const char* lookup_name = type_name;
    while (*lookup_name == ' ') lookup_name++; // skip leading spaces
    if (strncmp(lookup_name, "struct ", 7) == 0) lookup_name += 7;
    else if (strncmp(lookup_name, "union ", 6) == 0) lookup_name += 6;
    else if (strncmp(lookup_name, "enum ", 5) == 0) lookup_name += 5;
    
    while (*lookup_name == ' ') lookup_name++;

    // 1. Primitive?
    size_t prim = get_primitive_size_align(lookup_name);
    if (prim > 0) {
        *size = (uint32_t)prim;
        *align = (prim >= 8) ? 8 : (uint32_t)prim;
        if (strcmp(lookup_name, "string") == 0) *align = 8; // pointer align
        return;
    }

    // 2. Lookup Rec
    dm_rec_t* rec = dm_find_by_c_name(dm_types, lookup_name);
    if (!rec) rec = dm_find_by_name(dm_types, lookup_name);
    
    if (rec) {
        if (rec->kind && strcmp(rec->kind, "alias") == 0) {
             uint32_t base_size = 0, base_align = 1;
             resolve_type_size(rec->type, &base_size, &base_align);
             
             if (rec->is_array) { 
                 *size = base_size * rec->size; // rec->size is count
             } else {
                 *size = base_size;
             }
             *align = base_align;
        } else if (rec->kind && strcmp(rec->kind, "sequence") == 0) {
             *size = 24; *align = 8;
        } else {
            // Struct/Enum/Union
             *size = rec->size; // Byte size
             *align = rec->align;
        }
    }
}

static uint32_t align_up(uint32_t offset, size_t alignment) {
    if (alignment == 0) return offset;
    size_t remainder = offset % alignment;
    return (remainder == 0) ? offset : offset + (alignment - remainder);
}

void dm_calculate_layout(dm_rec_t* struct_rec) {
    if (!struct_rec || !struct_rec->members) return;
    
    int is_union = (struct_rec->kind && strcmp(struct_rec->kind, "union") == 0);

    if (is_union) {
        size_t disc_size = 4; // int32_t _d
        size_t disc_align = 4;
        uint32_t union_max_align = 1;
        uint32_t max_payload_size = 0;
        
        // Pass 1: Determine max alignment of payload
        for (dm_rec_t* member = struct_rec->members; member != NULL; member = member->next) {
            size_t member_align = 1;
            size_t member_size = 0;
            
            // Re-use primitive size logic
            size_t prim_size = get_primitive_size_align(member->type);
            if (prim_size > 0) {
                // Handle C-mapping of bounded strings (char array) in Unions
                if (strcmp(member->type, "string") == 0 && member->bound > 0) {
                    member_size = member->bound + 1;
                    member_align = 1;
                } else {
                    member_size = prim_size;
                    member_align = (prim_size >= 8) ? 8 : (uint32_t)prim_size;
                }
            } else {
                 dm_rec_t* nested = dm_find_by_c_name(dm_types, member->type);
                 if (!nested) nested = dm_find_by_name(dm_types, member->type);
                 if (nested && nested->size > 0) {
                     member_size = nested->size;
                     member_align = nested->align;
                 } else if ((member->kind && strcmp(member->kind, "sequence") == 0) || (member->type && strstr(member->type, "sequence"))) {
                     member_size = 24; member_align = 8;
                 } else {
                     member_size = 4; member_align = 4;
                 }
            }
            if (member->is_array && member->size > 0) member_size *= member->size;

            if (member_align > union_max_align) union_max_align = member_align;
            if (member_size > max_payload_size) max_payload_size = member_size;
            
            // Store size temporarily or re-calc in pass 2? 
            // Better to just calculate payload offset now.
        }

        // Calculate offset where the union payload (_u) starts
        uint32_t payload_offset = align_up((uint32_t)disc_size, union_max_align);
        
        // Pass 2: Set offsets
        for (dm_rec_t* member = struct_rec->members; member != NULL; member = member->next) {
            member->offset = payload_offset; // All start at same offset
        }

        uint32_t total_align = (disc_align > union_max_align) ? (uint32_t)disc_align : union_max_align;
        struct_rec->size = align_up(payload_offset + max_payload_size, total_align);
        struct_rec->align = total_align;
        return;
    }
    
    // Normal Struct Layout (Existing Logic is fine)
    uint32_t cursor = 0;
    uint32_t max_align = 1;
    
    for (dm_rec_t* member = struct_rec->members; member != NULL; member = member->next) {
        size_t member_size = 0;
        size_t member_align = 1;
        
        if (member->is_optional) {
            // Optionals are mapped as pointers in C
            member_size = 8;
            member_align = 8;
        } else {
            uint32_t resolved_size = 0, resolved_align = 1;
            resolve_type_size(member->type, &resolved_size, &resolved_align);
            
            if (resolved_size > 0) {
                 member_size = resolved_size;
                 member_align = resolved_align;
                 
                 // Handle C-mapping of bounded strings (char array)
                 if (strcmp(member->type, "string") == 0 && member->bound > 0) {
                    member_size = member->bound + 1;
                    member_align = 1;
                 }
            } else if ((member->kind && strcmp(member->kind, "sequence") == 0) || (member->type && strstr(member->type, "sequence"))) {
                    member_size = 24; member_align = 8;
            } else {
                    fprintf(stderr, "[JSON Plugin Debug] Unknown type '%s' for member '%s' (kind: %s), defaulting to size 4\n", 
                        member->type ? member->type : "NULL", 
                        member->name ? member->name : "NULL",
                        member->kind ? member->kind : "NULL");
                    member_size = 4; member_align = 4;
            }
            
            if (member->is_array && member->size > 0) member_size *= member->size;
        }
        
        cursor = align_up(cursor, member_align);
        member->offset = cursor;
        cursor += member_size;
        
        if (member_align > max_align) max_align = member_align;
    }
    
    struct_rec->size = align_up(cursor, max_align);
    struct_rec->align = max_align;
}

int dm_get_member_offset(const char* type_c_name, const char* member_name) {
    if (!type_c_name || !member_name) return 0;
    
    dm_rec_t* type_rec = dm_find_by_c_name(dm_types, type_c_name);
    if (!type_rec) {
        // Fallback: try IDL name
        type_rec = dm_find_by_name(dm_types, type_c_name);
        if (!type_rec) return 0;
    }
    
    // Handle nested paths (e.g., "ProcessAddr.StationId")
    char* path_copy = idl_strdup(member_name);
    char* saveptr = NULL;
    char* token = strtok_s(path_copy, ".", &saveptr);
    dm_rec_t* current_type = type_rec;
    int current_offset = 0;
    
    while (token != NULL) {
        if (current_type->kind && strcmp(current_type->kind, "union") == 0) {
            if (strcmp(token, "_u") == 0) {
                token = strtok_s(NULL, ".", &saveptr);
                continue;
            }
        }

        dm_rec_t* member = find_member_by_name(current_type, token);
        if (!member) {
             if (strcmp(token, "_d") == 0) {
                 // Discriminator is at offset 0 relative to the union container
                 free(path_copy);
                 return current_offset;
             }
            free(path_copy);
            return 0;
        }
        
        current_offset += member->offset;
        
        token = strtok_s(NULL, ".", &saveptr);
        if (token) {
            // Navigate to member's type
            current_type = dm_find_by_c_name(dm_types, member->type);
            if (!current_type) current_type = dm_find_by_name(dm_types, member->type);
            
            if (!current_type) {
                free(path_copy);
                return 0;
            }
        }
    }
    
    free(path_copy);
    return current_offset;
}

static const char* dm_escapize(const char* s) {
    static char buf[2048];
    buf[0] = '\0';
    if (!s) return buf;
    
    const char* c = s;
    char* d = buf;
    
    while (*c && (d - buf) < 2046) {
        if (*c == '\\' || *c == '"') {
            *d++ = '\\';
        }
        *d++ = *c++;
    }
    *d = '\0';
    
    return buf;
}

static void dm_indent(FILE* fh, int indent) {
    for (int i = 0; i < indent * 2; i++) {
        fprintf(fh, " ");
    }
}

static void dm_print_value(FILE* fh, dm_rec_t* rec, int indent) {
    if (!rec->has_value) return;
    
    dm_indent(fh, indent);
    
    switch (rec->value_type) {
        case DM_TYPE_BOOL:
            fprintf(fh, "\"Value\": %s,\n", rec->value.bln ? "true" : "false");
            break;
        case DM_TYPE_INT:
            fprintf(fh, "\"Value\": %" PRId64 ",\n", rec->value.int64);
            break;
        case DM_TYPE_UNSIGNED_INT:
            fprintf(fh, "\"Value\": %" PRIu64 ",\n", rec->value.uint64);
            break;
        case DM_TYPE_DOUBLE:
            fprintf(fh, "\"Value\": %lf,\n", rec->value.dbl);
            break;
        case DM_TYPE_LONG_DOUBLE:
            fprintf(fh, "\"Value\": %Lf,\n", rec->value.ldbl);
            break;
        case DM_TYPE_STRING:
            fprintf(fh, "\"Value\": \"%s\",\n", dm_escapize(rec->value.str));
            break;
    }
}

static void dm_print_labels(FILE* fh, dm_rec_t* labels, int indent) {
    if (!labels) return;
    
    dm_indent(fh, indent);
    fprintf(fh, "\"Labels\": [\n");
    
    for (dm_rec_t* l = labels; l; l = l->next) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"%s\"", dm_escapize(l->name));
        if (l->next) fprintf(fh, ",");
        fprintf(fh, "\n");
    }
    
    dm_indent(fh, indent);
    fprintf(fh, "],\n");
}

static void dm_print_qos(FILE* fh, dm_qos_t* qos, int indent) {
    if (!qos) return;
    
    dm_indent(fh, indent);
    fprintf(fh, "\"QoS\": {\n");
    
    if (qos->reliability) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Reliability\": \"%s\",\n", qos->reliability);
    }
    
    if (qos->durability) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Durability\": \"%s\",\n", qos->durability);
    }
    
    if (qos->history) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"History\": \"%s\",\n", qos->history);
    }
    
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"HistoryDepth\": %d\n", qos->depth);
    
    dm_indent(fh, indent);
    fprintf(fh, "},\n");
}

static void dm_print_descriptor(FILE* fh, dm_descriptor_t* desc, int indent) {
    if (!desc) return;
    
    dm_indent(fh, indent);
    fprintf(fh, "\"TopicDescriptor\": {\n");
    
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"Size\": %u,\n", desc->size);
    
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"Align\": %u,\n", desc->align);
    
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"FlagSet\": %u,\n", desc->flagset);
    
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"TypeName\": \"%s\",\n", desc->typename ? desc->typename : "");
    
    // Keys
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"Keys\": [\n");
    for (uint32_t i = 0; i < desc->n_keys; i++) {
        dm_indent(fh, indent + 2);
        fprintf(fh, "{ \"Name\": \"%s\", \"Offset\": %u, \"Order\": %u }",
                desc->keys[i].name,
                desc->keys[i].offset,
                desc->keys[i].order);
        if (i < desc->n_keys - 1) fprintf(fh, ",");
        fprintf(fh, "\n");
    }
    dm_indent(fh, indent + 1);
    fprintf(fh, "],\n");
    
    // Ops
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"Ops\": [\n");
    for (uint32_t i = 0; i < desc->n_ops; i++) {
        if (i % 8 == 0) dm_indent(fh, indent + 2);
        fprintf(fh, "%u", desc->ops[i]);
        if (i < desc->n_ops - 1) fprintf(fh, ", ");
        if ((i + 1) % 8 == 0 || i == desc->n_ops - 1) fprintf(fh, "\n");
    }
    dm_indent(fh, indent + 1);
    fprintf(fh, "]\n");
    
    dm_indent(fh, indent);
    fprintf(fh, "},\n");
}

static void dm_print_list(FILE* fh, dm_rec_t* list, int indent, int is_member);

static void dm_print_rec(FILE* fh, dm_rec_t* rec, int indent, int is_member) {
    if (!rec) return;
    
    dm_indent(fh, indent);
    fprintf(fh, "{\n");
    
    // Identity
    if (rec->name) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Name\": \"%s\",\n", dm_escapize(rec->name));
    }
    
    if (rec->kind) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Kind\": \"%s\",\n", dm_escapize(rec->kind));
    }
    
    if (rec->type) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Type\": \"%s\",\n", dm_escapize(rec->type));
    }
    
    // Annotations
    if (rec->extensibility) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Extensibility\": \"%s\",\n", rec->extensibility);
    }
    
    if (rec->discriminator) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Discriminator\": \"%s\",\n", dm_escapize(rec->discriminator));
    }
    
    if (rec->is_key) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"IsKey\": true,\n");
    }
    
    if (rec->has_explicit_id) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Id\": %d,\n", rec->member_id);
    }
    
    if (rec->is_optional) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"IsOptional\": true,\n");
    }
    
    if (rec->is_external) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"IsExternal\": true,\n");
    }
    
    if (rec->bound > 0) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Bound\": %u,\n", rec->bound);
    }
    
    if (rec->align > 0) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Align\": %d,\n", rec->align);
    }
    
    if (!rec->is_array && rec->size > 0) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Size\": %d,\n", rec->size);
    }
    
    if (is_member) {
         dm_indent(fh, indent + 1);
         fprintf(fh, "\"Offset\": %d,\n", rec->offset);
    }

    if (rec->is_array) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"CollectionType\": \"array\",\n");
        if (rec->size > 0) {
            dm_indent(fh, indent + 1);
            fprintf(fh, "\"Size\": %d,\n", rec->size);
        }
    } else if (rec->kind && strcmp(rec->kind, "sequence") == 0) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"CollectionType\": \"sequence\",\n");
    }
    
    // Labels
    if (rec->labels) {
        dm_print_labels(fh, rec->labels, indent + 1);
    }
    
    // Value
    if (rec->has_value) {
        dm_print_value(fh, rec, indent + 1);
    }
    
    // QoS
    if (rec->qos) {
        dm_print_qos(fh, rec->qos, indent + 1);
    }
    
    // Descriptor
    if (rec->topic_descriptor) {
        dm_print_descriptor(fh, rec->topic_descriptor, indent + 1);
    }
    
    // Members
    if (rec->members) {
        dm_indent(fh, indent + 1);
        fprintf(fh, "\"Members\":\n");
        dm_print_list(fh, rec->members, indent + 1, 1);
        fprintf(fh, ",\n");
    }
    
    // EOF marker
    dm_indent(fh, indent + 1);
    fprintf(fh, "\"_eof\": 0\n");
    
    dm_indent(fh, indent);
    fprintf(fh, "}");
}

static void dm_print_list(FILE* fh, dm_rec_t* list, int indent, int is_member) {
    if (!list) {
        fprintf(fh, "null"); 
        return; 
    }

    dm_indent(fh, indent);
    fprintf(fh, "[\n");
    
    for (dm_rec_t* rec = list; rec != NULL; rec = rec->next) {
        dm_print_rec(fh, rec, indent + 1, is_member);
        if (rec->next) fprintf(fh, ",");
        fprintf(fh, "\n");
    }
    
    dm_indent(fh, indent);
    fprintf(fh, "]");
}

void dm_fprint(FILE* fh) {
    fprintf(fh, "{\n");
    fprintf(fh, "  \"File\":\n");
    dm_print_list(fh, dm_sources, 2, 0);
    fprintf(fh, ",\n");
    fprintf(fh, "  \"Types\":\n");
    dm_print_list(fh, dm_types, 2, 0);
    fprintf(fh, "\n}\n");
}
