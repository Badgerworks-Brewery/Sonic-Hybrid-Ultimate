cmake_minimum_required(VERSION 3.15)
project(SonicHybridRSDK VERSION 1.0.0)

# Set C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Dependencies
find_package(SDL2 REQUIRED)
find_package(OpenGL REQUIRED)
find_package(GLEW REQUIRED)

# RSDK Core library
add_library(rsdk_core STATIC
    RSDKV4/RetroEngine.cpp
    RSDKV4/Scene.cpp
    RSDKV4/Animation.cpp
    RSDKV4/Drawing.cpp
    RSDKV4/Palette.cpp
    RSDKV4/Renderer.cpp
    RSDKV4/Sprite.cpp
    RSDKV4/Object.cpp
    RSDKV4/Audio.cpp
    RSDKV4/Input.cpp
    RSDKV4/Script.cpp
    RSDKV4/Networking.cpp
    RSDKV4/Debug.cpp
    RSDKV4/Math.cpp
    RSDKV4/Collision.cpp
    RSDKV4/ModAPI.cpp
    RSDKV4/String.cpp
    RSDKV4/Scene3D.cpp
    RSDKV4/Reader.cpp
    RSDKV4/Userdata.cpp
)

target_include_directories(rsdk_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/RSDKV4
    ${SDL2_INCLUDE_DIRS}
    ${OPENGL_INCLUDE_DIR}
    ${GLEW_INCLUDE_DIRS}
)

target_link_libraries(rsdk_core PUBLIC
    ${SDL2_LIBRARIES}
    ${OPENGL_LIBRARIES}
    ${GLEW_LIBRARIES}
)

# RSDK V4 executable
add_executable(rsdkv4
    RSDKV4/main.cpp
)

target_link_libraries(rsdkv4 PRIVATE
    rsdk_core
)

# Hybrid RSDK executable
add_executable(hybrid_rsdk
    sonic-hybrid/main.cpp
    sonic-hybrid/HybridEngine.cpp
    sonic-hybrid/StateManager.cpp
    sonic-hybrid/TransitionManager.cpp
)

target_link_libraries(hybrid_rsdk PRIVATE
    rsdk_core
)

# Install rules
install(TARGETS rsdk_core rsdkv4 hybrid_rsdk
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)
