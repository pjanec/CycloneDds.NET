#
# Native build for CycloneDDS.NET: an unmodified Eclipse Cyclone DDS (the
# ../cyclonedds submodule) plus the idljson idlc plugin (./idljson), which turns
# IDL into the JSON the C# code generator consumes.
#
# Used by build/native-linux.sh and build/native-win.ps1; all Cyclone DDS
# options (BUILD_IDLC, ENABLE_SECURITY, ...) are passed straight through.
#
cmake_minimum_required(VERSION 3.21)
project(CycloneDdsNetNative LANGUAGES C)

set(CYCLONEDDS_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../cyclonedds"
    CACHE PATH "Cyclone DDS source tree")

add_subdirectory("${CYCLONEDDS_SOURCE_DIR}" cyclonedds)

if(BUILD_IDLC)
  add_subdirectory(idljson)
endif()
