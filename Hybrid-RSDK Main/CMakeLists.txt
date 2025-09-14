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
pkg_check_modules(VORBIS REQUIRED vorbis vorbisfile)

# RSDK Core library
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
    ${GLEW_INCLUDE_DIRS}
    ${VORBIS_INCLUDE_DIRS}
)

target_link_libraries(rsdk_core PUBLIC
    ${SDL2_LIBRARIES}
    ${OPENGL_LIBRARIES}
    ${GLEW_LIBRARIES}
    ${VORBIS_LIBRARIES}
)

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

target_link_libraries(HybridRSDK PRIVATE
    rsdk_core
    ${SDL2_LIBRARIES}
    ${OPENGL_LIBRARIES}
    ${GLEW_LIBRARIES}
    ${VORBIS_LIBRARIES}
)

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

# Install rules
install(TARGETS rsdk_core rsdkv4 HybridRSDK
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)
