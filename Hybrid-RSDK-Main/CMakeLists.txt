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
else()
    find_package(SDL2 REQUIRED)
    find_package(OpenGL REQUIRED)
    find_package(GLEW REQUIRED)
    find_package(PkgConfig REQUIRED)
    pkg_check_modules(VORBIS REQUIRED vorbis vorbisfile)
    pkg_check_modules(TINYXML2 REQUIRED tinyxml2)
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

# RSDK V4 Core library
add_library(rsdk_core STATIC ${RSDKV4_SOURCES})

# Add -fPIC for shared library compatibility
set_target_properties(rsdk_core PROPERTIES POSITION_INDEPENDENT_CODE ON)

target_include_directories(rsdk_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/RSDKv4/NativeObjects
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/asio/asio/include
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4-Decompilation/dependencies/all/stb-image
)

if(WIN32)
    target_include_directories(rsdk_core PUBLIC
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
    )
    target_link_libraries(rsdk_core PUBLIC
        SDL2::SDL2
        SDL2::SDL2main
        ${OPENGL_LIBRARIES}
        GLEW::GLEW
        Vorbis::vorbis
        Vorbis::vorbisfile
        tinyxml2::tinyxml2
    )
else()
    target_include_directories(rsdk_core PUBLIC
        ${SDL2_INCLUDE_DIRS}
        ${OPENGL_INCLUDE_DIR}
        ${GLEW_INCLUDE_DIRS}
        ${VORBIS_INCLUDE_DIRS}
        ${TINYXML2_INCLUDE_DIRS}
    )
    target_link_libraries(rsdk_core PUBLIC
        ${SDL2_LIBRARIES}
        ${OPENGL_LIBRARIES}
        ${GLEW_LIBRARIES}
        ${VORBIS_LIBRARIES}
        ${TINYXML2_LIBRARIES}
    )
endif()

# Get libraries linked to rsdk_core
get_target_property(LIBS rsdk_core INTERFACE_LINK_LIBRARIES)

# RSDK V3 Core library (for Sonic CD)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3")
    if(NOT WIN32)
        file(GLOB_RECURSE RSDKV3_SOURCES "RSDKV3/RSDKv3/*.cpp")
    else()
        file(GLOB_RECURSE RSDKV3_SOURCES "RSDKV3/*.cpp")
    endif()

    add_library(rsdkv3_core STATIC ${RSDKV3_SOURCES})

    # Add -fPIC for shared library compatibility
    set_target_properties(rsdkv3_core PROPERTIES POSITION_INDEPENDENT_CODE ON)

    target_include_directories(rsdkv3_core PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3
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

# Install rules - include all available targets
set(INSTALL_TARGETS rsdk_core rsdkv4 HybridRSDK)

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