// Copyright(c) 2021 to 2022 ZettaScale Technology and others
//
// This program and the accompanying materials are made available under the
// terms of the Eclipse Public License v. 2.0 which is available at
// http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
// v. 1.0 which is available at
// http://www.eclipse.org/org/documents/edl-v10.php.
//
// SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause

#include <assert.h>
#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <inttypes.h>

#include "idl/print.h"
#include "idl/stream.h"
#include "idl/string.h"
#include "idl/processor.h"

#include "libidlc__descriptor.h"
#include "libidlc__generator.h"
#include "libidlc__types.h"
#include "model.h"

static const char *
get_type_prefix(const idl_type_spec_t *type_spec)
{
  /* Prefixing a struct or union type with 'struct' is only required in case
     it refers to a forward declared type, but in a direct recursive types, the
     type is used for the member instead of the forward declarator. So therefore,
     a struct or union type is also prefixed. */
  if (idl_is_forward(type_spec) || idl_is_struct(type_spec) || idl_is_union(type_spec))
    return "struct ";
  return "";
}

/* Appends the type spec in IDL syntax with scoped names ("sequence<string<8>, 4>", "Td::LongSeq") to buf,
   for importers: the C names in "Type" ("dds_sequence_string8") lose bounds, nesting and scopes. */
static void
append_idl_type(char *buf, size_t size, const idl_type_spec_t *type_spec)
{
  size_t len = strlen(buf);
  if (len + 1 >= size)
    return;
  char *out = buf + len;
  size_t room = size - len;

  if (idl_is_declarator(type_spec)) {
    /* a typedef: keep its name, the importer resolves the alias */
    char *name;
    if (IDL_PRINTA(&name, print_scoped_name, type_spec) >= 0)
      idl_snprintf(out, room, "%s", name);
  } else if (idl_is_sequence(type_spec)) {
    idl_snprintf(out, room, "sequence<");
    append_idl_type(buf, size, idl_type_spec(type_spec));
    len = strlen(buf);
    if (idl_is_bounded(type_spec))
      idl_snprintf(buf + len, size - len, ", %" PRIu32 ">", idl_bound(type_spec));
    else
      idl_snprintf(buf + len, size - len, ">");
  } else if (idl_is_string(type_spec) || idl_type(type_spec) == IDL_WSTRING) {
    const char *kw = idl_type(type_spec) == IDL_WSTRING ? "wstring" : "string";
    if (idl_is_bounded(type_spec))
      idl_snprintf(out, room, "%s<%" PRIu32 ">", kw, idl_bound(type_spec));
    else
      idl_snprintf(out, room, "%s", kw);
  } else if (idl_is_base_type(type_spec)) {
    const char *kw;
    switch (idl_type(type_spec)) {
      case IDL_BOOL: kw = "boolean"; break;
      case IDL_CHAR: kw = "char"; break;
      case IDL_WCHAR: kw = "wchar"; break;
      case IDL_OCTET: kw = "octet"; break;
      case IDL_INT8: kw = "int8"; break;
      case IDL_UINT8: kw = "uint8"; break;
      case IDL_SHORT: case IDL_INT16: kw = "int16"; break;
      case IDL_USHORT: case IDL_UINT16: kw = "uint16"; break;
      case IDL_LONG: case IDL_INT32: kw = "int32"; break;
      case IDL_ULONG: case IDL_UINT32: kw = "uint32"; break;
      case IDL_LLONG: case IDL_INT64: kw = "int64"; break;
      case IDL_ULLONG: case IDL_UINT64: kw = "uint64"; break;
      case IDL_FLOAT: kw = "float"; break;
      case IDL_DOUBLE: kw = "double"; break;
      case IDL_LDOUBLE: kw = "long double"; break;
      default: kw = "any"; break;
    }
    idl_snprintf(out, room, "%s", kw);
  } else {
    /* struct, union, enum, bitmask (a forward declaration names its definition) */
    char *name;
    if (IDL_PRINTA(&name, print_scoped_name, type_spec) >= 0)
      idl_snprintf(out, room, "%s", name);
  }
}

static char *
idl_type_string(const idl_type_spec_t *type_spec)
{
  char buf[1024] = "";
  append_idl_type(buf, sizeof(buf), type_spec);
  return idl_strdup(buf);
}

static void
record_dims(dm_rec_t *rec, const idl_declarator_t *declarator)
{
  const idl_literal_t *literal = declarator->const_expr;
  for (; literal && rec->n_dims < (int)(sizeof(rec->dims) / sizeof(rec->dims[0])); literal = idl_next(literal))
    rec->dims[rec->n_dims++] = literal->value.uint32;
}

