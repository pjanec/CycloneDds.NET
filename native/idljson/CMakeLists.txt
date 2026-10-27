#
# Copyright(c) 2018 to 2022 ZettaScale Technology and others
#
# This program and the accompanying materials are made available under the
# terms of the Eclipse Public License v. 2.0 which is available at
# http://www.eclipse.org/legal/epl-2.0, or the Eclipse Distribution License
# v. 1.0 which is available at
# http://www.eclipse.org/org/documents/edl-v10.php.
#
# SPDX-License-Identifier: EPL-2.0 OR BSD-3-Clause
#

# Built next to an unmodified Cyclone DDS source tree (see ../CMakeLists.txt), so
# Cyclone's directory-scoped variables are not visible here: paths go through
# CYCLONEDDS_SOURCE_DIR and the version comes from the ddsc target.
include(GenerateExportHeader)
include(GNUInstallDirs)
include(CheckIncludeFile)
check_include_file(getopt.h HAVE_GETOPT_H)
configure_file(config.h.in config.h)

get_target_property(CYCLONEDDS_LIB_VERSION ddsc VERSION)
get_target_property(CYCLONEDDS_LIB_SOVERSION ddsc SOVERSION)

set(
  libidljson_hdrs
  include/libidlc/libidlc_generator.h
  ${CMAKE_CURRENT_BINARY_DIR}/config.h)

set(
  libidljson_srcs
  src/libidlc/libidlc__types.h
  src/libidlc/libidlc__descriptor.h
  src/libidlc/libidlc__generator.h
  src/libidlc/model.h
  src/libidlc/libidlc__descriptor.c
  src/libidlc/libidlc__generator.c
  src/libidlc/libidlc__types.c
  src/libidlc/model.c)

add_library(
  libidljson SHARED ${libidljson_srcs} ${libidljson_hdrs})

if(MSVC)
  # ignore warning 5286 and 5287 about enum type conversion
  target_compile_options(libidljson PRIVATE /wd5286 /wd5287)
endif()

generate_export_header(libidljson BASE_NAME LIBIDLJSON EXPORT_FILE_NAME include/libidlc/libidljson_export.h)

set_target_properties(libidljson PROPERTIES
   OUTPUT_NAME "cycloneddsidljson"
   VERSION ${CYCLONEDDS_LIB_VERSION}
   SOVERSION ${CYCLONEDDS_LIB_SOVERSION}
   C_STANDARD 99)

if(BUILD_TESTING)
  set_property(TARGET libidljson PROPERTY WINDOWS_EXPORT_ALL_SYMBOLS TRUE)
else()
  set_property(TARGET libidljson PROPERTY C_VISIBILITY_PRESET hidden)
endif()

target_include_directories(
  libidljson
  PRIVATE
    include
    ${CMAKE_CURRENT_BINARY_DIR}/include
  PUBLIC
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/include/libidlc>"
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include/libidlc>"
    "$<BUILD_INTERFACE:${CYCLONEDDS_SOURCE_DIR}/src/core/ddsi/include>"
    "$<BUILD_INTERFACE:${CYCLONEDDS_SOURCE_DIR}/src/core/cdr/include>"
    "$<BUILD_INTERFACE:${CYCLONEDDS_SOURCE_DIR}/src/tools/idlc/idlc/include>")

target_link_libraries(libidljson PRIVATE idl ddsc)

install(
  TARGETS libidljson
  RUNTIME DESTINATION "${CMAKE_INSTALL_BINDIR}" COMPONENT lib
  LIBRARY DESTINATION "${CMAKE_INSTALL_LIBDIR}" COMPONENT lib
  ARCHIVE DESTINATION "${CMAKE_INSTALL_LIBDIR}" COMPONENT lib)

install(
  DIRECTORY
        "$<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/include/libidlc>"
        "${CMAKE_CURRENT_SOURCE_DIR}/include/libidlc"
  DESTINATION "${CMAKE_INSTALL_INCLUDEDIR}"
  COMPONENT dev
  FILES_MATCHING PATTERN "*.h")

# The CUnit tests use Cyclone's CUnit and idlc_generate CMake helpers.
if(BUILD_TESTING)
  list(APPEND CMAKE_MODULE_PATH "${CYCLONEDDS_SOURCE_DIR}/cmake/Modules")
  include("${CYCLONEDDS_SOURCE_DIR}/src/tools/idlc/cmake/Generate.cmake")
  add_subdirectory(tests)
endif()


