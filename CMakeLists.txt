cmake_minimum_required(VERSION 3.15)
#set(CMAKE_TOOLCHAIN_FILE "${CMAKE_CURRENT_SOURCE_DIR}/vcpkg/scripts/buildsystems/vcpkg.cmake" CACHE STRING "Vcpkg toolchain file")
project(HybridRSDK)

set(SCRIPTS_DIR "${CMAKE_CURRENT_SOURCE_DIR}/scripts")

find_package(GLEW REQUIRED)
find_package(Vorbis CONFIG REQUIRED)
find_package(SDL2 CONFIG REQUIRED)
find_package(tinyxml2 CONFIG REQUIRED)
find_package(unofficial-theora CONFIG REQUIRED)

include_directories(
    "${CMAKE_CURRENT_SOURCE_DIR}/vendor/theoraplay"
if(UNIX)
    "${CMAKE_CURRENT_SOURCE_DIR}/build/vcpkg_installed/x64-linux/include" 
elseif(WIN32)
    "${CMAKE_CURRENT_SOURCE_DIR}/build/vcpkg_installed/x64-windows/include"
endif()   
)

add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/vendor/theoraplay")
add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/Hybrid-RSDK Main")
