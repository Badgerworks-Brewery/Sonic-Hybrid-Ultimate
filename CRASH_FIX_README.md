# Sonic Hybrid Ultimate - Crash Fix Guide

This document explains how to fix the crashes when loading .rsdk files and .bin files for Sonic 3.

## Problem Description

The application crashes with errors like:
- "Failed to initialize Oxygen Engine"
- "Failed to load Sonic 3 & Knuckles"
- DllNotFoundException when trying to load games

## Root Cause

The crashes occur because the native libraries (DLLs) required by the C# application are missing:
- `OxygenEngine.dll` - Required for Sonic 3 AIR integration
- `RSDKv4.dll` - Required for RSDK games (Sonic 1, CD, 2)

These libraries need to be built from the C++ source code before the C# application can use them.

## Solution

### Step 1: Build Native Libraries

#### On Windows:
```powershell
# Run from the project root directory
.\build_native_libs.ps1
```

#### On Linux/macOS:
```bash
# Run from the project root directory
chmod +x build_native_libs.sh
./build_native_libs.sh
```

### Step 2: Build the C# Application

```bash
cd Custom-Client
dotnet build
```

### Step 3: Run the Application

```bash
cd Custom-Client
dotnet run
```

## What the Build Script Does

1. **Creates a build directory** and configures the project with CMake
2. **Builds the native libraries**:
   - `OxygenEngine.dll/libOxygenEngine.so` - Wrapper for Sonic 3 AIR
   - `RSDKv4.dll/libRSDKv4.so` - RSDK engine for classic Sonic games
3. **Copies the libraries** to the correct locations where the C# application expects them

## Troubleshooting

### Build Fails with CMake Errors

**Problem**: CMake configuration fails
**Solution**: 
- Ensure you have CMake installed (version 3.15+)
- On Windows, ensure you have Visual Studio or Build Tools installed
- Check that all dependencies are available (SDL2, OpenGL, etc.)

### Libraries Build But Games Still Don't Load

**Problem**: Games fail to load even after building libraries
**Solutions**:

#### For RSDK Games (Sonic 1, CD, 2):
1. Ensure you have valid `.rsdk` files
2. Check that SDL2 and other dependencies are installed
3. Verify the `.rsdk` files are not corrupted

#### For Sonic 3 & Knuckles:
1. **Install Sonic 3 AIR**: The OxygenEngine wrapper requires Sonic 3 AIR to be installed
2. **Place the executable**: Ensure `sonic3air.exe` is in one of these locations:
   - `../vendor/sonic3air/Oxygen/sonic3air/bin/sonic3air.exe`
   - `../sonic3air/sonic3air.exe`
   - Same directory as the application
3. **Valid ROM file**: Ensure you have a valid Sonic 3 & Knuckles ROM file (`sonic3.bin`)

### Application Still Crashes

**Problem**: Application crashes even with libraries built
**Solution**: 
1. Check the application logs for detailed error messages
2. Ensure all dependencies (SDL2, OpenGL, etc.) are installed on your system
3. Try running the application from the command line to see additional error output

## Dependencies

### Windows
- Visual Studio 2019+ or Build Tools
- CMake 3.15+
- vcpkg (recommended for dependencies)

### Linux
- GCC or Clang
- CMake 3.15+
- Development packages: `libsdl2-dev`, `libgl1-mesa-dev`, `libglew-dev`, `libvorbis-dev`, `libtinyxml2-dev`

## File Locations

After building, the libraries should be located in:
- `Custom-Client/bin/Debug/net6.0-windows/OxygenEngine.dll`
- `Custom-Client/bin/Debug/net6.0-windows/RSDKv4.dll`
- `Custom-Client/bin/Release/net6.0-windows/OxygenEngine.dll`
- `Custom-Client/bin/Release/net6.0-windows/RSDKv4.dll`

## Error Messages Explained

### "OxygenEngine native library not found"
- The `OxygenEngine.dll` is missing
- Run the build script to create it

### "RSDKv4 DLL not found"
- The `RSDKv4.dll` is missing
- Run the build script to create it

### "Sonic 3 AIR executable not found"
- Sonic 3 AIR is not installed or not in the expected location
- Install Sonic 3 AIR and ensure the executable is accessible

### "Failed to initialize Oxygen Engine"
- Could be missing DLL, missing Sonic 3 AIR, or invalid ROM file
- Check the detailed log messages for the specific cause

## Additional Notes

- The application now provides detailed error messages to help diagnose issues
- Missing native libraries no longer cause crashes - they're handled gracefully
- The build process is automated and should work on both Windows and Linux
- All required libraries are copied to the correct locations automatically

## Getting Help

If you continue to experience issues:
1. Check the application logs for detailed error information
2. Ensure all build steps completed successfully
3. Verify that all dependencies are installed
4. Make sure you have valid game files (.rsdk files and ROM files)