cmake_minimum_required(VERSION 3.15)
project(HybridRSDK)

# Force MSVC on Windows
if(WIN32)
    set(CMAKE_C_COMPILER "cl.exe")
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

# Check if RSDK decompilations exist
if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation")
    message(STATUS "RSDKv4 Decompilation not found, fetching...")
    execute_process(COMMAND bash ${CMAKE_CURRENT_SOURCE_DIR}/../fetch_rsdkv4.sh)
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3")
    message(STATUS "RSDKv3 not found, fetching...")
    execute_process(COMMAND bash ${CMAKE_CURRENT_SOURCE_DIR}/../fetch_rsdkv3.sh WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/..)
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5")
    message(STATUS "RSDKv5 not found, fetching...")
    execute_process(COMMAND bash ${CMAKE_CURRENT_SOURCE_DIR}/../fetch_rsdkv5.sh WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}/..)
endif()

file(GLOB_RECURSE RSDKV4_SOURCES ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/*.cpp)
# Exclude All.cpp to avoid duplicate symbols
list(FILTER RSDKV4_SOURCES EXCLUDE REGEX ".*NativeObjects/All.cpp$")

# Add additional C source files for Team Forever features
list(APPEND RSDKV4_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/fcaseopen.c
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/theoraplay/theoraplay.c
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
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/theoraplay
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
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3")
    # Only include core RSDKv3 sources, not platform-specific directories
    file(GLOB_RECURSE RSDKV3_SOURCES "RSDKV3/RSDKv3/*.cpp")

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

# RSDKv4 shared library for Custom Client P/Invoke
add_library(RSDKv4 SHARED
    RSDKv4Wrapper.cpp
)

target_include_directories(RSDKv4 PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/NativeObjects
)

target_link_libraries(RSDKv4 PRIVATE
    rsdk_core
)

# Set visibility for exported functions on Linux
if(NOT WIN32)
    set_target_properties(RSDKv4 PROPERTIES
        CXX_VISIBILITY_PRESET hidden
        C_VISIBILITY_PRESET hidden
    )
endif()

# Copy RSDKv4 library to Custom Client directory after build
add_custom_command(TARGET RSDKv4 POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net6.0-windows/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:RSDKv4>
        ${CMAKE_CURRENT_SOURCE_DIR}/../Custom-Client/bin/$<CONFIG>/net6.0-windows/
    COMMENT "Copying RSDKv4 library to Custom Client output directory"
)

# Also copy to the bin directory for standalone use
add_custom_command(TARGET RSDKv4 POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory ${CMAKE_BINARY_DIR}/bin/
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        $<TARGET_FILE:RSDKv4>
        ${CMAKE_BINARY_DIR}/bin/
    COMMENT "Copying RSDKv4 library to bin directory"
)

# Install rules - include all available targets
set(INSTALL_TARGETS rsdk_core rsdkv4 HybridRSDK RSDKv4)

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