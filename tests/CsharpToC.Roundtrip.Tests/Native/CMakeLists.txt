cmake_minimum_required(VERSION 3.16)
project(CsharpToC_Roundtrip_Native C)

# --- Configuration ---
if(NOT DEFINED CYCLONE_INSTALL_DIR)
    get_filename_component(REPO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../../.." ABSOLUTE)
    set(CYCLONE_INSTALL_DIR "${REPO_ROOT}/cyclone-compiled")
endif()
set(IDLC_EXE "${CYCLONE_INSTALL_DIR}/bin/idlc.exe")

# --- Find CycloneDDS ---
include_directories("${CYCLONE_INSTALL_DIR}/include")
link_directories("${CYCLONE_INSTALL_DIR}/lib")

# --- IDL Generation ---
set(IDL_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../idl/atomic_tests.idl")
set(GEN_C    "${CMAKE_CURRENT_BINARY_DIR}/atomic_tests.c")
set(GEN_H    "${CMAKE_CURRENT_BINARY_DIR}/atomic_tests.h")

add_custom_command(
    OUTPUT ${GEN_C} ${GEN_H}
    COMMAND ${IDLC_EXE} ${IDL_FILE}
    WORKING_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}
    DEPENDS ${IDL_FILE}
    COMMENT "Generating C from IDL..."
)

# --- Build Shared Library ---
include_directories("${CMAKE_CURRENT_BINARY_DIR}")

add_library(CsharpToC_Roundtrip_Native SHARED
    atomic_tests_native.c
    test_registry.c
    ${GEN_C}
    ${GEN_H}
)

# Link CycloneDDS
if(WIN32)
    target_link_libraries(CsharpToC_Roundtrip_Native ddsc)
endif()

# Rename output to ddsc_test_lib to match C# expectations
set_target_properties(CsharpToC_Roundtrip_Native PROPERTIES OUTPUT_NAME "ddsc_test_lib")

# Export symbols on Windows
if(WIN32)
    target_compile_definitions(CsharpToC_Roundtrip_Native PRIVATE NATIVE_EXPORT)
endif()
