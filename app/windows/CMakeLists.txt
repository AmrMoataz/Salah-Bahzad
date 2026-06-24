# Project-level configuration.
cmake_minimum_required(VERSION 3.14)
project(secure_player LANGUAGES CXX)

# The name of the executable created for the application. Change this to change
# the on-disk name of your application.
set(BINARY_NAME "secure_player")

# Explicitly opt in to modern CMake behaviors to avoid warnings with recent
# versions of CMake.
cmake_policy(VERSION 3.14...3.25)

# Define build configuration option.
get_property(IS_MULTICONFIG GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
if(IS_MULTICONFIG)
  set(CMAKE_CONFIGURATION_TYPES "Debug;Profile;Release"
    CACHE STRING "" FORCE)
else()
  if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE "Debug" CACHE
      STRING "Flutter build mode" FORCE)
    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS
      "Debug" "Profile" "Release")
  endif()
endif()
# Define settings for the Profile build mode.
set(CMAKE_EXE_LINKER_FLAGS_PROFILE "${CMAKE_EXE_LINKER_FLAGS_RELEASE}")
set(CMAKE_SHARED_LINKER_FLAGS_PROFILE "${CMAKE_SHARED_LINKER_FLAGS_RELEASE}")
set(CMAKE_C_FLAGS_PROFILE "${CMAKE_C_FLAGS_RELEASE}")
set(CMAKE_CXX_FLAGS_PROFILE "${CMAKE_CXX_FLAGS_RELEASE}")

# Use Unicode for all projects.
add_definitions(-DUNICODE -D_UNICODE)

# flutter_secure_storage_windows compiles against ATL (<atlstr.h>). ATL ships
# its headers/libs next to the MSVC toolset, but that directory is not on the
# default CMake include/lib path for generated plugin projects. Worse, the
# toolset the generator picks (often the standalone Build Tools) may not include
# ATL at all, even though another VS edition on the same machine ships it for the
# *same* toolset version. Discover ATL wherever it lives and add it before the
# plugin/runner subdirectories so they inherit it. All steps are EXISTS-guarded,
# so this is a safe no-op on toolchains that already expose ATL or lack it.
if(MSVC)
  get_filename_component(_SB_MSVC_BIN_DIR "${CMAKE_CXX_COMPILER}" DIRECTORY)
  get_filename_component(_SB_MSVC_TOOLSET "${_SB_MSVC_BIN_DIR}/../../.." ABSOLUTE)
  set(_SB_ATL_INC "")
  set(_SB_ATL_LIB "")
  if(EXISTS "${_SB_MSVC_TOOLSET}/atlmfc/include/atlstr.h")
    set(_SB_ATL_INC "${_SB_MSVC_TOOLSET}/atlmfc/include")
    set(_SB_ATL_LIB "${_SB_MSVC_TOOLSET}/atlmfc/lib/x64")
  else()
    # The selected toolset lacks ATL — borrow it from another VS edition that has
    # the same toolset version (binary-compatible), then any version as fallback.
    get_filename_component(_SB_MSVC_VER "${_SB_MSVC_TOOLSET}" NAME)
    file(GLOB _SB_ATL_HITS
      "C:/Program Files/Microsoft Visual Studio/*/*/VC/Tools/MSVC/${_SB_MSVC_VER}/atlmfc/include/atlstr.h"
      "C:/Program Files (x86)/Microsoft Visual Studio/*/*/VC/Tools/MSVC/${_SB_MSVC_VER}/atlmfc/include/atlstr.h"
      "C:/Program Files/Microsoft Visual Studio/*/*/VC/Tools/MSVC/*/atlmfc/include/atlstr.h"
      "C:/Program Files (x86)/Microsoft Visual Studio/*/*/VC/Tools/MSVC/*/atlmfc/include/atlstr.h")
    if(_SB_ATL_HITS)
      list(GET _SB_ATL_HITS 0 _SB_ATL_HDR)
      get_filename_component(_SB_ATL_INC "${_SB_ATL_HDR}" DIRECTORY)
      get_filename_component(_SB_ATL_ROOT "${_SB_ATL_INC}/.." ABSOLUTE)
      set(_SB_ATL_LIB "${_SB_ATL_ROOT}/lib/x64")
    endif()
  endif()
  if(_SB_ATL_INC)
    message(STATUS "Salah Bahzad: using ATL headers from ${_SB_ATL_INC}")
    include_directories("${_SB_ATL_INC}")
    if(EXISTS "${_SB_ATL_LIB}")
      link_directories("${_SB_ATL_LIB}")
    endif()
  endif()
