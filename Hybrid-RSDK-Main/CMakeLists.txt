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
    find_package(SDL2 CONFIG REQUIRED)
    find_package(OpenGL REQUIRED)
    find_package(GLEW REQUIRED)
    find_package(Vorbis REQUIRED)
    find_package(tinyxml2 CONFIG REQUIRED)
    # Team Forever requires ogg and theora for video playback
    # vcpkg provides these but without CMake config files, so we find them manually
    find_library(OGG_LIBRARY NAMES ogg libogg REQUIRED)
    find_library(THEORA_LIBRARY NAMES theora libtheora REQUIRED)
    find_library(THEORADEC_LIBRARY NAMES theoradec libtheoradec REQUIRED)
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
        SDL2::SDL2
        SDL2::SDL2main
        ${OPENGL_LIBRARIES}
        GLEW::GLEW
        Vorbis::vorbis
        Vorbis::vorbisfile
        tinyxml2::tinyxml2
        ${OGG_LIBRARY}
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
        ${VORBIS_LIBRARIES}
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
            SDL2::SDL2
            SDL2::SDL2main
            ${OPENGL_LIBRARIES}
            GLEW::GLEW
            Vorbis::vorbis
            Vorbis::vorbisfile
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
            ${VORBIS_LIBRARIES}
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
        set(USE_IMGUI OFF CACHE BOOL "Disable ImGui for production builds" FORCE)
        
        message(STATUS "Sonic 3 AIR build configuration:")
        message(STATUS "  BUILD_SDL_STATIC = ON")
        message(STATUS "  USE_DISCORD = OFF")
        message(STATUS "  BUILD_OXYGEN_SERVER = OFF")
        message(STATUS "  BUILD_OXYGEN_ENGINEAPP = OFF")
        message(STATUS "  USE_IMGUI = OFF")
        
        # Add Sonic 3 AIR as subdirectory
        add_subdirectory(
            ${SONIC3AIR_CMAKE_DIR}
            ${CMAKE_BINARY_DIR}/sonic3air
            EXCLUDE_FROM_ALL
        )
        
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
            target_compile_definitions(OxygenEngine PRIVATE OXYGEN_EMBEDDED_MODE)
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