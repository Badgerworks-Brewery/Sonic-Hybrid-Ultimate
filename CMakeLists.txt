cmake_minimum_required(VERSION 3.15)
project(HybridRSDK)

# Add vendor libraries
add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/vendor/theoraplay")

# Add Hybrid RSDK engine
add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/Hybrid-RSDK-Main")