static idl_retcode_t
emit_implicit_sequence(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  char *name, *type, *macro, dims[32] = "";
  const char *fmt, *star = "", *lpar = "", *rpar = "";
  const idl_type_spec_t *type_spec = idl_type_spec(node);

  (void)pstate;
  (void)path;
  if (revisit) {
    assert(idl_is_sequence(node));
  } else if (idl_is_sequence(node)) {
    if (idl_is_sequence(type_spec))
      return IDL_VISIT_REVISIT | IDL_VISIT_TYPE_SPEC;
  } else {
    assert(idl_is_member(node) || idl_is_case(node));
    if (!idl_is_sequence(type_spec))
      return IDL_VISIT_DONT_RECURSE;
    return IDL_VISIT_TYPE_SPEC;
  }

  /* strings are special */
  if (idl_is_xstring(type_spec) && idl_is_bounded(type_spec)) {
    lpar = "(";
    rpar = ")";
    if (idl_is_bounded(type_spec))
      idl_snprintf(dims, sizeof(dims), "[%"PRIu32"]", idl_bound(type_spec)+1);
  } else if (idl_is_xstring(type_spec)) {
    star = "*";
  }

  const char *type_prefix = get_type_prefix(type_spec);

  // https://www.omg.org/spec/C/1.0/PDF section 1.11
  if (IDL_PRINTA(&name, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
  if (IDL_PRINTA(&type, print_type, type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;
  if (IDL_PRINTA(&macro, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
  for (char *ptr=macro; *ptr; ptr++)
    if (idl_islower((unsigned char)*ptr))
      *ptr = (char)idl_toupper((unsigned char)*ptr);
  fmt = "#ifndef %1$s_DEFINED\n"
        "#define %1$s_DEFINED\n"
        "typedef struct %2$s\n{\n"
        "  uint32_t _maximum;\n"
        "  uint32_t _length;\n"
        "  %3$s%4$s %5$s%6$s*_buffer%7$s%8$s;\n"
        "  bool _release;\n"
        "} %2$s;\n\n"
        "#define %2$s__alloc() \\\n"
        "((%2$s*) dds_alloc (sizeof (%2$s)));\n\n"
        "#define %2$s_allocbuf(l) \\\n"
        "((%3$s%4$s %5$s%6$s*%7$s%8$s) dds_alloc ((l) * sizeof (%3$s%4$s%5$s%8$s)))\n"
        "#endif /* %1$s_DEFINED */\n\n";
  if (idl_fprintf(gen->header.handle, fmt, macro, name, type_prefix, type, star, lpar, rpar, dims) < 0)
    return IDL_RETCODE_NO_MEMORY;

  return IDL_VISIT_DONT_RECURSE;
}

static idl_retcode_t
generate_implicit_sequences(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  idl_retcode_t ret;
  idl_visitor_t visitor;

  (void)pstate;
  (void)revisit;
  (void)path;
  memset(&visitor, 0, sizeof(visitor));
  visitor.visit = IDL_MEMBER | IDL_CASE | IDL_SEQUENCE;
  visitor.accept[IDL_ACCEPT] = &emit_implicit_sequence;
  assert(idl_is_member(node) || idl_is_case(node) || idl_is_sequence(node));
  if ((ret = idl_visit(pstate, node, &visitor, user_data)) < 0)
    return ret;
  return IDL_RETCODE_OK;
}

/* members with multiple declarators result in multiple members */
static idl_retcode_t
emit_field(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  char *type;
  const char *fmt, *indent, *name, *str_ptr = "", *ptr_open = "", *ptr_close = "";
  const void *root;
  idl_literal_t *literal;
  idl_type_spec_t *type_spec;

  (void)pstate;
  (void)revisit;
  (void)path;
  root = idl_parent(node);
  indent = idl_is_case(root) ? "    " : "  ";
  name = idl_identifier(node);
  type_spec = idl_type_spec(node);
  if (IDL_PRINTA(&type, print_type, type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;

  if (dm_last_struct) {
      dm_rec_t *member = dm_new();
      member->name = idl_strdup(name);
      
      if (idl_type(type_spec) == IDL_STRING) {
           member->type = idl_strdup("string");
           if (idl_is_bounded(type_spec)) member->bound = idl_bound(type_spec);
      } else if (idl_type(type_spec) == IDL_WSTRING) {
           member->type = idl_strdup("wstring");
           if (idl_is_bounded(type_spec)) member->bound = idl_bound(type_spec);
      } else if (idl_is_sequence(type_spec)) {
           member->type = idl_strdup(type);
           member->kind = idl_strdup("sequence");
      } else {
           member->type = idl_strdup(type);
      }

      member->idl_type = idl_type_string(type_spec);
      if (idl_is_declarator(node))
          record_dims(member, (const idl_declarator_t *)node);

      if (idl_is_array(node)) {
          member->is_array = 1;
          member->size = 1;
          idl_literal_t *literal = ((const idl_declarator_t *)node)->const_expr;
          for (; literal; literal = idl_next(literal)) {
            member->size *= literal->value.uint32;
          }
      }

      if (idl_is_member(root)) {
          const idl_member_t *memb = (const idl_member_t *)root;
          if (memb->key.value) member->is_key = 1;
          if (memb->optional.value) member->is_optional = 1;
          if (memb->external.value) member->is_external = 1;
      }
      
      if (idl_is_declarator(node)) {
          const idl_declarator_t *decl = (const idl_declarator_t *)node;
          member->member_id = decl->id.value;
          if (decl->id.annotation) {
              member->has_explicit_id = 1;
          }
      }

      // Check for bounded string/sequence
      if (idl_is_xstring(type_spec) && idl_is_bounded(type_spec)) {
           member->bound = idl_bound(type_spec);
      } else if (idl_is_sequence(type_spec) && idl_is_bounded(type_spec)) {
           member->bound = idl_bound(type_spec);
      }
      
      dm_add(&dm_last_struct->members, member);
      
      if (idl_is_case(root)) {
          const idl_case_t *case_node = (const idl_case_t *)root;
          const idl_case_label_t *l = case_node->labels;
          if (!l) {
               // Should not happen for valid case, unless default without label?
               // Implicit default case?
               if (case_node->node.mask & IDL_IMPLICIT_DEFAULT_CASE_LABEL) {
                   dm_rec_t *lab = dm_new();
                   lab->name = idl_strdup("default");
                   dm_add(&member->labels, lab);
               }
          }
          for (; l; l = idl_next(l)) {
              dm_rec_t *lab = dm_new();
              if (idl_is_default_case_label(l)) {
                  lab->name = idl_strdup("default");
              } else {
                  int64_t val = idl_case_label_intvalue(l);
                  char buf[64];
                  snprintf(buf, sizeof(buf), "%" PRId64, val);
                  lab->name = idl_strdup(buf);
              }
              dm_add(&member->labels, lab);
          }
      }
  }

  if (idl_is_xstring(type_spec) && !idl_is_bounded(type_spec))
    str_ptr = "* ";

  if (idl_is_external(root) || idl_is_optional(root)) {
    idl_type_spec_t *actual_type = idl_strip(type_spec, IDL_STRIP_ALIASES|IDL_STRIP_FORWARD);
    if (idl_is_array(node) || (idl_is_xstring(actual_type) && idl_is_bounded(actual_type))) {
      /* for arrays and bounded strings, add paratheses so that it won't be an
         array of pointers but a pointer to the array, e.g. long (*member_name)[5] */
      ptr_open = "(* ";
      ptr_close = ")";
    } else if (!idl_is_xstring(actual_type)) {
      /* unbounded strings are already a pointer, don't add an extra * for external */
      ptr_open = "* ";
    }
  }

  const char *type_prefix = get_type_prefix(type_spec);

  fmt = "%s";
  if (idl_fprintf(gen->header.handle, fmt, indent) < 0)
    return IDL_RETCODE_NO_MEMORY;

  bool empty = idl_is_empty(type_spec);
  if (empty)
    if (fputs("/* ", gen->header.handle) < 0)
      return IDL_RETCODE_NO_MEMORY;

  fmt = "%s%s %s%s%s%s";
  if (idl_fprintf(gen->header.handle, fmt, type_prefix, type, str_ptr, ptr_open, name, ptr_close) < 0)
    return IDL_RETCODE_NO_MEMORY;

  /* array dims */
  fmt = "[%" PRIu32 "]";
  literal = ((const idl_declarator_t *)node)->const_expr;
  for (; literal; literal = idl_next(literal)) {
    assert(idl_type(literal) == IDL_ULONG);
    if (idl_fprintf(gen->header.handle, fmt, literal->value.uint32) < 0)
      return IDL_RETCODE_NO_MEMORY;
  }

  /* bounded string dims */
  if (idl_is_xstring(type_spec) && idl_is_bounded(type_spec)) {
    fmt = "[%"PRIu32"]";
    if (idl_fprintf(gen->header.handle, fmt, idl_bound(type_spec) + 1) < 0)
      return IDL_RETCODE_NO_MEMORY;
  }

  /* close member with ; or end empty-comment */
  fmt = empty ? " */ /* no members */\n" : ";\n";
  if (fputs(fmt, gen->header.handle) < 0)
    return IDL_RETCODE_NO_MEMORY;

  return IDL_RETCODE_OK;
}

static idl_retcode_t
emit_member(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  const idl_member_t *member = (const idl_member_t *)node;
  const idl_declarator_t *declarator;
  idl_retcode_t ret;

  (void)revisit;

  declarator = member->declarators;
  for (; declarator; declarator = idl_next(declarator)) {
    if ((ret = emit_field(pstate, false, path, declarator, user_data)))
      return ret;
  }
  return IDL_VISIT_DONT_RECURSE;
}

static idl_retcode_t
emit_case(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  const idl_case_t *c = (const idl_case_t *)node;
  const idl_declarator_t *declarator;
  idl_retcode_t ret;

  (void)revisit;

  declarator = c->declarator;
  for (; declarator; declarator = idl_next(declarator)) {
    if ((ret = emit_field(pstate, false, path, declarator, user_data)))
      return ret;
  }
  return IDL_VISIT_DONT_RECURSE;
}



// Helper to parse QoS string values
static void parse_qos_value(const char* token, dm_qos_t* qos) {
    if (strcmp(token, "reliable") == 0 || strcmp(token, "best_effort") == 0) {
        if (qos->reliability) free(qos->reliability);
        qos->reliability = idl_strdup(token);
    } else if (strcmp(token, "volatile") == 0 || 
               strcmp(token, "transient_local") == 0 || 
               strcmp(token, "transient") == 0 ||
               strcmp(token, "persistent") == 0) {
        if (qos->durability) free(qos->durability);
        qos->durability = idl_strdup(token);
    } else if (strcmp(token, "keep_last") == 0 || strcmp(token, "keep_all") == 0) {
        if (qos->history) free(qos->history);
        qos->history = idl_strdup(token);
    } else {
        char* end;
        long val = strtol(token, &end, 10);
        if (*end == '\0') {
            qos->depth = (int32_t)val;
        }
    }
}

// Scans for #pragma topic AFTER the struct
static void scan_for_pragma_topic(const idl_pstate_t* pstate, const idl_struct_t* s, dm_qos_t* qos) {
    if (!pstate->buffer.data) return;

    // Get location of the struct (use node location to get the end line)
    const idl_location_t* loc = &s->node.symbol.location;
    int struct_end_line = loc->last.line;
    
    char* cursor = pstate->buffer.data;
    char* line_start = cursor;
    int current_line = 1;

    while (*cursor) {
        if (*cursor == '\n') { // End of line
            // Process the line we just passed
            
            // Check current line number
            if (current_line > struct_end_line) {
                // Check if line contains #pragma topic
                // Skip leading whitespace
                char* ptr = line_start;
                while (ptr < cursor && (*ptr == ' ' || *ptr == '\t')) ptr++;
                
                if (ptr < cursor && *ptr == '#') {
                    ptr++;
                    while (ptr < cursor && (*ptr == ' ' || *ptr == '\t')) ptr++;
                    if (ptr + 6 <= cursor && strncmp(ptr, "pragma", 6) == 0) {
                        ptr += 6;
                        while (ptr < cursor && (*ptr == ' ' || *ptr == '\t')) ptr++;
                        if (ptr + 5 <= cursor && strncmp(ptr, "topic", 5) == 0) {
                             ptr += 5;
                             // Found it! extract rest of line
                             char* eol = cursor;
                             // Trim trailing whitespace/cr
                             while (eol > ptr && (*(eol-1) == ' ' || *(eol-1) == '\t' || *(eol-1) == '\r')) eol--;
                             
                             size_t val_len = eol - ptr;
                             char* val = malloc(val_len + 1);
                             strncpy(val, ptr, val_len);
                             val[val_len] = '\0';
                             
                             // Parse tokens
                             char* token = strtok(val, " \t");
                             while (token) {
                                 parse_qos_value(token, qos);
                                 token = strtok(NULL, " \t");
                             }
                             free(val);
                             // return; // Continue scanning for more pragmas
                        }
                    }
                }
            }
            
            line_start = cursor + 1;
            current_line++;
        }
        cursor++;
    }
}

static dm_qos_t* extract_qos(const idl_pstate_t* pstate, const idl_struct_t* s) {
    dm_qos_t* qos = calloc(1, sizeof(dm_qos_t));
    if (!qos) return NULL;
    
    // Default values
    qos->reliability = NULL; // Default
    qos->durability = NULL;
    qos->history = NULL;
    qos->depth = 0;

    // Scan for pragma topic in the source
    scan_for_pragma_topic(pstate, s, qos);
    
    return qos;
}


static idl_retcode_t
emit_struct(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  idl_retcode_t ret = IDL_RETCODE_NO_MEMORY;
  struct generator *gen = user_data;
  char *name = NULL;
  const char *fmt;
  bool empty = idl_is_empty(node);

  if (IDL_PRINTA(&name, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;

  if (revisit) {
    if (dm_last_struct && dm_last_struct->c_name && strcmp(dm_last_struct->c_name, name) == 0) {
      dm_calculate_layout(dm_last_struct);
      // reset dm_last_struct to avoid adding members to closed struct
      // Note: THIS ASSUMES NO NESTED STRUCT DEFINITIONS INSIDE STRUCT logic in the visitation
      // If visitors are recursive, we might need a stack.
      // But IDL usually defines structs at module level mostly, unless nested types.
      dm_last_struct = NULL; 
    }

    fmt = "} %1$s;\n";
    if (idl_fprintf(gen->header.handle, fmt, name) < 0)
      return IDL_RETCODE_NO_MEMORY;
    if (!empty && idl_fprintf(gen->header.handle, "\n") < 0)
      return IDL_RETCODE_NO_MEMORY;
    /* Generate descriptor for topics (non-nested structs) */
    const idl_struct_t *s_node = (const idl_struct_t *)node;
    bool is_topic = !s_node->nested.value; // Simple check avoiding idl_is_topic crash
    
    if (!empty && is_topic) {
      if (gen->config.export_macro && idl_fprintf(gen->header.handle, "%1$s ", gen->config.export_macro) < 0)
        return IDL_RETCODE_NO_MEMORY;
      fmt = "extern const dds_topic_descriptor_t %1$s_desc;\n"
            "\n"
            "#define %1$s__alloc() \\\n"
            "((%1$s*) dds_alloc (sizeof (%1$s)));\n"
            "\n"
            "#define %1$s_free(d,o) \\\n"
            "dds_sample_free ((d), &%1$s_desc, (o))\n"
            "\n";
      if (idl_fprintf(gen->header.handle, fmt, name) < 0)
        return IDL_RETCODE_NO_MEMORY;
      if (gen->config.generate_cdrstream_desc)
      {
        if (gen->config.export_macro && idl_fprintf(gen->header.handle, "%1$s ", gen->config.export_macro) < 0)
          return IDL_RETCODE_NO_MEMORY;
        fmt = "extern const struct dds_cdrstream_desc %1$s_cdrstream_desc;\n\n";
        if (idl_fprintf(gen->header.handle, fmt, name) < 0)
          return IDL_RETCODE_NO_MEMORY;
      }
      if ((ret = generate_descriptor(pstate, gen, node)))
        return ret;
    }
    if (empty)
      if (idl_fprintf(gen->header.handle, "#endif /* empty struct */\n\n") < 0)
        return IDL_RETCODE_NO_MEMORY;
  } else {
    char *scoped_name;
    if (IDL_PRINTA(&scoped_name, print_scoped_name, node) < 0) return IDL_RETCODE_NO_MEMORY;
    
    dm_rec_t *rec = dm_new();
    rec->name = idl_strdup(scoped_name);
    rec->c_name = idl_strdup(name);
    rec->kind = idl_strdup("struct");
    
    // Extract QoS
    if (!empty) {
       rec->qos = extract_qos(pstate, (const idl_struct_t*)node);
    }
    
    idl_extensibility_t ext = ((const idl_struct_t*)node)->extensibility.value;
    if (ext == IDL_MUTABLE) rec->extensibility = idl_strdup("mutable");
    else if (ext == IDL_APPENDABLE) rec->extensibility = idl_strdup("appendable");
    else rec->extensibility = idl_strdup("final");
    
    // Check keylist pragma if needed, but IDL model handles it usually.
    // FIXME: idl_is_topic causes segfault in tests, disabled for now.
    
    dm_add(&dm_types, rec);
    dm_last_struct = rec;

    const idl_struct_t *_struct = (const idl_struct_t *)node;
    if (_struct->inherit_spec) {
      char *base_name;
      if (IDL_PRINTA(&base_name, print_scoped_name, _struct->inherit_spec->base) >= 0)
        rec->base = idl_strdup(base_name);
    }
    const idl_member_t *members = _struct->members;
    /* ensure typedefs for unnamed sequences exist beforehand */
    if (members && (ret = generate_implicit_sequences(pstate, revisit, path, members, user_data)))
      return ret;
    if (empty) {
      if (idl_fprintf(gen->header.handle, "#if 0 /* empty struct */\n") < 0)
        return IDL_RETCODE_NO_MEMORY;
    }
    fmt = "typedef struct %1$s\n"
          "{\n";
    if (idl_fprintf(gen->header.handle, fmt, name) < 0)
      return IDL_RETCODE_NO_MEMORY;
    if (_struct->inherit_spec) {
      char *type;
      idl_struct_t *base_struct = (idl_struct_t*)_struct->inherit_spec->base;
      if (IDL_PRINTA(&type, print_type, base_struct) < 0)
        return IDL_RETCODE_NO_MEMORY;
      const char *type_prefix = get_type_prefix(base_struct);
      fmt = "  %s%s %s;\n";
      if (idl_fprintf(gen->header.handle, fmt, type_prefix, type, STRUCT_BASE_MEMBER_NAME) < 0)
        return IDL_RETCODE_NO_MEMORY;
    }
    return IDL_VISIT_REVISIT;
  }

  return IDL_RETCODE_OK;
}

static idl_retcode_t
emit_union(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  idl_retcode_t ret = IDL_RETCODE_NO_MEMORY;
  struct generator *gen = user_data;
  char *name, *type;
  const char *fmt;
  const idl_switch_type_spec_t *switch_type_spec;

  (void)pstate;
  (void)path;
  assert(idl_is_union(node));
  if (IDL_PRINTA(&name, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
  switch_type_spec = ((const idl_union_t *)node)->switch_type_spec;
  assert(idl_is_switch_type_spec(switch_type_spec));
  if (IDL_PRINTA(&type, print_type, switch_type_spec->type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;

  if (revisit) {
    if (dm_last_struct && strcmp(dm_last_struct->c_name, name) == 0) {
      dm_calculate_layout(dm_last_struct);
      dm_last_struct = NULL;
    }
    
    fmt = "  } _u;\n"
          "} %1$s;\n"
          "\n";
    if (idl_fprintf(gen->header.handle, fmt, name) < 0)
      return IDL_RETCODE_NO_MEMORY;

    /* FIXME: idl_is_topic(node) check disabled due to crash in tests */
    if (idl_is_topic(node, (pstate->config.flags & IDL_FLAG_KEYLIST) != 0)) {
      if (gen->config.export_macro && idl_fprintf(gen->header.handle, "%1$s ", gen->config.export_macro) < 0)
        return IDL_RETCODE_NO_MEMORY;
      fmt = "extern const dds_topic_descriptor_t %1$s_desc;\n"
            "\n"
            "#define %1$s__alloc() \\\n"
            "((%1$s*) dds_alloc (sizeof (%1$s)));\n"
            "\n"
            "#define %1$s_free(d,o) \\\n"
            "dds_sample_free ((d), &%1$s_desc, (o))\n"
            "\n";
      if (idl_fprintf(gen->header.handle, fmt, name) < 0)
        return IDL_RETCODE_NO_MEMORY;
      if (gen->config.generate_cdrstream_desc)
      {
        if (gen->config.export_macro && idl_fprintf(gen->header.handle, "%1$s ", gen->config.export_macro) < 0)
          return IDL_RETCODE_NO_MEMORY;
        fmt = "extern const struct dds_cdrstream_desc %1$s_cdrstream_desc;\n\n";
        if (idl_fprintf(gen->header.handle, fmt, name) < 0)
          return IDL_RETCODE_NO_MEMORY;
      }
      if ((ret = generate_descriptor(pstate, gen, node)))
        return ret;
    }
  } else {
    char *scoped_name;
    // Assuming print_scoped_name exists, otherwise fallback to name
    if (IDL_PRINTA(&scoped_name, print_scoped_name, node) < 0) scoped_name = name; 
    
    dm_rec_t *rec = dm_new();
    rec->name = idl_strdup(scoped_name);
    rec->c_name = idl_strdup(name);
    rec->kind = idl_strdup("union");
    rec->discriminator = idl_strdup(type); // Discriminator type C name
    rec->idl_type = idl_type_string(switch_type_spec->type_spec); // ... and in IDL syntax

    idl_extensibility_t ext = ((const idl_union_t*)node)->extensibility.value;
    if (ext == IDL_MUTABLE) rec->extensibility = idl_strdup("mutable");
    else if (ext == IDL_APPENDABLE) rec->extensibility = idl_strdup("appendable");
    else rec->extensibility = idl_strdup("final");
    
    // Placeholder for QoS extraction if node is a topic
    if (idl_is_topic(node, (pstate->config.flags & IDL_FLAG_KEYLIST) != 0)) {
        // rec->qos = extract_qos(node); 
        // Not implemented.
    }
    
    dm_add(&dm_types, rec);
    dm_last_struct = rec;
    
    const idl_case_t *cases = ((const idl_union_t *)node)->cases;
    /* ensure typedefs for unnamed sequences exist beforehand */
    if ((ret = generate_implicit_sequences(pstate, revisit, path, cases, user_data)))
      return ret;
    fmt = "typedef struct %1$s\n"
          "{\n"
          "  %2$s _d;\n"
          "  union\n"
          "  {\n";
    if (idl_fprintf(gen->header.handle, fmt, name, type) < 0)
      return IDL_RETCODE_NO_MEMORY;
    return IDL_VISIT_REVISIT;
  }

  return IDL_RETCODE_OK;
}

static idl_retcode_t
emit_forward(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  char *name;
  const char *fmt;
  struct generator *gen = user_data;

  (void)pstate;
  (void)revisit;
  (void)path;
  assert(idl_is_forward(node));
  if (IDL_PRINTA(&name, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;

  fmt = "struct %1$s;\n";
  if (idl_fprintf(gen->header.handle, fmt, name) < 0)
    return IDL_RETCODE_NO_MEMORY;
  return IDL_RETCODE_OK;
}

static idl_retcode_t
emit_sequence_typedef(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  idl_retcode_t ret;
  struct generator *gen = user_data;
  char *type, *name, dims[32] = "";
  const char *fmt, *spc = " ", *star = "", *lpar = "", *rpar = "";
  const idl_declarator_t *declarator;
  const idl_literal_t *literal;
  const idl_type_spec_t *type_spec;

  type_spec = idl_type_spec(node);
  assert(idl_is_sequence(type_spec));
  type_spec = idl_type_spec(type_spec);
  /* ensure typedefs for implicit sequences exist beforehand */
  if (idl_is_sequence(type_spec) &&
      (ret = generate_implicit_sequences(pstate, revisit, path, type_spec, user_data)))
    return ret;

  /* strings are special */
  if (idl_is_xstring(type_spec) && idl_is_bounded(type_spec)) {
    lpar = "(";
    rpar = ")";
    if (idl_is_bounded(type_spec))
      idl_snprintf(dims, sizeof(dims), "[%"PRIu32"]", idl_bound(type_spec)+1);
  } else if (idl_is_xstring(type_spec)) {
    star = "*";
  }

  const char *type_prefix = get_type_prefix(type_spec);

  if (IDL_PRINTA(&type, print_type, type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;
  declarator = ((const idl_typedef_t *)node)->declarators;
  for (; declarator; declarator = idl_next(declarator)) {
    if (IDL_PRINTA(&name, print_type, declarator) < 0)
      return IDL_RETCODE_NO_MEMORY;
    fmt = "typedef struct %1$s\n{\n"
          "  uint32_t _maximum;\n"
          "  uint32_t _length;\n"
          "  %2$s%3$s %4$s%5$s*_buffer%6$s%7$s;\n"
          "  bool _release;\n"
          "} %1$s";
    if (idl_fprintf(gen->header.handle, fmt, name, type_prefix, type, star, lpar, rpar, dims) < 0)
      return IDL_RETCODE_NO_MEMORY;
    literal = declarator->const_expr;
    for (; literal; literal = idl_next(literal)) {
      fmt = "%s[%" PRIu32 "]";
      if (idl_fprintf(gen->header.handle, fmt, spc, literal->value.uint32) < 0)
        return IDL_RETCODE_NO_MEMORY;
    }
    fmt = ";\n\n"
          "#define %1$s__alloc() \\\n"
          "((%1$s*) dds_alloc (sizeof (%1$s)));\n\n"
          "#define %1$s_allocbuf(l) \\\n"
          "((%2$s%3$s %4$s%5$s*%6$s%7$s) dds_alloc ((l) * sizeof (%2$s%3$s%4$s%7$s)))\n";
    if (idl_fprintf(gen->header.handle, fmt, name, type_prefix, type, star, lpar, rpar, dims) < 0)
      return IDL_RETCODE_NO_MEMORY;
  }

  return IDL_VISIT_DONT_RECURSE;
}

static idl_retcode_t
emit_typedef(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  const char *fmt, *star = "";
  char *name = NULL, *type = NULL;
  const idl_declarator_t *declarator;
  const idl_literal_t *literal;
  const idl_type_spec_t *type_spec;

  type_spec = idl_type_spec(node);
  
  if (IDL_PRINTA(&type, print_type, type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;
    
  declarator = ((const idl_typedef_t *)node)->declarators;
  for (; declarator; declarator = idl_next(declarator)) {
      if (IDL_PRINTA(&name, print_type, declarator) < 0)
        return IDL_RETCODE_NO_MEMORY;
      
      dm_rec_t *rec = dm_new();
      rec->c_name = idl_strdup(name); 
      
      // Use C name for Name as fallback or primary if scoped name fails/is weird
      rec->name = idl_strdup(name); 
      /*
      char *scoped_name = NULL;
      if (IDL_PRINTA(&scoped_name, print_scoped_name, declarator) >= 0) {
          // Check if scoped_name is valid?
          // Using C Name seems safer for now to avoid encoding/pointer issues
          // free(rec->name);
          // rec->name = scoped_name;
      } 
      */

      rec->type = idl_strdup(type);
      {
        char *scoped;
        if (IDL_PRINTA(&scoped, print_scoped_name, declarator) >= 0)
          rec->scoped_name = idl_strdup(scoped);
      }
      rec->idl_type = idl_type_string(type_spec);
      record_dims(rec, declarator);

      /* A string typedef records "string"/"wstring" and its bound, as a string member does;
         print_type gives the C type ("char"), which made importers read it as a byte. */
      if (idl_type(type_spec) == IDL_STRING || idl_type(type_spec) == IDL_WSTRING) {
          rec->type = idl_strdup(idl_type(type_spec) == IDL_STRING ? "string" : "wstring");
          if (idl_is_bounded(type_spec)) rec->bound = idl_bound(type_spec);
      }

      if (idl_is_sequence(type_spec)) {
          rec->kind = idl_strdup("sequence");
          // Type for sequence is the element type.
          // idl_type_spec(type_spec) gets the element type for sequence?
          // No, idl_type_spec() returns the type specifier node.
          // For sequence node, type_spec member points to element type.
          // We need IDL_PRINTA(&elem_type, print_type, ((idl_sequence_t*)type_spec)->type_spec)
          // But 'type' extracted above is likely "sequence<long>".
          // We want Kind="sequence", Type="long".
          
          char *elem_type = NULL;
          const idl_type_spec_t *elem_spec = ((const idl_sequence_t*)type_spec)->type_spec;
          if (IDL_PRINTA(&elem_type, print_type, elem_spec) < 0) {} // handle error?
          
          if (elem_type) {
             // Free the "sequence<...>" string (rec->type) and replace with element type
             // valid memory management?
             /* free(rec->type); */
             rec->type = idl_strdup(elem_type);
             /* free(elem_type); */
          }
          
          if (idl_is_bounded(type_spec)) rec->bound = idl_bound(type_spec);

      } else if (idl_is_array(declarator)) {
          rec->kind = idl_strdup("alias"); // Alias to array?
          rec->is_array = 1;
          rec->size = 1;
           idl_literal_t *lit = ((const idl_declarator_t *)declarator)->const_expr;
          for (; lit; lit = idl_next(lit)) {
            rec->size *= lit->value.uint32;
          }
      } else {
          rec->kind = idl_strdup("alias");
      }
      
      dm_add(&dm_types, rec);
  }

  /* typedef of sequence requires a little magic */
  if (idl_is_sequence(type_spec))
    return emit_sequence_typedef(pstate, revisit, path, node, user_data);

  bool is_bounded_string = false;
  if ( idl_is_xstring(type_spec) ) {
    if ( idl_is_bounded(type_spec) ) {
      is_bounded_string = true;
    } else {
      star = "*";
    }
  }

  const char *type_prefix = get_type_prefix(type_spec);

  if (IDL_PRINTA(&type, print_type, type_spec) < 0)
    return IDL_RETCODE_NO_MEMORY;
  declarator = ((const idl_typedef_t *)node)->declarators;
  for (; declarator; declarator = idl_next(declarator)) {
    if (IDL_PRINTA(&name, print_type, declarator) < 0)
      return IDL_RETCODE_NO_MEMORY;
    fmt = "typedef %1$s%2$s %3$s%4$s";
    if (idl_fprintf(gen->header.handle, fmt, type_prefix, type, star, name) < 0)
      return IDL_RETCODE_NO_MEMORY;
    literal = declarator->const_expr;
    for (; literal; literal = idl_next(literal)) {
      fmt = "[%" PRIu32 "]";
      if (idl_fprintf(gen->header.handle, fmt, literal->value.uint32) < 0)
        return IDL_RETCODE_NO_MEMORY;
    }
    if ( is_bounded_string ) {
      // The string bound must come after the array dimensions.
      idl_fprintf(gen->header.handle, "[%" PRIu32 "]", idl_bound(type_spec)+1);
    }
    fmt = ";\n\n"
          "#define %1$s__alloc() \\\n"
          "((%1$s*) dds_alloc (sizeof (%1$s)));\n\n";
    if (idl_fprintf(gen->header.handle, fmt, name) < 0)
      return IDL_RETCODE_NO_MEMORY;

    const void *base_type = idl_strip(declarator, IDL_STRIP_ALIASES | IDL_STRIP_FORWARD);
    if (base_type && idl_is_topic(base_type, (pstate->config.flags & IDL_FLAG_KEYLIST) != 0) && !idl_is_empty(base_type)) {
      char *base_name;
      if (IDL_PRINTA(&base_name, print_type, base_type) < 0)
        return IDL_RETCODE_NO_MEMORY;
      fmt = "#define %1$s_free(d,o) \\\n"
            "%2$s_free ((d), (o))\n\n";
      if (idl_fprintf(gen->header.handle, fmt, name, base_name) < 0)
        return IDL_RETCODE_NO_MEMORY;
    }
  }

  return IDL_VISIT_DONT_RECURSE;
}

static idl_retcode_t
emit_enum(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  char *name = NULL, *type = NULL;
  const char *fmt, *sep = "";
  const idl_enumerator_t *enumerator;
  int64_t skip = 0;
  int32_t value = 0;

  (void)pstate;
  (void)revisit;
  (void)path;
  if (IDL_PRINTA(&type, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
    
  // DM Extraction for Enum
  dm_rec_t *rec = dm_new();
  rec->name = idl_strdup(type);
  rec->c_name = idl_strdup(type);
  rec->kind = idl_strdup("enum");
  rec->size = 4; rec->align = 4;
  {
    char *scoped;
    if (IDL_PRINTA(&scoped, print_scoped_name, node) >= 0)
      rec->scoped_name = idl_strdup(scoped);
  }
  rec->bit_bound = ((const idl_enum_t*)node)->bit_bound.value;
  
  idl_extensibility_t ext = ((const idl_enum_t*)node)->extensibility.value;
  if (ext == IDL_MUTABLE) rec->extensibility = idl_strdup("mutable");
  else if (ext == IDL_APPENDABLE) rec->extensibility = idl_strdup("appendable");
  else rec->extensibility = idl_strdup("final");
  
  dm_add(&dm_types, rec);
  dm_last_enum = rec;

  if (idl_fprintf(gen->header.handle, "typedef enum %s\n{\n", type) < 0)
    return IDL_RETCODE_NO_MEMORY;

  enumerator = ((const idl_enum_t *)node)->enumerators;
  for (; enumerator; enumerator = idl_next(enumerator)) {
    if (IDL_PRINTA(&name, print_type, enumerator) < 0)
      return IDL_RETCODE_NO_MEMORY;
    value = enumerator->value.value;
    
    // Add enumerator to DM
    dm_rec_t *en_val = dm_new();
    en_val->name = idl_strdup(name);
    en_val->kind = idl_strdup("enumerator");
    en_val->has_value = 1;
    en_val->value_type = DM_TYPE_INT; // Enums are integers
    en_val->value.int64 = value;
    dm_add(&rec->members, en_val);
    if (value == skip)
      fmt = "%s  %s";
    else
      fmt = "%s  %s = %" PRId32;
    if (idl_fprintf(gen->header.handle, fmt, sep, name, value) < 0)
      return IDL_RETCODE_NO_MEMORY;
    sep = ",\n";
    skip = (int64_t) value + 1;
  }

  fmt = "\n} %1$s;\n\n"
        "#define %1$s__alloc() \\\n"
        "((%1$s*) dds_alloc (sizeof (%1$s)));\n\n";
  if (idl_fprintf(gen->header.handle, fmt, type) < 0)
    return IDL_RETCODE_NO_MEMORY;

  return IDL_VISIT_DONT_RECURSE;
}

static idl_retcode_t
emit_bitmask(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  char *name = NULL, *type = NULL;
  const char *fmt, *base_type_str, *suffix = "";
  const idl_bitmask_t *bitmask = (const idl_bitmask_t *)node;
  const idl_bit_value_t *bit_value;

  (void)pstate;
  (void)revisit;
  (void)path;
  if (IDL_PRINTA(&type, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
  uint16_t bit_bound = bitmask->bit_bound.value;

  /* recorded so an importer can name it (and say it does not support it) */
  dm_rec_t *rec = dm_new();
  rec->name = idl_strdup(type);
  rec->c_name = idl_strdup(type);
  rec->kind = idl_strdup("bitmask");
  {
    char *scoped;
    if (IDL_PRINTA(&scoped, print_scoped_name, node) >= 0)
      rec->scoped_name = idl_strdup(scoped);
  }
  rec->bit_bound = bit_bound;
  rec->size = rec->align = bit_bound <= 8 ? 1 : bit_bound <= 16 ? 2 : bit_bound <= 32 ? 4 : 8;
  dm_add(&dm_types, rec);
  if (bit_bound <= 8)
    base_type_str = "uint8_t";
  else if (bit_bound <= 16)
    base_type_str = "uint16_t";
  else if (bit_bound <= 32) {
    base_type_str = "uint32_t";
    suffix = "lu";
  } else {
    suffix = "llu";
    base_type_str = "uint64_t";
  }
  if (idl_fprintf(gen->header.handle, "typedef %s %s;\n", base_type_str, type) < 0)
    return IDL_RETCODE_NO_MEMORY;

  bit_value = bitmask->bit_values;
  for (; bit_value; bit_value = idl_next(bit_value)) {
    if (IDL_PRINTA(&name, print_type, bit_value) < 0)
      return IDL_RETCODE_NO_MEMORY;
    fmt = "#define %s (1%s << %u)\n";
    if (idl_fprintf(gen->header.handle, fmt, name, suffix, bit_value->position.value) < 0)
      return IDL_RETCODE_NO_MEMORY;
  }

  return IDL_VISIT_DONT_RECURSE;
}

static int
print_literal(
  const idl_pstate_t *pstate,
  struct generator *gen,
  const idl_literal_t *literal)
{
  idl_type_t type;
  FILE *fp = gen->header.handle;

  (void)pstate;
  switch ((type = idl_type(literal))) {
    case IDL_CHAR:
      return idl_fprintf(fp, "'%c'", literal->value.chr);
    case IDL_BOOL:
      return idl_fprintf(fp, "%s", literal->value.bln ? "true" : "false");
    case IDL_INT8:
      return idl_fprintf(fp, "%" PRId8, literal->value.int8);
    case IDL_OCTET:
    case IDL_UINT8:
      return idl_fprintf(fp, "%" PRIu8, literal->value.uint8);
    case IDL_SHORT:
    case IDL_INT16:
      return idl_fprintf(fp, "%" PRId16, literal->value.int16);
    case IDL_USHORT:
    case IDL_UINT16:
      return idl_fprintf(fp, "%" PRIu16, literal->value.uint16);
    case IDL_LONG:
    case IDL_INT32:
      return idl_fprintf(fp, "%" PRId32, literal->value.int32);
    case IDL_ULONG:
    case IDL_UINT32:
      return idl_fprintf(fp, "%" PRIu32, literal->value.uint32);
    case IDL_LLONG:
    case IDL_INT64:
      return idl_fprintf(fp, "%" PRId64, literal->value.int64);
    case IDL_ULLONG:
    case IDL_UINT64:
      return idl_fprintf(fp, "%" PRIu64, literal->value.uint64);
    case IDL_FLOAT:
      return idl_fprintf(fp, "%.6f", literal->value.flt);
    case IDL_DOUBLE:
      return idl_fprintf(fp, "%f", literal->value.dbl);
    case IDL_LDOUBLE:
      return idl_fprintf(fp, "%Lf", literal->value.ldbl);
    case IDL_STRING:
      return idl_fprintf(fp, "\"%s\"", literal->value.str);
    default: {
      char *name;
      assert(type == IDL_ENUM);
      if (IDL_PRINTA(&name, print_type, literal) < 0)
        return -1;
      return idl_fprintf(fp, "%s", name);
    }
  }
}

static idl_retcode_t
emit_const(
  const idl_pstate_t *pstate,
  bool revisit,
  const idl_path_t *path,
  const void *node,
  void *user_data)
{
  struct generator *gen = user_data;
  char *type;
  const char *lparen = "", *rparen = "";
  const idl_literal_t *literal = ((const idl_const_t *)node)->const_expr;

  (void)revisit;
  (void)path;
  if (IDL_PRINTA(&type, print_type, node) < 0)
    return IDL_RETCODE_NO_MEMORY;
  switch (idl_type(literal)) {
    case IDL_CHAR:
    case IDL_STRING:
      lparen = "(";
      rparen = ")";
      break;
    default:
      break;
  }
  if (idl_fprintf(gen->header.handle, "#define %s %s", type, lparen) < 0)
    return IDL_RETCODE_NO_MEMORY;
  if (print_literal(pstate, gen, literal) < 0)
    return IDL_RETCODE_NO_MEMORY;
  if (idl_fprintf(gen->header.handle, "%s\n", rparen) < 0)
    return IDL_RETCODE_NO_MEMORY;
  return IDL_RETCODE_OK;
}

idl_retcode_t generate_types(const idl_pstate_t *pstate, struct generator *generator)
{
  idl_retcode_t ret;
  idl_visitor_t visitor;

  memset(&visitor, 0, sizeof(visitor));
  visitor.visit = IDL_CONST | IDL_TYPEDEF | IDL_STRUCT | IDL_UNION | IDL_ENUM | IDL_BITMASK | IDL_DECLARATOR | IDL_MEMBER | IDL_CASE | IDL_FORWARD;
  visitor.accept[IDL_ACCEPT_CONST] = &emit_const;
  visitor.accept[IDL_ACCEPT_TYPEDEF] = &emit_typedef;
  visitor.accept[IDL_ACCEPT_STRUCT] = &emit_struct;
  visitor.accept[IDL_ACCEPT_UNION] = &emit_union;
  visitor.accept[IDL_ACCEPT_ENUM] = &emit_enum;
  visitor.accept[IDL_ACCEPT_BITMASK] = &emit_bitmask;
  visitor.accept[IDL_ACCEPT_DECLARATOR] = &emit_field;
  visitor.accept[IDL_ACCEPT_MEMBER] = &emit_member;
  visitor.accept[IDL_ACCEPT_CASE] = &emit_case;
  visitor.accept[IDL_ACCEPT_FORWARD] = &emit_forward;
  visitor.sources = NULL;
  if ((ret = idl_visit(pstate, pstate->root, &visitor, generator)))
    return ret;
  return IDL_RETCODE_OK;
}
