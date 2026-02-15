cmake_minimum_required(VERSION 3.16)
project(IdlJsonVerifier C)

# --- Configuration ---
if(NOT DEFINED CYCLONE_INSTALL_DIR)
    get_filename_component(REPO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../.." ABSOLUTE)
    set(CYCLONE_INSTALL_DIR "${REPO_ROOT}/artifacts/native-install")
endif()
set(IDLC_EXE "${CYCLONE_INSTALL_DIR}/bin/idlc.exe")

# --- Find CycloneDDS ---
# We need headers and libs to compile the generated C code
include_directories("${CYCLONE_INSTALL_DIR}/include")
include_directories("${CMAKE_CURRENT_BINARY_DIR}")
link_directories("${CYCLONE_INSTALL_DIR}/lib")

# --- Generation Step ---
set(IDL_FILE "${CMAKE_CURRENT_SOURCE_DIR}/verification.idl")
set(GEN_C    "${CMAKE_CURRENT_BINARY_DIR}/verification.c")
set(GEN_H    "${CMAKE_CURRENT_BINARY_DIR}/verification.h")
set(GEN_JSON "${CMAKE_CURRENT_BINARY_DIR}/verification.json")

# 1. Generate C Code (Reference)
add_custom_command(
    OUTPUT ${GEN_C} ${GEN_H}
    COMMAND ${IDLC_EXE} -l c ${IDL_FILE}
    WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
    DEPENDS ${IDL_FILE}
    COMMENT "Generating C code from IDL..."
)

# 2. Generate JSON (Target)
# Note: We assume 'cycloneddsidljson.dll' is in the same bin folder as idlc.exe
# If not, you might need to copy it there or set PATH.
add_custom_command(
    OUTPUT ${GEN_JSON}
    COMMAND ${IDLC_EXE} -l json ${IDL_FILE}
    WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
    DEPENDS ${IDL_FILE}
    COMMENT "Generating JSON from IDL..."
)

# --- Build Verifier ---
add_executable(verify_layout 
    verifier.c 
    cJSON/cJSON.c
    ${GEN_C} 
    ${GEN_H}
    ${GEN_JSON} # Add to target so it runs generation
)

# Link against CycloneDDS C library (ddsc)
target_link_libraries(verify_layout ddsc)

# Copy JSON to output dir for easy running
add_custom_command(TARGET verify_layout POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
        ${GEN_JSON}
        $<TARGET_FILE_DIR:verify_layout>/verification.idl.json
)