endif()

# Compilation settings that should be applied to most targets.
#
# Be cautious about adding new options here, as plugins use this function by
# default. In most cases, you should add new options to specific targets instead
# of modifying this function.
function(APPLY_STANDARD_SETTINGS TARGET)
  target_compile_features(${TARGET} PUBLIC cxx_std_17)
  target_compile_options(${TARGET} PRIVATE /W4 /WX /wd"4100")
  target_compile_options(${TARGET} PRIVATE /EHsc)
  target_compile_definitions(${TARGET} PRIVATE "_HAS_EXCEPTIONS=0")
  target_compile_definitions(${TARGET} PRIVATE "$<$<CONFIG:Debug>:_DEBUG>")
endfunction()

# Flutter library and tool build rules.
set(FLUTTER_MANAGED_DIR "${CMAKE_CURRENT_SOURCE_DIR}/flutter")
add_subdirectory(${FLUTTER_MANAGED_DIR})

# Application build; see runner/CMakeLists.txt.
add_subdirectory("runner")


# Generated plugin build rules, which manage building the plugins and adding
# them to the application.
include(flutter/generated_plugins.cmake)


# === Installation ===
# Support files are copied into place next to the executable, so that it can
# run in place. This is done instead of making a separate bundle (as on Linux)
# so that building and running from within Visual Studio will work.
set(BUILD_BUNDLE_DIR "$<TARGET_FILE_DIR:${BINARY_NAME}>")
# Make the "install" step default, as it's required to run.
set(CMAKE_VS_INCLUDE_INSTALL_TO_DEFAULT_BUILD 1)
if(CMAKE_INSTALL_PREFIX_INITIALIZED_TO_DEFAULT)
  set(CMAKE_INSTALL_PREFIX "${BUILD_BUNDLE_DIR}" CACHE PATH "..." FORCE)
endif()

set(INSTALL_BUNDLE_DATA_DIR "${CMAKE_INSTALL_PREFIX}/data")
set(INSTALL_BUNDLE_LIB_DIR "${CMAKE_INSTALL_PREFIX}")

install(TARGETS ${BINARY_NAME} RUNTIME DESTINATION "${CMAKE_INSTALL_PREFIX}"
  COMPONENT Runtime)

install(FILES "${FLUTTER_ICU_DATA_FILE}" DESTINATION "${INSTALL_BUNDLE_DATA_DIR}"
  COMPONENT Runtime)

install(FILES "${FLUTTER_LIBRARY}" DESTINATION "${INSTALL_BUNDLE_LIB_DIR}"
  COMPONENT Runtime)

if(PLUGIN_BUNDLED_LIBRARIES)
  install(FILES "${PLUGIN_BUNDLED_LIBRARIES}"
    DESTINATION "${INSTALL_BUNDLE_LIB_DIR}"
    COMPONENT Runtime)
endif()

# Copy the native assets provided by the build.dart from all packages.
set(NATIVE_ASSETS_DIR "${PROJECT_BUILD_DIR}native_assets/windows/")
install(DIRECTORY "${NATIVE_ASSETS_DIR}"
   DESTINATION "${INSTALL_BUNDLE_LIB_DIR}"
   COMPONENT Runtime)

# Fully re-copy the assets directory on each build to avoid having stale files
# from a previous install.
set(FLUTTER_ASSET_DIR_NAME "flutter_assets")
install(CODE "
  file(REMOVE_RECURSE \"${INSTALL_BUNDLE_DATA_DIR}/${FLUTTER_ASSET_DIR_NAME}\")
  " COMPONENT Runtime)
install(DIRECTORY "${PROJECT_BUILD_DIR}/${FLUTTER_ASSET_DIR_NAME}"
  DESTINATION "${INSTALL_BUNDLE_DATA_DIR}" COMPONENT Runtime)

# Install the AOT library on non-Debug builds only.
install(FILES "${AOT_LIBRARY}" DESTINATION "${INSTALL_BUNDLE_DATA_DIR}"
  CONFIGURATIONS Profile;Release
  COMPONENT Runtime)
