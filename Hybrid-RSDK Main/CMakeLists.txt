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

# Dependencies
find_package(SDL2 REQUIRED)
find_package(OpenGL REQUIRED)
find_package(GLEW REQUIRED)
find_package(PkgConfig REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(VORBIS REQUIRED vorbis vorbisfile)
pkg_check_modules(TINYXML2 REQUIRED tinyxml2)

# Check if RSDK decompilations exist
if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3")
    message(STATUS "RSDKv3 not found, fetching...")
    execute_process(COMMAND bash ${CMAKE_CURRENT_SOURCE_DIR}/../fetch_rsdkv3.sh)
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5")
    message(STATUS "RSDKv5 not found, fetching...")
    execute_process(COMMAND bash ${CMAKE_CURRENT_SOURCE_DIR}/../fetch_rsdkv5.sh)
endif()

# RSDK V4 Core library
add_library(rsdk_core STATIC
    RSDKV4/RSDKV4/RetroEngine.cpp
    RSDKV4/RSDKV4/Scene.cpp
    RSDKV4/RSDKV4/Animation.cpp
    RSDKV4/RSDKV4/Drawing.cpp
    RSDKV4/RSDKV4/Palette.cpp
    RSDKV4/RSDKV4/Renderer.cpp
    RSDKV4/RSDKV4/Sprite.cpp
    RSDKV4/RSDKV4/Object.cpp
    RSDKV4/RSDKV4/Audio.cpp
    RSDKV4/RSDKV4/Input.cpp
    RSDKV4/RSDKV4/Script.cpp
    RSDKV4/RSDKV4/Networking.cpp
    RSDKV4/RSDKV4/Debug.cpp
    RSDKV4/RSDKV4/Math.cpp
    RSDKV4/RSDKV4/Collision.cpp
    RSDKV4/RSDKV4/ModAPI.cpp
    RSDKV4/RSDKV4/String.cpp
    RSDKV4/RSDKV4/Scene3D.cpp
    RSDKV4/RSDKV4/Reader.cpp
    RSDKV4/RSDKV4/Userdata.cpp
)

target_include_directories(rsdk_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4/RSDKV4
    ${SDL2_INCLUDE_DIRS}
    ${OPENGL_INCLUDE_DIR}
    ${TINYXML2_INCLUDE_DIRS}
    ${GLEW_INCLUDE_DIRS}
    ${VORBIS_INCLUDE_DIRS}
)

target_link_libraries(rsdk_core PUBLIC
    ${SDL2_LIBRARIES}
    ${OPENGL_LIBRARIES}
    ${GLEW_LIBRARIES}
    ${VORBIS_LIBRARIES}
)

# RSDK V3 Core library (for Sonic CD)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3")
    file(GLOB_RECURSE RSDKV3_SOURCES "RSDKV3/*.cpp" "RSDKV3/*.c")
    if(RSDKV3_SOURCES)
        add_library(rsdkv3_core STATIC ${RSDKV3_SOURCES})

        target_include_directories(rsdkv3_core PUBLIC
            ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV3
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

# RSDK V5 Core library (Universal)
if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5")
    file(GLOB_RECURSE RSDKV5_SOURCES "RSDKV5/*.cpp" "RSDKV5/*.c")
    if(RSDKV5_SOURCES)
        add_library(rsdkv5_core STATIC ${RSDKV5_SOURCES})

        target_include_directories(rsdkv5_core PUBLIC
            ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV5
            ${SDL2_INCLUDE_DIRS}
            ${OPENGL_INCLUDE_DIR}
            ${GLEW_INCLUDE_DIRS}
            ${VORBIS_INCLUDE_DIRS}
        )

        target_link_libraries(rsdkv5_core PUBLIC
            ${SDL2_LIBRARIES}
            ${OPENGL_LIBRARIES}
            ${GLEW_LIBRARIES}
            ${VORBIS_LIBRARIES}
        )
    endif()
endif()

# Create Hybrid-RSDK library
add_library(HybridRSDK SHARED
    sonic-hybrid/HybridEngine.cpp
    sonic-hybrid/StateManager.cpp
    sonic-hybrid/TransitionManager.cpp
)

target_include_directories(HybridRSDK PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4/RSDKV4
    ${CMAKE_CURRENT_SOURCE_DIR}/sonic-hybrid
    ${SDL2_INCLUDE_DIRS}
    ${OPENGL_INCLUDE_DIR}
    ${GLEW_INCLUDE_DIRS}
    ${VORBIS_INCLUDE_DIRS}
)

set(HYBRID_LINK_LIBS
    rsdk_core
    ${SDL2_LIBRARIES}
    ${OPENGL_LIBRARIES}
    ${GLEW_LIBRARIES}
    ${VORBIS_LIBRARIES}
)

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
    RSDKV4/RSDKV4/main.cpp
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
