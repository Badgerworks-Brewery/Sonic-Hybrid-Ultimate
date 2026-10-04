cmake_minimum_required(VERSION 3.15)
project(HybridRSDK)

# Force MSVC on Windows
#
# NB: these must NOT be set to a bare "cl.exe" unconditionally. They are directory-scope
# normal variables, so every add_subdirectory() inherits them, and a nested project() then
# tries to validate a compiler that is not a full path and configure aborts:
#
#   CMake Error at vendor/sonic3air/.../\_cmake/CMakeLists.txt:9 (project):
#     The CMAKE_C_COMPILER: cl.exe is not a full path and was not found in the PATH.
#
# That is what made BUILD_SONIC3AIR=ON impossible, independently of the target-name bug
# fixed further down. A.I.R.'s CMakeLists calls project(Sonic3AIR); it inherited "cl.exe" from
# here and the configure died before reaching any of our own logic.
#
# The Visual Studio generator already selects MSVC on its own, so CMAKE_<LANG>_COMPILER is
# populated before this point and the guard below simply does not fire. It only intervenes
# for a generator that has not chosen a compiler.
if(WIN32 AND NOT CMAKE_C_COMPILER)
    set(CMAKE_C_COMPILER "cl.exe")
endif()
if(WIN32 AND NOT CMAKE_CXX_COMPILER)
    set(CMAKE_CXX_COMPILER "cl.exe")
endif()

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Windows-specific configuration
if(WIN32)
    # Use vcpkg if available
    if(DEFINED ENV{VCPKG_ROOT})
        set(CMAKE_TOOLCHAIN_FILE "$ENV{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake")
    elseif(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/../vcpkg/scripts/buildsystems/vcpkg.cmake")
        set(CMAKE_TOOLCHAIN_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../vcpkg/scripts/buildsystems/vcpkg.cmake")
    endif()
endif()

# Dependencies
if(WIN32)
# One SDL for the whole build.
#
# A.I.R. always builds its own SDL on Windows: its CMake does add_subdirectory() on the
# vendored copy unconditionally, and the BUILD_SDL_STATIC=OFF branch only consults
# pkg_check_modules on ARM/Linux. There is no Windows path where A.I.R. adopts an external
# SDL, so when Sonic 3 is embedded both engines must agree on A.I.R.'s copy or CMake refuses
# to generate at all:
#
#   The INTERFACE_SDL2_SHARED property of "SDL2-static" does not agree with the value of
#   SDL2_SHARED already determined for "OxygenEngine".
#
# That is vcpkg's shared SDL2::SDL2 setting SDL2_SHARED ON while A.I.R.'s SDL2-static sets it
# OFF, both propagating into OxygenEngine via rsdk_core and rmxmedia respectively. It is a
# usage-requirement conflict rather than a duplicate-symbol one, so it surfaces at configure
# time instead of as 63 LNK2005 errors - the same duplication, caught earlier and named more
# clearly.
#
# Unifying on A.I.R.'s copy rather than the other way round, because rmxmedia was compiled
# against A.I.R.'s SDL headers (2.30.12) and pairing those with vcpkg's newer library (2.32.10)
# would put header/library skew inside a single engine. SDL2 is ABI-stable within a major
# version, so compiling this project against the older headers is safe.
if(BUILD_SONIC3AIR)
    set(HYBRID_SDL2_TARGET SDL2-static)
else()
    set(HYBRID_SDL2_TARGET SDL2::SDL2)
endif()

# Same reasoning for GLEW. A.I.R. used to compile its bundled GLEW straight into rmxmedia,
# so the symbols sat inside a library every other target needs and could not be dropped by
# changing link order - see patches/sonic3air/0012. Patch 0012 gives it a real target, so
# exactly one copy can be linked.
#
# Both copies are GLEW 2.3.4 and both declare GLEWAPI as plain extern rather than dllimport,
# so choosing either is ABI-neutral. Deciding per-build rather than per-consumer is what
# resolves the sharing problem: rsdk_core links one GLEW target, rsdkv4 gets vcpcg's,
# OxygenEngine gets A.I.R.'s, and neither engine needs to know which is which.
if(BUILD_SONIC3AIR)
    set(HYBRID_GLEW_TARGET glew)
else()
    set(HYBRID_GLEW_TARGET GLEW::GLEW)
endif()

# Same again for ogg/vorbis. A.I.R. bundles a single combined static `oggvorbis`, while this
# project links vcpcg's shared ogg.lib, vorbis.lib and vorbisfile.lib. Both provide the same
# symbols, so with /WHOLEARCHIVE forcing all of rsdk_core in the overlap finally became
# visible - 14 duplicate ogg symbols, and more behind them.
#
# Worth noting what the same run proved negatively: there were ZERO unresolved externals
# (no LNK2019, no LNK2001). Every symbol RSDKv4 needs is satisfied. The two engines' code
# resolves against each other completely; what conflicts is only third-party libraries that
# both happen to vendor.
if(BUILD_SONIC3AIR)
    set(HYBRID_OGG_TARGET oggvorbis)
else()
    set(HYBRID_OGG_TARGET "${OGG_LIBRARY}")
endif()

if(NOT BUILD_SONIC3AIR)
    set(HYBRID_VORBIS_TARGETS "${VORBIS_LIBRARIES}")
else()
    # A.I.R.'s oggvorbis already contains vorbis, so linking vcpcg's vorbis on top of it is
    # the duplication rather than the fix. Link oggvorbis alone.
    set(HYBRID_VORBIS_TARGETS "")
endif()
    find_package(SDL2 CONFIG REQUIRED)
    find_package(OpenGL REQUIRED)
    find_package(GLEW REQUIRED)
    find_package(Vorbis REQUIRED)
    find_package(tinyxml2 CONFIG REQUIRED)
    # Team Forever requires ogg and theora for video playback
    #
    # NB: these are found with find_library, which is NOT triplet-aware. It searches a plain
    # PATH and is free to return vcpkg's *debug* tree even for a Release build, which is
    # exactly what it did - CMakeCache held
    #   THEORA_LIBRARY:FILEPATH=.../vcpkg_installed/x64-windows/debug/lib/theora.lib
    # while VCPKG_TARGET_TRIPLET was x64-windows. A Release build was therefore linking debug
    # theora/theoradec, mixing debug and release CRT in one binary, which is the failure mode
    # that has no link error to warn about it. It only surfaced as warning LNK4098 once
    # OxygenEngine linked this target alongside A.I.R. and the warning appeared in context.
    #
    # GLEW and Vorbis above use find_package and resolve correctly, because config mode is
    # triplet-aware. So constrain the library search to the release tree explicitly, rather
    # than trusting the ambient PATH order.
    set(_hybrid_vcpkg_libdir
        "${VCPKG_INSTALLED_DIR}/${VCPKG_TARGET_TRIPLET}/lib")
    if(NOT EXISTS "${_hybrid_vcpkg_libdir}")
        message(WARNING
            "Expected vcpkg release libs at ${_hybrid_vcpkg_libdir}. If find_library falls back
            "to a default search it may pick a debug library; check THEORA_LIBRARY in the
            "CMake cache for '/debug/' before trusting a Release build.")
    endif()
    find_library(OGG_LIBRARY NAMES ogg libogg
        HINTS "${_hybrid_vcpkg_libdir}" NO_DEFAULT_PATH REQUIRED)
    find_library(THEORA_LIBRARY NAMES theora libtheora
        HINTS "${_hybrid_vcpkg_libdir}" NO_DEFAULT_PATH REQUIRED)
    find_library(THEORADEC_LIBRARY NAMES theoradec libtheoradec
        HINTS "${_hybrid_vcpkg_libdir}" NO_DEFAULT_PATH REQUIRED)
    # Headers are shared between debug and release for a triplet, so these are left unconstrained.
    find_path(OGG_INCLUDE_DIR ogg/ogg.h REQUIRED)
    find_path(THEORA_INCLUDE_DIR theora/theora.h REQUIRED)
else()
    find_package(OpenGL REQUIRED)
    find_package(GLEW REQUIRED)
    find_package(PkgConfig REQUIRED)
    pkg_check_modules(SDL2 REQUIRED sdl2)
    pkg_check_modules(VORBIS REQUIRED vorbis vorbisfile)
    pkg_check_modules(TINYXML2 REQUIRED tinyxml2)
    pkg_check_modules(OGG REQUIRED ogg)
    pkg_check_modules(THEORA REQUIRED theora theoradec)
endif()

# Check if RSDK decompilations exist.
#
# NB: an uninitialised git submodule is an *empty but existing* directory, so testing
# for the directory alone is not enough - probe for real sources. Getting this wrong
# makes the RSDKv3 glob return nothing and CMake aborts with
# "No SOURCES given to target: rsdkv3_core".
if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/main.cpp")
    message(FATAL_ERROR
        "RSDKv4 engine sources are missing from Hybrid-RSDK-Main/RSDKV4-Decompilation.\n"
        "They are a git submodule. Initialise it with:\n"
        "    git submodule update --init --recursive\n"
        "Without it there is no engine to build.")
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3/RSDKv3")
    message(STATUS "RSDKv3 sources not found - Sonic CD engine target will be skipped "
                   "(run: git submodule update --init --recursive to enable)")
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5/RSDKv5")
    message(STATUS "RSDKv5 sources not found - RSDKv5 target is disabled anyway")
endif()

# Hybrid data generation using C# build tools
find_program(DOTNET_EXECUTABLE dotnet)
if(DOTNET_EXECUTABLE)
    message(STATUS "Found .NET SDK for hybrid data generation: ${DOTNET_EXECUTABLE}")
    
    set(SOURCE_RSDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/rsdk-source-data")
    set(HYBRID_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid")
    
    # Check if all required source .rsdk files exist
    set(SONICCD_RSDK "${SOURCE_RSDK_DIR}/soniccd.rsdk")
    set(SONIC1_RSDK "${SOURCE_RSDK_DIR}/sonic1.rsdk")
    set(SONIC2_RSDK "${SOURCE_RSDK_DIR}/sonic2.rsdk")
    
    if(EXISTS "${SONICCD_RSDK}" AND 
       EXISTS "${SONIC1_RSDK}" AND 
       EXISTS "${SONIC2_RSDK}")
        
        message(STATUS "Source .rsdk files found - hybrid data generation enabled")
        message(STATUS "  Sonic CD: ${SONICCD_RSDK}")
        message(STATUS "  Sonic 1:  ${SONIC1_RSDK}")
        message(STATUS "  Sonic 2:  ${SONIC2_RSDK}")
        
        # Custom target to build and run the hybrid generator.
        # Part of ALL so a plain `cmake --build` actually produces Data.rsdk.
        add_custom_target(hybrid_data ALL
            COMMAND ${CMAKE_COMMAND} -E echo "Building hybrid data generator..."
            COMMAND ${DOTNET_EXECUTABLE} build SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release
            COMMAND ${CMAKE_COMMAND} -E echo "Running hybrid data generator..."
            COMMAND ${DOTNET_EXECUTABLE} run --project SonicHybridRsdk.Build/SonicHybridRsdk.Build.csproj -c Release --no-build
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
            COMMENT "Generating unified Sonic Hybrid Ultimate data..."
            VERBATIM
        )

    else()
        message(STATUS "Source .rsdk files not found in ${SOURCE_RSDK_DIR}")
        message(STATUS "Hybrid data generation will be skipped")
        message(STATUS "To enable: Place soniccd.rsdk, sonic1.rsdk, and sonic2.rsdk in:")
        message(STATUS "  ${SOURCE_RSDK_DIR}/")
    endif()
else()
    message(STATUS ".NET SDK not found - hybrid data generation disabled")
    message(STATUS "Install .NET 6.0 SDK or later to enable hybrid data generation")
endif()

file(GLOB_RECURSE RSDKV4_SOURCES ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/*.cpp)
# Exclude All.cpp to avoid duplicate symbols
list(FILTER RSDKV4_SOURCES EXCLUDE REGEX ".*NativeObjects/All.cpp$")

# Add additional C source files for Team Forever features
list(APPEND RSDKV4_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/fcaseopen.c
    ${CMAKE_CURRENT_SOURCE_DIR}/../vendor/theoraplay/theoraplay.c
)

# RSDK V4 Core library
if(HYBRID_HEARTBEAT_LOG)
    add_compile_definitions(rsdk_core PRIVATE HYBRID_HEARTBEAT_LOG=1)
endif()

add_library(rsdk_core STATIC ${RSDKV4_SOURCES})

# Add -fPIC for shared library compatibility
set_target_properties(rsdk_core PROPERTIES POSITION_INDEPENDENT_CODE ON)

target_include_directories(rsdk_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/NativeObjects
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/asio/asio/include
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/stb-image
    ${CMAKE_CURRENT_SOURCE_DIR}/../vendor/theoraplay
)

if(WIN32)
    target_include_directories(rsdk_core PUBLIC
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
        ${OGG_INCLUDE_DIR}
        ${THEORA_INCLUDE_DIR}
    )
    target_link_libraries(rsdk_core PUBLIC
        ${HYBRID_SDL2_TARGET}
        SDL2::SDL2main
        ${OPENGL_LIBRARIES}
        ${HYBRID_GLEW_TARGET}
        ${HYBRID_VORBIS_TARGETS}
        ${HYBRID_VORBIS_TARGETS}
        tinyxml2::tinyxml2
        ${HYBRID_OGG_TARGET}
        ${THEORA_LIBRARY}
        ${THEORADEC_LIBRARY}
    )
else()
    target_include_directories(rsdk_core PUBLIC
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
        ${VORBIS_INCLUDE_DIRS}
        ${TINYXML2_INCLUDE_DIRS}
        ${OGG_INCLUDE_DIRS}
        ${THEORA_INCLUDE_DIRS}
    )
    target_link_libraries(rsdk_core PUBLIC
        ${SDL2_LIBRARIES}
        ${OPENGL_LIBRARIES}
        ${GLEW_LIBRARIES}
        ${HYBRID_VORBIS_TARGETS}
        ${TINYXML2_LIBRARIES}
        ${OGG_LIBRARIES}
        ${THEORA_LIBRARIES}
    )
endif()

# Get libraries linked to rsdk_core
get_target_property(LIBS rsdk_core INTERFACE_LINK_LIBRARIES)

# RSDK V3 Core library (for Sonic CD)
#
# Keyed off the glob result, not the directory: an uninitialised submodule is an
# empty directory that still "exists", and add_library() hard-fails on an empty
# source list ("No SOURCES given to target: rsdkv3_core").
file(GLOB_RECURSE RSDKV3_SOURCES "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3/RSDKv3/*.cpp")

if(RSDKV3_SOURCES)
    message(STATUS "RSDKv3 sources found - building rsdkv3_core")
    add_library(rsdkv3_core STATIC ${RSDKV3_SOURCES})

    # Add -fPIC for shared library compatibility
    set_target_properties(rsdkv3_core PROPERTIES POSITION_INDEPENDENT_CODE ON)

    target_include_directories(rsdkv3_core PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3
        ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3/RSDKv3
        ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3/dependencies/all/theoraplay
    )

    if(WIN32)
        target_include_directories(rsdkv3_core PUBLIC
            ${SDL2_INCLUDE_DIRS}
            ${OPENGL_INCLUDE_DIR}
            ${GLEW_INCLUDE_DIRS}
        )
        target_link_libraries(rsdkv3_core PUBLIC
            ${HYBRID_SDL2_TARGET}
            SDL2::SDL2main
            ${OPENGL_LIBRARIES}
            ${HYBRID_GLEW_TARGET}
            ${HYBRID_VORBIS_TARGETS}
            ${HYBRID_VORBIS_TARGETS}
        )
    else()
        target_include_directories(rsdkv3_core PUBLIC
            ${SDL2_INCLUDE_DIRS}
            ${OPENGL_INCLUDE_DIR}
            ${GLEW_INCLUDE_DIRS}
            ${VORBIS_INCLUDE_DIRS}
        )
        target_link_libraries(rsdkv3_core PUBLIC
            ${SDL2_LIBRARIES}
            ${OPENGL_LIBRARIES}
            ${GLEW_LIBRARIES}
            ${HYBRID_VORBIS_TARGETS}
        )
    endif()
endif()

# RSDK V5 Core library (Universal) - Disabled for now as it requires complex setup
# if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5")
#     file(GLOB_RECURSE RSDKV5_SOURCES "RSDKV5/*.cpp" "RSDKV5/*.c")
#     if(RSDKV5_SOURCES)
#         add_library(rsdkv5_core STATIC ${RSDKV5_SOURCES})
# 
#         target_include_directories(rsdkv5_core PUBLIC
#             ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5
#             ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5/RSDKv5
#         )
# 
#         if(WIN32)
#             target_include_directories(rsdkv5_core PUBLIC
#                 ${SDL2_INCLUDE_DIRS}
#                 ${OPENGL_INCLUDE_DIR}
#                 ${GLEW_INCLUDE_DIRS}
#             )
#             target_link_libraries(rsdkv5_core PUBLIC
#                 SDL2::SDL2
#                 SDL2::SDL2main
#                 ${OPENGL_LIBRARIES}
#                 GLEW::GLEW
#                 Vorbis::vorbis
#                 Vorbis::vorbisfile
#             )
#         else()
#             target_include_directories(rsdkv5_core PUBLIC
#                 ${SDL2_INCLUDE_DIRS}
#                 ${OPENGL_INCLUDE_DIR}
#                 ${GLEW_INCLUDE_DIRS}
#                 ${VORBIS_INCLUDE_DIRS}
#             )
#             target_link_libraries(rsdkv5_core PUBLIC
#                 ${SDL2_LIBRARIES}
#                 ${OPENGL_LIBRARIES}
#                 ${GLEW_LIBRARIES}
#                 ${VORBIS_LIBRARIES}
#             )
#         endif()
#     endif()
# endif()

# Create Hybrid-RSDK library
add_library(HybridRSDK SHARED
    sonic-hybrid/HybridEngine.cpp
    sonic-hybrid/StateManager.cpp
    sonic-hybrid/TransitionManager.cpp
)

target_include_directories(HybridRSDK PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid
)

if(WIN32)
    target_include_directories(HybridRSDK PRIVATE
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
    )
else()
    target_include_directories(HybridRSDK PRIVATE
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
        ${VORBIS_INCLUDE_DIRS}
    )
endif()

set(HYBRID_LINK_LIBS rsdk_core)

# Add optional RSDK versions if available
if(TARGET rsdkv3_core)
    list(APPEND HYBRID_LINK_LIBS rsdkv3_core)
endif()

if(TARGET rsdkv5_core)
    list(APPEND HYBRID_LINK_LIBS rsdkv5_core)
endif()

target_link_libraries(HybridRSDK PRIVATE ${HYBRID_LINK_LIBS})

# Set Windows-specific properties
if(WIN32)
    target_compile_definitions(HybridRSDK PRIVATE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
        _CRT_SECURE_NO_WARNINGS
    )
endif()

# RSDK V4 executable
add_executable(rsdkv4
    RSDKV4-Decompilation/RSDKv4/main.cpp
)

target_link_libraries(rsdkv4 PRIVATE
    rsdk_core
)

# Sonic Hybrid library - game progression and state management
add_library(sonic_hybrid STATIC
    sonic-hybrid/HybridEngine.cpp
    sonic-hybrid/StateManager.cpp
    sonic-hybrid/TransitionManager.cpp
)

target_include_directories(sonic_hybrid PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/NativeObjects
)

set_target_properties(sonic_hybrid PROPERTIES POSITION_INDEPENDENT_CODE ON)

target_link_libraries(sonic_hybrid PUBLIC
    rsdk_core
)

# RSDKv4 shared library for Custom Client P/Invoke
#
# NB: the target must NOT be called "RSDKv4" - that differs from the "rsdkv4"
# executable target only by case, and on case-insensitive filesystems (Windows)
# CMake puts both in the same <target>.dir intermediate folder, so the two targets
# overwrite each other and the executable is never produced. OUTPUT_NAME keeps the
# on-disk DLL named RSDKv4.dll for the Custom Client's P/Invoke loader.
add_library(rsdkv4_shared SHARED
    RSDKv4Wrapper.cpp
)

set_target_properties(rsdkv4_shared PROPERTIES OUTPUT_NAME "RSDKv4")

target_include_directories(rsdkv4_shared PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/NativeObjects
)

target_link_libraries(rsdkv4_shared PRIVATE
    rsdk_core
    sonic_hybrid
)

# Set visibility for exported functions on Linux
if(NOT WIN32)
    set_target_properties(rsdkv4_shared PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        C_VISIBILITY_PRESET hidden
    )
endif()

# OxygenEngine wrapper library for Sonic 3 AIR integration
add_library(OxygenEngine SHARED
    OxygenWrapper.cpp
)

# Set visibility for exported functions on Linux
if(NOT WIN32)
    set_target_properties(OxygenEngine PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        C_VISIBILITY_PRESET hidden
    )
endif()

# Copy OxygenEngine library to Custom Client directory
add_custom_command(TARGET OxygenEngine POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:OxygenEngine>
        ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
    COMMENT "Copying OxygenEngine library to Custom Client output directory (win-x64)"
)

add_custom_command(TARGET OxygenEngine POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:OxygenEngine>
        ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/
    COMMENT "Copying OxygenEngine library to Custom Client output directory (non-RID)"
)

# Copy RSDKv4 library to Custom Client directory after build
# .NET 6.0 with RuntimeIdentifier uses paths like bin/Release/net8.0-windows/win-x64/
add_custom_command(TARGET rsdkv4_shared POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:rsdkv4_shared>
        ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
    COMMENT "Copying RSDKv4 library to Custom Client output directory (win-x64)"
)

# Also copy to non-RID path for compatibility
add_custom_command(TARGET rsdkv4_shared POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:rsdkv4_shared>
        ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/
    COMMENT "Copying RSDKv4 library to Custom Client output directory (non-RID)"
)

# Copy dependency DLLs to Custom Client directory on Windows (vcpkg provides these)
if(WIN32)
    add_custom_command(TARGET rsdkv4_shared POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E echo "Copying RSDKv4 dependencies to Custom Client directory..."
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_RUNTIME_DLLS:rsdkv4_shared>
            ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
        COMMAND_EXPAND_LISTS
        COMMENT "Copying RSDKv4 dependency DLLs to win-x64 directory"
    )
    add_custom_command(TARGET rsdkv4_shared POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_RUNTIME_DLLS:rsdkv4_shared>
            ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/
        COMMAND_EXPAND_LISTS
        COMMENT "Copying RSDKv4 dependency DLLs to non-RID directory"
    )
endif()

# Also copy to the bin directory for standalone use
add_custom_command(TARGET rsdkv4_shared POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_BINARY_DIR}/bin/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:rsdkv4_shared>
        ${CMAKE_BINARY_DIR}/bin/
    COMMENT "Copying RSDKv4 library to bin directory"
)

# ============================================================================
# Sonic 3 AIR (Oxygen Engine) Integration
# ============================================================================

# Option to build Sonic 3 AIR from source
# Sonic 3 AIR build is currently only supported on Unix-like systems
# Windows builds use prebuilt binaries or require manual Sonic 3 AIR setup
if(WIN32)
    option(BUILD_SONIC3AIR "Build Sonic 3 AIR (Oxygen Engine) from source" OFF)
else()
    option(BUILD_SONIC3AIR "Build Sonic 3 AIR (Oxygen Engine) from source" ON)
endif()

if(BUILD_SONIC3AIR)
    message(STATUS "=================================================")
    message(STATUS "Building Sonic 3 AIR (Oxygen Engine) from source")
    message(STATUS "=================================================")
    
    # Check if sonic3air submodule is initialized
    set(SONIC3AIR_CMAKE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../vendor/sonic3air/Oxygen/sonic3air/build/_cmake")
    if(NOT EXISTS "${SONIC3AIR_CMAKE_DIR}/CMakeLists.txt")
        message(STATUS "Sonic 3 AIR submodule not initialized. Initializing...")
        execute_process(
            COMMAND git submodule update --init --recursive vendor/sonic3air
            WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/..
            RESULT_VARIABLE SUBMODULE_RESULT
        )
        if(NOT SUBMODULE_RESULT EQUAL 0)
            message(WARNING "Failed to initialize sonic3air submodule. Sonic 3 support will be limited.")
            set(BUILD_SONIC3AIR OFF)
        endif()
    endif()
    
    if(BUILD_SONIC3AIR)
        # Set Sonic 3 AIR build options to minimize dependencies
        set(BUILD_SDL_STATIC ON CACHE BOOL "Build SDL as static library" FORCE)
        set(USE_DISCORD OFF CACHE BOOL "Disable Discord integration" FORCE)
        set(BUILD_OXYGEN_SERVER OFF CACHE BOOL "Don't build Oxygen server" FORCE)
        set(BUILD_OXYGEN_ENGINEAPP OFF CACHE BOOL "Don't build standalone Oxygen app" FORCE)

        # Frame-loop heartbeat logging, for scripts/probe_stages.py.
        #
        # Off by default. On, it prints a marker at each engine phase boundary and every N
        # frames, which is the only way to tell a stalled frame loop from a slow one during a
        # headless probe - without it, "never started", "stalled mid-stage" and "ran but never
        # finished" all produce an empty log and are indistinguishable.
        #
        # It cannot change behaviour: every marker is a bare logging call, never wrapped around
        # a branch. See RSDKV4-Decompilation/RSDKv4/HeartbeatLog.h. The definition itself is
        # applied to rsdk_core further up, where the target is created.
        option(HYBRID_HEARTBEAT_LOG "Emit frame-loop heartbeat markers (probe diagnostics)" OFF)
        # NB: USE_IMGUI must stay ON on Windows, whatever the size saving looks like.
        #
        # oxygen/devmode/ImGuiDefinitions.h:14 reads
        #   #if defined(PLATFORM_WINDOWS) || (defined(PLATFORM_LINUX) && defined(USE_IMGUI)) || ...
        # so on Windows SUPPORT_IMGUI is defined *unconditionally* - USE_IMGUI is never
        # consulted. A.I.R.'s CMake, however, guards both the imgui include directory and
        # the imgui target behind `if (USE_IMGUI)`. Forcing it OFF here therefore produced a
        # build whose source demanded a header the build had not made available:
        #   ImGuiDefinitions.h(21,10): error C1083: Cannot open include file: 'imgui.h'
        #
        # This is an upstream inconsistency between the preprocessor condition and the build
        # option, not something this project can configure around. Leaving A.I.R.'s own
        # default (ON) is correct and costs one imgui library that is already built by
        # framework/external/build_externals_windows.bat anyway.
        
        message(STATUS "Sonic 3 AIR build configuration:")
        message(STATUS "  BUILD_SDL_STATIC = ON")
        message(STATUS "  USE_DISCORD = OFF")
        message(STATUS "  BUILD_OXYGEN_SERVER = OFF")
        message(STATUS "  BUILD_OXYGEN_ENGINEAPP = OFF")
        message(STATUS "  USE_IMGUI = OFF")
        
        # A.I.R. does `find_package(CURL REQUIRED)` and links `oxygen` against CURL::libcurl
        # (vendor/sonic3air/.../\_cmake/CMakeLists.txt:303-304). Configure succeeds, but no curl
        # is built as part of this project, so the prebuilt one find_package picks is not the
        # copy this project actually built and the OxygenEngine link fails with
        #   LINK : fatal error LNK1104: cannot open file 'libcurl.lib'
        #
        # Point it at the vendored build before add_subdirectory so find_package resolves to
        # it. Doing this here rather than re-linking CURL::libcurl onto OxygenEngine afterwards
        # fixes the cause: `oxygen` and the DLL then agree on one curl by construction.
        get_filename_component(AIR_FRAMEWORK_LIB "${CMAKE_CURRENT_SOURCE_DIR}/../vendor/sonic3air/framework/lib/x64" ABSOLUTE)
        if(EXISTS "${AIR_FRAMEWORK_LIB}/curl/libcurl.lib")
            set(CURL_LIBRARY "${AIR_FRAMEWORK_LIB}/curl/libcurl.lib" CACHE FILEPATH "" FORCE)
            message(STATUS "  CURL_LIBRARY = ${CURL_LIBRARY}")
        else()
            message(WARNING
                "No vendored curl at ${AIR_FRAMEWORK_LIB}/curl/libcurl.lib. Run "
                "framework/external/build_externals_windows.bat, or let find_package(CURL) "
                "resolve a system curl - the OxygenEngine link may fail with LNK1181/LNK1104.")
        endif()

        # Add Sonic 3 AIR as subdirectory
        add_subdirectory(
            ${SONIC3AIR_CMAKE_DIR}
            ${CMAKE_BINARY_DIR}/sonic3air
            EXCLUDE_FROM_ALL
        )

        # And pass the same resolution on to the wrapper, so a transitive curl reference
        # cannot diverge from the one `oxygen` was built against.
        if(TARGET CURL::libcurl)
            target_link_libraries(OxygenEngine PRIVATE CURL::libcurl)
        endif()

        # Link RSDKv4 and the Hybrid game layer into the same DLL as A.I.R.
        #
        # This is the coexistence test, and it is the whole point of the exercise: two complete
        # engines, each with its own main(), renderer, audio device and a broad set of
        # file-scope globals, in one binary. Until now OxygenEngine held A.I.R. alone and linked
        # cleanly, which says nothing about whether the two can share a link.
        #
        # Expected collision modes, in the order I would expect them:
        #   1. Two SDL copies. OxygenEngine already pulls A.I.R.'s SDL2-static.lib, while the
        #      rest of this project links vcpkg's SDL2::SDL2 (Hybrid-RSDK-Main:find_package
        #      (SDL2 CONFIG REQUIRED) + SDL2::SDL2 on rsdk_core). Both export the same SDL
        #      symbols, so adding rsdk_core here should collide unless one is dropped.
        #   2. Duplicate file-scope globals, if the two engines declare the same symbol.
        #   3. Two OpenGL and/or audio stacks.
        #
        # Linking rsdk_core here is therefore expected to FAIL, and the failure list is the
        # deliverable: it is the first real measurement of what coexistence costs. The
        # alternative - putting A.I.R. inside the rsdkv4 executable instead - has the same
        # collision set and additionally has to reconcile two main() symbols, so the DLL is
        # the cheaper place to find out.
        if(TARGET rsdk_core)
            # /WHOLEARCHIVE, not a plain link.
            #
            # With a plain link the coexistence test is hollow: rsdk_core is a static library
            # and nothing in OxygenWrapper.cpp references an RSDKv4 symbol, so the linker
            # pulls in none of it. That is exactly what happened - the DLL came out at
            # 3,286,016 bytes both with and without rsdk_core on the link line, byte for
            # byte, and dumpbin found no RSDKv4 symbols in it. Same trap as the earlier
            # 16,384-byte 'successful' DLL that contained none of A.I.R.
            #
            # /WHOLEARCHIVE forces every object in, which is what makes this a real test of
            # whether two engines can share a link rather than a test of whether the linker
            # can avoid the question.
            add_library(rsdk_core_whole INTERFACE)
            target_link_libraries(rsdk_core_whole INTERFACE
                "-WHOLEARCHIVE:$<TARGET_FILE:rsdk_core>"
                "-NOWHOLEARCHIVE"
            )
            target_link_libraries(OxygenEngine PRIVATE rsdk_core_whole sonic_hybrid)
            # TEMPORARY diagnostic: /WHOLEARCHIVE forces the linker to *resolve* every
            # rsdk_core object, but Release keeps /OPT:REF and then strips code nothing calls.
            # Turning REF off makes the retained bytes visible so the coexistence test can be
            # checked against the output file instead of taken on trust from the exit code.
            # Always on for this target, not a flag worth exposing. OxygenEngine exists
            # only to answer 'can these two engines share one link', and a green exit
            # code cannot answer that - the earlier plain-link build exited 0 while
            # rsdk_core contributed nothing at all. With REF off, the retained bytes
            # are checkable against the output file.
            target_link_options(OxygenEngine PRIVATE /OPT:NOREF /OPT:NOICF)
            message(STATUS "  OxygenEngine: rsdk_core (WHOLEARCHIVE) + sonic_hybrid + oxygen")
            message(STATUS "  -> coexistence test active; expect SDL or global collisions")
        else()
            message(WARNING "rsdk_core not available - coexistence test NOT active")
        endif()

        # A.I.R.'s static libraries carry embedded MSVC link directives:
        #   /DEFAULTLIB:libcurl.lib        (oxygen.lib)
        #   /DEFAULTLIB:zlib.lib          (rmxbase.lib)
        #   /DEFAULTLIB:minizip.lib       (oxygen.lib)
        # MSVC honours /DEFAULTLIB itself and resolves the bare name against
        # AdditionalLibraryDirectories *only*. It is not a CMake link item, so neither
        # CURL::libcurl above nor a correct CURL_LIBRARY supplies the directory - which is why
        #   LINK : fatal error LNK1104: cannot open file 'libcurl.lib'
        # persisted with CURL_LIBRARY set correctly at configure time. minizip resolves on its
        # own because it is already a CMake dependency.
        #
        # Verified by enumerating every directive rather than fixing them one build at a time:
        #   dumpbin /DIRECTIVES build/lib/Release/*.lib
        # The other 13 names found there (AdvApi32, crypt32, kernel32, user32, shell32, imm32,
        # uuid, wldap32, Ws2_32, Mswsock, msvcprt, MSVCRT, OLDNAMES) are Windows SDK and CRT
        # libraries, which MSVC resolves from its own default paths and which need nothing here.
        foreach(_air_libdir curl zlib)
            if(EXISTS "${AIR_FRAMEWORK_LIB}/${_air_libdir}")
                target_link_directories(OxygenEngine PRIVATE "${AIR_FRAMEWORK_LIB}/${_air_libdir}")
                message(STATUS "  /DEFAULTLIB search path: ${AIR_FRAMEWORK_LIB}/${_air_libdir}")
            else()
                message(WARNING
                    "No vendored ${_air_libdir} at ${AIR_FRAMEWORK_LIB}/${_air_libdir}. "
                    "Run framework/external/build_externals_windows.bat - the link will fail "
                    "with LNK1104 on '${_air_libdir}.lib'.")
            endif()
        endforeach()
        
        # Link Sonic 3 AIR libraries to OxygenEngine wrapper
        # This enables embedded mode instead of external process launching
        #
        # Two corrections to what was here before, both of which made this a no-op:
        #
        #  - The target is `oxygen`, not `sonic3air`. A.I.R.'s CMakeLists defines the
        #    executable as `Sonic3AIR` (its Visual Studio project is `sonic3air.vcxproj`,
        #    which is where the lowercase name came from). CMake target names are
        #    case-sensitive, so `if(TARGET sonic3air)` was never true and this block only
        #    ever printed its warning. Setting BUILD_SONIC3AIR=ON silently did nothing.
        #
        #  - `oxygen` is a library and `Sonic3AIR` is an executable. Linking an .exe into
        #    a .dll is not something MSVC will do, so even with the name corrected the
        #    original line could not have linked. `Sonic3AIR` itself does
        #    `target_link_libraries(Sonic3AIR oxygen)`, which is the relationship to copy:
        #    take the engine library, not the game front-end.
        if(TARGET oxygen)
            target_link_libraries(OxygenEngine PRIVATE oxygen)

            # Link rmxbase rather than transcribing its include directories.
            #
            # OxygenWrapper.cpp includes rmxbase/memory/StringImpl.h, and line 765 fails with
            #   error C3867: 'std::basic_string_view<char,...>::data': non-standard syntax
            # while compiling cleanly in rmxbase's own target. Release|x64 flags are identical
            # between the two targets (stdcpp17, WIN32 _WINDOWS NDEBUG, no AdditionalOptions),
            # so it is not a flag difference. The include lists are not identical either:
            # rmxbase has 18 directories and this target had 5, including several that
            # StringImpl.h may need - librmx/source/rmxmedia/_glew among them, since
            # rmxbase.h pulls rendering types through GLEW.
            #
            # Inheriting INTERFACE_INCLUDE_DIRECTORIES fixes that by construction. Hand-listing
            # directories is how the list came to be wrong in the first place, and it would
            # drift again the next time A.I.R. reorganises. The explicit list is kept only as
            # a fallback for the case where rmxbase is not a target.
            if(TARGET rmxbase)
                target_link_libraries(OxygenEngine PRIVATE rmxbase)
            else()
                message(WARNING
                    "rmxbase target absent; falling back to hand-listed A.I.R. include dirs. "
                    "StringImpl.h may fail to compile - see docs/STATUS.md.")
            endif()

            get_filename_component(SONIC3AIR_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../vendor/sonic3air" ABSOLUTE)
            target_include_directories(OxygenEngine PRIVATE
                ${SONIC3AIR_ROOT}/Oxygen/oxygenengine/source
                ${SONIC3AIR_ROOT}/Oxygen/sonic3air/source
                ${SONIC3AIR_ROOT}/Oxygen/lemonscript/source
                ${SONIC3AIR_ROOT}/librmx/source
                ${SONIC3AIR_ROOT}/librmx/source/rmxmedia/_glew
                ${SONIC3AIR_ROOT}/framework/include
                ${SONIC3AIR_ROOT}/framework/external/ogg-vorbis/libogg/include
                ${SONIC3AIR_ROOT}/framework/external/ogg-vorbis/libvorbis/include
                ${SONIC3AIR_ROOT}/framework/external/zlib/zlib/contrib/minizip
            )

            message(STATUS "✓ OxygenEngine will use embedded Sonic 3 AIR (linked 'oxygen')")
        else()
            message(WARNING
                "A.I.R. 'oxygen' target not found after add_subdirectory. Available Oxygen "
                "targets would be: oxygen, oxygen_netcore, lemonscript. Sonic 3 stays in "
                "stub mode until this resolves - check that "
                "${SONIC3AIR_CMAKE_DIR} configured without disabling the engine.")
        endif()
        
        # Copy Sonic 3 AIR executable and data files to distribution
        #
        # Only when not embedding. When OxygenEngine links `oxygen` directly there is no
        # separate Sonic 3 process to ship, and the exe is built anyway by A.I.R.'s own
        # solution. Guarded on the same condition as the link above so the two cannot
        # disagree.
        if(TARGET Sonic3AIR AND NOT TARGET oxygen)
            add_custom_command(TARGET Sonic3AIR POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E echo "Copying Sonic 3 AIR executable..."
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                    $<TARGET_FILE:Sonic3AIR>
                    ${CMAKE_BINARY_DIR}/bin/
                COMMENT "Copying Sonic 3 AIR executable to bin directory"
            )
            
            # Copy to Custom Client directory for dev builds
            add_custom_command(TARGET Sonic3AIR POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E make_directory 
                    ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                    $<TARGET_FILE:Sonic3AIR>
                    ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net8.0-windows/win-x64/
                COMMENT "Copying Sonic 3 AIR to Custom Client directory"
            )
        endif()
        
        message(STATUS "=================================================")
        message(STATUS "Sonic 3 AIR integration complete")
        message(STATUS "=================================================")
    endif()
else()
    message(STATUS "Sonic 3 AIR build disabled (BUILD_SONIC3AIR=OFF)")
    message(STATUS "Sonic 3 will run in stub mode - external executable required")
endif()

# Install rules - include all available targets
set(INSTALL_TARGETS rsdk_core rsdkv4 HybridRSDK rsdkv4_shared)

if(TARGET rsdkv3_core)
    list(APPEND INSTALL_TARGETS rsdkv3_core)
endif()

if(TARGET rsdkv5_core)
    list(APPEND INSTALL_TARGETS rsdkv5_core)
endif()

install(TARGETS ${INSTALL_TARGETS}
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)